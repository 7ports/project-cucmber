# Design: Progression, Upgrade-Menu & Boss-Drop (Features 3, 5, 4)

**Date:** 2026-07-07
**Author:** project-planner
**Status:** Design only — NO implementation in this doc.
**Scope:** Three related features for the top-down survivors game:
- **Feature 3** — Level-up menu offers **5** upgrades instead of 3.
- **Feature 5** — Level-gated enemy spawn progression (shooters @ L3, boss @ L5 with random bullet pattern).
- **Feature 4** — Boss death drops an item pickup that grants a placeholder item (real items = next sprint).

This doc is grounded in the current codebase (files read: `enemySpawner.cs`, `levelUpMenuController.cs`, `bossSpawner.cs`, `enemyHealth.cs`, `pickupBehaviour.cs`, `playerPickupRadius.cs`, `objectPool.cs`, `worldState.cs`, `playerInventory.cs`, `bossBehaviour.cs`, `boss.prefab`).

---

## Grounding facts (verified in source)

| Fact | Evidence |
|---|---|
| Level-up menu rolls exactly **3** upgrades into serialized `Button[] buttons` / `Text[] labels`. | `levelUpMenuController.cs:16-19` (`// 3`), guards `< 3` at `:23-24`, `rolled = new Upgrade[3]` `:19`, loop `for (int i = 0; i < 3; i++)` `:37`. |
| Upgrade pool = 8 `StatKind` × {Flat, Percent}, minus Percent for Defense/Regen until seeded. | `BuildPool()` `:48-79`. Base pool size = 8 Flat + 8 Percent = **16**, minimum (Defense+Regen Percent suppressed at game start) = **14**. Always ≥ 5. |
| Enemy spawner picks **one random prefab** from a flat `GameObject[] enemyPrefabs` each tick; no level awareness. | `enemySpawner.cs:5, :23` (`enemyPrefabs[Random.Range(0, enemyPrefabs.Length)]`). |
| Boss spawner spawns **once at Start** via a `spawned` gate; already has a documented "future timed cadence" TODO and an "already alive" guard. | `bossSpawner.cs:10-13, :17, :22-26, :29, :44-45`. |
| Boss is `Instantiate`d directly (NOT pooled), so it carries **no** `pooledObject` marker. | `bossSpawner.cs:44`. |
| Boss uses the **same `enemyHealth` component** as normal enemies (guid `dd2bff…` present in `boss.prefab`). Its `die()` drops xp + blood then calls `objectPool.ret`. | `boss.prefab:354`; `enemyHealth.cs:44-57`. |
| `objectPool.ret` on a non-pooled object (the boss) logs a warning and `Destroy`s it. | `objectPool.cs:44-50`. So boss death currently routes through `die()` → `ret()` → `Destroy`. |
| XP pickup collection = homing (gated by a `pickup` bool set externally) + player-contact `OnTriggerEnter2D`. | `pickupBehaviour.cs:9-29`. |
| `playerPickupRadius.scan()` only flips the flag on components of type `pickupBehaviour`. | `playerPickupRadius.cs:25-27`. A new pickup type is NOT auto-homed by it. |
| An inventory seam already exists: `playerInventory.instance.Add(string)` with a `List<string> items`. | `playerInventory.cs:5-17`. |
| `worldState.instance.level` increments in `addXP`; `OnLevelUp` static event fires per level. | `worldState.cs:36-59`. |

---

## NEW vs EDIT summary (all three features)

| File / Asset | New or Edit | Feature | Change |
|---|---|---|---|
| `levelUpMenuController.cs` | **EDIT** | 3 | Roll 5 instead of 3 (constant + array lengths + guards + loop). |
| Level-up menu panel (scene/prefab) | **EDIT (Editor)** | 3 | Add 2 more Buttons + child `Text`; grow serialized `buttons[]`/`labels[]` to 5; fix layout. |
| `enemySpawner.cs` | **EDIT** | 5 | Replace flat `enemyPrefabs[]` with level-gated `{prefab, minLevel}` entries; filter by `worldState.level` each spawn. |
| `bossSpawner.cs` | **EDIT** | 5 | Gate spawn on `level >= 5` instead of Start; call `RandomizePattern()` on spawned instance. |
| Enemy spawner GameObject (scene) | **EDIT (Editor)** | 5 | Populate the new `{prefab, minLevel}` list incl. `shooter.prefab` @ minLevel 3. |
| `itemPickup.cs` | **NEW** | 4 | Player-contact pickup that calls the placeholder grant. |
| `itemManager.cs` (or reuse `playerInventory`) | **NEW (thin)** | 4 | `GrantRandomItem()` placeholder seam. |
| `enemyHealth.cs` | **EDIT (additive, non-breaking)** | 4 | Optional serialized `deathDropPrefab`; spawn if set in `die()`. |
| `itemPickup.prefab` | **NEW (Editor)** | 4 | Prefab wired to `boss.prefab`'s `enemyHealth.deathDropPrefab`. |
| `boss.prefab` | **EDIT (Editor)** | 4/5 | Set `deathDropPrefab` = itemPickup; ensure `bossShooter` (sibling doc) present for `RandomizePattern()`. |

---

# Feature 3 — Upgrade menu offers 5 items

## What changes

`levelUpMenuController` currently hard-codes `3` in four places. Change to `5`.

### Exact code edits (`levelUpMenuController.cs`)

1. **`:19`** — array size:
   ```csharp
   private readonly Upgrade[] rolled = new Upgrade[3];   // -> new Upgrade[5]
   ```
   Recommended: introduce a single source of truth to avoid the current scatter of magic `3`s:
   ```csharp
   private const int OfferCount = 5;
   private readonly Upgrade[] rolled = new Upgrade[OfferCount];
   ```

2. **`:23-24`** — guards:
   ```csharp
   if (buttons == null || buttons.Length < 3) return;   // -> < OfferCount
   if (labels  == null || labels.Length  < 3) return;   // -> < OfferCount
   ```

3. **`:37`** — assignment loop:
   ```csharp
   for (int i = 0; i < 3; i++)                          // -> i < OfferCount
   ```

4. **`:16-17`** — update the `// 3` comments to `// 5`.

No change needed to `BuildPool()`, `Choose()`, or `LabelFor()`.

## Pool-size sufficiency (confirmed)

`BuildPool()` yields 8 stats × {Flat, Percent} = up to **16** entries, and **≥ 14** even at game start (Defense+Regen Percent suppressed on a 0 base — `:72-73`). The Fisher-Yates shuffle (`:29-35`) then takes the first `OfferCount`. **14 ≥ 5**, so rolling 5 **distinct** upgrades is always safe — no divide-by-empty, no duplicates. No pool changes required.

> Edge note: if a future change ever shrinks the pool below `OfferCount`, the `pool[i]` index at `:39` would throw. Cheap hardening (optional): clamp the loop to `Mathf.Min(OfferCount, pool.Count)`. Not required today; flagged as an open question.

## Editor task (scene-architect)

The serialized `Button[] buttons` and `Text[] labels` must both have **length 5**. Today the panel has 3 buttons wired. Editor work:
- Duplicate 2 existing upgrade Buttons (each with its child `Text`) into the level-up menu panel.
- Assign the 2 new Buttons into `buttons[3]`, `buttons[4]` and their child Texts into `labels[3]`, `labels[4]` (order must match; `labels[i]` is the label for `buttons[i]` — see `:40, :43-44`).

## Layout implications

- 3 → 5 rows increases panel height. If the panel already uses a `VerticalLayoutGroup`, the two new buttons auto-flow; only verify the panel's `RectTransform`/`ContentSizeFitter` (or fixed height) accommodates 5 rows without overflowing the screen or the pause backdrop.
- If buttons are absolutely positioned (no layout group), the 2 new buttons must be placed manually and vertical spacing re-balanced. **Recommend** adding a `VerticalLayoutGroup` + `ContentSizeFitter` during this task so future count changes are layout-only.
- Confirm the menu still fits at the game's min supported resolution; shrink button height / font if 5 rows overflow.

---

# Feature 5 — Level-gated enemy spawn progression

## Design goal

Spawn choice depends on `worldState.instance.level`:
- **From L1:** slimes + chasers (current behaviour).
- **L3+:** add **shooters** (`shooter.prefab`, designed in sibling doc) to the random pool.
- **L5:** spawn **one boss** with a **random bullet pattern**, once.

## Spawner: level-gated prefab list

Replace the flat `GameObject[] enemyPrefabs` (`enemySpawner.cs:5`) with a serialized list of `{prefab, minLevel}` entries and filter each spawn:

```csharp
[System.Serializable]
private struct SpawnEntry
{
    public GameObject prefab;
    public int minLevel;      // eligible when worldState.level >= minLevel
}

[SerializeField] private SpawnEntry[] spawnTable;
private readonly List<GameObject> eligible = new List<GameObject>();  // reused, no per-spawn GC
```

In the spawn branch (replacing `:23`):
```csharp
int lvl = worldState.instance.level;
eligible.Clear();
for (int i = 0; i < spawnTable.Length; i++)
    if (spawnTable[i].prefab != null && lvl >= spawnTable[i].minLevel)
        eligible.Add(spawnTable[i].prefab);
if (eligible.Count == 0) return;                       // nothing eligible yet -> skip this tick
GameObject prefab = eligible[Random.Range(0, eligible.Count)];
```
Everything else in `Update()` (edge-spawn viewport math `:25-35`, `objectPool.instance.get` `:35`) is unchanged. The existing null/empty guard at `:13` should be updated to check `spawnTable` instead of `enemyPrefabs`.

**Editor task (scene-architect / asset-manager):** populate `spawnTable` on the spawner GameObject:

| prefab | minLevel |
|---|---|
| slime | 1 |
| chaser | 1 |
| shooter (`shooter.prefab`) | 3 |

> Slimes/chasers keep spawning at all levels (they're not removed at higher levels — design default: additive difficulty, not replacement). If a later design wants a `maxLevel` retirement band, extend `SpawnEntry` with `maxLevel`; flagged as an open question.

## Boss trigger at L5 — recommendation: **keep `bossSpawner`, gate on level**

Two options were considered:

| Option | Pros | Cons |
|---|---|---|
| **A. Keep `bossSpawner`, gate its spawn on `level >= 5`** (recommended) | Minimal diff; boss spawn/leash/health-bar concerns stay isolated from wave spawning; the `spawned` gate + "already alive" guard already exist; matches the file's own documented future-cadence TODO. | Two spawner components in the scene (already the case today). |
| B. Fold boss spawn into `enemySpawner` | One spawner object. | Mixes single-shot boss logic into per-tick wave logic; boss is not pooled while waves are; larger, riskier diff to a hot-path file. |

**Chosen: Option A.** Change `bossSpawner` from "spawn at Start" to "spawn once when `level >= 5`":

```csharp
// bossSpawner.cs
void Start() { }   // remove the immediate SpawnBoss() call at :12

void Update()
{
    if (spawned) return;
    if (worldState.instance == null || worldState.instance.player == null) return;
    if (worldState.instance.level < 5) return;          // NEW gate
    SpawnBoss();
}
```
The existing `spawned` flag (`:8, :45`) guarantees **one** boss, no repeat — consistent with the deferred-cadence decision documented at `bossSpawner.cs:22-26`. (The future timed-cadence TODO there remains valid and untouched.)

## Random bullet pattern — exact call

The boss prefab exposes a `bossShooter` component (sibling doc) with a public `RandomizePattern()` method. The spawn code must call it on the **spawned instance** immediately after `Instantiate`. In `SpawnBoss()`, right after `:44`:

```csharp
activeBoss = Instantiate(bossPrefab, point, Quaternion.identity);
bossShooter shooter = activeBoss.GetComponent<bossShooter>();   // or GetComponentInChildren if it lives on a child
if (shooter != null) shooter.RandomizePattern();
spawned = true;
```

> The exact call is: **`activeBoss.GetComponent<bossShooter>().RandomizePattern();`** (null-guarded as above). Use `GetComponentInChildren<bossShooter>()` if the sibling doc places `bossShooter` on a child of `boss.prefab` rather than the root — the scene-architect/csharp-dev should confirm placement against the sibling doc before wiring.

**Default:** exactly **one** boss at level 5, no repeat.

---

# Feature 4 — Boss drops an item pickup (placeholder grant)

## Constraint: keep `enemyHealth.die()` non-breaking for normal enemies

The boss shares `enemyHealth` with slimes/chasers (`boss.prefab:354`). Any drop logic added to `die()` must be **opt-in per-instance** so normal enemies are unaffected.

### Chosen death-drop hook: optional serialized `deathDropPrefab` on `enemyHealth`

Least-invasive, fully backward-compatible. Add one serialized field (default `null`) and one guarded spawn line inside `die()`:

```csharp
// enemyHealth.cs — new field near the other [SerializeField]s (:7-12)
[SerializeField] private GameObject deathDropPrefab;   // optional; only the boss sets this

// inside die(), after the xp/blood drops and BEFORE the ret() at :55-56:
if (deathDropPrefab != null && objectPool.instance != null)
    objectPool.instance.get(deathDropPrefab, transform.position, Quaternion.identity);
```

- **Non-breaking:** normal enemy prefabs leave `deathDropPrefab` unset (`null`) → the guard skips it → their behaviour is byte-for-byte unchanged. Only `boss.prefab` sets the field.
- **Ordering inside `die()`:** spawn the drop **before** `objectPool.instance.ret(gameObject)` (`:55-56`). For the boss, `ret()` resolves to a `Destroy` (no `pooledObject` marker), so `transform.position` must be read before the object is torn down — placing the drop-spawn before `ret()` guarantees a valid position.
- **Pooling:** the drop is spawned via `objectPool.get`, consistent with xp/blood drops (`:46-53`). The `itemPickup.prefab` will therefore be pooled and must self-return via `objectPool.ret` on collection (see below).

> Alternative rejected: a boss-specific death script / subclass of `enemyHealth`. More files, and it would either duplicate `die()` or need `die()` made `virtual` — larger blast radius than one optional field. Rejected.

## `itemPickup` component (NEW)

Mirrors `pickupBehaviour`'s player-contact collection (`pickupBehaviour.cs:21-29`) but grants an item instead of XP. Homing is **optional and omitted by default** — `playerPickupRadius` only flags `pickupBehaviour` (`playerPickupRadius.cs:25`), so the item won't auto-home; the player walks onto it. (If homing is later desired, extend `playerPickupRadius.scan()` to also flag `itemPickup`, or make `itemPickup` derive from a shared base. Flagged as open question.)

```csharp
using UnityEngine;

public class itemPickup : MonoBehaviour
{
    void OnTriggerEnter2D(Collider2D other)
    {
        if (worldState.instance == null) return;
        if (other.transform == worldState.instance.player ||
            other.transform.root == worldState.instance.player)
        {
            itemManager.GrantRandomItem();          // placeholder grant seam
            if (objectPool.instance != null) objectPool.instance.ret(gameObject);
        }
    }
}
```

Prefab requirements (Editor task): `itemPickup.prefab` needs a trigger `Collider2D` (`isTrigger = true`), a `SpriteRenderer` (distinct icon so it reads as "special"), and — because it's spawned via the pool — it will get a `pooledObject` marker automatically on first `get()`, so `ret()` cleanly recycles it.

## Placeholder grant seam: `itemManager.GrantRandomItem()`

Design the seam so **real items drop in later without touching `itemPickup` or `enemyHealth`.** A thin static entry point that today just logs + records a placeholder, later swaps its body for real item selection:

```csharp
using UnityEngine;

public static class itemManager
{
    // Placeholder: real item pool + effects arrive next sprint.
    // The call site (itemPickup) never changes when this body is upgraded.
    public static void GrantRandomItem()
    {
        // TODO(items-sprint): replace with real weighted item selection + effect application.
        string placeholder = "PlaceholderItem";
        if (playerInventory.instance != null)
            playerInventory.instance.Add(placeholder);   // reuse existing inventory seam
        Debug.Log("[itemManager] Granted placeholder item. Inventory size: " +
                  (playerInventory.instance != null ? playerInventory.instance.items.Count : 0));
    }
}
```

**Why this seam is extensible:**
- The **collection→grant boundary** is a single call (`itemManager.GrantRandomItem()`). Real items = change the method body only.
- It **reuses the existing `playerInventory.instance.Add(string)`** (`playerInventory.cs:14`), which `pauseItemsView` already surfaces — so granted placeholders are immediately visible/testable this sprint.
- When real items land, `GrantRandomItem()` can return an item id / ScriptableObject, apply stat effects via `worldState`, and still call `playerInventory.Add` — no change to `itemPickup` or `enemyHealth`.

> Alternative: put `GrantRandomItem()` directly on the existing `playerInventory` MonoBehaviour instead of a new static `itemManager`. Acceptable and one-fewer-file, but a dedicated `itemManager` keeps "what item + what effect" (game logic) separate from "what the player is holding" (inventory data). Recommend the dedicated `itemManager`; flagged as an open question if the team prefers fewer files.

---

# Ordering & Risk

## Shared-file edit map

Four existing files are edited; three are hot-path or shared. Sequence to minimise merge/regression risk:

| Order | Task | File(s) | Agent | Depends on |
|---|---|---|---|---|
| 1 | Add optional `deathDropPrefab` + guarded spawn | `enemyHealth.cs` | csharp-dev | — (additive, self-contained) |
| 2 | New `itemPickup.cs` + `itemManager.cs` | new files | csharp-dev | itemManager referenced by itemPickup |
| 3 | Roll-5 edit | `levelUpMenuController.cs` | csharp-dev | — (independent) |
| 4 | Level-gated `spawnTable` | `enemySpawner.cs` | csharp-dev | shooter.prefab exists (sibling doc) |
| 5 | Level-5 gate + `RandomizePattern()` | `bossSpawner.cs` | csharp-dev | `bossShooter` exists (sibling doc) |
| 6 | Editor: +2 menu buttons; grow arrays | level-up panel | scene-architect | task 3 merged |
| 7 | Editor: populate `spawnTable`; set boss `deathDropPrefab`; build `itemPickup.prefab` | scene + prefabs | scene-architect | tasks 1,2,4 merged; shooter.prefab |
| 8 | Validate compile + Play Mode | — | build-validator | all above |

Tasks 1–5 touch **five different files with no overlapping regions**, so they can proceed in parallel if desired; the ordering above is the safe serial fallback. The only true code coupling is task 2 internal (itemPickup → itemManager).

## Risk notes

- **`enemyHealth.cs` non-breaking (highest-attention item):** the ONLY change is one new `null`-defaulted serialized field + one `if (deathDropPrefab != null)` guarded line. Every existing enemy prefab leaves the field unset → identical behaviour. **Do not** alter the existing xp/blood/`ret` sequence (`:46-56`); insert the drop spawn strictly between the blood drop and `ret()`.
- **Boss death timing:** the drop must spawn before `ret()`/`Destroy` (position read). Verified: boss has no `pooledObject` marker, so `ret()` → `Destroy` (`objectPool.cs:44-50`).
- **`levelUpMenuController` array/loop mismatch:** if the Editor task (task 6) is NOT done, the `< OfferCount` guard (`:23-24`) makes `OnEnable` silently `return` and the menu shows nothing. **Task 3 (code) and task 6 (scene) must ship together** or the menu breaks. Order task 6 immediately after task 3 and validate.
- **Sibling-doc dependencies:** Feature 5 depends on `shooter.prefab` and the boss's `bossShooter.RandomizePattern()` existing. If those aren't merged, `spawnTable` simply won't include a shooter row (harmless) and the `bossShooter` call is null-guarded (harmless) — but the feature is incomplete. Gate final validation on the sibling docs landing.
- **`enemySpawner` hot path:** reuse the `eligible` `List` field (don't allocate per tick) to avoid GC churn in `Update()`.

---

# Open Questions (with chosen safe defaults)

1. **Menu pool-size hardening** — clamp the roll loop to `Mathf.Min(OfferCount, pool.Count)`? **Default: skip** (pool is always ≥14). Add only if a future change could shrink the pool.
2. **Do slimes/chasers retire at high levels?** **Default: no** — additive difficulty; higher levels add shooters on top. Add a `maxLevel` band later if wave pacing needs it.
3. **Boss repeat cadence?** **Default: one boss at L5, no repeat** (via `spawned` gate), consistent with the deferred-cadence decision in `bossSpawner.cs:22-26`. Timed re-spawns remain a documented future TODO.
4. **`bossShooter` placement (root vs child of `boss.prefab`)?** **Default: assume root** → `GetComponent<bossShooter>()`. csharp-dev must confirm against the sibling doc and switch to `GetComponentInChildren` if needed.
5. **Item pickup homing?** **Default: no homing** (player walks onto it) — keeps it distinct from XP orbs and avoids touching `playerPickupRadius`. Add homing later by extending the radius scan or a shared base class.
6. **`itemManager` as a new static vs a method on `playerInventory`?** **Default: new static `itemManager`** (separates item-logic from inventory-data). Collapse into `playerInventory` if the team prefers fewer files — the call site is the only thing that would change.
7. **Menu layout mechanism** — add `VerticalLayoutGroup`+`ContentSizeFitter` during the Editor task? **Default: yes if not already present**, so future offer-count changes are layout-free.

---

## Handoff

This is a design doc only. Next: `/scrum-master` to decompose Features 3/5/4 into agent tasks (csharp-dev for the 5 code edits/new files; scene-architect for the 3 Editor tasks; build-validator for the final gate). Sequence per the Ordering table; ship menu code + menu Editor task together.
