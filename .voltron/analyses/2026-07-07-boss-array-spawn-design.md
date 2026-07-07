# Boss Array Spawn — Refactor Design

**Date:** 2026-07-07
**Author:** project-planner (Tier 2, DESIGN ONLY)
**Target file:** `Assets/Scripts/bossSpawner.cs`
**Feature (verbatim):** "I'd like to be able to preconfigure some bosses and have the boss spawner pick a random one to spawn, please move the implementation around to make this possible"

---

## (a) Current `bossSpawner.cs` structure — file:line anchors

All anchors below are from the file as it exists today (47 lines total).

| Anchor | Element | Notes |
|---|---|---|
| `bossSpawner.cs:5` | `[SerializeField] private GameObject bossPrefab;` | **The single-prefab field being replaced.** This is the serialized reference that will be dropped on migration. |
| `bossSpawner.cs:6` | `[SerializeField] private float edgeMargin = 0.08f;` | Spawn-edge margin, unchanged. |
| `bossSpawner.cs:7` | `private GameObject activeBoss;` | "Already alive" tracking, unchanged. |
| `bossSpawner.cs:8` | `private bool spawned;` | **Spawn-once guard flag**, unchanged. |
| `bossSpawner.cs:12-18` | `Update()` | Gate chain: `spawned` guard (14), `worldState.instance`/`player` null-retry (15), `level < 5` gate (16), then `SpawnBoss()` (17). **All preserved.** |
| `bossSpawner.cs:20-23` | Future timed-cadence TODO comment block | Preserved verbatim. |
| `bossSpawner.cs:25-46` | `SpawnBoss()` | Body detailed below. |
| `bossSpawner.cs:27` | `if (activeBoss != null && activeBoss.activeInHierarchy) return;` | "Already alive" re-entry guard, unchanged. |
| `bossSpawner.cs:28` | `if (worldState.instance == null ... player == null) return;` | Redundant null re-check, unchanged. |
| `bossSpawner.cs:29-30` | `Camera cam = Camera.main; if (cam == null || bossPrefab == null) return;` | **Camera + prefab null guard — the `bossPrefab == null` half is what we convert into the empty-array fail-safe.** |
| `bossSpawner.cs:32-40` | Viewport edge-pick math | 4-side random spawn point, unchanged. |
| `bossSpawner.cs:42` | `activeBoss = Instantiate(bossPrefab, point, Quaternion.identity);` | **Instantiate call — the prefab argument becomes the randomly-picked entry.** |
| `bossSpawner.cs:43-44` | `bossShooter shooter = activeBoss.GetComponent<bossShooter>(); if (shooter != null) shooter.RandomizePattern();` | **RandomizePattern() call on the spawned boss — preserved exactly.** |
| `bossSpawner.cs:45` | `spawned = true;` | Sets the spawn-once flag, unchanged. |

### Confirmed external signature
- `bossShooter.RandomizePattern()` → `public void RandomizePattern()` (no args, no return) at `Assets/Scripts/bossShooter.cs:44`. The existing call site is signature-correct and is preserved verbatim.

### Leash / catch-up note
There is **no leash/catch-up code in `bossSpawner.cs`** — that behavior lives on the boss prefab's own components, not in the spawner. Nothing to preserve here on that front. The spawner's only post-Instantiate action is the `RandomizePattern()` call.

---

## (b) Serialized-array field design — `GameObject[]` vs `BossEntry[]`

**Decision: use a plain `[SerializeField] private GameObject[] bossPrefabs;`**

| Option | What it buys | Cost |
|---|---|---|
| **`GameObject[] bossPrefabs`** *(chosen)* | Minimal, matches the exact feature ask ("preconfigure some bosses, pick a random one"). Zero new types. Each element is a full boss prefab that already carries its own `bossShooter`, HP, leash, health bar, etc. Randomization of the bullet pattern is already handled per-instance by `RandomizePattern()`. | No per-entry spawn metadata (e.g. weighting, min-level-per-boss). |
| `BossEntry[]` struct (prefab + weight + minLevel …) | Per-boss config: spawn weights, per-boss level gates. | Adds a new serializable type and UI surface for config the user did **not** ask for. YAGNI. |

**Rationale for `GameObject[]`:** The user's request is "preconfigure some bosses and pick a random one." A uniform random pick over an array of complete prefabs satisfies that exactly. Every meaningful per-boss difference (stats, bullet pattern, visuals) already lives inside each prefab, and pattern variety is already injected at runtime by `RandomizePattern()`. Introducing a `BossEntry` struct would add weighting/level-gate fields that are pure speculation against the current brief. If weighting is wanted later, it is a clean, additive follow-up (swap the array element type; the random-pick line becomes a weighted pick). Keep it a `GameObject[]` now.

---

## (c) Exact drop-in C# for refactored `bossSpawner.cs`

Replace the **entire file** with the following. Changes vs. current: line 5 field is now an array; `SpawnBoss()` adds an empty-array fail-safe guard and a random-index pick; every other behavior (level≥5 gate, spawn-once, player-null retry, "already alive" guard, edge math, `RandomizePattern()`) is byte-for-byte preserved.

```csharp
using UnityEngine;

public class bossSpawner : MonoBehaviour
{
    // Preconfigure one or more boss prefabs here; the spawner picks one at random.
    // NOTE: replacing the old single `bossPrefab` field DROPS its inspector reference —
    // this array must be re-populated in the Editor (see EDITOR MIGRATION in the design doc).
    [SerializeField] private GameObject[] bossPrefabs;
    [SerializeField] private float edgeMargin = 0.08f;   // same as enemySpawner
    private GameObject activeBoss;
    private bool spawned;

    void Start() { }

    void Update()
    {
        if (spawned) return;
        if (worldState.instance == null || worldState.instance.player == null) return;
        if (worldState.instance.level < 5) return;   // boss only at level 5+
        SpawnBoss();
    }

    // === FUTURE TIMED CADENCE HOOK ===
    // TODO(boss-cadence): add [SerializeField] float bossInterval + a spawnTimer in Update()
    //   (mirror enemySpawner's spawnTimer pattern), calling SpawnBoss() on interval.
    //   The "already alive" guard below makes repeat calls safe.

    void SpawnBoss()
    {
        if (activeBoss != null && activeBoss.activeInHierarchy) return;
        if (worldState.instance == null || worldState.instance.player == null) return;

        // Fail safe: no bosses configured -> do nothing (no null-ref spam).
        if (bossPrefabs == null || bossPrefabs.Length == 0) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        // Pick a random preconfigured boss; skip if the chosen slot is empty.
        GameObject chosen = bossPrefabs[Random.Range(0, bossPrefabs.Length)];
        if (chosen == null) return;

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
        if (shooter != null) shooter.RandomizePattern();
        spawned = true;
    }
}
```

### Design notes on the guards
- **Empty-array fail-safe:** `if (bossPrefabs == null || bossPrefabs.Length == 0) return;` — placed early in `SpawnBoss()`. Because `spawned` is only set to `true` at the very end (after a successful Instantiate), an empty array means `SpawnBoss()` returns without setting `spawned`, so `Update()` keeps retrying cheaply every frame with no allocation and no null-ref. The moment the array is populated in the Editor (even at runtime in play mode) the next frame spawns. This is the "fail safe, no null-ref spam" requirement.
- **Per-slot null guard:** `if (chosen == null) return;` protects against an array element left empty in the inspector (Unity allows null array slots). Same cheap-retry semantics.
- **Removed:** the old `bossPrefab == null` half of the line-30 guard is now covered by the two guards above; `Camera cam = Camera.main; if (cam == null) return;` is retained.

---

## (d) EDITOR MIGRATION (required, NOT Docker)

> **This is scene/inspector work and MUST be done in a live Unity Editor — it CANNOT run in Docker.** Owner: `scene-architect` (Editor exception) or the user directly.

**Why it's required:** Renaming/retyping the serialized field from `GameObject bossPrefab` to `GameObject[] bossPrefabs` breaks Unity's serialized-field name/type match. Unity **cannot auto-migrate** the old reference — the existing inspector link to `Assets/Prefabs/enemies/boss.prefab` will be **silently dropped**. After the code lands, `bossPrefabs` will be an **empty array**, and (thanks to the fail-safe) **no boss will ever spawn** until it is re-populated.

**Exact inspector steps:**
1. Open the scene containing the boss spawner GameObject (per git status, `Assets/Scenes/SampleScene.unity`).
2. Select the GameObject that has the `bossSpawner` component.
3. In the Inspector, find the new **Boss Prefabs** array field (it will show `Size 0`).
4. Set **Size** to the number of bosses to preconfigure (at minimum `1`).
5. Drag `Assets/Prefabs/enemies/boss.prefab` into **Element 0** (restores the original boss).
6. Drag any additional preconfigured boss variant prefabs into the remaining elements.
7. **Save the scene** (and any prefab, if the spawner lives on a prefab).

**Acceptance for the Editor step:** entering Play Mode and reaching `worldState.instance.level >= 5` with a non-null player spawns exactly one boss chosen from the array, and that boss's bullet pattern is randomized on spawn.

---

## (e) Grep-based acceptance checklist (code)

Run against `Assets/Scripts/bossSpawner.cs` after the edit:

| # | Check | Command | Expect |
|---|---|---|---|
| 1 | Array field exists, old scalar gone | `grep -n 'GameObject\[\] bossPrefabs' Assets/Scripts/bossSpawner.cs` | 1 match |
| 2 | Old single field removed | `grep -n 'GameObject bossPrefab;' Assets/Scripts/bossSpawner.cs` | 0 matches |
| 3 | Empty-array fail-safe present | `grep -n 'bossPrefabs.Length == 0' Assets/Scripts/bossSpawner.cs` | 1 match |
| 4 | Random pick present | `grep -n 'Random.Range(0, bossPrefabs.Length)' Assets/Scripts/bossSpawner.cs` | 1 match |
| 5 | Per-slot null guard present | `grep -n 'chosen == null' Assets/Scripts/bossSpawner.cs` | 1 match |
| 6 | Level≥5 gate preserved | `grep -n 'worldState.instance.level < 5' Assets/Scripts/bossSpawner.cs` | 1 match |
| 7 | Spawn-once guard preserved | `grep -n 'if (spawned) return;' Assets/Scripts/bossSpawner.cs` | 1 match |
| 8 | Player-null retry preserved | `grep -n 'worldState.instance.player == null' Assets/Scripts/bossSpawner.cs` | 2 matches (Update + SpawnBoss) |
| 9 | RandomizePattern call preserved | `grep -n 'shooter.RandomizePattern()' Assets/Scripts/bossSpawner.cs` | 1 match |
| 10 | Instantiate uses chosen entry | `grep -n 'Instantiate(chosen' Assets/Scripts/bossSpawner.cs` | 1 match |

Additionally, the Editor-side compile gate (`build-validator`) must report **0 compile errors** after the change.

---

## (f) Risk note

1. **Dropped scene reference (HIGH, expected):** As detailed in (d), the old `bossPrefab` link is lost on migration. Mitigated by the fail-safe (no crash, just no spawn) and the mandatory Editor re-populate step. **If the Editor step is skipped, the boss silently never spawns** — this is the single most important follow-up and must be tracked as a beads task assigned to the Editor owner.
2. **Silent "no boss" if array left empty/misconfigured:** By design the spawner fails quiet. During QA, verify a boss actually spawns at level 5 rather than assuming the code change alone is sufficient.
3. **`.meta` / GUID:** editing the script body does not change its GUID, so the component stays attached to the GameObject; only the serialized field data is affected. No `.meta` action needed.
4. **Uniform distribution:** random pick is unweighted. If the user later wants some bosses rarer/more common, that's the `BossEntry`-with-weight follow-up noted in (b) — out of scope now.
5. **No leash/RandomizePattern regression risk:** neither leash nor pattern logic lives in this file; the only post-spawn call (`RandomizePattern()`) is preserved verbatim and its signature was grep-confirmed.

---

## Handoff

- **Code change** (`bossSpawner.cs` replacement in section c): `csharp-dev` via Docker.
- **Editor re-populate** (section d): `scene-architect` (Editor exception) or the user — **NOT Docker**.
- Both should be filed as beads tasks with the code task blocking the Editor task.
