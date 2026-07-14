# Design: Weapon Items via Level-Up (every 10th level → item-select menu)

**Branch:** `slot-machine-levlup`  ·  **Status:** DESIGN-ONLY (do not implement until user approves)
**Scope:** make weapon items obtainable through leveling. Every 10th level opens an ITEM-SELECT menu **instead of** a normal level-up. The item menu **reuses** `itemChoiceMenuController` (the boss-drop picker), which offers up to 3 not-yet-owned items from `ItemId.All` and grants one via `playerInventory.Add`.

---

## Overview

`levelUpManager` is (and must remain) the **sole owner of `Time.timeScale`** across the level-up flow. Today it already picks between two panels — the normal `menuPanel` and the slot-machine `slotMenuPanel` — via `SelectPanelForCurrentLevel()`, pausing in `OpenMenu()` and un-pausing in `ApplyChoiceAndAdvance()`.

This feature adds a **third** panel option (the item menu) governed by a 3-way priority rule, and adapts `itemChoiceMenuController` so it can act as a `levelUpManager`-owned panel **without** double-managing pause, while still working standalone for boss drops.

---

## A. Trigger resolution (levelUpManager panel selection)

Extend `SelectPanelForCurrentLevel()` in `levelUpManager.cs:70` to a 3-way rule. **Item wins on multiples of 10** (10 is also a multiple of 5, so order matters):

```
level % 10 == 0  -> item menu panel   (itemMenuPanel)
else level % 5 == 0 -> slot panel      (slotMenuPanel)
else                -> normal menu      (menuPanel)
```

Concrete changes:
- **New serialized field:** `[SerializeField] private GameObject itemMenuPanel;` (sits alongside `menuPanel`, `slotMenuPanel` at `levelUpManager.cs:8-9`).
- **New serialized reference (optional but recommended):** `[SerializeField] private itemChoiceMenuController itemMenu;` so `levelUpManager` can call its level-up entry method when the item panel is the one being opened. Alternatively resolve via `itemChoiceMenuController.instance` (a singleton already exists, `itemChoiceMenuController.cs:15`) to avoid extra wiring — see §E.
- Rewrite `SelectPanelForCurrentLevel()` guarding each branch on both the panel `!= null` and `worldState.instance != null`:
  ```
  if (itemMenuPanel != null && ws.level % 10 == 0 && ItemMenuHasOffers()) return itemMenuPanel;
  if (slotMenuPanel != null && ws.level % 5  == 0) return slotMenuPanel;
  return menuPanel;
  ```
  `ItemMenuHasOffers()` is the owned-all guard from §C.
- **`Time.timeScale` stays owned only by `levelUpManager`.** `OpenMenu()` still sets `Time.timeScale = 0f` (`levelUpManager.cs:83`) and `ApplyChoiceAndAdvance()` still restores `1f` (`levelUpManager.cs:103`). The item panel must NOT touch timescale when opened this way (§B).

**Priming the item panel on open.** `SelectActive`/`OpenMenu` only calls `SetActive(true)` on the chosen panel today. The item menu needs its buttons *populated* before it's shown. So when the selected panel is the item panel, `levelUpManager` must call a populate-only method on the controller (e.g. `itemMenu.PopulateForLevelUp()` — see §B) right before/after activating, in **both** `OpenMenu()` and the re-toggle branch of `ApplyChoiceAndAdvance()`. Cleanest: give the controller a single `OpenForLevelUp()` that both populates and activates its own panel, and have `levelUpManager` call that instead of `SetActive(true)` for the item branch (timescale still owned by manager).

---

## B. Adapting itemChoiceMenuController to the level-up flow (dual-path)

`itemChoiceMenuController.Offer()` today (`itemChoiceMenuController.cs:52`) is a self-contained boss-drop flow: it builds candidates, populates buttons, **pauses itself** (`Time.timeScale = 0f`, line 87), and on click `Choose()` → `inv.Add()` → `Close()` which **un-pauses** (line 106). It does NOT call `ApplyChoiceAndAdvance()`.

To make it also serve as a `levelUpManager` panel **without breaking boss drops**, introduce a **mode flag** that toggles (1) whether it owns pause and (2) whether it advances the level-up queue.

Recommended shape — a private `bool _fromLevelUp;` plus split entry points that funnel into a shared populate routine:

- **Refactor** the candidate-build + button-population block (`itemChoiceMenuController.cs:58-84`) into a private `bool PopulateOffers()` that returns `false` when there are no candidates (owns-all). It must NOT touch `Time.timeScale` or `menuPanel.SetActive`.
- **Boss-drop entry (unchanged behaviour):** `public bool Offer()` sets `_fromLevelUp = false`, calls `PopulateOffers()`; on `false` returns `false` (owns-all → caller grants nothing, exactly as today). On `true`: `menuPanel.SetActive(true)`; **`Time.timeScale = 0f`**; return `true`. Identical external contract to current `Offer()` for `itemPickup`.
- **Level-up entry (new):** `public bool OpenForLevelUp()` sets `_fromLevelUp = true`, calls `PopulateOffers()`. On `false` returns `false` (owns-all — the manager handles fallback per §C). On `true`: `menuPanel.SetActive(true)` **but does NOT touch `Time.timeScale`** (manager already paused). Returns `true`.
  - Because `levelUpManager` also does `SetActive(true)` on the returned panel, guard against double-activate: EITHER have `OpenForLevelUp()` do the activate and have the manager skip `SetActive` for the item branch, OR have `OpenForLevelUp()` be populate-only and let the manager activate. Pick one and document it in the code comment. Recommended: `OpenForLevelUp()` populates only; manager owns `SetActive` — keeps the manager's existing panel-toggle symmetry in `ApplyChoiceAndAdvance()`.
- **`Choose(idx)` becomes mode-aware** (`itemChoiceMenuController.cs:91`):
  - Grant is identical in both modes: `if (!inv.Has(offered[idx])) inv.Add(offered[idx]);`.
  - Branch on `_fromLevelUp`:
    - `false` (boss drop): call existing `Close()` → hides panel + `Time.timeScale = 1f`. Unchanged.
    - `true` (level-up): hide the item panel (`menuPanel.SetActive(false)`) **but do NOT touch `Time.timeScale`**, then call `levelUpManager.instance.ApplyChoiceAndAdvance()` — mirroring how the normal/slot menus advance. The manager re-toggles the next panel or restores timescale. This is the critical parity requirement.
- **Split `Close()`** so the timescale restore only happens on the boss-drop path. Simplest: keep `Close()` as boss-only (panel off + `timeScale = 1f`); add `CloseForLevelUp()` (panel off only, no timescale) used by the level-up branch before calling `ApplyChoiceAndAdvance()`.

Net effect: two callers, one populate/grant core, and `Time.timeScale` is touched by the controller **only** on the boss-drop path — never when driven by `levelUpManager`.

---

## C. Owned-all edge case (level-10 with every item already owned)

At a `% 10 == 0` level the candidate pool can be empty (`candidates.Count == 0`, `itemChoiceMenuController.cs:62`). Showing an empty item menu would soft-lock the pick (no buttons to advance the queue while paused).

**Recommendation (concrete): fall back to the NORMAL level-up menu.** It is the lowest-risk option — the normal menu always has stat upgrades to offer, so the player still gets value and the queue always advances.

Implementation:
- Add `private bool ItemMenuHasOffers()` on `levelUpManager` that asks the controller whether it currently has any not-yet-owned candidates. Give `itemChoiceMenuController` a cheap `public bool HasOffers()` that runs the same not-owned predicate over `ItemId.All` (no UI, no pause). 
- In `SelectPanelForCurrentLevel()` the item branch is `level % 10 == 0 && ItemMenuHasOffers()`. When it's false at a level-10, control falls through to the `% 5 == 0` branch — and since 10 % 5 == 0, the player transparently gets the **slot** menu; if the slot panel is null it falls through again to the normal `menuPanel`. Either way the queue advances and timescale is safely restored.
- *(Rejected alternative: auto-grant a stat upgrade with no UI — it silently skips player agency and diverges from the "menu per level-up" UX. Not recommended.)*

So the effective fallback chain at an owned-all level-10 is: **item → (slot if present) → normal menu**, all still under `levelUpManager`'s single timescale owner.

---

## D. Multi-pending / queue interaction (pendingLevelUps > 0)

The item menu obeys the exact same queue contract as slot/normal because it advances through `ApplyChoiceAndAdvance()` (`levelUpManager.cs:86`):

- Each grant calls `ApplyChoiceAndAdvance()` → `pendingLevelUps--`.
- If `pendingLevelUps > 0`, the manager hides the current panel and re-runs `SelectPanelForCurrentLevel()` for the **new current level**, re-populating/activating whichever panel now applies (`levelUpManager.cs:89-97`). So a queued sequence like level 9→10→11 shows normal → item → normal automatically; two stacked level-ups landing on 20 then 21 show item → normal.
- The re-toggle branch must call the item populate path (`OpenForLevelUp()` / `PopulateForLevelUp()`) when the recomputed panel is the item panel — same "prime before show" requirement as §A. Add this to the `pendingLevelUps > 0` branch, not just `OpenMenu()`.
- When `pendingLevelUps` reaches 0 the manager restores `Time.timeScale = 1f` (`levelUpManager.cs:103`) — the item controller never does this in level-up mode, so there is no double-restore race.

---

## E. Editor wiring (scene-architect)

- **Assign `itemMenuPanel`** on the `levelUpManager` component to the item-menu panel GameObject (the same panel `itemChoiceMenuController.menuPanel` points at, or a dedicated one — see below).
- **Panel must start INACTIVE** (matching `menuPanel`/`slotMenuPanel` convention; `itemChoiceMenuController.menuPanel` already "starts INACTIVE" per its comment at `itemChoiceMenuController.cs:17`).
- **Controller reference:** if `levelUpManager` calls `itemChoiceMenuController.instance.OpenForLevelUp()`, no inspector wiring is needed (singleton set in `Awake`). If you prefer an explicit `[SerializeField] private itemChoiceMenuController itemMenu;`, wire it in the inspector. **Recommend the singleton** to keep wiring minimal and avoid a null-ref if the field is forgotten — but guard the call with a null check.
- **Panel sharing decision (call out for human):** the boss-drop flow and level-up flow can **share the same panel + buttons** (one `itemChoiceMenuController.menuPanel`) since only one is ever open at a time. This is simplest and needs no new UI. If a distinct visual treatment is wanted for level-up, create a second panel — but then the controller needs two panel refs, which complicates the single-panel `SetActive` logic. **Recommend sharing the existing panel.**
- **Button wiring is identical** to boss-drop usage — the same `buttons[]`/`labels[]` arrays (`itemChoiceMenuController.cs:18-19`) are re-bound each open via `onClick.RemoveAllListeners()` + `AddListener` (lines 82-83). No per-mode button differences; the mode flag only changes what `Choose()` does after granting.
- **Verify** in Play Mode: level to 10 → item menu appears while paused; pick → resumes; boss-drop still pauses/resumes independently.

---

## F. Changed / new surface + suggested phase order (for scrum-master)

**Files CHANGED (script — csharp-dev, Docker):**
- `Assets/Scripts/levelUpManager.cs` — new `itemMenuPanel` field; 3-way `SelectPanelForCurrentLevel()`; `ItemMenuHasOffers()` guard; item-populate call in both `OpenMenu()` and the re-toggle branch of `ApplyChoiceAndAdvance()`. Stays sole `Time.timeScale` owner.
- `Assets/Scripts/itemChoiceMenuController.cs` — extract `PopulateOffers()`; add `_fromLevelUp` flag; add `OpenForLevelUp()` + `HasOffers()`; make `Choose()`/close path mode-aware (level-up path calls `ApplyChoiceAndAdvance()` and does NOT touch timescale). Boss-drop `Offer()` contract unchanged.

**Files NEW:** none required (reuses existing panel/controller). Optional new UI panel only if human chooses distinct visuals in §E.

**Editor work (scene-architect, host `Agent` tool — NOT Docker):**
- Assign `itemMenuPanel` on `levelUpManager`; confirm it starts inactive; (optional) wire `itemMenu` reference; confirm button/label arrays.

**Validation (build-validator, host):** compile clean; Play-Mode level-to-10 shows item menu & resumes; boss-drop unaffected; owned-all level-10 falls back to normal/slot menu without soft-lock.

**Suggested phase order:**
1. **Script — controller dual-path.** Refactor `itemChoiceMenuController` (`PopulateOffers`, `_fromLevelUp`, `OpenForLevelUp`, `HasOffers`, mode-aware `Choose`). Verify boss-drop path unchanged first.
2. **Script — manager 3-way trigger.** Extend `levelUpManager` selection + owned-all guard + populate calls. Depends on phase 1's `OpenForLevelUp`/`HasOffers` API.
3. **Editor wiring.** scene-architect assigns `itemMenuPanel`, confirms inactive-on-start, singleton vs field choice.
4. **Validation.** build-validator Play-Mode checks (level-10 item, boss-drop, owned-all fallback, multi-pending queue).

---

## Open Questions (human input)

1. **Share the boss-drop panel or make a dedicated level-up item panel?** Design recommends **sharing** (zero new UI). Confirm.
2. **Owned-all fallback:** design recommends **fall back to normal (via slot) level-up menu**. Confirm vs auto-granting a stat upgrade.
3. **Controller reference style:** singleton (`itemChoiceMenuController.instance`) vs explicit serialized field. Recommend singleton.
