# Spawn Distribution Fix — Design (project-cucumber)

**Date:** 2026-07-07
**Author:** project-planner (Tier 2 — DESIGN/DIAGNOSE ONLY, no implementation)
**File under analysis:** `Assets/Scripts/enemySpawner.cs`
**User report (verbatim):** *"past level 3 I'm noticing way too many shooters, there should still be a fairly even distribution of enemies throughout the run, please look into it"*
**Current table:** slime@1, chaser@1, shooter@3

---

## (a) Current selection logic — grounded, with file:line anchors

Declarations (confirmed via grep):

- `enemySpawner.cs:6-10` — `struct SpawnEntry { GameObject prefab; int minLevel; }`
- `enemySpawner.cs:12` — `[SerializeField] private SpawnEntry[] spawnTable;`
- `enemySpawner.cs:13` — reusable `List<GameObject> eligible` (rebuilt each spawn)

Per-spawn selection path (inside `Update()`):

- `enemySpawner.cs:31` — reads current level: `int lvl = worldState.instance != null ? worldState.instance.level : 1;`
- `enemySpawner.cs:32` — `eligible.Clear();` — list is correctly reset every spawn.
- `enemySpawner.cs:33-35` — rebuild loop: for each table row, add `prefab` to `eligible` when `prefab != null && lvl >= minLevel`.
- `enemySpawner.cs:36` — bail if nothing eligible.
- `enemySpawner.cs:37` — **the selection:** `GameObject prefab = eligible[Random.Range(0, eligible.Count)];`

### Why shooters over-represent — there is NO selection *bug*

I checked for the usual suspects and found none:

- **Off-by-one?** No. `Random.Range(0, eligible.Count)` uses the `int` overload, whose upper bound is *exclusive* — indices `0 .. Count-1`. Correct.
- **Re-roll / double-pick?** No. Exactly one draw per spawn.
- **Stale eligible list?** No. `eligible.Clear()` (`:32`) precedes a full rebuild (`:33-35`) every spawn, so the list always matches the current level.
- **Index skew / weighting?** None present — the draw is *uniform*.

The line-37 draw is **uniform across eligible _types_**, not across a weighted or population-proportional space. That is precisely the design flaw:

1. **Step-function introduction.** For levels 1–2, `eligible = {slime, chaser}` → each type ≈ **50%**, shooter **0%**. The instant the player hits level 3, `eligible = {slime, chaser, shooter}` → each type ≈ **33.3%**. Shooter leaps from **0% to 33% in a single level** — a large, abrupt, highly noticeable jump.
2. **Threat salience.** With only three types, a flat 1-in-3 share of the *most dangerous* type (shooter fires projectiles; slime/chaser are melee) *reads* as "way too many," even though by raw count it is only even. Uniform-over-types gives a brand-new, high-threat type the same share as long-established fodder from its very first eligible level.

**Conclusion:** the code does what it says — uniform over eligible types — but "uniform over types" is the wrong policy for smoothly introducing newly-unlocked, high-threat enemies. The fix is a *policy* change (weighted selection with a level-based ramp-in), not a bug patch.

---

## (b) Designed distribution scheme

**Goal:** a fairly even long-run distribution across eligible types, but *no single newly-unlocked type dominates the moment it unlocks*. A newly-eligible type should phase in gradually and converge toward its fair share over a few levels.

**Scheme: weighted random with a minLevel-derived "ramp-in" default.**

For each eligible entry compute:

```
baseWeight = (entry.weight > 0) ? entry.weight : 1        // existing entries have weight 0 -> treated as 1
ramp       = max(1, unlockRampLevels)                      // default 4
t          = clamp01( (lvl - entry.minLevel + 1) / ramp )  // 1/ramp at unlock, 1.0 after `ramp` levels
w          = baseWeight * t
```

Then draw proportionally to `w`.

**Why this satisfies both requirements:**

- At the exact unlock level (`lvl == minLevel`), `t = 1/ramp` → the new type enters at a *small* fraction of its base weight, then grows linearly to full weight over `unlockRampLevels` levels. No step-function spike.
- Long-established types are already at `t = 1.0` (clamped), so the distribution converges to **even** (or to the configured base weights) once everything has been unlocked for a few levels — matching *"fairly even throughout the run."*
- Tuning lives entirely in **code defaults**: no per-entry weights need to be set, and the ramp constant defaults to 4.

**Worked example (default `unlockRampLevels = 4`, all base weights 1):**

| Level | slime `t`/w | chaser `t`/w | shooter `t`/w | shooter share |
|------:|------------:|-------------:|--------------:|--------------:|
| 1–2   | 1.0 / 1.0   | 1.0 / 1.0    | — (0%)        | 0%            |
| 3     | 0.75 / 0.75 | 0.75 / 0.75  | 0.25 / 0.25   | ~14%          |
| 4     | 1.0 / 1.0   | 1.0 / 1.0    | 0.50 / 0.50   | 20%           |
| 5     | 1.0 / 1.0   | 1.0 / 1.0    | 0.75 / 0.75   | 27%           |
| 6+    | 1.0 / 1.0   | 1.0 / 1.0    | 1.0 / 1.0     | 33% (even)    |

Shooter now *eases in* from ~14% and reaches a fair even share only by level 6, instead of slamming to 33% at level 3. Melee stays dominant through the introduction window, which is exactly the "fairly even distribution" feel the user wants.

**Optional future tuning (no action required now):** the serialized `weight` field lets a designer later make any type intrinsically rarer/commoner (e.g. shooter base weight 0.7) without touching code. Left unset (0), every entry keeps base weight 1.

---

## (c) Exact drop-in C# for `enemySpawner.cs`

Replace the **entire file** with the following. Changes vs. current: added `weight` to `SpawnEntry` (default-safe), added a parallel `eligibleWeights` list, added serialized `unlockRampLevels` (default 4), and replaced the uniform draw at `:37` with a weighted draw. The spawn-position block (`:39-49`) is unchanged.

```csharp
using UnityEngine;

public class enemySpawner : MonoBehaviour
{
    [System.Serializable]
    private struct SpawnEntry
    {
        public GameObject prefab;
        public int minLevel;   // eligible when worldState.level >= minLevel
        public float weight;    // relative base weight; <= 0 is treated as 1 (default for existing entries)
    }

    [SerializeField] private SpawnEntry[] spawnTable;
    // Rebuilt every spawn: eligible prefabs + their computed (ramped) weights, index-aligned.
    private readonly System.Collections.Generic.List<GameObject> eligible = new System.Collections.Generic.List<GameObject>();
    private readonly System.Collections.Generic.List<float> eligibleWeights = new System.Collections.Generic.List<float>();
    [SerializeField] private float spawnInterval = 2f;
    [SerializeField] private float edgeMargin = 0.08f;
    // Levels a newly-unlocked type takes to ramp from 1/ramp of its base weight up to full weight.
    [SerializeField] private int unlockRampLevels = 4;
    private float spawnTimer;

    void Update()
    {
        if (worldState.instance == null || worldState.instance.player == null) return;
        if (objectPool.instance == null || spawnTable == null || spawnTable.Length == 0) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        spawnTimer += Time.deltaTime;
        float interval = (worldState.instance != null) ? worldState.instance.currentSpawnInterval : spawnInterval;
        if (spawnTimer >= interval)
        {
            spawnTimer = 0f;
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
            if (eligible.Count == 0 || totalWeight <= 0f) return;

            // Weighted pick over the eligible set.
            float r = Random.value * totalWeight;
            int pick = eligible.Count - 1;   // fallback guards float rounding at the top of the range
            for (int i = 0; i < eligibleWeights.Count; i++)
            {
                r -= eligibleWeights[i];
                if (r <= 0f) { pick = i; break; }
            }
            GameObject prefab = eligible[pick];

            int side = Random.Range(0, 4);
            Vector3 vp;
            if (side == 0)      vp = new Vector3(-edgeMargin, Random.value, 0f);       // left
            else if (side == 1) vp = new Vector3(1f + edgeMargin, Random.value, 0f);   // right
            else if (side == 2) vp = new Vector3(Random.value, -edgeMargin, 0f);       // bottom
            else                vp = new Vector3(Random.value, 1f + edgeMargin, 0f);   // top
            vp.z = Mathf.Abs(cam.transform.position.z);   // distance from camera to the 2D plane
            Vector3 point = cam.ViewportToWorldPoint(vp);
            point.z = 0f;

            objectPool.instance.get(prefab, point, Quaternion.identity);
        }
    }
}
```

---

## (d) Editor / inspector change required?

**NONE.** Confirmed by two deliberate default-safety measures:

1. **`SpawnEntry.weight`** — existing `spawnTable` array elements were serialized with only `{prefab, minLevel}`. Unity fills the newly-added struct field with the type default `0f`. The code coerces `weight <= 0 → 1f` (`baseWeight = spawnTable[i].weight > 0f ? ... : 1f`), so **all three existing entries keep base weight 1 with no inspector edit.**
2. **`unlockRampLevels`** — has a C# field initializer (`= 4`), which Unity retains for missing serialized data; and `Mathf.Max(1, unlockRampLevels)` guards the known Unity edge case where a newly-added serialized field could deserialize to `0`. Worst case (ramp coerced to 1) degrades gracefully to *even distribution at unlock* — i.e. the old 33% behavior — **never** shooter-dominant.

No prefab re-wiring, no scene edits, no manual weight entry. Optionally, a designer *may* later set per-entry `weight` in the inspector for intrinsic rarity, but nothing requires it.

---

## (e) Grep-based acceptance checklist

Run after the drop-in edit is applied:

```bash
# 1. weight field added to SpawnEntry
grep -n "public float weight" Assets/Scripts/enemySpawner.cs                       # expect 1 hit (~line 9)

# 2. weight is default-safe (0 -> 1 coercion present)
grep -n "spawnTable\[i\].weight > 0f ? spawnTable\[i\].weight : 1f" Assets/Scripts/enemySpawner.cs   # expect 1 hit

# 3. ramp-in constant present with guard
grep -n "unlockRampLevels" Assets/Scripts/enemySpawner.cs                          # expect >=2 hits (decl + Mathf.Max)
grep -n "Mathf.Max(1, unlockRampLevels)" Assets/Scripts/enemySpawner.cs           # expect 1 hit

# 4. weighted draw replaced the uniform draw
grep -n "eligibleWeights" Assets/Scripts/enemySpawner.cs                           # expect >=4 hits
grep -n "Random.value \* totalWeight" Assets/Scripts/enemySpawner.cs              # expect 1 hit
grep -n "eligible\[Random.Range(0, eligible.Count)\]" Assets/Scripts/enemySpawner.cs   # expect 0 hits (old logic gone)

# 5. eligible list still cleared/rebuilt each spawn (regression guard)
grep -n "eligible.Clear()" Assets/Scripts/enemySpawner.cs                          # expect 1 hit
```

Behavioral check (Play Mode / build-validator, host-side): level to 3 and confirm shooters are visibly a *minority* (~1 in 7), rising toward even (~1 in 3) by level ~6. No compile errors; no NREs from the spawner.

---

## (f) Risk note

- **Low risk, self-contained.** Change is one file, no public API surface, no `worldState`/`objectPool` contract change. Spawn-position logic untouched.
- **Unity serialization edge case** on the newly-added `unlockRampLevels` (deserialize-to-0) is explicitly neutralized by `Mathf.Max(1, ...)`; worst case is *even* distribution, never the reported over-spawn.
- **Struct field addition to a serialized array** is a benign, additive schema change — Unity zero-fills the new field; the `>0 ? : 1` coercion absorbs it. No data migration.
- **Determinism unchanged.** Still one `Random` draw per spawn; no per-frame allocation added (both lists are reused members, `Clear()`ed in place).
- **Tuning caveat:** `unlockRampLevels = 4` is a starting value. If introductions should be slower/faster, adjust the constant (code) — still no inspector requirement. Very high base-weight disparities set later by a designer could skew distribution; that is opt-in and out of scope here.
- **Not a bug fix.** Because the original was not buggy, anyone diffing may expect a "one-line fix." This is intentionally a policy change; the checklist above documents the before/after so the intent is clear.

---

**Handoff:** implementation belongs to `@agent-csharp-dev` (file edit, Docker); Play-Mode verification of the observed distribution belongs to `@agent-build-validator` (host/Editor).
