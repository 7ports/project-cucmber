# Design: HUD Counters, Spawn Scaling, Level-Up Text, Collision Fixes, Pause Menu

**Date:** 2026-07-06
**Author:** project-planner
**Scope:** Design only — no implementation. A 6-item feature/fix batch for the 2D top-down survivor prototype.
**Related:** `.voltron/analyses/2026-07-04-xp-system-design.md`, `.voltron/analyses/2026-07-06-combat-levelup-vfx-design.md`

---

## Ground Truth (verified by reading source + YAML)

Facts every item below relies on. Cited inline where relevant.

**Layers** (`ProjectSettings/TagManager.asset`): `0 Default`, `2 Ignore Raycast`, `4 Water`, `5 UI`, `6 Pickups`, **`7 Player`**, **`8 Walls`**. Tags: `Enemy`, `playerBullet`.

**`worldState.cs`** — plain (non-Mono) class, `static worldState instance`. Fields: `attackSpeed=1`, `attackDamage=1`, `moveSpeed=1`, `baseAttackSpeed=1.2`, `range=4`, `lvlUpXP=16`, `currentXP=0`, `level=1`, `maxHP=100`, `currentHP=100`. `static event Action OnLevelUp`. `addXP(int)` loops: subtract, `level++`, `lvlUpXP *= 1.5`, fire `OnLevelUp`.

**Player GameObject** (scene `SampleScene.unity`, fileID `252393797`), **`m_Layer: 7` (Player)**, scale 1.5:
- `Rigidbody2D` — **BodyType 0 (Dynamic)**, gravityScale 0, `m_Constraints: 4` (freeze rot Z), `m_CollisionDetection: 0` (Discrete), `m_Interpolate: 0`, ExcludeLayers 0.
- `BoxCollider2D` — **non-trigger**, size 0.64, ExcludeLayers 0 (solid body).
- `CircleCollider2D` — **trigger, radius 0.5, ExcludeLayers 0** (the hurtbox `playerHealth` listens on).
- Scripts: `playerMovement`, `playerProjectileShooter` (projectileSpeed **5**), `playerPickupRadius`, `playerHealth` (damageInterval 2).

**`star.prefab`** (projectile), tag `playerBullet`, layer 0, scale 0.75:
- `Rigidbody2D` — **BodyType 0 (Dynamic)**, gravityScale 0, **`m_CollisionDetection: 0` (Discrete)**, **`m_Interpolate: 0`**.
- `CircleCollider2D` — **trigger**, radius 0.34375 (≈0.258 world), **`m_ExcludeLayers m_Bits: 128` = layer 7 (Player)** → bullet ignores the player (intended).
- `projectileBehaviour` — `lifeSeconds 3`, `wallLayer = 256` (layer 8 Walls), damages `Enemy`-tag on `OnTriggerEnter2D`, returns to pool on wall layer or at `worldState.range`.

**`slime.prefab`** (enemy), tag `Enemy`, layer 0, scale 0.75, **NO Rigidbody2D**:
- `CircleCollider2D` — **non-trigger**, radius 0.359375 (≈0.270 world), **`m_ExcludeLayers m_Bits: 128` = layer 7 (Player)**. ← key fact for Item 5.
- `slimeBehaviour` (transform-lerp hop), `enemyHealth` (maxHp 2, enemyDamage 5).

**`chaser.prefab`** — prefab variant of slime: removes `slimeBehaviour`, adds `chaserBehaviour` (transform `MoveTowards`, chaseSpeed 1), maxHp 1, enemyDamage 8, collider `m_IsTrigger: 0`. **Also NO Rigidbody2D**, inherits the same `ExcludeLayers = Player`.

**`Physics2DSettings.asset`**: `SimulationMode 0` (FixedUpdate, 50 Hz → 0.02 s/step), `QueriesHitTriggers 1`, `ReuseCollisionCallbacks 1`, `CallbacksOnDisable 1` (⇒ `OnTriggerExit2D` fires when a collider is pooled/disabled), `AutoSyncTransforms 0`. **Layer collision matrix is all-`ff` (nothing disabled)** — so per-collider `ExcludeLayers` is the ONLY layer-level filtering in play.

**`objectPool.cs`** — `get()`/`ret()` toggle `SetActive`; adds a `pooledObject` marker. Enemies, bullets, XP, blood, floating text all pooled.

**`floatingText.cs`** on `floatingLevelText.prefab` — world-space `Canvas` (RenderMode 2 = WorldSpace) + `CanvasGroup`, child legacy `UI.Text` ("Level Up"). `OnEnable` sets alpha 1; `Update` (unscaled) rises + **fades alpha 1→0** over `lifeSeconds`, returns to pool. Spawned by `levelUpManager.PlayJuice()` at `player.position + textOffset (0,1.2,0)`.

**UI:** project uses **legacy `UnityEngine.UI.Text`** throughout (`floatingText`, `levelUpMenuController`). HUD hierarchy: `Canvas/HPBarBackground/HPBarFill` and `Canvas/XPBarBackground/XPBarFill` (Filled `Image`s driven by `playerHealthUI`/`xpBarUI`).

**Input:** `com.unity.inputsystem` 1.19.0 installed, but `playerMovement` uses **legacy `Input.GetAxisRaw`** (backends = Both). Legacy `Input.GetKeyDown` is therefore available.

**No inventory/items system exists** (`grep -il inventory|item Assets/Scripts` → none). Item 6's items panel must be a placeholder.

---

## Item 1 — Numeric counters on the HP and XP bars

**Goal:** Show `currentHP/maxHP` on the HP bar and `currentXP/lvlUpXP` (plus level) on the XP bar.

**Approach:** Extend the two existing UI drivers rather than add new scripts — they already run `Update` against `worldState`.

**Scripts to edit:**
- `playerHealthUI.cs` — add `[SerializeField] private Text hpLabel;`. In `Update`, after setting `fillAmount`, set `hpLabel.text = $"{currentHP}/{maxHP}"` (null-guard the label). No new fields in worldState.
- `xpBarUI.cs` — add `[SerializeField] private Text xpLabel;`. Set `xpLabel.text = $"{currentXP}/{lvlUpXP}"`. Optionally a second `[SerializeField] Text levelLabel;` → `"Lv {level}"`, or fold level into one string (`"Lv 3   12/54"`).

**worldState/pool interactions:** none — pure read.

**Editor prerequisites (scene-architect):**
- Add a child `Text` (legacy UI) under `HPBarBackground` (e.g. `HPBarLabel`) and under `XPBarBackground` (`XPBarLabel`); center-aligned, RaycastTarget off, sorting above the fill image.
- Wire the new `Text` refs into the `hpLabel`/`xpLabel`/`levelLabel` fields on the existing `playerHealthUI`/`xpBarUI` components.
- **Decision:** legacy `Text` keeps parity with the rest of the UI. If the team prefers TMP, that is a separate migration (open question).

---

## Item 2 — Enemy spawn interval decreases per level

**Goal:** On each level-up, decrease the spawn interval by `0.3 * (1/level)`, with constants living in `worldState` and a floor so it never reaches 0.

**Design decision — where state lives & recompute happens:** `worldState` owns the interval and recomputes it *inside `addXP`* (right after `level++`, in the same loop that fires `OnLevelUp`). This keeps the spawner a dumb reader and guarantees the decrement is applied exactly once per level, even on multi-level XP gains.

**Scripts to edit:**
- `worldState.cs` — add named constants + live field:
  - `public float baseSpawnInterval = 2f;` (initial cadence — matches the spawner's current serialized 2f)
  - `public float spawnIntervalCoefficient = 0.3f;` (the `0.3` in the formula)
  - `public float minSpawnInterval = 0.4f;` (floor — see open questions)
  - `public float currentSpawnInterval = 2f;` (initialized to `baseSpawnInterval`)
  - In `addXP`, inside the `while` loop after `level++`: `currentSpawnInterval = Mathf.Max(minSpawnInterval, currentSpawnInterval - spawnIntervalCoefficient * (1f / level));`
    (uses the *new* level, so the first decrement at level 2 is `0.3*0.5 = 0.15`.)
- `enemySpawner.cs` — remove reliance on its own `[SerializeField] spawnInterval` for the live value; read `worldState.instance.currentSpawnInterval` in `Update` when comparing `spawnTimer`. Keep a serialized fallback only for when `worldState.instance == null`. (The existing null-guard `if (worldState.instance == null ...) return;` already short-circuits before spawn, so simplest is: `float interval = worldState.instance.currentSpawnInterval;`.)

**Why a floor:** the decrement is a harmonic-ish series (`0.3 * Σ 1/level`) which diverges, so without a clamp the interval eventually goes ≤ 0. `Mathf.Max(minSpawnInterval, …)` caps it. Default floor 0.4 s (open question).

**worldState/pool interactions:** spawner already routes through `objectPool.get`; unchanged. No `OnLevelUp` subscription needed in the spawner (recompute is centralized in `worldState.addXP`).

**Editor prerequisites:** none required (fields have defaults). Optionally expose `baseSpawnInterval`/`coefficient`/`minSpawnInterval` for tuning — but `worldState` is a plain class, not a MonoBehaviour, so these are **not** inspector-editable unless someone adds a serialized tuning MonoBehaviour. Flagged as open question (tuning surface).

---

## Item 3 — "LEVEL UP!" floating text (fade IN + rise + settle)

**Goal:** Text reading exactly `LEVEL UP!` that starts at the player's position, **fades IN** (opposite of current), slowly moves UP to a point above the player's head, and settles/holds before disappearing.

**Current behavior to change:** `floatingText.Update` sets `alpha = 1 - timer/lifeSeconds` → fades OUT immediately. Requirement is the inverse plus a controlled travel + hold.

**Design decision:** refine the existing `floatingText.cs` into a small state model rather than forking a new script — the prefab, pool wiring, and `levelUpManager` spawn call are already in place. Add a serialized phase model:

**Script to edit — `floatingText.cs`:**
- New serialized fields:
  - `float fadeInSeconds = 0.25f`
  - `float travelSeconds = 0.6f` (time to rise from start → target)
  - `float holdSeconds = 0.4f`
  - `float fadeOutSeconds = 0.3f` (brief tail so it doesn't pop; requirement emphasizes fade-in, a short fade-out at the very end is acceptable — or set 0 to hard-pop)
  - `Vector3 travelOffset = new Vector3(0, 1.0f, 0)` (end point above head, relative to spawn)
- `OnEnable`: `timer = 0`, cache `startPos = transform.position`, set `group.alpha = 0`.
- `Update` (keep **`Time.unscaledDeltaTime`** — level-up menu sets `timeScale = 0`, animation must still play):
  - Phase A (0 → fadeIn): alpha ramps 0→1.
  - Position: `transform.position = startPos + travelOffset * Ease(Clamp01(timer/travelSeconds))` (ease-out so it "settles"; a smoothstep is enough).
  - Phase C (hold): alpha stays 1, position at target.
  - Phase D (fadeOut, optional): alpha 1→0 over `fadeOutSeconds`.
  - Total lifetime = `fadeIn...+travel(overlaps)+hold+fadeOut`; when elapsed, `objectPool.ret`.
  - Because fade-in and travel overlap, drive them from the same `timer` with separate durations.

**Prefab/Editor prerequisites (scene-architect):**
- On `floatingLevelText.prefab`, set the child `Text.m_Text = "LEVEL UP!"` (currently "Level Up").
- Set the new serialized durations/offset on the `floatingText` component to taste.
- `levelUpManager` already spawns it at `player.position + textOffset (0,1.2,0)`. **Decision:** to make it start *at* the player and rise to above the head, either (a) reduce `levelUpManager.textOffset` toward `(0,0,0)` and let `travelOffset` do the rise, or (b) keep spawn offset small and set `travelOffset` for the remaining rise. Recommend (a): spawn at ~`(0,0.2,0)`, `travelOffset (0,1.0,0)`.

**worldState/pool interactions:** unchanged spawn path (`levelUpManager.PlayJuice → objectPool.get`). Still unscaled-time so it animates during the paused level-up menu.

---

## Item 4 — Bullet-vs-enemy collision is inconsistent — diagnosis + fix

### Root cause (grounded in the YAML)
The bullet is a **fast Dynamic trigger with Discrete collision detection and no interpolation**, and the enemy is a **static collider with no Rigidbody2D**:

- `star.prefab` `Rigidbody2D`: `m_CollisionDetection: 0` (**Discrete**), `m_Interpolate: 0`. Fired at `projectileSpeed 5` (player field) up to 10 → at 50 Hz physics that is **0.1–0.2 world-units per FixedUpdate step**.
- Combined trigger radii: bullet ≈0.258 + enemy ≈0.270 = **≈0.53 u** overlap window. Per-step travel (0.1–0.2) is under that, so *most* hits register — but the margin collapses at glancing angles, when frame time spikes, against the smaller effective footprint of a scaled 0.75 sprite, or if speed is tuned up. Discrete detection only tests overlap at each discrete step position, so the trigger can **tunnel** through the enemy between steps → intermittent misses.
- The enemy having **no Rigidbody2D** makes it pure static geometry; dynamic-trigger-vs-static-collider is exactly the case Continuous detection exists to fix.
- Layers are NOT the cause here: bullet and enemy are both layer 0, neither excludes layer 0, and the collision matrix is all-on. (Both exclude layer 7 Player, which is irrelevant to bullet-vs-enemy.)

### Fix (primarily Editor/prefab; no code required)
1. **`star.prefab` `Rigidbody2D` → `Collision Detection = Continuous`** (`m_CollisionDetection: 1`) and **`Interpolate = Interpolate`** (`m_Interpolate: 1`). Continuous performs a swept test between steps, catching fast trigger passes. — **Editor/prefab (asset-manager or scene-architect editing the prefab).**
2. **Add a Kinematic `Rigidbody2D` to the enemy prefabs** (see Item 5 — the same change). With the enemy as a real body rather than static geometry, bullet(Dynamic)-vs-enemy(Kinematic) trigger contacts are evaluated far more robustly, and Continuous sweeps resolve against it. — **Editor/prefab.**
3. *(Optional, only if 1+2 prove insufficient)* Slightly enlarge the bullet or enemy `CircleCollider2D` radius (e.g. enemy 0.36 → 0.42) for a wider capture window. — **Editor/prefab.**
4. *(Optional code fallback)* If tunneling persists at very high speeds, add a short `Physics2D.CircleCast` from previous→current position in `projectileBehaviour.Update` and treat a hit like `OnTriggerEnter2D`. — **C# (csharp-dev).** Not expected to be needed once 1+2 are in.

**Verdict:** #4 is fixed by **prefab/Editor changes** (Continuous detection + a Kinematic RB on enemies). No C# required for the primary fix.

---

## Item 5 — Enemy-vs-player collision not fully working — diagnosis + fix

### Root cause (grounded in the YAML) — two compounding issues, one primary
**PRIMARY — the enemy collider explicitly excludes the Player layer.**
The enemy `CircleCollider2D` has **`m_ExcludeLayers m_Bits: 128`**, and bit 7 = **layer 7 "Player"** (`TagManager`). The player GameObject is **`m_Layer: 7`**. `Collider2D.excludeLayers` suppresses *all* contact/trigger reporting between that collider and colliders on the excluded layer. Since the collision matrix is otherwise all-on, this per-collider exclusion is what silences enemy↔player. Result: the player's trigger hurtbox never receives `OnTriggerEnter2D` from enemies → `playerHealth.touchingEnemies` stays empty → no contact damage. This is the dominant cause.

**SECONDARY — enemies have no Rigidbody2D and move by `transform`.**
Even with the exclusion cleared, enemies are **static colliders** moved via `transform.position` (`chaserBehaviour.MoveTowards`, `slimeBehaviour` lerp) with `AutoSyncTransforms 0`. The trigger-callback rule requires at least one of the pair to have a `Rigidbody2D` — the **player's Dynamic RB2D satisfies that rule**, so triggers *can* fire — but moving a *static* collider by transform is unreliable/inefficient for continuous trigger tracking; the engine treats it as re-created static geometry each sync. Giving the enemy its own (Kinematic) Rigidbody2D makes it a first-class moving body and makes enter/exit deterministic.

**Pooling is NOT a root cause but was checked:** `CallbacksOnDisable 1` means `OnTriggerExit2D` fires when an enemy is pooled (`SetActive(false)`), and `playerHealth.Update` also defensively `RemoveWhere(c == null || !activeInHierarchy)`. On re-`get()` the collider re-registers and `OnTriggerEnter2D` fires on the next overlap. So pooled reactivation is handled once the exclusion/RB issues are fixed.

### Fix
1. **Clear `Exclude Layers` on the enemy `CircleCollider2D`** (set `m_ExcludeLayers` bits to 0 / "Nothing") on `slime.prefab` (and confirm the `chaser.prefab` variant inherits it — the variant currently only overrides `m_IsTrigger`, so clearing on the base propagates). — **Editor/prefab.** *This is the single change that unblocks contact damage.*
2. **Add a Kinematic `Rigidbody2D`** to the enemy prefabs (BodyType = Kinematic, gravityScale 0, freeze rotation Z). Kinematic (not Dynamic) because movement is script-driven and Dynamic would fall under the scene's gravity (`y = -9.81`) and get shoved by contacts. — **Editor/prefab.**
3. **Make the enemy `CircleCollider2D` a trigger** (`m_IsTrigger: 1`) — recommended. Rationale: the player also carries a **non-trigger solid `BoxCollider2D`**; if the enemy stays non-trigger and we clear the Player exclusion, the enemy's solid circle will *physically block/shove* the player's solid box. For a survivor swarm you want enemies to **overlap and deal contact damage, not body-block** the player. Making the enemy a trigger keeps `playerHealth`'s trigger-based detection working while removing unwanted solid pushback. It also keeps `projectileBehaviour` (a trigger) detecting the enemy (trigger+trigger+RB fires). — **Editor/prefab.**
   - *Trade-off:* trigger enemies no longer separate from each other (they can stack). For a top-down survivor this is usually desirable. If separation is later wanted, the alternative is keeping enemies non-trigger + moving the player's solid box to a dedicated layer and disabling the Player↔Enemy matrix cell for the solid pair only — more complex; deferred.
4. *(No code strictly required.)* Optionally switch enemy movement to `rb.MovePosition` in `chaserBehaviour`/`slimeBehaviour` for physically-correct kinematic motion, but `transform` movement works once a Kinematic RB exists. — **C# (csharp-dev), optional polish.**

**Verdict:** #5 is fixed by **prefab/Editor changes** — clear the Player exclusion (primary), add a Kinematic RB, make the enemy collider a trigger. No C# required for the primary fix; optional `MovePosition` polish is C#.

> **Note — items 4 and 5 share the "add Kinematic Rigidbody2D to enemies" change.** Do it once on the enemy prefab; it serves both.

---

## Item 6 — Pause menu with three side-by-side panels

**Goal:** A key (default Esc) toggles a pause overlay that sets `Time.timeScale = 0` and shows three side-by-side panels: (a) menu buttons, (b) player stats, (c) acquired items.

**Design decision — coexistence with existing pauses:** `levelUpManager` and `gameOverManager` both drive `Time.timeScale`. The pause controller must not fight them. Rule: the pause key is ignored while the level-up menu or game-over panel is active (guard on `menuPanel.activeSelf` / `gameOverPanel.activeSelf`, or a shared `GamePauseState`). On unpause, restore `timeScale = 1` only if no other pause owner is active.

**New scripts (C# — csharp-dev):**
- **`pauseMenuController.cs`** (MonoBehaviour)
  - `[SerializeField] GameObject pauseRoot;` (the overlay containing all three panels)
  - `[SerializeField] KeyCode pauseKey = KeyCode.Escape;`
  - `bool isPaused;`
  - `Update`: on `Input.GetKeyDown(pauseKey)` (legacy Input is available — see Ground Truth), and only if no level-up/game-over pause is active, call `Toggle()`.
  - `Toggle()/Pause()/Resume()`: set `pauseRoot.SetActive`, set `Time.timeScale = 0/1`. Guard restore against other pause owners.
  - Public `Quit()` → `Application.Quit()` (+ `EditorApplication.isPlaying = false` under `#if UNITY_EDITOR`).
  - Public `MainMenu()` → **stubbed**: logs "Main menu not implemented" and/or `SceneManager.LoadScene("MainMenu")` guarded by a build-settings check. No MainMenu scene exists yet (open question) → wire the button but leave it a no-op stub.
- **`pauseStatsView.cs`** (MonoBehaviour) — populates the stats panel on `OnEnable` from `worldState.instance`:
  - Reads `level`, `currentHP`/`maxHP`, `attackDamage`, `moveSpeed`, effective fire-rate (`attackSpeed * baseAttackSpeed` → shots/sec = `1f / that`), `range`.
  - `[SerializeField] Text[] statLabels;` or individual `Text` refs. Formats numbers (see open question on formatting; default 1 decimal, fire-rate as "x.x/s").
- **`pauseItemsView.cs`** (MonoBehaviour) — **placeholder**. Reads from a minimal items source (below). If empty, shows "No items yet". Populates a vertical list of item labels/icons.
- **`playerInventory.cs`** (optional new minimal source) — a tiny stub: `static playerInventory instance; List<string> Items = new();` with `Add(string)`. **No system consumes/produces items yet** — this exists only so the panel has a real source to bind to. Flagged as open question; default is an empty list so the panel renders "No items yet".

**worldState/pool interactions:** stats panel reads `worldState` (no writes). No pooling. Time.timeScale coordination as above. `floatingText`/level-up animations use unscaled time so they are unaffected.

**Editor prerequisites (scene-architect / build-validator):**
- Under `Canvas`, add `PauseRoot` (full-screen, inactive by default) containing a `HorizontalLayoutGroup` with three child panels: `MenuPanel`, `StatsPanel`, `ItemsPanel` (side by side).
- `MenuPanel`: `Resume` button (calls `pauseMenuController.Resume`), `Main Menu` button (calls `MainMenu` stub), `Quit` button (calls `Quit`).
- `StatsPanel`: labeled `Text` rows for level/HP/damage/move speed/fire rate/range; wire refs into `pauseStatsView`.
- `ItemsPanel`: a `VerticalLayoutGroup` content area + a "No items yet" placeholder `Text`; wire into `pauseItemsView`.
- Add `pauseMenuController` to a persistent GameObject (e.g. `gameController`) and wire `pauseRoot`.
- Optional: a semi-transparent full-screen backdrop `Image` on `PauseRoot`.
- **Input decision:** use legacy `Input.GetKeyDown(KeyCode.Escape)` for parity with `playerMovement`'s legacy input usage. If the team standardizes on the new Input System, swap to `Keyboard.current.escapeKey.wasPressedThisFrame` (open question).

---

## Dependency-ordered task list (Docker / csharp-dev  vs  Editor / scene-architect · build-validator)

Ordering respects: shared enemy-prefab change (4+5) done once; C# before its Editor wiring where a script must exist to attach; collision fixes are **prefab/Editor**, not code.

### Track A — C# (Docker · csharp-dev)
- **A1 (Item 1):** Extend `playerHealthUI` + `xpBarUI` with `Text` label fields and formatting. *(blocks E1 wiring)*
- **A2 (Item 2):** Add `baseSpawnInterval`/`spawnIntervalCoefficient`/`minSpawnInterval`/`currentSpawnInterval` to `worldState`; recompute in `addXP`; make `enemySpawner` read `currentSpawnInterval`.
- **A3 (Item 3):** Refactor `floatingText` to fade-IN + eased rise + hold (+optional short fade-out), unscaled time, `travelOffset`. *(blocks E3 prefab text/tuning)*
- **A4 (Item 6):** Create `pauseMenuController`, `pauseStatsView`, `pauseItemsView`, optional `playerInventory` stub. *(blocks E5 wiring)*
- **A5 (Item 5, optional polish):** switch enemy movement to `rb.MovePosition` (only if kinematic motion needs to be physically exact).

### Track B — Editor / prefab (host · scene-architect for scene/UI; asset-manager or scene-architect for prefab edits; build-validator to verify)
- **B1 (Item 4 — prefab):** `star.prefab` `Rigidbody2D` → Collision Detection = **Continuous**, Interpolate = **Interpolate**.
- **B2 (Items 4+5 — prefab, shared):** enemy prefabs (`slime`, confirm `chaser` variant) → add **Kinematic Rigidbody2D** (gravityScale 0, freeze rot Z).
- **B3 (Item 5 — prefab, PRIMARY):** enemy `CircleCollider2D` → **clear `Exclude Layers`** (remove Player) and set **`IsTrigger = true`**.
- **E1 (Item 1 — scene UI):** add HP/XP `Text` children; wire into `playerHealthUI`/`xpBarUI`. *(after A1)*
- **E3 (Item 3 — prefab UI):** set `floatingLevelText` text to `"LEVEL UP!"`, set durations/`travelOffset`, adjust `levelUpManager.textOffset`. *(after A3)*
- **E5 (Item 6 — scene UI):** build `PauseRoot` + three panels, buttons, stat/item labels; attach `pauseMenuController` and wire refs. *(after A4)*
- **V (all):** build-validator — compile check, Play-Mode smoke test: HP/XP numbers update; spawn cadence tightens after level-ups; "LEVEL UP!" fades in and rises; **bullets reliably kill enemies**; **enemies deal contact damage to the player**; Esc pauses/*unpauses* with correct panels and `timeScale`.

**Collision-fix classification (explicit):** Items 4 and 5 are **prefab/Editor changes** (B1/B2/B3), *not* code. The only C# touching collisions is optional (`MovePosition` polish, A5) and an optional CircleCast fallback for #4 if Continuous proves insufficient.

---

## Open questions / decisions for the user (with proposed defaults)

1. **Items/inventory system (Item 6c).** None exists. **Default:** ship `pauseItemsView` as a placeholder bound to a minimal `playerInventory` stub (empty list → "No items yet"). Confirm whether a real inventory is in scope now or later. *(Recommended: placeholder now.)*
2. **Pause key (Item 6).** **Default:** `Esc` via legacy `Input.GetKeyDown` (parity with `playerMovement`). Alternative: new Input System (`Keyboard.current.escapeKey`). Confirm key + input backend.
3. **Main Menu button (Item 6a).** No MainMenu scene exists. **Default:** wire the button to a no-op stub (log + guarded `LoadScene("MainMenu")`). Confirm if/when a MainMenu scene will be added to Build Settings.
4. **Spawn-interval floor (Item 2).** **Default:** `minSpawnInterval = 0.4 s`. Confirm the fastest cadence you want (0.3–0.5 typical). Also: the harmonic decrement means the interval keeps creeping toward the floor indefinitely — acceptable?
5. **Spawn tuning surface (Item 2).** `worldState` is a plain class, so `baseSpawnInterval`/coefficient/floor are **not** inspector-editable. **Default:** hard-coded defaults in `worldState`. If designers need to tune in the Inspector, add a small serialized tuning MonoBehaviour that pushes values into `worldState` on Start (extra task).
6. **Stat number formatting (Item 6b).** **Default:** integers for HP/level; 1 decimal for damage/moveSpeed/range; fire-rate as shots/sec `"x.x/s"` (from `1/(attackSpeed*baseAttackSpeed)`). Confirm whether to show raw stats or "+%" deltas.
7. **HP/XP label widget (Item 1).** **Default:** legacy `UnityEngine.UI.Text` (matches existing UI). Confirm if a TMP migration is desired (separate effort).
8. **Level-up text tail (Item 3).** Requirement stresses fade-IN. **Default:** brief `fadeOutSeconds ≈ 0.3` tail so it doesn't hard-pop; set to 0 for an instant disappear. Confirm preference.
9. **Enemy trigger vs solid (Item 5).** **Default:** make enemies triggers (overlap, no body-block). Confirm you don't want enemies to physically separate from each other / block the player.

---

## Handoff

This is a design document only — no source files were modified. Decomposition into agent tasks is the scrum-master's job.

> Plan saved to `.voltron/analyses/2026-07-06-ui-collision-pause-design.md`. Invoke `/scrum-master` with this design to generate a work breakdown.
