# Design: Boss blood particles on every hit

**Date:** 2026-07-07
**Type:** DESIGN ONLY (project-planner, Tier 2 — no implementation)
**Feature (verbatim):** "bosses should make blood particles each time they are hit instead of just on death"
**Scope:** BOSS-ONLY blood on every `takeDamage`; regular enemies unchanged; death blood preserved.

---

## (a) Anchors (verified — no invented anchors)

File: `Assets/Scripts/enemyHealth.cs` (shared by ALL enemies)

| Anchor | Line | Exact text |
|---|---|---|
| Class decl | 3 | `public class enemyHealth : MonoBehaviour` |
| Blood prefab field | 10 | `[SerializeField] private GameObject bloodPrefab;` |
| `Awake()` (caches `flash`) | 21–24 | `flash = GetComponent<damageFlash>();` |
| `takeDamage(int amount)` | 31 | `public void takeDamage(int amount)` |
| HP decrement (top of takeDamage) | 33 | `currentHp -= amount;` |
| Death branch | 41–42 | `if (currentHp <= 0)` → `die();` |
| `die()` | 45 | `void die()` |
| **Death blood-spawn call (the reuse target)** | 53–54 | `if (bloodPrefab != null && objectPool.instance != null)` → `objectPool.instance.get(bloodPrefab, transform.position, Quaternion.identity);` |

Boss-only components (confirmed real classes):
- `Assets/Scripts/bossBehaviour.cs:3` → `public class bossBehaviour : MonoBehaviour`
- `Assets/Scripts/bossShooter.cs:3` → `public class bossShooter : MonoBehaviour`

Both live on `boss.prefab` (per task brief) and on NO other enemy prefab.

---

## (b) Boss-detection decision + justification

**CHOSEN: Option (ii) — detect a boss-only component, cached in `Awake()`.**

```csharp
private bool _isBoss;
// in Awake:
_isBoss = GetComponent<bossBehaviour>() != null;
```

**Justification:**
- **Zero Editor work.** `bossBehaviour` is already on `boss.prefab` and exists on no other enemy. Detection is intrinsic to the boss's component makeup — nothing to tick, nothing that can be forgotten on a future boss variant prefab.
- **No spam risk on normal enemies.** Regular enemies have no `bossBehaviour`, so `_isBoss` is `false` and their code path is byte-for-byte unchanged (blood only in `die()`).
- **Cheap at runtime.** `GetComponent` runs once in `Awake` (pooled objects `Awake` once), cached to a `bool`. `takeDamage` does a single bool test — no per-hit `GetComponent`.
- **Pooling-safe.** `_isBoss` depends only on the GameObject's components, which never change across pool get/ret cycles, so caching in `Awake` (not `OnEnable`) is correct and stable.

**Rejected — Option (i) serialized `bool bloodOnHit`:** functionally fine but adds a mandatory **Editor task** (tick the bool on `boss.prefab`, and on every future boss prefab). Silent-failure risk if forgotten. The brief says prefer least Editor work when clean — Option (ii) is clean and needs none.

**Rejected — Option (iii) tag/layer marker:** couples to project-wide tag conventions and still needs an Editor step; no advantage over (ii).

> **EDITOR STEP REQUIRED: NONE.** This is a pure code change in one file.

---

## (c) EXACT drop-in code

All edits in `Assets/Scripts/enemyHealth.cs`. Three surgical additions; existing lines unchanged.

**1. Add cached field — after line 15 (`private damageFlash flash;`):**
```csharp
    private damageFlash flash;
    private bool _isBoss;   // ADDED: true only for the boss (has bossBehaviour); gates blood-on-hit
```

**2. Cache detection in `Awake()` — inside the existing Awake body (after line 23):**
```csharp
    void Awake()
    {
        flash = GetComponent<damageFlash>();
        _isBoss = GetComponent<bossBehaviour>() != null;   // ADDED: boss-only marker, cached once
    }
```

**3. Emit blood on every hit — inside `takeDamage`, immediately after the HP decrement (after line 33 `currentHp -= amount;`, before the flash call):**
```csharp
    public void takeDamage(int amount)
    {
        currentHp -= amount;
        if (_isBoss && bloodPrefab != null && objectPool.instance != null)   // ADDED: boss-only blood on each hit
            objectPool.instance.get(bloodPrefab, transform.position, Quaternion.identity);
        if (flash != null) flash.Flash();
        ...
```

This reuses the **identical** blood spawn call already in `die()` (line 54): same `bloodPrefab`, same `objectPool.instance.get(...)`, same position/rotation. `die()` is untouched, so death blood still fires (boss and normal enemies alike). Placing the emit after `currentHp -= amount` but guarded independently of the `<=0` check means the final killing hit produces one on-hit blood **and** the death blood — matching "each time they are hit" plus preserved death effect.

---

## (d) Grep acceptance checklist

```bash
# 1. Field added exactly once
grep -n "private bool _isBoss;" Assets/Scripts/enemyHealth.cs                 # expect 1

# 2. Detection cached in Awake via bossBehaviour, not per-hit GetComponent
grep -n "_isBoss = GetComponent<bossBehaviour>() != null;" Assets/Scripts/enemyHealth.cs  # expect 1

# 3. On-hit blood guarded by _isBoss, inside takeDamage
grep -n "if (_isBoss && bloodPrefab != null" Assets/Scripts/enemyHealth.cs    # expect 1

# 4. Death blood in die() STILL present (unchanged)
grep -n "objectPool.instance.get(bloodPrefab, transform.position, Quaternion.identity);" Assets/Scripts/enemyHealth.cs  # expect 2 (one in takeDamage, one in die)

# 5. No per-hit GetComponent (perf guard)
grep -n "GetComponent<bossBehaviour>" Assets/Scripts/enemyHealth.cs           # expect 1 (Awake only)

# 6. die() body untouched
grep -n "void die()" Assets/Scripts/enemyHealth.cs                            # expect 1
```

---

## (e) Risk notes

- **Normal-enemy blood unchanged:** `_isBoss` is `false` for every enemy lacking `bossBehaviour`, so their `takeDamage` path is identical to today — blood only via `die()`. ✅ No visual/perf spam.
- **Pooled-spawn correctness:** blood is spawned through the same `objectPool.instance.get(...)` the death path already uses; blood prefab resets in its own `OnEnable` per the pool contract. `_isBoss` is cached in `Awake` (once per pooled object, stable across get/ret) — no re-detection cost and no staleness. Null-guards on `bloodPrefab` and `objectPool.instance` mirror `die()`.
- **Death blood preserved:** `die()` is not modified; both boss and normal enemies still emit blood on death. On the boss's killing blow, the on-hit blood (from takeDamage) and death blood (from die) both fire — intended.
- **Boss pool exhaustion:** boss takes many hits (250 HP), so on-hit blood raises blood-prefab pool churn for the boss only. Bounded by the existing blood pool; if the pool is small, blood may briefly recycle — acceptable and boss-scoped. Flag only if profiling shows GC/pool pressure.
- **Future bosses:** any new boss automatically inherits blood-on-hit as long as it carries `bossBehaviour`. If a future boss omits `bossBehaviour`, swap the check to `bossShooter` or OR the two — noted, not required now.
