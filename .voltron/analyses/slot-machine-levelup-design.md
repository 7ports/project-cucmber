# Design: Slot-Machine Level-Up Menu

**Status:** DESIGN ONLY — no implementation. Read and approve before any code is written.
**Author:** project-planner
**Date:** 2026-07-13
**Branch:** `slot-machine-levlup`
**Feature:** A slot-machine styled variant of the level-up menu. Three reels spin through
CIRCLE / TRIANGLE / SQUARE symbols; the player stops them by pressing a movement key or
space; each reel lands on a random symbol carrying one upgrade; the player then picks a
*symbol* and receives every upgrade whose reel shows that symbol.

---

## 0. Grounding — what the existing code gives us

Read: `levelUpMenuController.cs`, `levelUpManager.cs`, `worldState.cs`,
`itemChoiceMenuController.cs`, `pauseMenuController.cs`, `playerMovement.cs`.

| Existing piece | What it does | How the slot machine reuses it |
|---|---|---|
| `levelUpMenuController.Upgrade` (private struct `{StatKind kind; Mode mode;}`) | The atomic "one upgrade" value | This is the payload each reel carries. Must become reachable to the new script. |
| `levelUpMenuController.BuildPool()` (private) | Builds the full `List<Upgrade>` honouring cap/0-base gating (XpGain cap, Defense/Regen/CritChance percent only once seeded, Pierce/XpGain flat-only) | The slot machine draws its 3 reel upgrades from **exactly this pool** so gating stays consistent. |
| `levelUpMenuController.LabelFor(Upgrade)` (private) | "+50 Damage" / "+20% Move Speed" text | Reused to label the upgrade shown on/over each reel. |
| `levelUpMenuController.Choose(Upgrade)` (private) | Mutates `worldState` for one upgrade **then** calls `levelUpManager.ApplyChoiceAndAdvance()` | The mutation half is reused; the advance half must be **decoupled** (slot machine applies 0–3 upgrades, then advances **once**). |
| `levelUpManager` (singleton) | `OnLevelUp` → `pendingLevelUps++` → `OpenMenuAfterDelay` (WaitForSecondsRealtime) → `OpenMenu()` sets `menuPanel` active + `Time.timeScale=0`. `ApplyChoiceAndAdvance()` decrements; re-toggles panel for the next pending level-up, else closes + `Time.timeScale=1`. | The slot menu plugs in as the manager's `menuPanel` (or as an alternate panel — see §E). Manager remains the single owner of pause + advance. |
| `worldState` (plain C# class, `static instance`, base+mult fields) | The stat model upgrades mutate | Unchanged. |
| `itemChoiceMenuController` / `pauseMenuController` | Both flip `Time.timeScale` directly → known race (CLAUDE.md tech debt) | We deliberately keep the slot machine from becoming a *fourth* independent `timeScale` writer (see §E). |

### ⚠️ Blocking discovery — input system mismatch
The task brief (and CLAUDE.md "Key Packages") says **New Input System only, no legacy `Input`**.
The actual gameplay code contradicts this:
- `playerMovement.cs:24-25` uses `Input.GetAxisRaw("Horizontal"/"Vertical")` (legacy).
- `pauseMenuController.cs` uses `Input.GetKeyDown(KeyCode.Escape)` (legacy).
- **No** script in `Assets/Scripts/` imports `UnityEngine.InputSystem` or uses `Keyboard.current` / `InputAction`.

So "read movement keys or space via the New Input System" has no existing pattern to copy, and
the project currently has legacy input **enabled** (or Active Input Handling = Both) or `playerMovement`
would not compile. **This is Open Question #1 and must be resolved before implementation.** The design
below is written for the New Input System (per the brief) but flags the exact spot the decision lands.

---

## A. Data model

### Symbols
```
enum SlotSymbol { Circle, Triangle, Square }   // new; 3 values
```
A reel's *visual* is one `Image`; a symbol maps to one `Sprite`. A `Sprite[3]` (indexed by
`(int)SlotSymbol`) wired in the Editor is the sprite lookup.

### One reel = one upgrade + one landed symbol
```
struct SlotResult {
    SlotSymbol symbol;                        // where this reel stopped
    levelUpMenuController.Upgrade upgrade;    // the single upgrade this reel carries
}
```
- **Exactly 3 reels.** Each reel carries **exactly one** upgrade.
- The 3 upgrades are drawn from `BuildPool()`: shuffle (Fisher-Yates, as in the existing
  controller) and take the first 3 → **3 distinct upgrades** (assuming pool ≥ 3, which it always
  is in practice; guard for the degenerate small-pool case, see §F).
- Each reel **independently** lands on a random `SlotSymbol` (`Random.Range(0,3)`), rolled
  *per reel*. Symbols therefore **can and will repeat** across reels.

### The resolution semantics (the whole point of the feature)
Because symbols are rolled independently per reel, a symbol may appear on 0, 1, 2, or 3 reels:

```
Reel 0: [Triangle]  carries "+50 Damage"
Reel 1: [Circle]    carries "+20% Move Speed"
Reel 2: [Triangle]  carries "+200 Max HP"

Pick TRIANGLE -> grant BOTH "+50 Damage" AND "+200 Max HP"   (2 upgrades)
Pick CIRCLE   -> grant "+20% Move Speed"                     (1 upgrade)
Pick SQUARE   -> grant nothing                               (0 upgrades)  <-- see §F/OQ#2
```
This "gamble" — a symbol may be loaded (many upgrades) or empty — is the core mechanic. The
mapping is **symbol → set-of-reels-showing-it**, not reel → symbol one-to-one.

---

## B. Spin animation (coroutine, no DOTween)

One controller (`slotMachineLevelUpMenu`) owns three reel `Image` references and runs the reels.

- **State:** `enum Phase { Spinning, Stopping, Stopped }`.
- **Per-reel spin:** either one coroutine per reel, or a single coroutine that advances all reels
  each tick. Recommended: **one coroutine per reel** so a per-reel stop delay (a "cha-cha-chunk"
  cascade) is possible later. Each tick it advances the reel's displayed sprite to the next
  `SlotSymbol` (cycling Circle→Triangle→Square→…) and waits `spinFrameInterval`
  (`WaitForSecondsRealtime`, ~0.06–0.1s — **realtime** because the game is paused, see §C).
- **Start:** the reels begin spinning when the panel opens (`OnEnable`) — mirrors
  `levelUpMenuController.OnEnable`'s populate-on-open lifecycle, so it composes cleanly with the
  manager toggling the panel for multiple pending level-ups.
- **Roll targets up front:** the 3 upgrades and 3 landed symbols are decided at open time
  (deterministic result, purely cosmetic spin). Storing the target lets the stop be exact.
- **Stop:** on the stop signal (§C), each reel's coroutine finishes its current tick, snaps the
  `Image.sprite` to its pre-rolled `SlotResult.symbol`, and the reel enters `Stopped`.
  - **OQ#7:** all-at-once stop vs staggered per-reel stop (add a small increasing delay per reel).
    Default recommendation: **all-at-once** for v1 (simplest, matches "presses key → slots stop").
- **Coroutine hygiene (CLAUDE.md rule):** every started reel coroutine is tracked and stopped in
  `OnDisable` / on transition to `Stopped`, so re-opening (next pending level-up) never leaves an
  orphaned spin running.

---

## C. Input-to-stop (New Input System, under `Time.timeScale = 0`)

**Trigger:** ANY movement key (WASD + arrow keys) OR Space, pressed while `Phase == Spinning`,
transitions to `Stopping`.

**Why timeScale doesn't block it:** `Time.timeScale = 0` freezes `FixedUpdate` and scaled time,
but **`Update` still runs every frame** and input polling is unaffected. So a paused menu can still
read input — the same reason the existing menus' buttons remain clickable while paused.

**Two things must be realtime-safe:**
1. The spin coroutine's waits use **`WaitForSecondsRealtime`** (not `WaitForSeconds`, which never
   advances at `timeScale=0`) — matching `levelUpManager.OpenMenuAfterDelay`.
2. Input is polled in `Update` (frame-based, unaffected by timeScale). Do **not** gate the read
   behind anything using scaled `Time.deltaTime`.

**Recommended read (New Input System):** poll `Keyboard.current` in `Update` while `Spinning`:
- Space: `Keyboard.current.spaceKey.wasPressedThisFrame`.
- Movement: `wKey/aKey/sKey/dKey` + `upArrowKey/leftArrowKey/downArrowKey/rightArrowKey`
  `.wasPressedThisFrame` (OR them together). `null`-guard `Keyboard.current`.
- Cleaner alternative: a serialized `InputActionReference` ("Move" + "Stop"/"Jump") whose
  `.action.WasPressedThisFrame()` is read; requires an `.inputactions` asset (none exists today).

**If OQ#1 resolves to "keep legacy input"** (matching `playerMovement`/`pauseMenuController`):
substitute `Input.GetAxisRaw("Horizontal"/"Vertical") != 0 || Input.GetKeyDown(KeyCode.Space)`.
Functionally identical; this note exists so the implementer doesn't fight the brief vs. the repo.

**Guard:** only accept the stop input while `Spinning` (ignore held keys after stop, and ignore
the very-first frame if the same keypress that opened context could bleed through).

---

## D. Post-spin symbol selection

After all reels reach `Stopped`, reveal the selection UI "like the normal menu" — i.e. buttons
appear over the reels (mirroring `levelUpMenuController`'s button presentation).

- **3 symbol buttons:** Circle, Triangle, Square (fixed set — one per symbol, always the same 3;
  distinct from the normal menu where buttons are per-upgrade).
- Optional but recommended UX: on each symbol button (or a caption near it) show the **count /
  labels** of upgrades that symbol will grant, using `LabelFor(Upgrade)` for each matching reel —
  so the player sees "Triangle → +50 Damage, +200 Max HP" before committing. (OQ#3: reveal
  contents before pick, or keep it a blind gamble?)
- **Resolution rule (`ResolvePick(SlotSymbol picked)`):**
  1. For each of the 3 `SlotResult`s where `result.symbol == picked`, apply that upgrade to
     `worldState` via the reused mutation path (see §G — the decoupled `ApplyUpgrade(Upgrade)`).
  2. After applying 0–N upgrades, advance the flow **exactly once** via
     `levelUpManager.ApplyChoiceAndAdvance()` (which handles unpause / next-pending re-open).
- Buttons `RemoveAllListeners()` before wiring (mirrors existing controllers) to avoid stacked
  listeners across re-opens.

---

## E. Integration with `levelUpManager` / `levelUpMenuController`

**Recommendation: make the slot machine a drop-in replacement panel, with the manager unchanged.**

`levelUpManager` is agnostic — it only toggles a `GameObject menuPanel` and owns `Time.timeScale`.
So the cleanest integration is:

- The **existing** `levelUpMenuController` panel and the **new** `slotMachineLevelUpMenu` panel are
  two sibling UI objects. `levelUpManager.menuPanel` points at **whichever mode is active**.
- `slotMachineLevelUpMenu` lives on the `slotMachineLevelUpMenu` GameObject the user already created,
  with its `Image` "slots" wired as the reels.
- On resolve, the slot controller calls `levelUpManager.ApplyChoiceAndAdvance()` — the **same**
  advance the normal menu uses — so multi-level-up queueing, re-open, and unpause all work for free.

**Single `timeScale` owner (addresses the known race):** the slot controller must **NOT** set
`Time.timeScale` itself. `levelUpManager.OpenMenu()` already sets it to 0 and
`ApplyChoiceAndAdvance()` restores it to 1. Adding a fourth independent writer would worsen the
`itemChoiceMenu` vs `levelUpMenu` race noted in CLAUDE.md. Keep the manager as the sole owner.

**Mode selection (which panel does the manager open?)** — this is **OQ#4**. Options:
- (a) **Global replace:** `levelUpManager.menuPanel` = slot panel; the classic menu is retired.
  Simplest; one code path.
- (b) **Config toggle:** a serialized `bool useSlotMachine` on `levelUpManager` picks which panel to
  `SetActive` in `OpenMenu()`/`ApplyChoiceAndAdvance()`. Small manager edit; both modes coexist.
- (c) **Per-level-up random / unlockable mode.** Most work; probably out of scope for v1.
Recommended: **(b)** — one small, reversible manager change; lets the user A/B the feature.

**Multi-pending level-ups:** `ApplyChoiceAndAdvance` re-toggles the panel off→on when
`pendingLevelUps > 0`, which re-fires the slot controller's `OnEnable` → a **fresh spin per pending
level-up**. That is the desired behaviour (each level = its own spin) and needs no extra code.

---

## F. Edge cases & OPEN QUESTIONS (please answer before implementation)

| # | Question | Options / default recommendation |
|---|---|---|
| **1** | **Input system:** brief says New Input System; repo uses legacy `Input`/`KeyCode` everywhere and has no `.inputactions` asset. Which do we target? | (a) New Input System + author an `.inputactions` (consistent with brief, but a new dependency to wire), (b) legacy `Input` to match `playerMovement`/`pauseMenuController` (consistent with repo *reality*). **Blocks §C.** Recommend deciding explicitly. |
| **2** | **Zero-match symbol:** player picks a symbol no reel shows → 0 upgrades. Allowed? | (a) Allow the "bust" (high-risk/high-reward, thematically a slot machine), (b) disable/hide symbol buttons that match no reel so the player can't pick a dud, (c) guarantee every symbol appears on ≥1 reel (constrains the roll). **Default: (b)** — never let a level-up grant nothing by accident. |
| **3** | **Reveal before pick?** Show which/how-many upgrades each symbol grants, or blind gamble? | (a) Show `LabelFor` list per symbol (informed pick), (b) blind (pure gamble). **Default: (a).** |
| **4** | **Replace vs alternate mode** (see §E). | **Default: (b) serialized `useSlotMachine` toggle on `levelUpManager`.** |
| **5** | **Distinct upgrades across reels?** Take-3-from-shuffled-pool already yields distinct upgrades; confirm no intentional duplicates. | **Default: distinct** (matches existing shuffle-and-take). Guard the rare pool<3 case (offer fewer reels, like `itemChoiceMenu`'s fewer-than-3 handling). |
| **6** | **Reroll behaviour?** Should the player be able to re-spin (e.g. a currency, or a free reroll button)? | **Default: no reroll in v1.** Flag as a future stat if desired. |
| **7** | **Stop granularity:** all reels stop on one press, or one press stops one reel at a time (classic slot "nudge")? | **Default: all-at-once** for v1; per-reel stop is a straightforward extension of the per-reel coroutine design. |
| **8** | **Symbol count per reel:** exactly the 3 symbols cycling, or a longer strip with weighting? | **Default: 3 symbols, uniform random.** |
| **9** | **What counts as a "movement key"?** WASD + arrows only, or also gamepad stick? | **Default: WASD + arrow keys.** Gamepad optional, tied to OQ#1. |

---

## G. Changed / new surface (for scrum-master decomposition)

### New scripts (`Assets/Scripts/`, flat, global namespace, `[SerializeField] private`, `_camelCase`)
- **`slotMachineLevelUpMenu.cs`** — the reel controller. Suggested surface:
  - Serialized: `Image[] _reels` (3), `Sprite[] _symbolSprites` (3, indexed by `SlotSymbol`),
    `Button[] _symbolButtons` (3: circle/triangle/square), `float _spinFrameInterval`,
    optional `Text`/label refs for per-symbol upgrade preview, optional `InputActionReference`s
    (if OQ#1 → New Input System).
  - Fields: `SlotResult[] _results` (3), `Phase _phase`, tracked reel `Coroutine`s.
  - Methods: `OnEnable()` (roll results from pool + start spin), `Update()` (poll stop input while
    Spinning), `IEnumerator SpinReel(int i)`, `StopAll()`, `ShowSymbolButtons()`,
    `ResolvePick(SlotSymbol)`, `OnDisable()` (stop coroutines, clear listeners).
- **`SlotSymbol` enum** — can live at top of `slotMachineLevelUpMenu.cs` or its own tiny file.

### Existing files to touch
- **`levelUpMenuController.cs`** — expose the reused pieces to the slot controller. Minimal-churn
  options (pick one; this is a small design decision for the implementer):
  - Make `Upgrade` accessible (it's referenced as `levelUpMenuController.Upgrade` above) and expose
    `BuildPool()`, `LabelFor(Upgrade)`, and a **new decoupled `ApplyUpgrade(Upgrade)`** that mutates
    `worldState` **without** calling `ApplyChoiceAndAdvance()` (the existing `Choose` becomes
    `ApplyUpgrade(u)` + `ApplyChoiceAndAdvance()`), **or**
  - Extract pool/label/apply into a shared static helper (e.g. `upgradePool.cs`) that both
    controllers call. **Recommended** — cleanest separation, no cross-controller coupling, and it
    removes the advance-coupling that currently lives inside `Choose`.
- **`levelUpManager.cs`** — only if OQ#4 → option (b): add serialized `bool _useSlotMachine` +
  `GameObject _slotMenuPanel`, and choose which panel to `SetActive` in `OpenMenu()` /
  `ApplyChoiceAndAdvance()`. No `timeScale` logic changes.

### Editor wiring (scene-architect / user — Unity Editor, host-only)
- On the existing `slotMachineLevelUpMenu` GameObject: add `slotMachineLevelUpMenu` component.
- Wire `_reels` → the 3 slot `Image`s already created.
- Import/assign 3 symbol sprites (Circle, Triangle, Square) → `_symbolSprites` (order = enum order).
- Create/assign 3 symbol selection `Button`s (Circle/Triangle/Square) → `_symbolButtons`
  (initially hidden; revealed after stop). Optional per-symbol preview `Text`.
- If OQ#4 → (b): wire the slot panel + `_useSlotMachine` on `levelUpManager`; ensure the slot panel
  starts **inactive** (manager toggles it), exactly like the existing `menuPanel`.
- If OQ#1 → New Input System: author an `.inputactions` asset with Move + Stop actions and assign
  the `InputActionReference`s.

### Suggested phase order (for scrum-master)
1. **Refactor** `levelUpMenuController` → shared upgrade pool/label/apply helper (decouple advance).
   *No behaviour change to the existing menu — validate the classic menu still works.*
2. **Core `slotMachineLevelUpMenu.cs`** — data model + coroutine spin + stop input + resolve
   (against the helper). Compile-clean, no scene deps beyond serialized refs.
3. **Manager integration** — `_useSlotMachine` toggle (if OQ#4=b) + unpause/advance path.
4. **Editor wiring** — reels, sprites, symbol buttons, panel toggle (scene-architect).
5. **Validation** — build-validator: compile clean, Play Mode level-up opens slot menu, spin →
   key stops → symbol pick grants the right upgrade set, unpause restores `timeScale=1`, multi
   level-up re-spins.

---

## Summary of key decisions embedded above
- 3 reels, each carrying 1 distinct upgrade from the existing `BuildPool()`; each reel lands on an
  independent random symbol → symbols repeat → picking a symbol grants **all** reels showing it (0–3).
- Coroutine spin with `WaitForSecondsRealtime` (paused-safe); stop on movement/space in `Update`.
- `levelUpManager` stays the single owner of pause/advance — slot controller never touches
  `Time.timeScale`, avoiding a new instance of the known menu-race.
- Reuse (via a shared helper) `BuildPool`/`LabelFor`/upgrade-apply; decouple the manager-advance
  from the worldState mutation so 0–3 upgrades can be applied before one advance.
