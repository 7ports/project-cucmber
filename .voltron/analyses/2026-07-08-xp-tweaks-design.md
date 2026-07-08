# XP Tweaks Design — (A) x2 XP at 7 min, (B) XP drop prefab by value

**Date:** 2026-07-08
**Author:** project-planner (Tier 2, DESIGN ONLY — no implementation)
**Scope:** Tasks 4 & 5 — "at 7 minutes also double all earned xp" + "ensure xp pickup visually changes and uses all the different prefabs when it increases in value".
**Implementer:** hand to `@agent-csharp-dev` (script edits) + one Editor step for the prefab array (`@agent-scene-architect`).

---

## (a) Ground-truth anchors (real files/lines read)

| Anchor | File:line | Current fact |
|---|---|---|
| XP grant site (only `addXP` caller) | `Assets/Scripts/pickupBehaviour.cs:26` | `worldState.instance.addXP(xpValue + worldState.instance.XpBonus());` |
| Per-prefab pickup value | `Assets/Scripts/pickupBehaviour.cs:7` | `[SerializeField] private int xpValue = 1;` |
| XP ledger | `Assets/Scripts/worldState.cs:124-135` | `addXP(int amount)` — raw add + level-up loop; **only one caller** (grep confirmed) |
| Flat XP bonus | `Assets/Scripts/worldState.cs:50,84` | `xpBonusPerPickup` (0), `XpBonus() => xpBonusPerPickup` |
| XP bonus cap | `Assets/Scripts/worldState.cs:68` | `const int xpBonusCap = 3` — comment: *"max total bonus (base 1 pickup -> 4 XP)"* |
| XP bonus mutation | `Assets/Scripts/levelUpMenuController.cs:181` | `ws.xpBonusPerPickup = Mathf.Min(xpBonusCap, ws.xpBonusPerPickup + ws.xpBonusStep);` |
| HP time-scaling model to mirror | `Assets/Scripts/worldState.cs:104-120` | `EnemyHpTimeMultiplier()` uses `Time.timeSinceLevelLoad`, `hpScaleInterval = 420f` (7 min) |
| **Death-drop path** | `Assets/Scripts/enemyHealth.cs:119-135` (`die()`), loop `121-125`, field `xpPrefab` at `:8`, `xpDropCount` at `:9` | Drops `xpDropCount` copies of a single serialized `xpPrefab` via `objectPool.instance.get(...)`. Grep confirms `xpPrefab` is used **only here**. |
| Pool semantics | `Assets/Scripts/objectPool.cs:8,15-40,42-59` | Pool keyed by **prefab reference** (`Dictionary<GameObject,Queue>`); `pooledObject.source` remembers the origin prefab. Distinct prefabs → distinct queues. |
| **Four prefabs' baked xpValue** | `Assets/Prefabs/pickups/XP{1..4}.prefab` line 111 | **XP1→1, XP2→2, XP3→3, XP4→4** (confirmed by grep). `homeSpeed: 8` on all. |

**Effective value chain today:** enemy dies → drops `xpPrefab` (currently XP1, value 1) → player collects → `addXP(1 + XpBonus())`. The XpGain upgrade is realized **at collection**, not in the dropped prefab. This is the crux for Part B (see risk note).

---

## (b) PART A — Double all earned XP at 7 minutes

### Decision: **single flat ×2 threshold at 7:00** (not tiered)

- **Chosen:** one doubling. `multiplier = (runTime >= threshold) ? 2 : 1`. This is the **literal reading** of *"at 7 minutes also **double** all earned xp"* — one doubling event, one factor of 2, permanent for the rest of the run. The word "also" ties it to the 7-minute HP milestone, so we reuse the same 420 s boundary.
- **Rejected (documented alternative):** tiered ×2 per 7-min tier (2×, 4×, 8×…) mirroring `EnemyHpTimeMultiplier()`. This is defensible as "keep pace with the HP ramp", but the task says *double*, singular, and geometric XP growth trivialises leveling within ~20 min. If the user later wants parity with HP scaling, flip to the tiered branch shown below — it is a one-line change.

### Config (add to `worldState.cs`, near the HP-scaling block ~line 104)

```csharp
// --- Time-based XP doubling (seconds of elapsed run time) ---
// After xpDoubleThreshold seconds, ALL earned XP is doubled (single ×2, permanent).
// Separate field from hpScaleInterval so it can be retuned independently, default
// equal (420 = 7 min) so it fires with the same milestone as EnemyHpTimeMultiplier().
public float xpDoubleThreshold = 420f;   // 7 minutes
public float xpDoubleFactor     = 2f;    // the "double"

// Multiplier applied to earned XP at the grant site. Uses Time.timeSinceLevelLoad,
// the same run-time source as the HP/spawn/boss systems, so all time-gates agree.
public float XpTimeMultiplier()
{
    if (xpDoubleThreshold <= 0f) return 1f;                 // guard/disable
    return (Time.timeSinceLevelLoad >= xpDoubleThreshold) ? xpDoubleFactor : 1f;
    // TIERED alternative (retune): mirror EnemyHpTimeMultiplier —
    //   int tier = Mathf.FloorToInt(Time.timeSinceLevelLoad / xpDoubleThreshold);
    //   return Mathf.Pow(xpDoubleFactor, Mathf.Max(0, tier));   // 2×,4×,8×…
}
```

### Drop-in at the grant site — apply the multiplier ONCE, at the outermost earned amount

The multiplier must wrap the **entire earned amount** ("all earned xp"), and must be applied exactly once. Keep `addXP` a pure ledger (do **not** put the multiplier inside `addXP`, or a future second caller would silently double again). Apply at `pickupBehaviour.cs:26`.

> ⚠️ The exact body of line 26 depends on Part B's double-count fix below — see the **combined final line** in Part B. Standalone (Part A only) it would be:
> ```csharp
> int earned = Mathf.RoundToInt((xpValue + worldState.instance.XpBonus()) * worldState.instance.XpTimeMultiplier());
> worldState.instance.addXP(earned);
> ```
> `Mathf.RoundToInt` keeps `addXP(int)` intact; with factor 2 and integer inputs it is exact.

**Composition with `xpBonusPerPickup`:** the bonus is inside the parentheses, so the ×2 covers base + bonus. e.g. at 8:00 with XpGain maxed: `(1 + 3) * 2 = 8`. Correct — "all earned xp" includes the bonus.

---

## (c) PART B — Drop the XP prefab whose value matches the effective per-drop value

### What drives "increases in value"
The **XpGain upgrade** (`xpBonusPerPickup`, 0→3, capped 3) is the sole driver of per-pickup worth. Base drop = 1, so **effective per-drop value = 1 + XpBonus() ∈ [1,4]** — which maps exactly onto XP1(1)…XP4(4). The comment at `worldState.cs:68` already states this intent ("base 1 pickup -> 4 XP"). The 7-min doubling (Part A) is a **collection-time** multiplier and deliberately **does NOT** select the prefab (see below).

### Design: prefab bakes the value; selection by index

Replace the single `xpPrefab` with a 4-slot array indexed by value tier, and pick by the computed effective value.

```csharp
// enemyHealth.cs — replace field at :8
[SerializeField] private GameObject[] xpPrefabsByValue;   // [0]=XP1(v1) .. [3]=XP4(v4)
[SerializeField] private int baseDropValue = 1;           // per-drop worth before the XpGain bonus

// helper
GameObject PickXpPrefab()
{
    if (xpPrefabsByValue == null || xpPrefabsByValue.Length == 0) return null;
    int bonus = (worldState.instance != null) ? worldState.instance.XpBonus() : 0;
    int value = baseDropValue + bonus;                       // 1..4 under current cap
    int idx   = Mathf.Clamp(value - 1, 0, xpPrefabsByValue.Length - 1);
    return xpPrefabsByValue[idx];
}
```

```csharp
// enemyHealth.cs die() — replace loop at :121-125
GameObject xp = PickXpPrefab();
for (int i = 0; i < xpDropCount; i++)
{
    if (objectPool.instance != null && xp != null)
        objectPool.instance.get(xp, transform.position, Quaternion.identity);
}
```

Higher upgrade level → higher `value` → bigger prefab (XP4), so the drop **visually reflects its worth** and all four prefabs get used as the player invests in XpGain.

### The double-count fix (mandatory — this is the trap)
Each prefab already **bakes its own `xpValue`** (XP4 = 4). If the enemy drops XP4 *and* `pickupBehaviour` still adds `+ XpBonus()`, collection yields `4 + 3 = 7` — wrong. The bonus would be counted **twice** (once by choosing the bigger prefab, once at collection).

**Fix:** the dropped prefab's baked `xpValue` is now the single source of truth for the earned amount, so `pickupBehaviour` must **stop adding `XpBonus()`**. The XpGain upgrade is now realized purely by *which prefab drops*.

**Combined final grant line** (`pickupBehaviour.cs:26`, Part A × Part B together):
```csharp
int earned = Mathf.RoundToInt(xpValue * worldState.instance.XpTimeMultiplier());
worldState.instance.addXP(earned);
```
- `xpValue` (baked 1..4) already encodes base + XpGain bonus via prefab choice → no `+ XpBonus()`.
- `XpTimeMultiplier()` applies the 7-min ×2 at collection, independent of prefab.
- Net at 8:00, XpGain maxed: enemy drops XP4 → collect `4 * 2 = 8` — matches Part A's `(1+3)*2=8`. Consistent. ✅

### Why the doubling is NOT baked into prefab selection
Only four prefabs exist (max value 4). Folding the ×2 into `value` would demand XP5..XP8 and overflow the array (would clamp to XP4, silently under-representing). Keeping ×2 at collection avoids overflow and keeps prefab selection tied solely to the (bounded) XpGain value. Clean separation: **prefab = XpGain worth; collection multiplier = time.**

### Editor step (FLAG)
`xpPrefabsByValue` is a new `GameObject[]` — **serialized reference field, must be populated in the Inspector** on every enemy prefab that drops XP (assign XP1,XP2,XP3,XP4 in order to slots 0–3). This is an **Editor task → `@agent-scene-architect`** (Docker cannot set prefab references). Enemies with an empty/unassigned array drop nothing (guarded, no NRE). *Alternative to reduce Editor churn:* a project-wide `xpPrefabsByValue` on a ScriptableObject/`worldState`-adjacent config so enemies don't each need the array — but that is a larger refactor; the per-enemy array matches the existing `xpPrefab`/`bloodPrefab`/`deathDropPrefab` serialized pattern and is the least-surprising change.

---

## (d) Grep acceptance criteria

```bash
# Part A: config + multiplier exist, addXP still pure (single caller)
grep -n "xpDoubleThreshold\|xpDoubleFactor\|XpTimeMultiplier" Assets/Scripts/worldState.cs   # 3 defs
grep -rn "addXP" Assets/Scripts/                                                              # still ONE caller (pickupBehaviour)
grep -n "XpTimeMultiplier" Assets/Scripts/pickupBehaviour.cs                                  # applied at grant site

# Part B: array selection in place, bonus NOT double-counted
grep -n "xpPrefabsByValue\|PickXpPrefab\|baseDropValue" Assets/Scripts/enemyHealth.cs         # array + helper
grep -n "xpPrefab\b" Assets/Scripts/enemyHealth.cs                                            # old single-ref path gone/replaced
grep -n "XpBonus()" Assets/Scripts/pickupBehaviour.cs                                         # MUST be ABSENT (moved into prefab choice)

# Prefabs unchanged (values still 1..4 → array indices valid)
grep -h "xpValue" Assets/Prefabs/pickups/XP{1,2,3,4}.prefab                                   # 1,2,3,4
```

Play-Mode acceptance (→ `@agent-build-validator`): before 7:00 collect XP1 = +1; buy XpGain once → enemies drop XP2, collect = +2; after 7:00 collect XP2 = +4 (×2). No compile errors; array assigned on enemy prefabs (no NRE, no "returned a non-pooled object" warnings).

---

## (e) Risk notes

1. **Double-count (highest risk):** moving the bonus into prefab selection **requires** deleting `+ XpBonus()` from `pickupBehaviour.cs:26`. Ship both edits together; a partial merge (array added, bonus not removed) inflates XP by up to +3 per pickup. Grep `XpBonus()` in `pickupBehaviour.cs` must return **nothing**.
2. **Pooled-drop correctness:** `objectPool` keys on the **prefab reference** and stamps `pooledObject.source`, so XP1–XP4 live in **separate queues** and recycle back to their own prefab — switching which prefab `die()` requests is safe and never cross-contaminates a queue (verified `objectPool.cs:19,36,54`). Each XP prefab keeps its own baked `xpValue`, so a recycled XP3 always grants 3. No shared-instance mutation.
3. **Multiplier applied once:** keep it at the single grant site, **not** inside `addXP` — `addXP` has exactly one caller today, but burying the ×2 in the ledger would silently double any future caller (e.g. quest reward). `Mathf.RoundToInt` preserves the `int` contract; integer × 2 is exact.
4. **Cap coupling:** index math assumes `baseDropValue + XpBonus() ≤ 4`. `xpBonusCap = 3` + base 1 guarantees this today; `Mathf.Clamp` protects if the cap is later raised (extra value clamps to XP4 rather than IndexOutOfRange). If more prefabs (XP5+) are added, extend the array — no code change needed.
5. **Unassigned array:** new `GameObject[]` defaults to empty on existing enemy prefabs until the Editor step runs → guarded to drop nothing (no NRE), but enemies would **stop dropping XP** until assigned. Sequence the scene-architect Editor pass **immediately after** the script edit, before Play-Mode validation.
6. **Boss `deathDropPrefab` untouched:** bosses use a separate `deathDropPrefab` (item drops) at `enemyHealth.cs:130-131`; this design does not alter it. Bosses that should also drop scaled XP just need `xpPrefabsByValue` populated like any enemy.

---

## Handoff

Design only — no code written. Next: `@agent-csharp-dev` applies the `worldState.cs`, `pickupBehaviour.cs`, `enemyHealth.cs` edits above; then `@agent-scene-architect` populates `xpPrefabsByValue` (XP1–4) on XP-dropping enemy prefabs; then `@agent-build-validator` runs the Play-Mode acceptance. Route through `/scrum-master` for task decomposition.
