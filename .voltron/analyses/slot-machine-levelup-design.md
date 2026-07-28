# Design: Slot-Machine Level-Up Menu

**Status:** DESIGN ONLY — no implementation. Read and approve before any code is written.
**Author:** project-planner
**Date:** 2026-07-13 (revised — finalized decisions + blind-pick flow + stateful pity / bad-luck protection)
**Branch:** `slot-machine-levlup`
**Feature:** A slot-machine styled variant of the level-up menu shown **every 5th level**. The
player is presented with 3 symbol buttons (CIRCLE / TRIANGLE / SQUARE) and **picks one BLIND,
before the reels resolve**. Five reels then spin and stop on random symbols; the player receives
every upgrade whose reel landed on their pre-picked symbol. A **stateful bad-luck-protection
("pity") system** — persisted on `worldState` between the 5th-level appearances — biases the roll
that **immediately follows** any slot roll that granted only a single upgrade. Non-multiple-of-5
levels use the existing normal level-up menu.

---

## 0. Grounding — what the existing code gives us

Read: `levelUpMenuController.cs`, `levelUpManager.cs`, `worldState.cs`,
`itemChoiceMenuController.cs`, `pauseMenuController.cs`, `playerMovement.cs`.

| Existing piece | What it does | How the slot machine reuses it |
|---|---|---|
| `levelUpMenuController.Upgrade` (private struct `{StatKind kind; Mode mode;}`) | The atomic "one upgrade" value | This is the payload each reel carries. Must become reachable to the new script. |
| `levelUpMenuController.BuildPool()` (private) | Builds the full `List<Upgrade>` honouring cap/0-base gating (XpGain cap, Defense/Regen/CritChance percent only once seeded, Pierce/XpGain flat-only) | The slot machine draws its **5** reel upgrades from **exactly this pool** so gating stays consistent. |
| `levelUpMenuController.LabelFor(Upgrade)` (private) | "+50 Damage" / "+20% Move Speed" text | Reused to label the upgrade shown on/over each reel. |
| `levelUpMenuController.Choose(Upgrade)` (private) | Mutates `worldState` for one upgrade **then** calls `levelUpManager.ApplyChoiceAndAdvance()` | The mutation half is reused; the advance half must be **decoupled** (slot machine applies 0–5 upgrades, then advances **once**). |
| `levelUpManager` (singleton) | `OnLevelUp` → `pendingLevelUps++` → `OpenMenuAfterDelay` (WaitForSecondsRealtime) → `OpenMenu()` sets `menuPanel` active + `Time.timeScale=0`. `ApplyChoiceAndAdvance()` decrements; re-toggles panel for the next pending level-up, else closes + `Time.timeScale=1`. | Manager now **selects which panel to open based on the level count** (slot on `level % 5 == 0`, else classic) and remains the **SOLE** owner of pause + advance. |
| `worldState` (plain C# class, `static instance`, base+mult fields, run-state reset each run) | The stat model upgrades mutate; also holds per-run flags reset on new game | Extended with **(a)** the persistent **pity flag** for bad-luck protection (reset each run, like other run-state) and **(b)** optionally one base+mult "Loaded Dice" stat that strengthens the boost (see §H). |
| `playerMovement.cs` / `pauseMenuController.cs` | Both read **legacy** input (`Input.GetAxisRaw`, `Input.GetKeyDown(KeyCode.Escape)`) | The slot machine reads input the **same way** — legacy `Input` only (see §C). |
| `itemChoiceMenuController` / `pauseMenuController` | Both flip `Time.timeScale` directly → known race (CLAUDE.md tech debt) | We deliberately keep the slot machine from becoming a *fourth* independent `timeScale` writer (see §E). |

### Input decision (RESOLVED — legacy input only)
This design targets **legacy `Input`** to match the repo reality:
- `playerMovement.cs:24-25` uses `Input.GetAxisRaw("Horizontal"/"Vertical")` (legacy).
- `pauseMenuController.cs` uses `Input.GetKeyDown(KeyCode.Escape)` (legacy).
- **No** script imports `UnityEngine.InputSystem` / uses `Keyboard.current` / `InputAction`.

The earlier "New Input System" recommendation is **withdrawn**. Migrating to the New Input System
is **deferred** — out of scope for this feature; the slot machine uses `Input.GetAxisRaw(...)` and
`Input.GetKeyDown(KeyCode.Space)`, identical in spirit to `playerMovement`/`pauseMenuController`.

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
- **Exactly 5 reels.** Each reel carries **exactly one** upgrade. **NO duplicate upgrades** across
  reels.
- The 5 upgrades are drawn from `BuildPool()`: shuffle (Fisher-Yates, as in the existing
  controller) and take the first 5 → **5 distinct upgrades**. Guard the rare `pool < 5` case:
  spawn only as many reels as the pool has entries (hide the extra reel `Image`s), mirroring
  `itemChoiceMenu`'s fewer-than-N handling. In practice the pool is ≥ 5.
- Each reel **independently** lands on a random `SlotSymbol`, rolled *per reel* (see §H for the
  pity-biased roll). With **5 reels but only 3 symbols**, symbols **must repeat** — this is the
  intended gamble.

### Run-state: the pity flag (persists across 5th-level appearances)
```
// on worldState, reset each run alongside other per-run state
bool slotPityPending = false;   // set when a slot roll granted exactly 1 upgrade;
                                // consumed to boost the NEXT slot roll (see §H)
```
- This flag is the **only** state that carries **between** slot-machine appearances (i.e. from one
  multiple-of-5 level to the next). It lives on `worldState` so it survives menu open/close and is
  reset on a new run exactly like the other run-state fields.

### The resolution semantics (the whole point of the feature)
Because symbols are rolled independently per reel across 5 reels and only 3 symbols exist, at least
two reels always share a symbol. Picking a symbol grants **all** reels showing it — often 2–3
upgrades:

```
Reel 0: [Triangle]  carries "+50 Damage"
Reel 1: [Circle]    carries "+20% Move Speed"
Reel 2: [Triangle]  carries "+200 Max HP"
Reel 3: [Square]    carries "+1 Pierce"
Reel 4: [Triangle]  carries "+10% Crit Chance"

Pick TRIANGLE -> grant "+50 Damage" + "+200 Max HP" + "+10% Crit Chance"   (3 upgrades)
Pick CIRCLE   -> grant "+20% Move Speed"                                   (1 upgrade)  <- lone single: arms pity
Pick SQUARE   -> grant "+1 Pierce"                                         (1 upgrade)  <- lone single: arms pity
```
This "gamble" — a symbol may be loaded (many upgrades) or lean (one) — is the core mechanic. A
**lone single-upgrade result arms the pity flag**, which boosts the *next* slot roll (§H). The
mapping is **symbol → set-of-reels-showing-it**, not reel → symbol one-to-one. The pick is made
**before** these symbols are known (see §C/§D).

---

## B. Spin animation (coroutine, no DOTween)

One controller (`slotMachineLevelUpMenu`) owns **five** reel `Image` references and runs the reels.

- **State:** `enum Phase { Choosing, Spinning, Stopping, Stopped }` — note the `Choosing`
  phase, which is where the blind pick happens (see §C).
- **Per-reel spin:** either one coroutine per reel, or a single coroutine that advances all reels
  each tick. Recommended: **one coroutine per reel** so a per-reel stop delay (a "cha-cha-chunk"
  cascade) is possible later. Each tick it advances the reel's displayed sprite to the next
  `SlotSymbol` (cycling Circle→Triangle→Square→…) and waits `spinFrameInterval`
  (`WaitForSecondsRealtime`, ~0.06–0.1s — **realtime** because the game is paused, see §C).
- **Lifecycle:** the panel opens in `Choosing` — 3 symbol buttons visible, 5 reels shown idle /
  pre-spin. Reels begin spinning only **after** the player commits a symbol pick (§C). Mirrors
  `levelUpMenuController.OnEnable`'s populate-on-open lifecycle, so it composes cleanly with the
  manager toggling the panel for multiple pending level-ups.
- **Roll targets after pick:** the 5 upgrades are decided at open time (from `BuildPool`); the 5
  landed symbols are rolled **at pick time** (once the chosen symbol is known — this is what lets
  the pity system bias toward it, §H). Storing the targets lets the stop be exact; the spin is
  purely cosmetic.
- **Stop:** on the stop signal (§C), each reel's coroutine finishes its current tick, snaps the
  `Image.sprite` to its pre-rolled `SlotResult.symbol`, and the reel enters `Stopped`.
  - Default: **all-at-once** stop for v1 (simplest, matches "presses key → slots stop").
    Staggered per-reel stop is a straightforward extension of the per-reel coroutine design.
- **Coroutine hygiene (CLAUDE.md rule):** every started reel coroutine is tracked and stopped in
  `OnDisable` / on transition to `Stopped`, so re-opening (next pending level-up) never leaves an
  orphaned spin running.

---

## C. Input — blind pick, then spin/stop (legacy `Input`, under `Time.timeScale = 0`)

**The flow is REVERSED from a classic slot machine: the player commits a symbol BEFORE the reels
resolve.**

1. **Open → `Choosing`:** panel becomes active; 3 symbol buttons (Circle / Triangle / Square) are
   shown up front; the 5 reels are idle / pre-spin. No outcome is known yet.
2. **Blind pick (commit):** the player clicks one symbol button. This is a **blind** commit — the
   reels have not resolved, so the outcome is unknown. The chosen `SlotSymbol` is stored, the
   buttons are disabled/hidden, the per-reel symbols are rolled (pity-biased toward the chosen
   symbol when the pity flag is armed — §H), and the controller transitions `Choosing → Spinning`.
3. **Spin → stop:** the reels spin (cosmetic). While `Spinning`, **ANY movement key OR Space**
   drives the stop: press → `Stopping` → reels snap to their pre-rolled symbols → `Stopped`.
4. **Reveal & grant:** once `Stopped`, the player receives **every upgrade whose reel landed on
   their pre-picked symbol** (see §D), and the pity flag is updated from the match count.

**Legacy input read (RESOLVED):** poll in `Update` while `Phase == Spinning`:
- Movement: `Input.GetAxisRaw("Horizontal") != 0f || Input.GetAxisRaw("Vertical") != 0f`
  (WASD + arrow keys, via the project's existing axis bindings — same as `playerMovement.cs`).
- Space: `Input.GetKeyDown(KeyCode.Space)`.
- OR them together; any one transitions `Spinning → Stopping`.

**Why timeScale doesn't block it:** `Time.timeScale = 0` freezes `FixedUpdate` and scaled time,
but **`Update` still runs every frame** and legacy `Input` polling is unaffected. So a paused menu
still reads input — the same reason the existing menus' buttons remain clickable while paused.

**Two things must be realtime-safe:**
1. The spin coroutine's waits use **`WaitForSecondsRealtime`** (not `WaitForSeconds`, which never
   advances at `timeScale=0`) — matching `levelUpManager.OpenMenuAfterDelay`.
2. Input is polled in `Update` (frame-based, unaffected by timeScale). Do **not** gate the read
   behind anything using scaled `Time.deltaTime`.

**Guard:** only accept the stop input while `Spinning` (ignore held keys after stop, and ignore
input during `Choosing`, so the click that opened the menu / picked the symbol cannot bleed
through into an immediate stop).

---

## D. Selection & resolution (pick already made in §C)

Because the symbol was chosen **before** the spin (§C), there is **no post-spin selection UI**.
After all reels reach `Stopped`, the controller resolves immediately against the pre-picked symbol.

- **3 symbol buttons** (Circle / Triangle / Square) are shown **at open, during `Choosing`** — one
  per symbol, always the same 3 (distinct from the normal menu's per-upgrade buttons). They are
  disabled/hidden the moment a pick is committed.
- Because the pick is blind, per-symbol upgrade previews are **not** shown before the pick (that
  would defeat the gamble). Labels via `LabelFor(Upgrade)` may still be surfaced on the reels
  **after** they stop, to show what was won.
- **Resolution rule (`ResolvePick()`), run automatically once `Stopped`:**
  1. Let `picked` = the symbol the player committed in §C.
  2. Count `matches` = number of `SlotResult`s where `result.symbol == picked`. For each, apply
     that upgrade to `worldState` via the reused mutation path (see §G — the decoupled
     `ApplyUpgrade(Upgrade)`).
  3. **Update pity state (Change 2 — the exact detection point):**
     - if `matches == 1` → **arm** pity: `worldState.instance.slotPityPending = true`.
     - if `matches >= 2` → **clear** pity: `worldState.instance.slotPityPending = false`.
     - (`matches == 0` is prevented by the guaranteed-minimum rule below, so it cannot occur.)
     Note the pity flag consumed for *this* roll was already cleared at roll time (§H); this step
     sets the flag for the **next** slot appearance based on *this* roll's outcome.
  4. After applying the upgrades, advance the flow **exactly once** via
     `levelUpManager.ApplyChoiceAndAdvance()` (handles unpause / next-pending re-open).
- **Zero-match guard:** with 5 reels over 3 symbols, every symbol is *likely* to appear; the roll
  additionally enforces a **guaranteed-minimum of 1 matching reel** for the picked symbol (§H) so a
  blind pick can never grant nothing.
- Symbol buttons `RemoveAllListeners()` before wiring (mirrors existing controllers) to avoid
  stacked listeners across re-opens.

---

## E. Integration with `levelUpManager` / `levelUpMenuController`

**Recommendation: the manager chooses the panel by level; both menus stay as sibling panels.**

`levelUpManager` owns the level count, the pending queue, and `Time.timeScale`. The cleanest
integration:

- The **existing** `levelUpMenuController` panel and the **new** `slotMachineLevelUpMenu` panel are
  two sibling UI objects (both start **inactive**; manager toggles them).
- **Trigger (RESOLVED):** in `OpenMenu()`, the manager picks the panel from the current level:
  **`level % 5 == 0` → slot panel; otherwise → classic panel.** `ApplyChoiceAndAdvance()` must
  toggle the **same** panel back off (and re-select per the level of the next pending level-up when
  re-opening). The manager needs a serialized `GameObject _slotMenuPanel` alongside the existing
  `menuPanel`, plus read access to the level count it already tracks (or the value that `worldState`
  / the XP system exposes).
- On resolve, the slot controller calls `levelUpManager.ApplyChoiceAndAdvance()` — the **same**
  advance the normal menu uses — so multi-level-up queueing, re-open, and unpause all work for free.

**Single `timeScale` owner (addresses the known race):** the slot controller must **NOT** set
`Time.timeScale` itself. `levelUpManager.OpenMenu()` already sets it to 0 and
`ApplyChoiceAndAdvance()` restores it to 1. `levelUpManager` remains the **SOLE** owner; adding a
fourth independent writer would worsen the `itemChoiceMenu` vs `levelUpMenu` race noted in
CLAUDE.md.

**Multi-pending level-ups:** `ApplyChoiceAndAdvance` re-toggles the panel off→on when
`pendingLevelUps > 0`, re-firing the chosen controller's `OnEnable`. Each pending level-up is
re-evaluated against `level % 5`, so a queue that crosses a multiple of 5 correctly shows the slot
menu on that level and the classic menu on the others. Each slot open = a **fresh choose → spin**,
and the pity flag correctly carries the outcome of the previous slot roll into the next one.

---

## F. Edge cases & remaining notes

| # | Item | Decision |
|---|---|---|
| 1 | **Input system** | **RESOLVED — legacy `Input`** (`GetAxisRaw` + `GetKeyDown(KeyCode.Space)`). New Input System deferred. |
| 2 | **Zero-match symbol** | Prevented by the **guaranteed-minimum-1-match** rule in the roll (§H). A blind pick always grants ≥1 upgrade. |
| 3 | **Reveal before pick?** | **Blind by design** (Change 1) — pick precedes the spin, so no preview. Reel labels may be shown *after* stop. |
| 4 | **Which panel opens?** | **RESOLVED — `level % 5 == 0` → slot menu; else classic.** Manager selects. |
| 5 | **Distinct upgrades across reels?** | **RESOLVED — 5 distinct, NO duplicates** (shuffle-and-take-5). Guard `pool < 5` → fewer reels. |
| 6 | **Reroll behaviour?** | No reroll in v1. |
| 7 | **Stop granularity** | All-at-once for v1; per-reel stop is a later extension. |
| 8 | **Symbol strip** | 3 symbols cycling; per-reel roll uniform 1/3 **unless** the pity flag is armed, then biased toward the picked symbol (§H). |
| 9 | **"Movement key" definition** | WASD + arrow keys via `GetAxisRaw` axes (matches `playerMovement`). Gamepad deferred with New Input System. |
| 10 | **Pity persistence** | Flag lives on `worldState`, survives between the 5th-level appearances, reset on a new run like other run-state (§A/§H). |

---

## H. NEW — stateful pity / bad-luck protection (Change 2)

**Goal (user clarification, verbatim intent):** the luck boost is applied to the slot-machine roll
that **immediately follows** any roll which granted **only 1 upgrade**. A lone-single-upgrade
result sets a "pity" flag that boosts the **next** slot roll; a normal (2+) roll clears/leaves it
cleared. This is **bad-luck protection**, stateful **across** level-ups — the state persists on
`worldState` between the 5th-level appearances.

**Why it's clean now:** with the blind pick preceding the roll (Change 1), the chosen symbol is
**known at roll time**, so the bias can directly favour it — no need to reverse-engineer intent
after the fact.

### Always-on system vs. purchasable upgrade — how it fits
The user calls it an "upgrade," but the mechanic as specified (a flag armed by a lone-single result
and consumed on the next roll) is a **stateful, always-on pity system** — it is not something the
player buys; it triggers automatically after an unlucky roll. This design therefore treats it as:

- **(Primary) Always-on pity system** — always active, no purchase. This is what the clarification
  describes and what §D/§H below implement.
- **(Optional, additive) Purchasable "Loaded Dice" stat** — a `worldState` base+mult stat that, if
  included, *strengthens* the pity boost (larger probability shift / higher guaranteed-minimum) when
  the flag is armed. This is where the word "upgrade" can map to a real pool entry. **Recommend
  shipping the always-on pity system first**; the purchasable stat is a small, optional add that
  reuses the exact same hook. Both are scoped below.

### State (`worldState`) — where the pity flag lives
```
// run-state, reset each run alongside the other per-run fields
bool slotPityPending = false;   // armed by a lone-single-upgrade slot roll; consumed next slot roll

// OPTIONAL purchasable strengthener (base+mult, same pattern as Defense/Regen/CritChance)
float loadedDiceBase = 0f;      // extra probability mass added to the picked symbol WHEN pity armed
float loadedDiceMult = 1f;
float LoadedDice => loadedDiceBase * loadedDiceMult;   // clamp effective value to [0, someMax]
```
- `slotPityPending` is the **required** state. It is reset to `false` on a new run, exactly like the
  other run-state fields on `worldState`, so pity never leaks across runs.
- `LoadedDice` is **optional**; when absent, the pity boost uses a fixed built-in weight constant.

### How a resolved roll detects "granted exactly 1 upgrade" and sets the flag
This is the §D `ResolvePick()` step 3, restated as the write side of the mechanic:
```
matches = count of reels whose landed symbol == pickedSymbol       // computed in ResolvePick()
if (matches == 1) worldState.instance.slotPityPending = true;      // arm for the NEXT slot roll
else /* matches >= 2 */ worldState.instance.slotPityPending = false; // normal roll clears it
```
The detection point is the resolution of the *current* roll; the effect lands on the *next* slot
appearance (a later multiple-of-5 level).

### The exact hook — the reel-symbol roll (read + consume side)
The bias is applied in the **per-reel symbol roll** (§B "Roll targets after pick" / §A independent
per-reel `SlotSymbol`), which runs **after** the blind pick, so `pickedSymbol` is known:

```
bool boosted = worldState.instance.slotPityPending;   // read the flag armed by the PREVIOUS roll
For each reel i in 0..4:
    weights[Circle]=weights[Triangle]=weights[Square] = 1f
    if (boosted)
        weights[pickedSymbol] += PITY_WEIGHT + worldState.instance.LoadedDice;  // raise above baseline 1/3
    symbol[i] = WeightedPick(weights)   // baseline 1/3 each; picked symbol's share rises when boosted
Guaranteed-minimum rule: after rolling all 5, if 0 reels show pickedSymbol,
    force one random reel to pickedSymbol (never grant nothing on a blind pick).
Pity extra-minimum (when boosted): also enforce a guaranteed-minimum of 2 matching reels
    (promote a second random reel to pickedSymbol) so the very next roll after a lone single
    is protected from repeating a single — directly satisfying "bad-luck protection."
// consume the flag NOW so the boost applies to exactly ONE roll:
if (boosted) worldState.instance.slotPityPending = false;
```
- When `slotPityPending` is **false** (a normal roll or the first slot of the run), this is the
  plain uniform 1/3 roll with only the always-on 1-match guarantee → baseline gamble intact.
- When `slotPityPending` is **true** (previous roll gave exactly one upgrade), the picked symbol's
  per-reel probability climbs above 1/3 **and** a 2-match minimum is enforced, so the follow-up roll
  is very unlikely to be another lone single. The flag is **consumed** in the same roll, so the
  boost is one-shot; the *result* of this boosted roll then re-arms or clears the flag in §D.
- If the optional `LoadedDice` stat exists and the player has bought it, its value stacks onto
  `PITY_WEIGHT`, making the pity boost stronger — the only place the purchasable "upgrade" touches.

### Optional pool entry (only if shipping the purchasable strengthener)
Following the existing flat/percent pattern in `levelUpMenuController`:
- **`StatKind`:** add `LoadedDice` (or `Luck`).
- **`BuildPool()`:** add the entry (flat and/or percent Modes like Defense/Regen); honour the same
  0-base gating if percent-only-once-seeded.
- **`LabelFor(Upgrade)`:** add a case → e.g. `"+15% Luck"` / `"+1 Loaded Dice"`.
- **`Choose(Upgrade)` / `ApplyUpgrade(Upgrade)`:** add the mutation branch bumping
  `loadedDiceBase` / `loadedDiceMult`. Because the slot draws from the same `BuildPool()`, the luck
  upgrade can itself appear on a reel — thematically apt.

### Assessment — **INCLUDE NOW (EASY)**
**Recommendation: include-now.** The stateful pity mechanic is easy to integrate because both sides
are single, well-localized spots created for free by Change 1:
- **Read/consume side** — one boolean read + weight bump + one-line consume, at the reel-symbol roll
  where `pickedSymbol` is already in scope (pick precedes roll, so no reverse-engineering).
- **Write/detect side** — the `matches` count already computed in `ResolvePick()`; setting/clearing
  one boolean is a two-line branch.
- **State** — one `bool` on `worldState`, reset with the other run-state; no new plumbing.
- The **optional** purchasable `LoadedDice` stat is pure copy-the-existing-pattern
  (`BuildPool`/`LabelFor`/`Choose`), the same shape as Defense/Regen/CritChance — safe to add now or
  defer without touching the pity logic.

The always-on pity system is included in the changed-surface list (§G) and the phase order below;
the purchasable strengthener is flagged optional there.

---

## G. Changed / new surface (for scrum-master decomposition)

### New scripts (`Assets/Scripts/`, flat, global namespace, `[SerializeField] private`, `_camelCase`)
- **`slotMachineLevelUpMenu.cs`** — the reel controller. Suggested surface:
  - Serialized: **`Image[] _reels` (5)**, `Sprite[] _symbolSprites` (3, indexed by `SlotSymbol`),
    `Button[] _symbolButtons` (3: circle/triangle/square, **shown at open during `Choosing`**),
    `float _spinFrameInterval`.
  - Fields: `SlotResult[] _results` (5), `SlotSymbol _pickedSymbol`, `Phase _phase`
    (`Choosing/Spinning/Stopping/Stopped`), tracked reel `Coroutine`s.
  - Methods: `OnEnable()` (draw 5 upgrades from pool, show symbol buttons, enter `Choosing`),
    `OnSymbolPicked(SlotSymbol)` (store pick, roll pity-biased symbols + consume flag, hide
    buttons, start spin), `Update()` (poll legacy stop input while `Spinning`),
    `IEnumerator SpinReel(int i)`, `StopAll()`, `ResolvePick()` (apply matching upgrades, update
    pity flag from match count, one advance), `OnDisable()` (stop coroutines, clear listeners).
- **`SlotSymbol` enum** — can live at top of `slotMachineLevelUpMenu.cs` or its own tiny file.

### Existing files to touch
- **`levelUpMenuController.cs`** — expose the reused pieces to the slot controller. Minimal-churn
  options (pick one):
  - Make `Upgrade` accessible and expose `BuildPool()`, `LabelFor(Upgrade)`, and a **new decoupled
    `ApplyUpgrade(Upgrade)`** that mutates `worldState` **without** calling
    `ApplyChoiceAndAdvance()` (existing `Choose` becomes `ApplyUpgrade(u)` +
    `ApplyChoiceAndAdvance()`), **or**
  - Extract pool/label/apply into a shared static helper (e.g. `upgradePool.cs`) that both
    controllers call. **Recommended** — cleanest separation, removes the advance-coupling in
    `Choose`.
  - **Optional luck upgrade:** if shipping the purchasable strengthener, add `StatKind.LoadedDice`
    (or `Luck`), its `BuildPool` entry, `LabelFor` case, and `Choose`/`ApplyUpgrade` mutation
    branch (§H). Not required for the always-on pity system.
- **`worldState.cs`** — add the **`slotPityPending` bool** (required; reset each run with other
  run-state) and, optionally, the `loadedDiceBase`/`loadedDiceMult` + `LoadedDice` accessor (§H).
- **`levelUpManager.cs`** — add serialized `GameObject _slotMenuPanel`; in `OpenMenu()` /
  `ApplyChoiceAndAdvance()` choose the panel by **`level % 5 == 0`** (slot) vs else (classic).
  **No `timeScale` logic changes** — manager stays the sole owner.

### Editor wiring (scene-architect / user — Unity Editor, host-only)
- On the existing `slotMachineLevelUpMenu` GameObject: add `slotMachineLevelUpMenu` component.
- Wire **`_reels` → the 5 slot `Image`s** (create the 5th if only 3 exist today).
- Import/assign **3 symbol sprites** (Circle, Triangle, Square) → `_symbolSprites` (order = enum
  order).
- Create/assign **3 symbol selection `Button`s** (Circle/Triangle/Square) → `_symbolButtons`,
  **shown at open (during `Choosing`)**, disabled/hidden on commit.
- Wire the slot panel → `levelUpManager._slotMenuPanel`; ensure it starts **inactive** (manager
  toggles it), exactly like the existing `menuPanel`.
- No `.inputactions` asset needed (legacy input). No Editor wiring needed for the pity flag — it is
  pure `worldState` run-state.

### Suggested phase order (for scrum-master)
1. **Refactor** `levelUpMenuController` → shared upgrade pool/label/apply helper (decouple advance).
   *No behaviour change to the existing menu — validate the classic menu still works.*
2. **Pity state** — add the **`slotPityPending` bool** to `worldState` (reset each run). *Optional
   in the same phase: the purchasable `LoadedDice` base+mult stat + `StatKind`/`BuildPool`/
   `LabelFor`/`ApplyUpgrade` entries (§H).* *Validate run-state resets on a new game.*
3. **Core `slotMachineLevelUpMenu.cs`** — data model (5 reels, blind pick) + pity-biased roll
   (read/consume flag) + coroutine spin + legacy stop input + `ResolvePick()` (apply, then arm/clear
   the pity flag from the match count). Compile-clean.
4. **Manager integration** — `level % 5` panel selection + `_slotMenuPanel` + unpause/advance path.
5. **Editor wiring** — 5 reels, 3 sprites, 3 symbol buttons (shown at open), panel toggle
   (scene-architect).
6. **Validation** — build-validator: compile clean; on a multiple-of-5 level the slot menu opens
   with symbol buttons up front → blind pick → reels spin → key/space stops → matching upgrades
   granted (≥1 guaranteed), unpause restores `timeScale=1`; non-multiple-of-5 levels still show the
   classic menu; a lone-single roll arms the pity flag and the **next** slot roll is visibly boosted
   (≥2 matches for the picked symbol) then clears; a 2+ roll leaves pity cleared; multi level-up
   re-opens correctly.

---

## Summary of key decisions embedded above
- **5 reels**, each carrying **1 distinct** upgrade (no duplicates) from the existing `BuildPool()`
  (shuffle, take 5; guard `pool < 5`); each reel lands on an independent random symbol → symbols
  repeat across 3 symbols → picking a symbol grants **all** reels showing it (often 2–3).
- **Blind pick-before-spin:** player commits a symbol first (blind), then the reels roll and stop;
  matching upgrades are granted on stop. Reversed from a classic slot.
- **Trigger:** slot menu on **`level % 5 == 0`** only; all other levels use the classic menu.
  `levelUpManager` selects the panel and remains the **sole** `Time.timeScale` owner.
- **Legacy input only** (`GetAxisRaw` + `GetKeyDown(KeyCode.Space)`); New Input System deferred.
- **Stateful pity / bad-luck protection (Change 2):** a `slotPityPending` bool on `worldState`
  (reset each run) is **armed** when a slot roll grants exactly 1 upgrade and **consumed to boost
  the immediately-following** slot roll (bias the picked symbol above 1/3 + guarantee ≥2 matches),
  then cleared; a 2+ roll clears it. This is an **always-on** system (not purchased); an
  **optional** purchasable `LoadedDice` stat can strengthen the boost via the same hook.
  **Recommended: include now** — read/consume and detect/set are both one localized spot each,
  made trivial by the pick-before-roll ordering; the state is one boolean of run-state.
