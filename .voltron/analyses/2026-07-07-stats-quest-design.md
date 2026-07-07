# Design: Stats Refactor + Quest System (6-item batch)

**Date:** 2026-07-07
**Author:** project-planner
**Scope:** Design only — no source edits. Consumed by `/scrum-master` for task decomposition.

## Context snapshot (verified from source)

- `worldState` is a **plain class** (not a MonoBehaviour), constructed once in `gameController.Start()` (`gameController.cs:11`) and exposed via `worldState.instance`. All stats are **public mutable fields** read directly by consumers.
- Current combat stats: `attackDamage(float)`, `moveSpeed(float)`, `attackSpeed(float, cooldown multiplier)`, `baseAttackSpeed(float=0.8)`, `range(float)`, `maxHP(int)`, `currentHP(int)`.
- **Effective fire cooldown today** = `attackSpeed * baseAttackSpeed` (`playerProjectileShooter.cs:13`); lower = faster.
- Level-up flow: `worldState.addXP` raises `OnLevelUp` → `levelUpManager.HandleLevelUp` opens `menuPanel` (pauses via `Time.timeScale=0`) → `levelUpMenuController.OnEnable` rolls 3-of-N via Fisher-Yates → `Choose` mutates a stat and calls `levelUpManager.ApplyChoiceAndAdvance`.
- `playerHealth` tracks `touchingEnemies` (HashSet), ticks contact damage every `damageInterval=2s`; the **first tick only fires after a full 2s** (bug targeted by Item 3).
- Pooling via `objectPool.get/ret` + `pooledObject` marker. `enemySpawner` spawns off-screen through the pool.
- Editor assets present (do not hand-edit YAML): base `white-guy.controller`, `chainsaw-guy.overrideController`, `chaser.prefab` (SpriteRenderer, no Animator yet). Tilemaps: ground/background + a **Walls** Tilemap on layer `Walls` (index 8). 2D triggers throughout.

---

## Item 1 — Defense stat (damage reduction)

**Goal:** a new stat that reduces incoming contact damage, offered as a level-up option.

**Scripts to edit:** `worldState.cs`, `playerHealth.cs`, `levelUpMenuController.cs`.

**worldState:**
- Add `defenseBase = 0f`, `defenseMult = 1f` (see Item 4 for the base/mult convention) and getter `float Defense()` → `defenseBase * defenseMult` (with `defenseBase=0`, mult is inert until a flat bonus lands, which is the intended default).

**playerHealth.ApplyContactDamage (`playerHealth.cs:34`):**
- After summing `dmg` from touching enemies, apply reduction before subtracting from HP.
- **Formula (default = flat subtract):** `int applied = Mathf.Max(1, dmg - Mathf.RoundToInt(worldState.instance.Defense()));`
  - Rationale: flat-subtract is transparent to players, cannot reach 100% immunity (floored at 1), and composes cleanly with the flat/percent upgrade system (a flat +2 defense and a +10% defense both read through `Defense()`). Percentage reduction risks runaway invulnerability as mult stacks. Flagged in Open Questions.
- Also apply the same reduction to Item 3's immediate first hit so both paths are consistent — route both through one private helper `int Reduce(int raw)`.

**levelUpMenuController:**
- Add `StatKind.Defense` to the enum + pool; label e.g. `"+2 Defense"` (flat) — see Item 4 for flat-vs-% offer plumbing.

**Editor prerequisites:** none (formula lives in code; label text is code-driven).

---

## Item 2 — Health regen stat

**Goal:** player passively regenerates HP over time; base 0, upgradable in +0.1 HP/s increments.

**Scripts to edit:** `worldState.cs`, `playerHealth.cs`, `levelUpMenuController.cs`.

**worldState:**
- Add `regenBase = 0f`, `regenMult = 1f`, getter `float Regen()` → `regenBase * regenMult` (HP per second).

**playerHealth:**
- Add a private `float regenAccumulator;`.
- In `Update()` (before/after the contact-tick block, independent of it): accumulate `regenAccumulator += worldState.instance.Regen() * Time.deltaTime;` and when `regenAccumulator >= 1f`, convert whole points: `int whole = Mathf.FloorToInt(regenAccumulator); regenAccumulator -= whole; currentHP = Mathf.Min(worldState.instance.MaxHP(), currentHP + whole);`
- Guard: skip while `currentHP <= 0` (dead) and clamp to `MaxHP()` (Item 4 effective getter). Accumulator approach avoids losing fractional regen at sub-1-HP/s rates.
- Note: `currentHP` is `int`; regen must accumulate fractionally as above rather than truncating each frame.

**levelUpMenuController:**
- Add `StatKind.Regen`; flat option `"+0.1 HP/s Regen"` raises `regenBase` by 0.1.

**Editor prerequisites:** none.

---

## Item 3 — Immediate first-contact damage

**Goal:** damage the player the instant an enemy first touches; the 2s interval applies only *after* the first hit.

**Scripts to edit:** `playerHealth.cs` only.

**Design (change to contact-tick logic):**
- In `OnTriggerEnter2D` (`playerHealth.cs:16`), when a **newly-added** enemy is registered (i.e. `touchingEnemies.Add(other)` returns `true` — `HashSet.Add` returns bool), if this enemy is the *first* contact after a gap, apply an immediate hit:
  - Compute damage from that single enemy's `enemyHealth.EnemyDamage`, run it through `Reduce()` (Item 1), subtract from `currentHP`, trigger flash/shake/flash, and run the death check — factor the existing tail of `ApplyContactDamage` (`playerHealth.cs:44-51`) into a shared `ApplyDamage(int raw)` helper so both immediate and interval paths reuse it.
  - Then set `tickTimer = 0f` so the next interval tick is a full `damageInterval` later.
- Keep the interval loop in `Update()` for *sustained* contact. Behavior: enter → instant hit → 2s → tick → 2s → tick…
- **Edge case:** if an enemy is already touching and a second enters, do not double-apply the world-tick; the immediate hit is per newly-entering enemy. Simplest correct rule (default): apply the immediate hit only when `touchingEnemies` was **empty before** this Add (first contact of a fresh engagement). Flagged in Open Questions (per-enemy vs per-engagement immediacy).

**Editor prerequisites:** none.

---

## Item 4 — Base + Multiplier stat system (ARCHITECTURAL CRUX)

**Goal:** every upgradeable stat has a **base** and a **multiplier**; **effective = base × mult**. Flat level-up bonuses raise the base; percentage bonuses raise the mult. All consumers read the effective value. Backward-compatible defaults (`mult = 1`, `base = current value`).

### 4.1 Stats converted

| Stat | base field (default) | mult field | Effective getter | Notes |
|---|---|---|---|---|
| Attack damage | `attackDamageBase = 10f` | `attackDamageMult = 1f` | `float AttackDamage()` | consumers round when needed |
| Move speed | `moveSpeedBase = 1f` | `moveSpeedMult = 1f` | `float MoveSpeed()` | |
| Fire rate | `fireRateBase = 1.25f` | `fireRateMult = 1f` | `float FireRate()` (shots/sec) + `float FireCooldown()` → `1f / FireRate()` | see 4.2 — replaces `attackSpeed`/`baseAttackSpeed` |
| Range | `rangeBase = 4f` | `rangeMult = 1f` | `float Range()` | |
| Max HP | `maxHPBase = 100f` | `maxHPMult = 1f` | `int MaxHP()` → `Mathf.RoundToInt(maxHPBase*maxHPMult)` | `currentHP` stays a plain int field |
| Defense | `defenseBase = 0f` | `defenseMult = 1f` | `float Defense()` | Item 1 |
| Regen | `regenBase = 0f` | `regenMult = 1f` | `float Regen()` | Item 2 |

- Implementation shape: parallel `float xBase; float xMult;` fields + getter **methods** on the plain class (methods, not properties, are fine here — either works; methods chosen for uniform call syntax `worldState.instance.AttackDamage()`). Keep old public field names **removed** and replaced by getters to force every read-site to migrate (compiler surfaces stragglers).
- `currentHP` and XP/level/spawn-interval fields are **not** base/mult — they stay plain fields.

### 4.2 Fire-rate inversion (the one non-uniform stat)

Fire rate is "lower cooldown = stronger", which breaks the "higher effective = stronger" convention. **Default resolution:** model it as a **rate (shots/sec)** where higher = stronger, uniform with every other stat.
- `fireRateBase = 1.25` (= `1 / 0.8`, so `FireCooldown() = 0.8s` — matches current cadence exactly, backward-compatible).
- Flat fire-rate upgrade: `fireRateBase += 0.25` (→ faster). Percent: `fireRateMult *= 1.1`.
- Consumer computes cooldown via `FireCooldown()` = `1f / FireRate()`.
- This retires both `attackSpeed` and `baseAttackSpeed`. (Alternative kept in Open Questions: keep a cooldown model where flat subtracts base and % multiplies mult by <1 — rejected as default because it inverts the flat/% direction and is error-prone.)

### 4.3 EVERY consumer read-site that MUST change

| # | File:line | Current read | Change to |
|---|---|---|---|
| 1 | `playerProjectileShooter.cs:13` | `worldState.instance.attackSpeed * worldState.instance.baseAttackSpeed` | `worldState.instance.FireCooldown()` |
| 2 | `projectileBehaviour.cs:21` | `worldState.instance.range` | `worldState.instance.Range()` |
| 3 | `projectileBehaviour.cs:49` | `worldState.instance.attackDamage` (RoundToInt) | `Mathf.RoundToInt(worldState.instance.AttackDamage())` |
| 4 | `playerMovement.cs:44` | `worldState.instance.moveSpeed` | `worldState.instance.MoveSpeed()` |
| 5 | `playerHealth.cs` (Item 1 reduction) | new | `worldState.instance.Defense()` |
| 6 | `playerHealth.cs` (Item 2 regen clamp) | `maxHP` | `worldState.instance.MaxHP()` |
| 7 | `levelUpMenuController.cs:69-71` | reads/writes `maxHP`, `currentHP` | raise `maxHPBase`/`maxHPMult`; adjust `currentHP` by the delta of `MaxHP()` before vs after |
| 8 | `levelUpMenuController.cs:75` | `attackSpeed *= 0.9` | raise `fireRateBase`/`fireRateMult` |
| 9 | `levelUpMenuController.cs:78` | `attackDamage *= 1.1` | raise `attackDamageBase`/`attackDamageMult` |
| 10 | `levelUpMenuController.cs:81` | `moveSpeed *= 1.1` | raise `moveSpeedBase`/`moveSpeedMult` |
| 11 | `levelUpMenuController.cs:84` | `range *= 1.1` | raise `rangeBase`/`rangeMult` |

**Catch-all (outside the files I was scoped to read):** grep the whole project for `worldState.instance.attackDamage`, `.moveSpeed`, `.attackSpeed`, `.baseAttackSpeed`, `.range`, `.maxHP` — HUD/counter scripts from prior commits (e.g. HUD counters, XP bar) likely read `maxHP`/`currentHP` and MUST migrate `maxHP` → `MaxHP()`. This grep is a mandatory step in the csharp-dev task; removing the old fields makes any missed site a compile error (intended safety net).

### 4.4 Level-up: mixing flat and percentage offers

- Rework `levelUpMenuController` so each rolled option carries **both** a `StatKind` and a `Mode { Flat, Percent }`.
- Represent a rolled option as a small struct `Upgrade { StatKind kind; Mode mode; }`. The pool becomes the cross product (each stat × {Flat, Percent}) minus stats that only make sense one way (e.g. Regen/Defense start at base 0 → **Percent is inert until a Flat lands**, so offer Flat-only for those until base > 0; or always offer both and accept early no-ops — default: Flat-only when base == 0).
- Shuffle and take 3 distinct `(kind, mode)` entries.
- `LabelFor(Upgrade)` renders e.g. `"+2 Damage"` (flat) vs `"+10% Damage"` (percent).
- `Apply(Upgrade)` switches on kind+mode and raises the matching `xBase` (flat) or `xMult` (percent). Keep the MaxHP special-case: after changing MaxHP, add `(MaxHP()_new - MaxHP()_old)` to `currentHP`.
- Default flat magnitudes (Open Questions): Damage +2, MoveSpeed +0.2, FireRate +0.25/s, Range +0.5, MaxHP +15, Defense +2, Regen +0.1/s. Percent magnitude stays +10% (MoveSpeed/Range/Damage/FireRate/MaxHP/Defense/Regen).

**Editor prerequisites:** none for the stat refactor itself. UI already exists (3 buttons + labels). If more than 3 stat kinds should appear, no scene change needed (labels are code-driven).

**Ordering constraint:** Item 4 (worldState getters) must land **before or in the same change** as the consumer edits (rows 1–11), because removing the old fields breaks compilation until every read-site migrates. Treat Item 4 + its 11 read-site edits as **one atomic csharp-dev task** (or a tightly ordered pair where the getter-add and field-remove bracket the consumer edits).

---

## Item 5 — Animated chaser enemy (Animator Override Controller)

**Goal:** the chaser plays a directional walk animation driven by its movement vector, using an override of `white-guy.controller`.

**Scripts:** new small `enemyAnimator.cs` (preferred over bloating `chaserBehaviour`), OR add animator-float logic to `chaserBehaviour.cs`. Default: new `enemyAnimator` component so movement and animation stay separable and reusable across enemy types.

**enemyAnimator.cs design:**
- `Animator anim;` cached in `Awake`/`Start`.
- Track previous position; each `Update` (or `LateUpdate`) compute `Vector3 delta = transform.position - lastPos;` then `Vector2 dir = delta.sqrMagnitude > epsilon ? delta.normalized : lastDir;`
- Set floats **matching the white-guy blend tree parameter names** — mirror `playerMovement.cs:28,32` which uses `"x"` and `"y"`. So `anim.SetFloat("x", dir.x); anim.SetFloat("y", dir.y);`
- Apply the same `animThreshold` debounce pattern as `playerMovement` (only set when the value changes beyond threshold) to avoid per-frame churn.
- Because `chaserBehaviour` uses `Vector3.MoveTowards` toward the player, the movement direction = `(player.position - transform.position).normalized`; `enemyAnimator` can either recompute this or read the frame delta. Default: frame delta (works even if movement source changes later).

**Coupling to existing systems:** enemies are pooled — `enemyAnimator` must reset `lastPos = transform.position` in `OnEnable` so a reused enemy doesn't snap a huge delta on its first animated frame.

**Editor prerequisites (scene-architect):**
1. Create an Animator Override Controller variant of `white-guy.controller` for the enemy (e.g. `chaser.overrideController`) — override the clip slots with the enemy's sprite-sheet walk clips. (An existing `chainsaw-guy.overrideController` is the template pattern to follow.)
2. On `chaser.prefab`: add an `Animator` component, assign `chaser.overrideController`; add the `enemyAnimator` component.
3. Confirm the base controller's blend-tree parameter names are exactly `x`/`y` (must match the SetFloat calls). If they differ, the parameter names in code must be updated — flagged in Open Questions.

---

## Item 6 — Quest items + off-screen edge indicators

### 6a — Quest items + valid spawn locations

**Scripts:** new `questItem.cs` (collectible), new `questManager.cs`.

**questItem.cs:**
- Placeholder-sprite collectible with a 2D trigger collider (isTrigger).
- `OnTriggerEnter2D`: if collided with the player (same pattern as `pickupBehaviour.cs:24`: `other.transform == worldState.instance.player || other.transform.root == worldState.instance.player`), notify `questManager` (`questManager.instance.Collect(this)`) and deactivate/return.
- Holds a public `Sprite icon` (its own sprite, forwarded to the indicator).

**questManager.cs:**
- `static questManager instance;` singleton set in `Awake`.
- `[SerializeField] GameObject questItemPrefab; [SerializeField] Tilemap groundTilemap; [SerializeField] LayerMask wallsMask; int questCount = 3;`
- On `Start` (or a trigger): spawn `questCount` items at **valid** random locations.
- **Valid-location selection (default algorithm):**
  1. Get walkable candidate cells from `groundTilemap.cellBounds` / `GetUsedCells` (ground = walkable). Convert each candidate cell to a world center via `groundTilemap.GetCellCenterWorld`.
  2. Reject any cell whose world position overlaps the **Walls** layer: `Physics2D.OverlapBox(center, cellSize*0.9f, 0f, wallsMask) == null` must hold (also usable: `OverlapCircle`). This uses the same Walls layer (index 8) the projectiles already respect.
  3. Optionally reject cells too close to the player's spawn or to each other (min-distance) so the 3 items are spread out. Default min-separation flagged in Open Questions.
  4. Pick `questCount` surviving cells at random (shuffle + take, or rejection-sample with a max-attempts cap; **log** if fewer than `questCount` valid cells found rather than silently spawning fewer).
- Track `List<questItem> active;` and `int collected;`. `Collect(questItem q)` removes it, increments `collected`; when `collected == questCount`, fire "ready for next part": raise a `static event Action OnAllQuestItemsCollected` **and** `Debug.Log("Quest complete — ready for next part")` (simple signal for now).

**Editor prerequisites:**
- `questItem.prefab` with placeholder sprite (shader-artist/asset-manager can supply a simple placeholder sprite; a solid-color square is fine), 2D collider (isTrigger), `questItem` component.
- A `questManager` GameObject in the scene with `groundTilemap` and `wallsMask` wired, `questItemPrefab` assigned.

### 6b — Off-screen edge indicators

**Scripts:** new `questIndicator.cs` (one per item), managed by `questManager` (or a small `questIndicatorController`).

**UI design:** pre-instantiate `questCount` (3) indicator UI objects under a screen-space Canvas (pooling optional; 3 is small — default = pre-instantiate 3, hide unused). Each indicator has:
- an **arrow** Image (rotated toward the item),
- the **item's sprite** Image (set from `questItem.icon`),
- a **distance** Text (number = distance from player to item).

**Per-frame math (in `LateUpdate`), for each uncollected item:**
1. `Vector3 vp = Camera.main.WorldToViewportPoint(item.position);`
2. **On-screen test:** if `vp.z > 0 && vp.x in [0,1] && vp.y in [0,1]` → item is visible → **hide** the indicator (`SetActive(false)`), skip.
3. **Off-screen:** handle `vp.z < 0` (behind camera) by flipping `vp.x/vp.y` and treating as off-screen. Clamp the viewport point to the screen edge: convert vp to centered coords `c = (vp - 0.5)`, find the axis that hits the edge first, scale so the larger `|c|` component = 0.5 (with a small inset margin so the indicator sits inside the visible border), then map back to screen position for the indicator's `RectTransform.anchoredPosition` (or set position directly on an overlay canvas).
4. **Arrow rotation:** angle = `Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg` where `dir` is the on-screen direction from screen-center toward the (unclamped) item screen position; set arrow `rotation = Quaternion.Euler(0,0,angle - 90)` (offset depends on the arrow sprite's rest orientation).
5. **Distance number:** `Vector2.Distance(player.position, item.position)` → format to integer; units = world units (Open Questions: raw world units vs a scaled "meters").
6. Set the item-sprite Image to `item.icon`.
- When an item is collected, its indicator is permanently hidden (and freed for reuse if pooled).

**Editor prerequisites (scene-architect):**
- A screen-space-overlay Canvas with 3 indicator prefabs (arrow Image + icon Image + Text), wired into `questManager`/controller.
- Arrow sprite + placeholder (shader-artist/asset-manager for the arrow sprite if none exists).

---

## Dependency-ordered task list (Docker csharp-dev vs Editor)

Legend: **[D]** = Docker / `csharp-dev` (file-only C#). **[E]** = Editor host (`scene-architect` / `shader-artist` / `build-validator`).

1. **[D] Item 4 core + consumer migration (ATOMIC, must land first).** Add base/mult fields + effective getters to `worldState.cs`; remove old public stat fields; migrate all 11 read-sites (§4.3) + project-wide grep for HUD/other readers. Rationale: removing old fields breaks compilation until all consumers migrate — do it as one change so the tree stays green. **Item 4 must land before or with every other C# item** since Items 1–3 read the new getters.
2. **[D] Item 1 — defense** in `worldState`/`playerHealth`/`levelUpMenuController` (depends on 1: uses `Defense()` + flat/percent offer plumbing).
3. **[D] Item 2 — regen** in `worldState`/`playerHealth`/`levelUpMenuController` (depends on 1).
4. **[D] Item 3 — immediate first-contact** in `playerHealth` (depends on 1; shares the `ApplyDamage`/`Reduce` helpers introduced in Item 1).
5. **[D] Item 4 offer system** — flat+percent `Upgrade` struct in `levelUpMenuController` (depends on 1; Items 1–3 add their StatKinds into this pool, so land after their worldState fields exist or coordinate the enum in step 1).
6. **[D] Item 5 script** — new `enemyAnimator.cs` (independent of stats; can run in parallel with 2–5).
7. **[D] Item 6a scripts** — `questItem.cs`, `questManager.cs` (independent; parallel-safe). Note: uses `Tilemap` API (`using UnityEngine.Tilemaps;`) and `Physics2D` — no Editor needed to compile.
8. **[D] Item 6b script** — `questIndicator.cs` (+ optional controller) (independent; parallel-safe).
9. **[E] build-validator** — compile check + Play Mode smoke after each C# batch (host, Unity MCP). Gate before Editor wiring.
10. **[E] Item 5 Editor** — create `chaser.overrideController` (override of `white-guy.controller`), add `Animator` + `enemyAnimator` to `chaser.prefab`, verify `x`/`y` param names. (`scene-architect`; art clips via `asset-manager`/`shader-artist` if needed.)
11. **[E] Item 6a Editor** — `questItem.prefab` (placeholder sprite, trigger collider), `questManager` GameObject wired to ground Tilemap + Walls mask.
12. **[E] Item 6b Editor** — Canvas + 3 indicator prefabs (arrow/icon/text), wire to manager; placeholder arrow sprite via `shader-artist`/`asset-manager`.
13. **[E] build-validator** — full Play Mode pass: level-up shows flat+% options, defense/regen/immediate-hit behave, chaser animates, 3 quest items spawn on valid ground with working edge indicators.

**Critical flag:** **Task 1 (Item 4) must land before or together with the consumer edits** — the field removal is a compile-breaking, cross-cutting change. All other C# tasks (2–8) depend on the getters existing.

---

## Open questions / decisions for the user (with defaults)

1. **Defense formula** — *Default: flat subtract* `Max(1, incoming - defense)`. Alternative: percentage reduction `incoming * (1 - defense%)`. Flat chosen to avoid runaway invulnerability; confirm?
2. **Regen increment** — *Default: +0.1 HP/s per flat upgrade*, base 0. OK, or different step (e.g. +0.25)?
3. **Fire-rate model** — *Default: rate (shots/sec), higher=better, `fireRateBase=1.25` for backward-compat.* Alternative: keep cooldown model with inverted flat/%. Confirm rate model?
4. **Flat vs percent options per level-up roll** — *Default: pool = each stat × {Flat, Percent}; roll 3 distinct (kind,mode); Flat-only for a stat while its base==0 (Defense/Regen).* Alternative: fixed ratio (e.g. always ≥1 flat + ≥1 percent per roll). Confirm mix policy + the default flat magnitudes in §4.4?
5. **Immediate-hit granularity (Item 3)** — *Default: apply the instant hit only on first contact of a fresh engagement (touching set was empty).* Alternative: per newly-entering enemy always. Confirm?
6. **Quest item count & "next part"** — *Default: 3 items; completion = fire `OnAllQuestItemsCollected` event + `Debug.Log`.* What should "ready for next part" actually do later (load scene? spawn boss? unlock)?
7. **Quest spawn spread** — *Default: reject candidate cells within a min-separation of each other/player.* Provide the min distance, or leave purely random among valid ground cells?
8. **Indicator distance units** — *Default: raw world units, rounded to integer.* Want a scale factor / "m" suffix / different rounding?
9. **Animator parameter names (Item 5)** — *Default: `x`/`y`, mirroring `playerMovement`.* Must be confirmed against the actual `white-guy.controller` blend-tree parameters; if they differ, code param names change.
10. **Placeholder art** — quest item sprite and edge-indicator arrow: OK to use solid-color square + simple triangle placeholders (asset-manager/shader-artist), replaced later?

---

*Next step:* invoke `/scrum-master` with this document to generate the work breakdown.
