# Design: Time-Based Enemy HP Scaling (+50% every 7 min, spawn-applied, pooled-safe, boss-inclusive)

**Author:** project-planner (Tier 2 — DESIGN ONLY, no implementation)
**Date:** 2026-07-07
**Feature (verbatim):** *"make enemy health scale over time, every 7 mins enemies should recieve 50% more hp, this should only affect monsters that are newly spawned after the 7 min mark and should affect bosses too."*

---

## (a) Anchors — where HP is initialized at spawn, in the REAL files

All line numbers are from the current `Assets/Scripts/enemyHealth.cs`.

| Anchor | File:line | Current code | Role |
|---|---|---|---|
| Serialized base HP | `enemyHealth.cs:5` | `[SerializeField] private int maxHp = 3;` | Design-time base HP per enemy prefab. **This is the base we multiply — it must NEVER be mutated at runtime** (pooled objects would compound it). |
| Runtime current HP | `enemyHealth.cs:6` | `private int currentHp;` | Live HP. |
| **Spawn-init hook** | `enemyHealth.cs:28-31` | `void OnEnable() { currentHp = maxHp; }` | **THE anchor.** This is where currentHp is (re)initialized from maxHp. |
| Boss/HUD health-bar getters | `enemyHealth.cs:19-20` | `public int MaxHp => maxHp;` / `public int CurrentHp => currentHp;` | The boss world-space health bar reads these for its fill proportion (`CurrentHp / MaxHp`). |
| Boss marker (already exists) | `enemyHealth.cs:16,25` | `_isBoss = GetComponent<bossBehaviour>() != null;` | Confirms the SAME `enemyHealth` component is on the boss. |

### Confirmation it runs on EVERY pooled respawn
`objectPool` disables an object on `ret()` and re-enables it on `get()`. Unity fires **`OnEnable` every time a GameObject transitions disabled→enabled**, i.e. on every pool checkout. `Awake` (lines 22-26) fires only once per object lifetime and caches `flash`/`_isBoss` — correctly NOT the place for per-spawn HP. The prompt's stated pool contract ("pooled objects RESET their state in OnEnable each time they are (re)spawned") matches exactly: **`OnEnable` at line 28-31 is the correct, per-respawn hook.** Because the multiplier is computed here and applied to a fresh `currentHp` each checkout, a living enemy is never re-scaled — requirement (2) falls out naturally.

---

## (b) Chosen scaling formula (one sentence)

> **ADDITIVE tiers:** the HP multiplier applied at spawn is `1 + hpScalePerTier * floor(Time.timeSinceLevelLoad / hpScaleInterval)`, so every 7 minutes a newly-spawned enemy gains **+50% of its BASE HP** (multipliers `1.0, 1.5, 2.0, 2.5, 3.0 …`).

**Worked tier values (interval = 420 s, perTier = 0.5):**

| Elapsed run time | tier = floor(t/420) | multiplier | Example base 100 → HP |
|---|---|---|---|
| 0:00 – 6:59 | 0 | 1.00 | 100 |
| 7:00 – 13:59 | 1 | 1.50 | 150 |
| 14:00 – 20:59 | 2 | 2.00 | 200 |
| 21:00 – 27:59 | 3 | 2.50 | 250 |
| 28:00 – 34:59 | 4 | 3.00 | 300 |

**Why additive, not compounding:** "receive 50% more hp" reads most naturally as a fixed +50%-of-base increment per interval, and additive scaling is linear/predictable for balancing (an enemy at 42 min is 4× base, not 7.6×). Compounding `(1+perTier)^tier` (`1.0, 1.5, 2.25, 3.375 …`) ramps super-linearly and quickly out-scales player DPS. **Retune to compounding with a one-line change** (shown commented in the code below) if a steeper curve is wanted — same two config fields drive both.

---

## (c) worldState config fields (with defaults)

Mirrors the existing plain-field + helper-getter style already in `worldState.cs` (e.g. `bossFirstTime`/`bossInterval` at lines 78-80, getters like `MaxHP()` at line 57).

| Field | Default | Meaning |
|---|---|---|
| `hpScaleInterval` | `420f` | Seconds per tier (7 minutes). |
| `hpScalePerTier` | `0.5f` | Fraction of BASE HP added per tier (+50%). |

Both are public mutable floats → tunable in code and by any future upgrade/menu system, exactly like the other `worldState` config.

---

## (d) EXACT drop-in C# code

### `worldState.cs` — add near the boss-cadence block (after line 80)

```csharp
    // --- Time-based ENEMY HP scaling (seconds of elapsed run time) ---
    // Every hpScaleInterval seconds, NEWLY-spawned enemies (and bosses) get
    // +hpScalePerTier of their BASE hp. ADDITIVE: mult = 1 + perTier * tier.
    public float hpScaleInterval = 420f;   // 7 minutes per tier
    public float hpScalePerTier  = 0.5f;   // +50% of base per tier

    // Multiplier for HP applied AT SPAWN, from elapsed run time.
    // Uses Time.timeSinceLevelLoad — the run-time source already adopted by
    // enemySpawner/bossSpawner — so all time-based systems agree.
    public float EnemyHpTimeMultiplier()
    {
        if (hpScaleInterval <= 0f) return 1f;   // guard: no divide-by-zero / disable
        int tier = Mathf.FloorToInt(Time.timeSinceLevelLoad / hpScaleInterval);
        if (tier < 0) tier = 0;
        return 1f + hpScalePerTier * tier;
        // COMPOUNDING alternative (retune): return Mathf.Pow(1f + hpScalePerTier, tier);
    }
```

### `enemyHealth.cs` — three edits

**1. Add a scaled-max field (after line 6):**
```csharp
    private int currentHp;
    private int scaledMaxHp;   // ADDED: base maxHp * time multiplier, recomputed each (re)spawn
```

**2. Point the health-bar getter at the scaled max (replace line 19):**
```csharp
    public int MaxHp => scaledMaxHp;   // CHANGED: bars read the scaled max so fill proportions stay correct
    public int CurrentHp => currentHp;
```

**3. Compute & apply the multiplier at the spawn-init hook (replace OnEnable, lines 28-31):**
```csharp
    void OnEnable()
    {
        float mult = (worldState.instance != null) ? worldState.instance.EnemyHpTimeMultiplier() : 1f;
        scaledMaxHp = Mathf.Max(1, Mathf.RoundToInt(maxHp * mult));   // never mutate serialized maxHp
        currentHp = scaledMaxHp;
    }
```

**Key correctness points:**
- The serialized **`maxHp` (base) is read but never written**, so pooled reuse can't compound the multiplier — the multiplier is derived fresh from elapsed time each `OnEnable`.
- `scaledMaxHp` and `currentHp` are both set from the same value → the bar starts at exactly 100% fill.
- `Mathf.Max(1, …)` prevents a 0-HP enemy from any rounding pathology.

---

## (e) Boss coverage + health-bar proportion confirmation

- **Bosses are covered automatically.** The boss uses the SAME `enemyHealth` component (confirmed by `_isBoss = GetComponent<bossBehaviour>() != null;` at `enemyHealth.cs:25` — the marker only exists because the boss carries `enemyHealth`). Because the scaling lives entirely in `enemyHealth.OnEnable`, the boss inherits it with **zero boss-specific code**.
- **Boss health bar stays correct.** The bar reads `MaxHp`/`CurrentHp` (lines 19-20). After the change, `MaxHp => scaledMaxHp` and `currentHp` are both initialized to `scaledMaxHp`, so fill = `CurrentHp / MaxHp` starts at 1.0 and decreases correctly as damage is taken. **Had we scaled only `currentHp` and left `MaxHp => maxHp` (base), the bar would read >100% and overflow — which is exactly why edit #2 is required.**
- If the boss is pooled/respawned, its multiplier is recomputed at its own spawn time — a boss that spawns at 14:30 gets tier-2 (×2.0), consistent with monsters spawning at that moment.

---

## (f) Grep acceptance checklist

```bash
# worldState config + helper present
grep -n 'hpScaleInterval'        Assets/Scripts/worldState.cs   # expect: field = 420f
grep -n 'hpScalePerTier'         Assets/Scripts/worldState.cs   # expect: field = 0.5f
grep -n 'EnemyHpTimeMultiplier'  Assets/Scripts/worldState.cs   # expect: helper method def
grep -n 'timeSinceLevelLoad'     Assets/Scripts/worldState.cs   # expect: used inside helper

# enemyHealth wires it at the spawn hook
grep -n 'scaledMaxHp'                       Assets/Scripts/enemyHealth.cs   # expect: field + MaxHp getter + OnEnable
grep -n 'EnemyHpTimeMultiplier'             Assets/Scripts/enemyHealth.cs   # expect: called in OnEnable
grep -n 'MaxHp => scaledMaxHp'              Assets/Scripts/enemyHealth.cs   # expect: getter now returns scaled

# regression guard: base maxHp must NOT be reassigned anywhere at runtime
grep -n 'maxHp *=' Assets/Scripts/enemyHealth.cs   # expect ONLY the serialized declaration (line 5), no runtime assignment
```

Manual acceptance:
1. Spawn an enemy at t<7:00 → HP == base.
2. Spawn same-prefab enemy at t≥7:00 → HP == base×1.5; an enemy already alive from before 7:00 is unchanged.
3. Boss spawning after a tier boundary shows a full (100%) bar at the scaled max.

---

## (g) Risk notes

1. **Pooled OnEnable reset correctness (highest risk).** The whole design hinges on `maxHp` being a read-only base. If any future edit does `maxHp = scaledMaxHp` (or similar) the multiplier compounds every respawn and HP explodes. The grep `maxHp *=` guard above must return only line 5.
2. **Never scale already-living enemies.** Satisfied structurally: the multiplier is only ever read in `OnEnable`, never on a timer, so a living enemy keeps the HP it spawned with. Do NOT add an `Update` that re-reads the multiplier.
3. **Integer-HP rounding.** `Mathf.RoundToInt(maxHp * mult)` on small base values rounds (e.g. base 3 × 1.5 = 4.5 → 5). This is fine and intended; `Mathf.Max(1, …)` floors at 1 so nothing spawns dead. Rounding is per-spawn and deterministic for a given tier.
4. **`Time.timeSinceLevelLoad` scope.** Consistent with `enemySpawner`/`bossSpawner` (the adopted run-time source). It resets on level (re)load, which is the desired "elapsed run time" semantics. If the game is paused via `Time.timeScale = 0`, `timeSinceLevelLoad` also pauses — desirable (tiers track real gameplay time, not wall-clock).
5. **Null-safety.** `worldState.instance` is null-guarded in `OnEnable` (falls back to ×1.0), so early bootstrap spawns before `worldState` exists won't NRE.

---

## Handoff

This is a DESIGN document only — no code was written to `enemyHealth.cs` or `worldState.cs`. Implementation belongs to **@agent-csharp-dev** (both are pure `.cs` file edits → `run_agent_in_docker`), then **@agent-build-validator** for the Unity compile/Play-Mode gate. Route via `/scrum-master`.
