# Design: Projectile Size stat + level-up reward

**Feature (verbatim):** "add projectile size as a stat and also as a level up reward"
**Date:** 2026-07-08 · **Author:** project-planner (design only — no implementation)

Mirror the existing base+mult stat model in `worldState`, add a `StatKind.ProjectileSize`
level-up reward in `levelUpMenuController`, and apply the size to the player bullet at
spawn so both the sprite and its 2D collider scale together. Default `ProjectileSize()`
is **1.0**, so behavior is unchanged until the player takes the upgrade.

---

## (a) Anchors (verified — no invented anchors)

| # | File | Line(s) | Anchor / relevance |
|---|------|---------|--------------------|
| A1 | `Assets/Scripts/projectileBehaviour.cs` | 12–19 | `void OnEnable()` — pooled reset (lifeTimer/enemiesHit/spawnOrigin/rb velocity). **This is where scale must be re-applied each spawn.** |
| A2 | `Assets/Scripts/projectileBehaviour.cs` | 1–10 | Class has **no `Awake()`** and **no stored base scale** today. We must add both a base-scale capture and (optionally) `Awake`. |
| A3 | `Assets/Scripts/playerProjectileShooter.cs` | 32 | `GameObject shot = objectPool.instance.get(projectile, transform.position, transform.rotation);` — the player shooter spawn site (pooled `get()`, not `Instantiate`). |
| A4 | `Assets/Scripts/worldState.cs` | 23–24 | `pickupRadiusBase/Mult` — insert new stat fields directly after (last of the float base+mult pairs). |
| A5 | `Assets/Scripts/worldState.cs` | 44 | `pickupRadiusFlatStep = 0.5f;` — insert `projectileSizeFlatStep` after. |
| A6 | `Assets/Scripts/worldState.cs` | 60 | `PickupRadius()` getter — insert `ProjectileSize()` after. |
| A7 | `Assets/Scripts/levelUpMenuController.cs` | 7 | `enum StatKind { ... PickupRadius, Pierce, XpGain }` — add `ProjectileSize`. |
| A8 | `Assets/Scripts/levelUpMenuController.cs` | 54–63 | `stats` array in `BuildPool()` — add entry. |
| A9 | `Assets/Scripts/levelUpMenuController.cs` | 111 / 129 | `LabelFor` Flat & Percent `PickupRadius` cases — add `ProjectileSize` after each. |
| A10 | `Assets/Scripts/levelUpMenuController.cs` | 168–170 / 209–211 | `Choose` Flat & Percent `PickupRadius` cases — add `ProjectileSize` after each. |

**Spawn-path decision:** The player shooter (A3) uses `objectPool.instance.get(...)`, which
re-activates a pooled GameObject → fires `OnEnable` (A1) every spawn. Therefore **apply scale in
`projectileBehaviour.OnEnable`, not at the shooter site.** This keeps the shooter untouched, works
for any future spawner of the same prefab, and guarantees each reused bullet reads the **current**
`ProjectileSize()` at the moment it fires (so upgrades apply live).

---

## (b) worldState stat fields + getter + step config

`worldState.cs` — after line 24 (`pickupRadiusMult = 1f;`):

```csharp
    // Projectile visual+collider scale multiplier. Effective = base * mult, applied to
    // the player bullet's localScale at spawn (OnEnable). Default 1.0 -> size unchanged.
    public float projectileSizeBase = 1f;
    public float projectileSizeMult = 1f;
```

After line 44 (`pickupRadiusFlatStep = 0.5f;`):

```csharp
    public float projectileSizeFlatStep = 0.2f;   // +0.2 size per Flat upgrade
```

After line 60 (`PickupRadius()` getter):

```csharp
    public float ProjectileSize() => projectileSizeBase * projectileSizeMult;
```

No new percent field needed — reuse the existing shared `levelUpPercentStep` (line 50),
exactly like AttackDamage/Range/etc.

**Defaults keep size ×1.0:** `1f * 1f = 1.0`. Until an upgrade is chosen, `ProjectileSize()`
returns 1.0 and `localScale = baseScale × 1.0` = the prefab's authored scale → behavior unchanged.

---

## (c) levelUpMenuController additions

**Flat-only vs flat+percent decision: FLAT + PERCENT.**
Rationale: projectile size is a strictly-positive multiplicative stat with a non-zero base
(`projectileSizeBase = 1f`), so `mult *= 1.1` is meaningful and non-inert — identical to how
AttackDamage/Range/PickupRadius are handled. It is **not** in the flat-only family (Pierce, XpGain),
which are excluded from the percent branch because they're integer/capped counters. So `ProjectileSize`
needs **no** `BuildPool` guard — it flows through the default Flat+Percent path.

**A7 — enum (line 7):**
```csharp
    private enum StatKind { MaxHP, FireRate, AttackDamage, MoveSpeed, Range, Defense, Regen, PickupRadius, Pierce, XpGain, ProjectileSize }
```

**A8 — `stats` array in `BuildPool()` (add after `StatKind.XpGain`, line 63):**
```csharp
            StatKind.XpGain,
            StatKind.ProjectileSize
```
(No `continue`/guard needed — it takes both Flat and Percent like the other multiplicative stats.)

**A9 — `LabelFor` Flat switch (after PickupRadius case, line 111):**
```csharp
                case StatKind.ProjectileSize: return "+" + ws.projectileSizeFlatStep.ToString(ci) + " Projectile Size";
```
**`LabelFor` Percent switch (after PickupRadius case, line 129):**
```csharp
            case StatKind.ProjectileSize: return "+" + pct + "% Projectile Size";
```

**A10 — `Choose` Flat switch (after PickupRadius case, line 170):**
```csharp
                case StatKind.ProjectileSize:
                    ws.projectileSizeBase += ws.projectileSizeFlatStep;
                    break;
```
**`Choose` Percent switch (after PickupRadius case, line 211):**
```csharp
                case StatKind.ProjectileSize:
                    ws.projectileSizeMult *= p;
                    break;
```

> Note: `OfferCount = 5` (line 16). Adding another stat only widens the shuffle pool; no
> count change required. (The `// 3` comments on lines 18/22 are pre-existing and out of scope.)

---

## (d) Drop-in code — apply localScale from ProjectileSize() each spawn

Applied in `projectileBehaviour.cs`. Capture the prefab's authored scale **once** in `Awake`
(a stored base), then multiply that stored base by `ProjectileSize()` on **every** `OnEnable`.
This prevents compounding across pooled respawns (see risk note (f)).

**Add field (near line 8, with the other private fields):**
```csharp
    private Vector3 baseScale = Vector3.one;   // authored prefab scale, captured once
    private bool baseScaleCaptured;
```

**Add `Awake` (new method, before `OnEnable`):**
```csharp
    void Awake()
    {
        baseScale = transform.localScale;   // capture the prefab's authored scale ONCE
        baseScaleCaptured = true;
    }
```

**Modify `OnEnable` (lines 12–19) — add the scale line:**
```csharp
    void OnEnable()
    {
        lifeTimer = 0f;
        enemiesHit = 0;
        spawnOrigin = transform.position;

        // Re-apply size EVERY spawn from the stored base, reading the CURRENT stat so
        // upgrades taken mid-run apply to the very next bullet. Never multiply the live
        // localScale (that would compound across pooled reuse).
        if (!baseScaleCaptured) { baseScale = transform.localScale; baseScaleCaptured = true; } // safety if OnEnable precedes Awake
        float size = worldState.instance != null ? worldState.instance.ProjectileSize() : 1f;
        transform.localScale = baseScale * size;

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null) rb.linearVelocity = Vector2.zero;
    }
```

**Why the `baseScaleCaptured` guard:** Unity may raise the first `OnEnable` before/around `Awake`
depending on activation ordering; the guard ensures `baseScale` is the authored scale, captured
from an unscaled instance, not an already-scaled one. `CircleCollider2D`/`BoxCollider2D` radius/size
follow `transform.localScale` automatically in 2D, so the collider scales with the sprite — no
separate collider edit needed.

---

## (e) Grep acceptance checklist

```bash
# worldState: fields, step, getter
grep -n 'projectileSizeBase'      Assets/Scripts/worldState.cs   # -> field = 1f
grep -n 'projectileSizeMult'      Assets/Scripts/worldState.cs   # -> field = 1f
grep -n 'projectileSizeFlatStep'  Assets/Scripts/worldState.cs   # -> = 0.2f
grep -n 'ProjectileSize()'        Assets/Scripts/worldState.cs   # -> getter base*mult

# levelUpMenuController: enum + pool + label + choose (Flat & Percent)
grep -n 'ProjectileSize'          Assets/Scripts/levelUpMenuController.cs   # -> 6 hits: enum, pool, 2x LabelFor, 2x Choose
grep -c 'ProjectileSize'          Assets/Scripts/levelUpMenuController.cs   # -> 6

# projectileBehaviour: base scale + apply
grep -n 'baseScale'               Assets/Scripts/projectileBehaviour.cs    # -> field + Awake + OnEnable
grep -n 'transform.localScale = baseScale' Assets/Scripts/projectileBehaviour.cs  # -> applied in OnEnable
grep -n 'ProjectileSize()'        Assets/Scripts/projectileBehaviour.cs    # -> read each spawn

# shooter site untouched (still pooled get())
grep -n 'objectPool.instance.get' Assets/Scripts/playerProjectileShooter.cs  # -> line 32, unchanged
```

Acceptance: builds with 0 errors; a fresh run fires size-1.0 bullets; after taking a
"Projectile Size" upgrade, the very next bullet is visibly larger and its collider hits at the
larger radius; repeated pooled bullets stay at the correct (non-compounding) size.

---

## (f) Risk note — pooled respawn must re-apply from a stored base

`objectPool.get()` reuses the **same** GameObject instance across shots. If `OnEnable` did
`transform.localScale *= ProjectileSize()` (multiplying the live scale), every reuse would compound:
1.0 → 1.2 → 1.44 → … an ever-growing bullet. The design avoids this by:

- Capturing the authored scale **once** in `Awake` into `baseScale` (never overwritten after capture).
- Setting `transform.localScale = baseScale * ProjectileSize()` (assignment, not multiply) each `OnEnable`.

Secondary risks & mitigations:
- **First OnEnable before Awake:** guarded by `baseScaleCaptured` so `baseScale` is captured from an
  unscaled instance the first time regardless of order.
- **worldState null (e.g. test scene):** falls back to `size = 1f` → authored scale, no exception.
- **Prefab authored at non-uniform scale:** `baseScale * size` scales uniformly by the stat and
  preserves any authored non-uniform ratio — correct.
- **Non-player projectiles sharing this component:** if enemies ever reuse `projectileBehaviour`,
  they would also scale by the player stat. Currently only the player shooter (A3) spawns it, so
  this is not a live issue — flag for scrum-master if the component is later shared.

---

## Handoff

Design complete. Implementation belongs to `csharp-dev` (all three edits are `.cs` file edits,
no Editor access required → `run_agent_in_docker`). No scene/prefab changes required beyond the
prefab already carrying `projectileBehaviour` and a 2D collider.

Invoke `/scrum-master` with this design to generate the work breakdown.
