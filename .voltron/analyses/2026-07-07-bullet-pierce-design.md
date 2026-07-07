# Design: Player Bullet Pierce (default 1) + flat-only Pierce upgrade

**Date:** 2026-07-07
**Feature (verbatim):** "the player's bullets should pierce through 1 enemy by default. add enemies pierced as an upgrade option, but only as a flat value (not a percentage)"
**Author role:** project-planner (DESIGN ONLY — no implementation performed)

---

## (a) The player bullet script + collision/despawn method

- **File:** `Assets/Scripts/projectileBehaviour.cs` (class `projectileBehaviour : MonoBehaviour`).
- This is the PLAYER's fired shot: it damages enemies via `enemyHealth.takeDamage(dmg)` and despawns through `objectPool.instance.ret(gameObject)`.
- **DISTINCT from `Assets/Scripts/enemyProjectile.cs`** (the ENEMY's bullet) — that file MUST NOT be touched by this feature.

Relevant anchors (current code):
- `OnEnable()` — `projectileBehaviour.cs:11-17` — resets per-shot state (`lifeTimer`, `spawnOrigin`, zeroes `rb.linearVelocity`). This is the pooled-object reset hook.
- `Update()` — `projectileBehaviour.cs:19-34` — range + lifetime despawn (unchanged by this feature).
- `OnTriggerEnter2D(Collider2D other)` — `projectileBehaviour.cs:36-55` — wall despawn (lines 38-42, unchanged) and the **enemy hit block (lines 44-54)** which currently damages the enemy and *immediately* rets to pool. This is the ONLY block that changes.

Current enemy-hit block (`projectileBehaviour.cs:44-54`):
```csharp
        if (other.CompareTag("Enemy"))
        {
            enemyHealth eh = other.GetComponent<enemyHealth>();
            if (eh != null)
            {
                int dmg = worldState.instance != null ? Mathf.RoundToInt(worldState.instance.AttackDamage()) : 1;
                eh.takeDamage(dmg);
                Debug.Log("hit");
            }
            if (objectPool.instance != null) objectPool.instance.ret(gameObject);
        }
```
Today the bullet damages exactly ONE enemy and despawns (no pierce).

---

## (b) Chosen pierce semantics (one sentence)

**Pierce = N means a fired bullet passes through (and damages) N enemies and then despawns on contact with the (N+1)th enemy it hits — damaging that final enemy too — so it damages up to N+1 enemies total; with the default Pierce = 1 the bullet passes through the first enemy it hits and despawns on the second.**

Rationale for this definition: it makes the user's phrase "pierce through 1 enemy" literally true at the default (`pierceBase = 1` → passes through 1 enemy), and it degrades naturally — `pierceBase = 0` would reproduce today's non-piercing behaviour (despawn on first hit).

---

## (c) Per-shot pierce counter — EXACT drop-in code (`projectileBehaviour.cs`)

Add a per-shot counter field, reset it in `OnEnable` (pooled reset), increment on each enemy hit, and only `ret` once the counter EXCEEDS the pierce allotment.

**1. Add field** (near line 8, alongside `lifeTimer`):
```csharp
    private float lifeTimer;
    private Vector3 spawnOrigin;
    private int enemiesHit;   // per-shot pierce counter; reset in OnEnable (pooled reset)
```

**2. Reset in `OnEnable`** (inside the existing body, `projectileBehaviour.cs:11-17`):
```csharp
    void OnEnable()
    {
        lifeTimer = 0f;
        enemiesHit = 0;
        spawnOrigin = transform.position;
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null) rb.linearVelocity = Vector2.zero;
    }
```

**3. Replace the enemy-hit block** (`projectileBehaviour.cs:44-54`) with:
```csharp
        if (other.CompareTag("Enemy"))
        {
            enemyHealth eh = other.GetComponent<enemyHealth>();
            if (eh != null)
            {
                int dmg = worldState.instance != null ? Mathf.RoundToInt(worldState.instance.AttackDamage()) : 1;
                eh.takeDamage(dmg);
            }

            enemiesHit++;
            int pierce = worldState.instance != null ? worldState.instance.Pierce() : 1;
            if (enemiesHit > pierce)
            {
                if (objectPool.instance != null) objectPool.instance.ret(gameObject);
            }
        }
```

Walkthrough at default `Pierce() == 1`:
- Enemy #1: damage applied, `enemiesHit = 1`, `1 > 1` is false → bullet keeps travelling (pierced enemy #1).
- Enemy #2: damage applied, `enemiesHit = 2`, `2 > 1` is true → `ret` (damages enemy #2, then despawns). ✔ passes through exactly 1 enemy.

Notes:
- The wall block (lines 38-42) is untouched — walls always despawn immediately regardless of pierce.
- The `Debug.Log("hit")` line is dropped in the rewrite (optional cleanup; harmless to keep — not load-bearing).
- A single collider can only fire `OnTriggerEnter2D` once per enemy, so each enemy counts at most once toward `enemiesHit`.

---

## (d) worldState pierce field — drop-in code (`worldState.cs`)

`worldState` is a plain class exposing `worldState.instance`, with `xxxBase`/`xxxMult` pairs and getters. Pierce is **flat-only**, so it gets a base field and getter but **NO `Mult` field** (deliberate — there is nothing to multiply).

Add alongside the other stat fields (after `pickupRadiusMult`, `worldState.cs:24`):
```csharp
    public int pierceBase = 1;   // enemies a bullet passes through before despawning; 1 = "pierce through 1 enemy" default
```

Add getter alongside the other getters (after `PickupRadius()`, `worldState.cs:34`):
```csharp
    public int Pierce() => pierceBase;
```

`pierceBase = 1` gives the required default: the bullet passes through 1 enemy. (Setting it to `0` would reproduce the old despawn-on-first-hit behaviour.)

---

## (e) FLAT-ONLY `StatKind.Pierce` — drop-in code (`levelUpMenuController.cs`)

Pierce must appear as a Flat upgrade only and **never roll as a Percent**. Four edits, and one explicit exclusion.

**1. Add to the enum** (`levelUpMenuController.cs:7`):
```csharp
    private enum StatKind { MaxHP, FireRate, AttackDamage, MoveSpeed, Range, Defense, Regen, PickupRadius, Pierce }
```

**2. Add to the `stats` array in `BuildPool()`** (`levelUpMenuController.cs:52-62`):
```csharp
        StatKind[] stats =
        {
            StatKind.MaxHP,
            StatKind.FireRate,
            StatKind.AttackDamage,
            StatKind.MoveSpeed,
            StatKind.Range,
            StatKind.Defense,
            StatKind.Regen,
            StatKind.PickupRadius,
            StatKind.Pierce
        };
```

**3. EXCLUDE Pierce from the Percent branch in `BuildPool()`.** The loop (`levelUpMenuController.cs:68-78`) currently adds Flat for every stat, then adds Percent unless gated. Add a `continue` for Pierce AFTER the Flat add and BEFORE the Percent add, so Pierce never enters the pool as Percent:
```csharp
        foreach (StatKind k in stats)
        {
            // Flat is always offered.
            pool.Add(new Upgrade { kind = k, mode = Mode.Flat });

            // Pierce is a flat-only stat — never offer it as a percent.
            if (k == StatKind.Pierce) continue;

            // Percent is inert on a 0 base for Defense/Regen — only offer once seeded.
            if (k == StatKind.Defense && !defenseHasBase) continue;
            if (k == StatKind.Regen && !regenHasBase) continue;

            pool.Add(new Upgrade { kind = k, mode = Mode.Percent });
        }
```
This is the single source of truth for what can roll. Because no `Upgrade { kind = Pierce, mode = Percent }` is ever created, `LabelFor`/`Choose` can never receive a Percent Pierce.

**4. Add the Flat label** in `LabelFor()`'s flat switch (`levelUpMenuController.cs:87-98`):
```csharp
                case StatKind.PickupRadius: return "+0.5 Pickup Radius";
                case StatKind.Pierce: return "+1 Pierce";
                default: return "";
```
Do **NOT** add a Pierce case to the percent switch (`levelUpMenuController.cs:101-112`); leaving it out means a stray percent Pierce (which cannot occur) would harmlessly return `""`.

**5. Add the Flat mutation** in `Choose()`'s flat switch (`levelUpMenuController.cs:119-152`):
```csharp
                case StatKind.PickupRadius:
                    worldState.instance.pickupRadiusBase += 0.5f;
                    break;
                case StatKind.Pierce:
                    worldState.instance.pierceBase += 1;
                    break;
```
Do **NOT** add a Pierce case to the percent switch (`levelUpMenuController.cs:153-186`). There is no `pierceMult`, and a percent Pierce can never be rolled, so the percent switch's default no-op is the correct guard.

**How Pierce is guaranteed never to roll as a percent:** it is excluded at the *only* place upgrades are constructed — the `continue` in `BuildPool()` — so no downstream code (label or mutation) can ever be handed a `Mode.Percent` Pierce. `+1 Pierce` is an integer flat step, consistent with the "flat value (not a percentage)" requirement.

---

## (f) Grep-based acceptance checklist

```bash
# Pierce field + getter exist in worldState (flat-only: base + getter, NO mult)
grep -n "pierceBase" Assets/Scripts/worldState.cs           # expect: public int pierceBase = 1;
grep -n "int Pierce()" Assets/Scripts/worldState.cs         # expect: public int Pierce() => pierceBase;
grep -c "pierceMult" Assets/Scripts/worldState.cs           # expect: 0  (no multiplier — flat only)

# Bullet uses a per-shot counter reset in OnEnable and compares against Pierce()
grep -n "enemiesHit" Assets/Scripts/projectileBehaviour.cs  # expect: field, "enemiesHit = 0;" in OnEnable, "enemiesHit++"
grep -n "worldState.instance.Pierce()" Assets/Scripts/projectileBehaviour.cs  # expect: used in OnTriggerEnter2D
grep -n "enemiesHit > pierce" Assets/Scripts/projectileBehaviour.cs           # expect: the despawn gate

# Enemy projectile is UNTOUCHED
git diff --name-only | grep enemyProjectile.cs              # expect: NO output

# Pierce is in the enum, flat label, flat mutation
grep -n "StatKind.Pierce" Assets/Scripts/levelUpMenuController.cs   # expect: enum, stats[], BuildPool continue, LabelFor, Choose
grep -n "+1 Pierce" Assets/Scripts/levelUpMenuController.cs         # expect: flat label
grep -n "pierceBase += 1" Assets/Scripts/levelUpMenuController.cs   # expect: flat mutation

# Pierce is NEVER a percent: exactly one guard, and no percent label/mutation
grep -n "k == StatKind.Pierce) continue" Assets/Scripts/levelUpMenuController.cs  # expect: the exclusion in BuildPool
# Manual check: percent switch in LabelFor (lines ~101-112) and Choose (lines ~153-186) contain NO "case StatKind.Pierce"
```

Behavioural acceptance (Play Mode, manual):
- Default run: a single bullet fired into a line of ≥2 enemies damages 2 of them (passes through the first, despawns on the second).
- After picking "+1 Pierce" once: the same bullet damages 3 enemies.
- The level-up menu never displays a "% Pierce" option across many rolls.

---

## (g) Risk notes

1. **Must not change enemy projectile behaviour.** All edits are confined to `projectileBehaviour.cs` (player bullet), `worldState.cs`, and `levelUpMenuController.cs`. `enemyProjectile.cs` is NOT edited — confirm with `git diff --name-only | grep enemyProjectile.cs` returning nothing.
2. **Pooled-state reset correctness.** `enemiesHit` is per-shot mutable state; it MUST be reset in `OnEnable` (`projectileBehaviour.cs:11`). Because pooled objects reset state in `OnEnable` (project convention) and `get` re-enables the object, resetting there is correct and consistent with the existing `lifeTimer = 0f;` reset. If `enemiesHit` were not reset, a reused bullet would carry a stale count and despawn too early (or never), corrupting pierce for the object's whole lifetime.
3. **Flat-only invariant is single-sourced.** Pierce is excluded from percent at exactly one point (the `continue` in `BuildPool`). Do not add a `pierceMult` or a percent `case` — doing so would silently re-enable a percentage upgrade the requirement forbids. The percent switches' `default` no-op is the intended backstop.
4. **`OfferCount` unaffected.** `BuildPool` now yields one more Flat entry; the pool still exceeds `OfferCount` (5), so the Fisher-Yates shuffle + slice is unaffected. No change to `buttons`/`labels` array sizing.
5. **Semantics are off-by-one sensitive.** The gate is `enemiesHit > pierce` (strictly greater), evaluated AFTER incrementing. Using `>=` would despawn on the first hit at `Pierce()==1` (0 pierce), contradicting the default. The chosen form is deliberate and matches definition (b).

---

## Handoff

This is a design document only. Implementation of the three C# files should be dispatched to `@agent-csharp-dev` (Docker), followed by `@agent-build-validator` (Editor) for compile + Play Mode verification of the pierce behaviour and the flat-only menu roll.
