# Item Projectile + Weapon Mods Design — project-cucumber

**Date:** 2026-07-08
**Author:** project-planner (Tier 2, design only — no implementation)
**Scope:** PROJECTILE + WEAPON modifications for three items (**Cone**, **Bounce**, **Explode**) and the on-hit **Fire/Freeze status wiring**. All gated on `playerInventory.instance.Has(ItemId.X)` per the framework contract (`.voltron/analyses/2026-07-08-item-framework-design.md`).

> **PLAYER-ONLY (confirmed):** The player bullet is `Assets/Scripts/projectileBehaviour.cs` — spawned by `playerProjectileShooter.cs`. The enemy bullet is a **different** component, `enemyProjectile.cs`, spawned by `shooterBehaviour.cs`/`bossShooter.cs`. **Every edit below lands in `projectileBehaviour.cs` and `playerProjectileShooter.cs` ONLY.** Nothing in this doc touches `enemyProjectile.cs`, `shooterBehaviour.cs`, or `bossShooter.cs`.

---

## 1. Anchors (real file references)

### Weapon spawn site — `Assets/Scripts/playerProjectileShooter.cs`
- **Class:** `public class playerProjectileShooter : MonoBehaviour` (L3). Fields: `[SerializeField] GameObject projectile` (L6), `[SerializeField] float projectileSpeed = 10f` (L7).
- **Fire gate:** `if (shootTimer >= worldState.instance.FireCooldown())` (L13).
- **Nearest-enemy selection:** L16–28 (`FindGameObjectsWithTag("Enemy")` → `nearest`).
- **THE SPAWN SITE (Cone hooks here):** L30–41. `objectPool.instance.get(projectile, transform.position, transform.rotation)` (L32) → `rb.linearVelocity = dir * projectileSpeed` (L37), where `dir = ((Vector2)nearest.transform.position - myPos).normalized` (L36). `shootTimer = 0` on L40.
- **Note:** the player bullet is aimed by setting `Rigidbody2D.linearVelocity` **externally here** — `projectileBehaviour` has **no `Launch()` method** (that API is on `enemyProjectile`). Cone reuses this exact spawn+aim, N times at offset angles.

### Player projectile — `Assets/Scripts/projectileBehaviour.cs`
- **Class:** `public class projectileBehaviour : MonoBehaviour` (L3). Fields: `lifeSeconds` (L5), `wallLayer` (L6), `fallbackRange` (L7); private `lifeTimer`, `spawnOrigin`, `enemiesHit` (L8–10).
- **OnEnable (pooled reset):** L20–35 — resets `lifeTimer=0`, `enemiesHit=0`, `spawnOrigin=transform.position`, re-applies scale, zeroes `rb.linearVelocity`. **`hasBounced` reset is added here.**
- **Update — RANGE/LIFETIME EXPIRY despawn path:** L37–52. `traveled >= limit` → `ret()` (L41–45); `lifeTimer >= lifeSeconds` → `ret()` (L47–51). **This path must NOT bounce.**
- **OnTriggerEnter2D — wall + ENEMY-HIT despawn path:** L54–78. Wall → `ret()` (L56–60). Enemy branch (L62–77): `eh.takeDamage(dmg)` (L64–69), then `enemiesHit++` (L71), `pierce = worldState...Pierce()` (L72), **`if (enemiesHit > pierce) ret()` (L73–76) ← this is the FINAL-TARGET despawn.** Bounce + Explode + Fire/Freeze all hook inside this enemy branch.

### Stats — `Assets/Scripts/worldState.cs`
- `AttackDamage()` (L58) — now large (prior ×10 rebalance); explosion's ⅓ is relative, unaffected.
- `Range()` (L62) = `rangeBase(2.5) * rangeMult`. Explosion radius scales off this.
- `Pierce()` (L68), `ProjectileSize()` (L67). **New tunable fields added here (§6).**

### Enemy health — `Assets/Scripts/enemyHealth.cs`
- **Damage sink (confirmed signature):** `public void takeDamage(int amount)` (L36). Handles death/pooling internally. Explosion + normal hit both call this.
- **Status API (ASSUMED, from parallel design):** each enemy exposes `ApplyFire()` and `ApplyFreeze(float seconds)`. We CALL these; we do NOT design their internals. Call via `other.GetComponent<enemyHealth>()` (already fetched as `eh` at L64) or the component that hosts them — see §5 note.

---

## 2. CONE (ItemId.Cone) — fire 3 projectiles in a cone

**Where:** `playerProjectileShooter.cs`, replacing the single spawn at L30–41.
**Behaviour:** if `Has(ItemId.Cone)`, fire **3** shots — center `dir`, plus `dir` rotated by `±coneHalfAngleDeg`. Otherwise fire 1 (unchanged). Each shot is an independent pooled bullet aimed by `rb.linearVelocity`, so pierce/bounce/explode/status all work per-bullet with no extra wiring.

**Drop-in replacement for L30–41:**
```csharp
if (nearest != null)
{
    Vector2 dir = ((Vector2)nearest.transform.position - myPos).normalized;

    bool cone = playerInventory.instance != null && playerInventory.instance.Has(ItemId.Cone);
    if (cone)
    {
        float half = worldState.instance != null ? worldState.instance.coneHalfAngleDeg : 15f;
        FireOne(RotateVec(dir, -half));
        FireOne(dir);
        FireOne(RotateVec(dir, +half));
    }
    else
    {
        FireOne(dir);
    }

    // Reset cooldown only after a shot actually fires so the cadence repeats.
    shootTimer = 0;
}
```

**New helper methods on `playerProjectileShooter` (add to the class):**
```csharp
// Spawns one player bullet aimed along `dir`. Same spawn+aim as the original inline code.
private void FireOne(Vector2 dir)
{
    GameObject shot = objectPool.instance.get(projectile, transform.position, transform.rotation);
    Rigidbody2D rb = shot.GetComponent<Rigidbody2D>();
    if (rb != null) rb.linearVelocity = dir * projectileSpeed;
}

// Rotate a 2D vector by `deg` degrees (CCW positive).
private static Vector2 RotateVec(Vector2 v, float deg)
{
    float r = deg * Mathf.Deg2Rad;
    float c = Mathf.Cos(r), s = Mathf.Sin(r);
    return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
}
```
**Notes:** `FireOne` is the original L32–37 body verbatim, factored out so cone and non-cone share it. Cone count is fixed at 3 (center + 2) per the framework note (`Has` is boolean, not stackable). Half-angle is a worldState tunable (§6).

---

## 3. BOUNCE (ItemId.Bounce) — one-time redirect on FINAL-TARGET hit only

**Where:** `projectileBehaviour.cs`, inside `OnTriggerEnter2D`, at the **pierce-exhausted** branch (L73–76) — the ONLY final-target despawn. **Never** in `Update` (the range/lifetime path).

**Disambiguation (CRITICAL — final-target vs range-expiry):** the two despawn causes live in **two different methods**:
- **Enemy-hit final despawn** = `OnTriggerEnter2D`, `enemiesHit > pierce`. → bounce eligible.
- **Range/lifetime expiry** = `Update`, `traveled >= limit` or `lifeTimer >= lifeSeconds`. → **NOT** eligible; `Update` contains no bounce code, so expiry can never bounce.

Because bounce logic is physically placed only in the enemy branch, the disambiguation is structural — no flag is needed to tell the causes apart. A per-projectile `hasBounced` flag prevents an **infinite** bounce chain (bounce fires at most once per shot).

**New fields (add near L10):**
```csharp
private bool hasBounced;   // one-time bounce guard; reset in OnEnable (pooled reset)
[SerializeField] private float bounceSearchRadius = 6f;   // OverlapCircle radius for next target
```

**OnEnable reset (add inside OnEnable, alongside `enemiesHit = 0;` at L23):**
```csharp
        hasBounced = false;
```

**Replace the pierce-exhausted branch (L73–76) with:**
```csharp
            if (enemiesHit > pierce)
            {
                // BOUNCE: on the FINAL enemy hit (pierce exhausted), redirect ONCE toward
                // the nearest OTHER enemy instead of despawning. Range/lifetime expiry lives
                // in Update() and never reaches here, so it can never bounce.
                if (!hasBounced
                    && playerInventory.instance != null
                    && playerInventory.instance.Has(ItemId.Bounce)
                    && TryBounce(other))
                {
                    hasBounced = true;
                    enemiesHit = 0;   // grant fresh pierce budget for the post-bounce segment
                    return;           // do NOT despawn — keep flying toward the new target
                }

                if (objectPool.instance != null) objectPool.instance.ret(gameObject);
            }
```

**New helper method `TryBounce` (add to `projectileBehaviour`):**
```csharp
// Finds the nearest enemy other than `justHit` within bounceSearchRadius and redirects
// this projectile toward it, preserving current speed. Returns false if none found.
private bool TryBounce(Collider2D justHit)
{
    Vector2 pos = transform.position;
    Collider2D[] hits = Physics2D.OverlapCircleAll(pos, bounceSearchRadius);
    Transform best = null;
    float bestSqr = Mathf.Infinity;
    foreach (Collider2D c in hits)
    {
        if (c == null || c == justHit) continue;
        if (!c.CompareTag("Enemy")) continue;
        float sqr = ((Vector2)c.transform.position - pos).sqrMagnitude;
        if (sqr < bestSqr) { bestSqr = sqr; best = c.transform; }
    }
    if (best == null) return false;

    Rigidbody2D rb = GetComponent<Rigidbody2D>();
    if (rb == null) return false;
    float speed = rb.linearVelocity.magnitude;
    if (speed <= 0.001f) speed = 10f;   // fallback if velocity was zeroed
    Vector2 dir = ((Vector2)best.position - pos).normalized;
    rb.linearVelocity = dir * speed;

    // Re-anchor the range odometer so the post-bounce leg gets a fresh Range() budget
    // (otherwise the bullet may instantly exceed traveled>=limit and despawn next frame).
    spawnOrigin = transform.position;
    return true;
}
```
**Notes:**
- **One-time:** `hasBounced` gates a single bounce; the second final-target hit despawns normally. No infinite chains.
- **Speed preserved** from live `linearVelocity`; `spawnOrigin` reset so the range-expiry check (`Update`) doesn't kill the bounced bullet immediately.
- `enemiesHit = 0` gives the post-bounce leg the same pierce budget; alternative (leave as-is → bounced bullet dies on first new enemy) is noted as a tuning choice, not required.
- Excludes `justHit` explicitly (the collider just processed) so it can't bounce back into the same enemy.

---

## 4. EXPLODE (ItemId.Explode) — range-scaled AoE for ⅓ attack damage

**Where:** `projectileBehaviour.cs`, inside the enemy branch of `OnTriggerEnter2D`, **after** the direct `eh.takeDamage(dmg)` (after L69), before the pierce/bounce logic.
**Behaviour:** if `Has(ItemId.Explode)`, `Physics2D.OverlapCircleAll` at the hit position, radius = `Range() * explosionRadiusFactor`, dealing `floor(AttackDamage()/3)` to **each** enemy in radius via `enemyHealth.takeDamage`.

**New tunable field (add near L10):**
```csharp
[SerializeField] private float explosionRadiusFactor = 1f;   // radius = Range() * this
```

**Insert after L69 (`eh.takeDamage(dmg);` block close), inside `if (other.CompareTag("Enemy"))`:**
```csharp
                // EXPLODE: range-scaled AoE dealing 1/3 attack damage to every enemy nearby.
                if (playerInventory.instance != null && playerInventory.instance.Has(ItemId.Explode))
                {
                    float baseDmg = worldState.instance != null ? worldState.instance.AttackDamage() : 3f;
                    int splash = Mathf.FloorToInt(baseDmg / 3f);   // the 1/3-damage item; relative, ×10-safe
                    if (splash > 0)
                    {
                        float range = worldState.instance != null ? worldState.instance.Range() : 2.5f;
                        float factor = worldState.instance != null ? worldState.instance.explosionRadiusFactor : 1f;
                        float radius = range * factor;
                        Collider2D[] near = Physics2D.OverlapCircleAll(transform.position, radius);
                        foreach (Collider2D c in near)
                        {
                            if (c == null || !c.CompareTag("Enemy")) continue;
                            enemyHealth splashEh = c.GetComponent<enemyHealth>();
                            if (splashEh != null) splashEh.takeDamage(splash);   // includes the directly-hit enemy (extra 1/3)
                        }
                    }
                }
```
**Formula:** `radius = Range() * explosionRadiusFactor` (default factor `1.0`). `splash = floor(AttackDamage() / 3)`. The directly-hit enemy takes its normal hit **plus** a splash tick (acceptable; excluding it is a one-line `if (c == justHit) continue` tuning option). Explosion does not itself despawn the bullet — normal pierce/bounce flow continues.

---

## 5. STATUS WIRING (ItemId.Fire / ItemId.Freeze) at the hit site

**Where:** `projectileBehaviour.cs`, enemy branch, **immediately after the direct `eh.takeDamage(dmg)`** (after L69) and **before** Explode/pierce/bounce. Order: **damage first, then status**, so a status that (in the parallel design) reads state sees the post-hit enemy, and so freeze/fire apply even on a killing blow harmlessly (the parallel `ApplyFire/ApplyFreeze` own their own null/dead guards).

**New tunable fields (add near L10):**
```csharp
[SerializeField] private float freezeChance   = 0.2f;   // roll per hit when Freeze owned
[SerializeField] private float freezeDuration = 2f;     // seconds passed to ApplyFreeze
```

**Insert after L69, before the Explode block:**
```csharp
                // STATUS: apply on-hit effects to the enemy just struck (damage already applied).
                if (eh != null && playerInventory.instance != null)
                {
                    if (playerInventory.instance.Has(ItemId.Fire))
                        eh.ApplyFire();

                    if (playerInventory.instance.Has(ItemId.Freeze)
                        && Random.value < freezeChance)
                        eh.ApplyFreeze(freezeDuration);
                }
```
**Notes:**
- `eh` is the already-fetched `enemyHealth` for the struck enemy (L64). **ASSUMPTION:** `ApplyFire()` / `ApplyFreeze(float)` live on `enemyHealth` (or are reachable through it). If the parallel design hosts them on a separate `enemyStatus` component, replace `eh.ApplyFire()` with `other.GetComponent<enemyStatus>()?.ApplyFire()` — a one-line swap. **Flag for handoff (§8 open question).**
- `Random.value` is `UnityEngine.Random`, `[0,1)` — correct for a probability roll.
- Fire always applies when owned; Freeze is chance-gated. Both are per-hit, so cone/bounce/explode multiply status opportunities naturally.

---

## 6. Configurable params + defaults

Tunables that describe **weapon/economy balance** go in `worldState.cs` (single source of truth, mutable at runtime like the other stats). Tunables that are **per-prefab projectile mechanics** stay as `[SerializeField]` on `projectileBehaviour` (set once on the bullet prefab). Split rationale below.

**Add to `worldState.cs` (public fields, alongside the stat block ~L50):**
```csharp
    // --- Item: Cone ---
    public float coneHalfAngleDeg = 15f;      // ± spread of the 3-shot cone
    // --- Item: Explode ---
    public float explosionRadiusFactor = 1f;  // AoE radius = Range() * this
```

**Keep as `[SerializeField]` on `projectileBehaviour` (prefab-authored):**
| Param | Default | Meaning |
|-------|---------|---------|
| `bounceSearchRadius` | `6f` | `OverlapCircle` radius to find the next bounce target |
| `explosionRadiusFactor` | (read from worldState) | — factor lives in worldState; radius computed at hit |
| `freezeChance` | `0.2f` | per-hit probability to freeze |
| `freezeDuration` | `2f` | seconds passed to `ApplyFreeze` |
| explosion damage fraction | **`1/3` (hard-coded `/ 3f`)** | the ⅓-damage item; fraction is intrinsic to the item's identity, not a balance knob |

**Rationale for the split:** `coneHalfAngleDeg` and `explosionRadiusFactor` are referenced across systems and are natural runtime-balance levers → worldState. `bounceSearchRadius`, `freezeChance`, `freezeDuration` are localized to the bullet and read every hit → cheap as serialized prefab fields. Either location is defensible; if a single home is preferred, move all five into worldState (§8). The ⅓ fraction is deliberately **not** configurable — it defines the Explode item.

---

## 7. Grep acceptance checklist

Run after implementation lands:
```bash
# Cone at the PLAYER spawn site (not enemy shooters)
grep -n 'ItemId.Cone\|FireOne\|RotateVec\|coneHalfAngleDeg' Assets/Scripts/playerProjectileShooter.cs

# Bounce: one-time flag, reset in OnEnable, final-target-only redirect
grep -n 'hasBounced' Assets/Scripts/projectileBehaviour.cs          # expect: field decl, OnEnable reset, guard in OnTriggerEnter2D
grep -n 'TryBounce\|bounceSearchRadius\|ItemId.Bounce' Assets/Scripts/projectileBehaviour.cs

# Explode: OverlapCircleAll + 1/3 damage + range-scaled radius
grep -n 'OverlapCircleAll\|ItemId.Explode\|explosionRadiusFactor\|/ 3f' Assets/Scripts/projectileBehaviour.cs

# Status wiring
grep -n 'ApplyFire\|ApplyFreeze\|ItemId.Fire\|ItemId.Freeze\|freezeChance' Assets/Scripts/projectileBehaviour.cs

# worldState tunables
grep -n 'coneHalfAngleDeg\|explosionRadiusFactor' Assets/Scripts/worldState.cs

# PLAYER-ONLY: none of this leaks into the enemy bullet/shooters
grep -n 'ItemId\.\|playerInventory' Assets/Scripts/enemyProjectile.cs Assets/Scripts/shooterBehaviour.cs Assets/Scripts/bossShooter.cs   # expect: NO matches
```
Expected: cone helpers in the player shooter; `hasBounced` appears 3× in projectileBehaviour; `OverlapCircleAll` + `/ 3f` present; `ApplyFire/ApplyFreeze` called; worldState has the two tunables; **zero** item references in enemy files.

---

## 8. Risk notes

1. **Pooled reset of `hasBounced`:** MUST be reset in `OnEnable` (alongside the existing `enemiesHit = 0;`). If missed, a pooled bullet reused after a bounce would never bounce again — silent degradation. Mirrors the existing `enemiesHit` pooled-reset pattern, so the risk is low if the convention is followed.
2. **Final-target vs range-expiry disambiguation:** guaranteed **structurally** — bounce code exists only in `OnTriggerEnter2D`'s `enemiesHit > pierce` branch; the range/lifetime `ret()` calls in `Update` contain no bounce path. Do NOT refactor despawn into a shared helper that both paths call, or the distinction collapses. `spawnOrigin` is re-anchored on bounce so the post-bounce leg isn't instantly killed by `traveled >= limit`.
3. **Infinite bounce:** prevented by the one-time `hasBounced` flag (at most one redirect per shot). Even with many enemies, the bullet bounces once then despawns on its next final-target hit.
4. **PLAYER-ONLY:** all edits are confined to `projectileBehaviour.cs` + `playerProjectileShooter.cs`. `enemyProjectile.cs`/`shooterBehaviour.cs`/`bossShooter.cs` are untouched (grep-verified in §7). Enemy bullets never read `playerInventory`.
5. **Explode self-hit:** the OverlapCircle includes the directly-struck enemy, giving it hit + splash. Intended (marginal extra ⅓); exclude with `if (c == other) continue` if undesired.
6. **`ApplyFire/ApplyFreeze` host uncertainty (OPEN):** this design calls them on `eh` (`enemyHealth`). If the parallel status design hosts them on a separate component, the call target is a one-line swap (§5). **Confirm the host component before implementation.**
7. **Cone perf:** 3× bullets → 3× pooled objects + physics. Acceptable for a survivors game but note if pool sizes were tuned for 1 shot; the object pool should be sized for the cone case.

---

## 9. Open questions for human input

1. **Status host component:** are `ApplyFire()`/`ApplyFreeze(float)` on `enemyHealth` or a separate `enemyStatus`? (Determines the exact call target in §5.)
2. **Tunable home:** keep the split (cone/explode in worldState; bounce/freeze on the prefab) or consolidate all five into worldState for one balance surface?
3. **Bounce pierce budget:** reset `enemiesHit = 0` after bounce (post-bounce leg re-pierces) vs leave it (bounced bullet dies on first new enemy). Design defaults to reset; confirm.
4. **Explode self-splash:** include or exclude the directly-hit enemy from the AoE? Default: include.
