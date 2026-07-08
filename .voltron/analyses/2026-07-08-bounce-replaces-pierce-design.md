# Design: Bounce Replaces Piercing, Scales with Pierce()

**Date:** 2026-07-08
**Agent:** project-planner (Tier 2, DESIGN ONLY — no implementation)
**Task (verbatim):** "make the bounce upgrade just replace piercing and scale based on your piercing amount"
**Target file:** `Assets/Scripts/projectileBehaviour.cs`
**Read-only refs:** `Assets/Scripts/worldState.cs`

---

## Summary of intended behavior

| Player state | Behavior |
|---|---|
| **Has `ItemId.Bounce`** | Bullet does **NOT** pierce. On each enemy hit it **redirects to the nearest other enemy**. Number of bounces = `worldState.instance.Pierce()`. Upgrading Pierce increases bounces. After the bounce budget is spent (or no target found), it despawns. |
| **Does NOT have Bounce** | **Unchanged.** Normal piercing: passes through `Pierce()` enemies via `enemiesHit`, then despawns. |

The two are **mutually exclusive**: when Bounce is owned the `enemiesHit` pierce counter is never consulted, so the bullet cannot both pierce and bounce.

---

## (a) Anchors in current code (`projectileBehaviour.cs`)

**Pierce field + one-time bounce flag — lines 10–11:**
```csharp
private int enemiesHit;   // per-shot pierce counter; reset in OnEnable (pooled reset)
private bool hasBounced;  // one-time bounce guard; reset in OnEnable (pooled reset)
```

**OnEnable pooled reset — lines 21–26:**
```csharp
void OnEnable()
{
    lifeTimer = 0f;
    enemiesHit = 0;
    hasBounced = false;
    spawnOrigin = transform.position;
```

**Update() expiry path — lines 39–54** (range + lifetime despawn; **must never reach bounce logic** — structural guarantee is that bounce lives only in `OnTriggerEnter2D`).

**Hit decision / existing one-time bounce — lines 107–125:**
```csharp
enemiesHit++;
int pierce = worldState.instance != null ? worldState.instance.Pierce() : 1;
if (enemiesHit > pierce)
{
    // BOUNCE: on the FINAL enemy hit (pierce exhausted), redirect ONCE ...
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

**`TryBounce(Collider2D justHit)` — lines 131–158:** finds nearest enemy `!= justHit` within `bounceSearchRadius`, redirects velocity, re-anchors `spawnOrigin`. **Kept as-is** (already excludes the just-hit collider and re-anchors the range odometer).

**`worldState.cs` reference — line 83:** `public int Pierce() => pierceBase;` (`pierceBase = 1` default, line 31). This is the scaling source for the bounce budget.

---

## (b) Exact drop-in changes

### Change 1 — replace the one-time flag field (line 11)
**Remove:**
```csharp
private bool hasBounced;  // one-time bounce guard; reset in OnEnable (pooled reset)
```
**Add:**
```csharp
private int bounceCount;  // Bounce: bounces used this shot; budget = Pierce(); reset in OnEnable (pooled reset)
```

### Change 2 — OnEnable reset (line 25)
**Remove:**
```csharp
        hasBounced = false;
```
**Add:**
```csharp
        bounceCount = 0;
```

### Change 3 — replace the hit-decision block (lines 107–125)
**Remove** the entire `enemiesHit++; ... ret(gameObject);` block (lines 107–125) and **replace** with:
```csharp
            int pierce = worldState.instance != null ? worldState.instance.Pierce() : 1;

            bool hasBounce = playerInventory.instance != null
                             && playerInventory.instance.Has(ItemId.Bounce);

            if (hasBounce)
            {
                // BOUNCE REPLACES PIERCE: the bullet never passes through. On each enemy
                // hit it redirects to the nearest OTHER enemy, up to Pierce() bounces
                // (upgrading Pierce raises the bounce budget). enemiesHit is intentionally
                // NOT touched here, so pierce and bounce are mutually exclusive when owned.
                // Range/lifetime expiry lives in Update() and never reaches here.
                if (bounceCount < pierce && TryBounce(other))
                {
                    bounceCount++;
                    return;   // keep flying toward the new target; do NOT despawn
                }
                if (objectPool.instance != null) objectPool.instance.ret(gameObject);
            }
            else
            {
                // PIERCE (unchanged for non-owners): pass through Pierce() enemies, then despawn.
                enemiesHit++;
                if (enemiesHit > pierce)
                {
                    if (objectPool.instance != null) objectPool.instance.ret(gameObject);
                }
            }
```

`TryBounce` (lines 131–158) is **unchanged**.

---

## (c) Non-owners keep pierce unchanged

In the original code the bounce attempt was gated by `playerInventory.instance.Has(ItemId.Bounce)`; a non-owner always failed that check and fell straight through to `ret(gameObject)`. The new `else` branch is byte-for-byte the same logic that a non-owner hit before:

```
enemiesHit++;
if (enemiesHit > pierce) { despawn }
```

So a player without Bounce experiences **identical** piercing behavior — `bounceCount` is never read, `TryBounce` is never called.

### Bounce math (owner)
- `Pierce()==1`: hit E1 → bounce (count 1) → hit E2 → count(1) `<` pierce(1) is false → despawn. **2 enemies struck, 1 bounce.**
- `Pierce()==3`: E1→bounce→E2→bounce→E3→bounce→E4→despawn. **4 enemies struck, 3 bounces.**

Bounces scale linearly with the Pierce stat, exactly as with the pierce budget.

---

## (d) Grep acceptance

Run after implementation (all must hold):
```bash
cd /workspace
grep -c 'hasBounced' Assets/Scripts/projectileBehaviour.cs                 # expect 0 (flag fully removed)
grep -n  'private int bounceCount' Assets/Scripts/projectileBehaviour.cs   # expect 1 (field declared)
grep -n  'bounceCount = 0'         Assets/Scripts/projectileBehaviour.cs   # expect 1 (OnEnable reset)
grep -n  'bounceCount < pierce'    Assets/Scripts/projectileBehaviour.cs   # expect 1 (Pierce-scaled budget gate)
grep -n  'Has(ItemId.Bounce)'      Assets/Scripts/projectileBehaviour.cs   # expect >=1 (mutual-exclusion gate)
grep -n  'enemiesHit++'            Assets/Scripts/projectileBehaviour.cs   # expect 1, inside the else (pierce) branch only
```

---

## (e) Risk notes

1. **Pooled reset of `bounceCount`.** `bounceCount` MUST be reset in `OnEnable` (Change 2). Projectiles come from `objectPool`; without the reset a reused bullet would start with a stale count and refuse to bounce. Mirrors the existing `enemiesHit = 0` reset — keep both.

2. **Enemy-hit-only / never on expiry.** Bounce logic stays entirely inside `OnTriggerEnter2D`'s `other.CompareTag("Enemy")` block. `Update()`'s range (`traveled >= limit`) and lifetime (`lifeTimer >= lifeSeconds`) despawns are untouched and call `ret(gameObject)` directly — they can never bounce. This is structural; do not add bounce logic to `Update()`.

3. **No infinite loop / re-hitting the same enemy.** `TryBounce` already excludes the just-hit collider (`c == justHit` skip), so a bullet never bounces off the enemy it just struck. It *could* later curve back toward a previously-hit enemy, but the `bounceCount < pierce` budget hard-caps total bounces at `Pierce()`, so the bullet always terminates. There is no unbounded recursion — each bounce consumes one budget unit.

4. **Wall hits unaffected.** The wall-layer early return (lines 58–62) is above the enemy block and always despawns; bounce never applies to walls.

5. **Balance shift (informational).** Owners lose pass-through and instead spread damage across `Pierce()+1` distinct enemies. This is the intended design ("replace piercing"), but it changes single-target DPS — flag for playtest tuning, not a code defect.

---

## Handoff

Implementation belongs to `@agent-csharp-dev` (file-only C# edit, Docker-eligible). Post-edit, `@agent-build-validator` should confirm 0 compile errors in the Unity console.
