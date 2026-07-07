# Design: Time-Based Enemy TYPE Progression (project-cucumber)

**Date:** 2026-07-07
**Author:** project-planner (DESIGN ONLY — no implementation)
**Feature (verbatim):** "instead of by level I'd like the enemy type progression to increment over time instead. after 2 mins, shooters should start (keep the easing in increments and replace levels with minutes for now) after 5 mins a boss should spawn. a boss should spawn every 5 mins, make the timing of all this configurable in worldstate. the volume of enemies should remain gated by level up."

---

## (a) Current structure + anchors

### `Assets/Scripts/enemySpawner.cs` (MonoBehaviour)
- `SpawnEntry` struct — anchor `Assets/Scripts/enemySpawner.cs:5-11`:
  - `public GameObject prefab;`
  - `public int minLevel;`  ← **LEVEL-based unlock to be replaced with TIME**
  - `public float weight;`
- Fields: `spawnTable` (`:13`), `eligible`/`eligibleWeights` lists (`:15-16`), `spawnInterval` fallback (`:17`), `unlockRampLevels = 4` (`:20`), `spawnTimer` (`:21`).
- `Update()` (`:23-80`):
  - RATE/volume tick: `spawnTimer += Time.deltaTime;` (`:31`) and **`float interval = worldState.instance.currentSpawnInterval;`** (`:32`) — **THIS is the level-gated VOLUME. DO NOT TOUCH.**
  - `int lvl = worldState.instance.level;` (`:36`) — used ONLY for the type-eligibility loop below.
  - Eligibility/ramp loop (`:42-55`): `if (... lvl < spawnTable[i].minLevel) continue;` (`:44`); ramp `float t = Mathf.Clamp01((lvl - spawnTable[i].minLevel + 1) / (float)ramp);` (`:48`); `float w = baseWeight * t;` (`:49`).
  - Weighted pick (`:59-66`) + edge-spawn placement (`:68-78`). **Placement/pick logic is unchanged by this design.**

### `Assets/Scripts/bossSpawner.cs` (MonoBehaviour)
- Fields: `bossPrefabs[]` (`:8`), `edgeMargin` (`:9`), `activeBoss` (`:10`), **`private bool spawned;`** (`:11`) ← spawn-once flag to be replaced.
- `Update()` (`:15-21`): `if (spawned) return;` (`:17`); player-null retry (`:18`); **`if (worldState.instance.level < 5) return;`** (`:19`) ← LEVEL gate to be replaced with TIME cadence.
- `SpawnBoss()` (`:28-57`): alive-guard `if (activeBoss != null && activeBoss.activeInHierarchy) return;` (`:30`); player-null retry (`:31`); empty-array fail-safe (`:34`); random pick + null-slot skip (`:40-41`); edge placement (`:43-51`); `Instantiate` (`:53`); `bossShooter shooter = ...; shooter.RandomizePattern();` (`:54-55`); `spawned = true;` (`:56`).
- Existing TODO hook at `:23-26` already anticipates this exact change.

### `Assets/Scripts/worldState.cs` (plain static-instance class, `worldState.instance`)
- Level-based VOLUME mechanism — anchor `Assets/Scripts/worldState.cs:62-65` + `:76`:
  - `baseSpawnInterval`, `spawnIntervalCoefficient`, `minSpawnInterval`, `currentSpawnInterval`.
  - `addXP()` (`:69-80`) mutates `level++` (`:75`) and **`currentSpawnInterval = Mathf.Max(minSpawnInterval, currentSpawnInterval - spawnIntervalCoefficient * (1f / level));`** (`:76`). **This is the level→volume coupling. UNCHANGED.**
- Insertion anchor for new config: after `:65` (spawn-interval block) / before `OnLevelUp` event (`:67`).

---

## (b) Chosen elapsed-run-time source

**Source: `Time.timeSinceLevelLoad`, read directly inside each spawner's `Update()`.**

- **No new component. No Editor wiring. No `worldState.runTime` needed.** Both spawners are already MonoBehaviours that run `Update()`, so they read `Time.timeSinceLevelLoad` inline — zero scene changes.
- Resets automatically to 0 on scene (re)load → correct "elapsed run time" for a fresh run.
- Respects `Time.timeScale` (scaled time), so progression **pauses** while the level-up menu / pause menu freeze the game (`levelUpManager.cs:69`, `pauseMenuController.cs:36`, `gameOverManager.cs:14` all set `timeScale = 0`). This is the desired behavior — the boss/shooter clock should not advance while paused. It is also consistent with the existing `spawnTimer += Time.deltaTime` (also scaled) at `enemySpawner.cs:31`.
- **Grep confirmed** no pre-existing run timer, `worldState.runTime`, or `Time.timeSinceLevelLoad` usage anywhere in `Assets/Scripts/`, so there is nothing to reuse or conflict with.

> **No Editor step required for the time source.** (One minor Editor *data* step is unavoidable — see Risk note — because renaming the serialized `SpawnEntry.minLevel` (int) → `minTimeSeconds` (float) drops the old inspector values, so `spawnTable` entries must be re-populated with unlock times, e.g. shooter = 120.)

---

## (c) worldState config fields + defaults

Add to `worldState.cs` (single source of truth for all progression timings):

```csharp
// --- Time-based TYPE progression (seconds of elapsed run time) ---
// Per-type unlock times live on each enemySpawner.SpawnEntry.minTimeSeconds.
// e.g. the shooter entry should be set to shooterStartTime (120s) in the inspector.
public float shooterStartTime  = 120f;   // reference default: shooters begin phasing in at 2:00
public float unlockRampSeconds = 30f;    // a newly-unlocked type ramps 0 -> full weight over this window

// --- Repeating boss cadence (seconds of elapsed run time) ---
public float bossFirstTime = 300f;   // first boss at 5:00
public float bossInterval  = 300f;   // then another every 5:00
```

Notes:
- `shooterStartTime` is a documented reference value; the *actual* per-type unlock is `SpawnEntry.minTimeSeconds` (set the shooter entry to `120`). This keeps the table data-driven (each type can unlock at its own time) while worldState owns the shared ramp + boss cadence.
- `unlockRampSeconds` replaces the role of `unlockRampLevels` (now measured in seconds, centralized in worldState).

---

## (d) EXACT drop-in code

### 1. `worldState.cs` — insert config block after line 65 (before `public static event ... OnLevelUp;` at `:67`)

```csharp
    public float baseSpawnInterval = 1.75f;
    public float spawnIntervalCoefficient = 0.3f;
    public float minSpawnInterval = 0.6f;
    public float currentSpawnInterval = 1.75f;

    // --- Time-based TYPE progression (seconds of elapsed run time) ---
    public float shooterStartTime  = 120f;   // reference: shooters begin at 2:00 (set on the shooter SpawnEntry)
    public float unlockRampSeconds = 30f;    // newly-unlocked type ramps 0 -> full weight over this window

    // --- Repeating boss cadence (seconds of elapsed run time) ---
    public float bossFirstTime = 300f;   // first boss at 5:00
    public float bossInterval  = 300f;   // then every 5:00
```
*(No `runTime` field — the elapsed source is `Time.timeSinceLevelLoad`, read in the spawners.)*
**`addXP()` and the `currentSpawnInterval` block are UNCHANGED.**

---

### 2. `enemySpawner.cs` — time-based unlock + time ramp

Replace the `SpawnEntry` struct (`:5-11`):
```csharp
    [System.Serializable]
    private struct SpawnEntry
    {
        public GameObject prefab;
        public float minTimeSeconds;   // eligible when elapsed run time >= minTimeSeconds
        public float weight;           // relative base weight; <= 0 is treated as 1
    }
```

Remove the `unlockRampLevels` field (`:20`) — ramp now lives in worldState. (Keep `spawnTable`, `eligible`, `eligibleWeights`, `spawnInterval`, `edgeMargin`, `spawnTimer` as-is.)

Replace the eligibility section inside `Update()`. Current `:36-55`:
```csharp
            int lvl = worldState.instance != null ? worldState.instance.level : 1;

            eligible.Clear();
            eligibleWeights.Clear();
            float totalWeight = 0f;
            int ramp = Mathf.Max(1, unlockRampLevels);   // guards against a deserialized 0
            for (int i = 0; i < spawnTable.Length; i++)
            {
                if (spawnTable[i].prefab == null || lvl < spawnTable[i].minLevel) continue;

                float baseWeight = spawnTable[i].weight > 0f ? spawnTable[i].weight : 1f;
                // 1/ramp of base weight at the unlock level, growing linearly to full weight over `ramp` levels.
                float t = Mathf.Clamp01((lvl - spawnTable[i].minLevel + 1) / (float)ramp);
                float w = baseWeight * t;
                if (w <= 0f) continue;

                eligible.Add(spawnTable[i].prefab);
                eligibleWeights.Add(w);
                totalWeight += w;
            }
```
becomes:
```csharp
            float elapsed = Time.timeSinceLevelLoad;

            eligible.Clear();
            eligibleWeights.Clear();
            float totalWeight = 0f;
            // Ramp window in SECONDS, from worldState (guards against a deserialized 0).
            float ramp = (worldState.instance != null)
                ? Mathf.Max(0.0001f, worldState.instance.unlockRampSeconds)
                : 30f;
            for (int i = 0; i < spawnTable.Length; i++)
            {
                if (spawnTable[i].prefab == null || elapsed < spawnTable[i].minTimeSeconds) continue;

                float baseWeight = spawnTable[i].weight > 0f ? spawnTable[i].weight : 1f;
                // Linear phase-in: 0 at unlock time, growing to full weight over `ramp` seconds.
                float t = Mathf.Clamp01((elapsed - spawnTable[i].minTimeSeconds) / ramp);
                float w = baseWeight * t;
                if (w <= 0f) continue;

                eligible.Add(spawnTable[i].prefab);
                eligibleWeights.Add(w);
                totalWeight += w;
            }
```
Everything below (`if (eligible.Count == 0 ...)`, weighted pick, placement, `objectPool.instance.get(...)`) is **UNCHANGED**. The RATE line `float interval = worldState.instance.currentSpawnInterval;` (`:32`) is **UNCHANGED**.

> Easing note: the discrete `+1` from the level version is dropped because time is continuous — the analogue of "1/ramp at unlock, linear to full over `ramp`" is `Clamp01((elapsed - minTime)/rampSeconds)`. A type is weight 0 for the single frame at its exact unlock instant, then phases in smoothly. Behaviorally identical intent, cleaner in continuous time.

---

### 3. `bossSpawner.cs` — repeating time cadence (replace spawn-once flag)

Replace field `private bool spawned;` (`:11`) with:
```csharp
    private float nextBossTime = -1f;   // lazily initialized to bossFirstTime on first valid frame
```

Replace `Update()` (`:15-21`):
```csharp
    void Update()
    {
        if (worldState.instance == null || worldState.instance.player == null) return;   // player-null retry preserved
        if (nextBossTime < 0f) nextBossTime = worldState.instance.bossFirstTime;          // first boss at bossFirstTime
        if (Time.timeSinceLevelLoad < nextBossTime) return;

        // Only advance the cadence when a boss actually spawns; if one is still alive,
        // SpawnBoss() no-ops via its alive-guard and we retry next frame (spawns as soon as it dies).
        if (SpawnBoss())
            nextBossTime += worldState.instance.bossInterval;   // schedule the next boss (+5:00 default)
    }
```

Change `SpawnBoss()` to return `bool` (so the cadence only advances on a real spawn). Current signature/return points `:28`, `:30`, `:31`, `:34`, `:37`, `:41`, `:56`:
```csharp
    bool SpawnBoss()
    {
        if (activeBoss != null && activeBoss.activeInHierarchy) return false;   // one boss at a time
        if (worldState.instance == null || worldState.instance.player == null) return false;

        if (bossPrefabs == null || bossPrefabs.Length == 0) return false;   // empty-array fail-safe

        Camera cam = Camera.main;
        if (cam == null) return false;

        GameObject chosen = bossPrefabs[Random.Range(0, bossPrefabs.Length)];   // random pick preserved
        if (chosen == null) return false;

        int side = Random.Range(0, 4);
        Vector3 vp;
        if (side == 0)      vp = new Vector3(-edgeMargin, Random.value, 0f);
        else if (side == 1) vp = new Vector3(1f + edgeMargin, Random.value, 0f);
        else if (side == 2) vp = new Vector3(Random.value, -edgeMargin, 0f);
        else                vp = new Vector3(Random.value, 1f + edgeMargin, 0f);
        vp.z = Mathf.Abs(cam.transform.position.z);
        Vector3 point = cam.ViewportToWorldPoint(vp);
        point.z = 0f;

        activeBoss = Instantiate(chosen, point, Quaternion.identity);
        bossShooter shooter = activeBoss.GetComponent<bossShooter>();
        if (shooter != null) shooter.RandomizePattern();   // RandomizePattern preserved
        return true;
    }
```
The old `spawned = true;` line is removed (replaced by the `nextBossTime` accumulator in `Update()`). The `Start(){}` stub and `edgeMargin`/`activeBoss` fields and the FUTURE-CADENCE TODO comment can stay or the TODO be deleted; no functional dependency.

---

## (e) Level-based VOLUME code — explicit UNCHANGED statement

**The volume/rate of enemy spawns remains gated by player level. It is NOT moved to time.**

- The volume knob is **`worldState.currentSpawnInterval`**, computed on each level-up in `worldState.addXP()` at **`worldState.cs:76`** (`currentSpawnInterval = Mathf.Max(minSpawnInterval, currentSpawnInterval - spawnIntervalCoefficient * (1f / level));`), and consumed as the spawn cadence at **`enemySpawner.cs:32`** (`float interval = worldState.instance.currentSpawnInterval;`).
- This design **touches neither line**. `addXP()`, `level`, `currentSpawnInterval`, `baseSpawnInterval`, `spawnIntervalCoefficient`, `minSpawnInterval`, and the `enemySpawner.cs:31-33` rate tick are all left exactly as-is. Only the **TYPE-eligibility** loop (which prefab is chosen) switches from `level`/`minLevel` to `Time.timeSinceLevelLoad`/`minTimeSeconds`. Rate (how often) stays on level; type (which enemy) moves to time.

---

## (f) Grep acceptance checklist

After implementation, these should hold:

```bash
# TYPE progression is time-based, level references removed from the eligibility loop:
grep -n "minTimeSeconds"        Assets/Scripts/enemySpawner.cs   # struct field + eligibility test present
grep -n "timeSinceLevelLoad"    Assets/Scripts/enemySpawner.cs   # elapsed source used
grep -c "minLevel"              Assets/Scripts/enemySpawner.cs   # EXPECT 0
grep -c "unlockRampLevels"      Assets/Scripts/enemySpawner.cs   # EXPECT 0

# Boss is a repeating time cadence, spawn-once flag gone:
grep -n "timeSinceLevelLoad"    Assets/Scripts/bossSpawner.cs    # cadence check present
grep -n "nextBossTime"          Assets/Scripts/bossSpawner.cs    # accumulator present
grep -c "bool spawned"          Assets/Scripts/bossSpawner.cs    # EXPECT 0
grep -c "level < 5"             Assets/Scripts/bossSpawner.cs    # EXPECT 0

# Timings centralized in worldState:
grep -n "shooterStartTime\|unlockRampSeconds\|bossFirstTime\|bossInterval" Assets/Scripts/worldState.cs   # 4 fields

# VOLUME still level-gated and UNTOUCHED:
grep -n "currentSpawnInterval"  Assets/Scripts/worldState.cs Assets/Scripts/enemySpawner.cs   # :76 mutate + :32 read intact
```

---

## (g) Risk note

1. **Serialized-field rename drops inspector data (unavoidable Editor DATA step).** Renaming `SpawnEntry.minLevel` (int) → `minTimeSeconds` (float) and removing `unlockRampLevels` means Unity cannot map old serialized values. After the code change, the `enemySpawner` component's `spawnTable` must be re-populated in the Editor with unlock times (shooter entry → `120`; base enemies → `0`). This is the ONLY Editor step and it is data-entry, not new wiring. **FLAG for scene-architect / a Unity-Editor pass.** (Mirrors the existing `bossPrefabs` migration note already in `bossSpawner.cs:6-7`.)
2. **Scaled time pauses progression.** `Time.timeSinceLevelLoad` respects `Time.timeScale`, so the shooter/boss clock freezes during level-up and pause menus. This is intended and consistent with the existing spawn tick — noted so it is not mistaken for a bug.
3. **Boss cadence + still-alive boss.** If a boss is alive when the next cadence fires, `SpawnBoss()` no-ops and `nextBossTime` does not advance, so the next boss spawns the instant the current one dies (then cadence resumes +interval). This preserves "one boss at a time" and avoids pile-ups; it does NOT strictly guarantee a boss exactly every 300s if a fight runs long — an acceptable, arguably desirable, trade-off. Flag if strict fixed-interval stacking is later wanted.
4. **`baseSpawnInterval` is defined but only `currentSpawnInterval` is read** (pre-existing, out of scope) — no change, just noted.
