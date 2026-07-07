# Design: Combat, Level-Up Menu & VFX Batch

**Date:** 2026-07-06
**Author:** project-planner
**Scope:** Design-only (no implementation). Covers 4 features — projectile range, player HP + contact damage, level-up upgrade menu + juice, enemy death blood particles.
**Grounding:** Field/method names below were confirmed by reading the current scripts under `Assets/Scripts/`. Background: `.voltron/analyses/2026-07-04-xp-system-design.md`, `2026-07-04-xp-system-baseline.md`.

---

## Confirmed current state (read, not assumed)

| Script | Key surface (verified names) |
|---|---|
| `worldState.cs` | Plain class (NOT MonoBehaviour). `static worldState instance`. Fields: `Transform player`, `float attackSpeed=1`, `float attackDamage=1`, `float moveSpeed=1`, `float baseAttackSpeed=1.2`, `int lvlUpXP=16`, `int currentXP=0`, `int level=1`, `int maxHP=100`, `int currentHP=100`. Method `void addXP(int)` — **auto-levels in a `while` loop** (`currentXP -= lvlUpXP; level++; lvlUpXP = RoundToInt(lvlUpXP*1.5f)`). Instance created in `gameController.Start()`. |
| `projectileBehaviour.cs` | On `star.prefab`. `[SerializeField] float lifeSeconds=3`, `LayerMask wallLayer`, `float lifeTimer`. `OnEnable`: zero `Rigidbody2D.linearVelocity`, `lifeTimer=0`. `Update`: pool-return after `lifeSeconds`. `OnTriggerEnter2D`: wall-layer → `ret`; `Enemy` tag → `eh.takeDamage(RoundToInt(attackDamage))` + `ret`. |
| `enemyHealth.cs` | On `slime.prefab`/`chaser.prefab` (tag `Enemy`). `[SerializeField] int maxHp=3`, `int currentHp`, `GameObject xpPrefab`, `int xpDropCount=1`. `OnEnable` resets `currentHp`. `public void takeDamage(int)`. `void die()` — drops XP via `objectPool.instance.get(xpPrefab,...)` then `objectPool.instance.ret(gameObject)`. |
| `pickupBehaviour.cs` | `xpValue`; calls `worldState.instance.addXP(xpValue)` on player contact. |
| `playerProjectileShooter.cs` | On `player`. Fires pooled `projectile` at nearest `Enemy`, `rb.linearVelocity = dir * projectileSpeed`. |
| `objectPool.cs` | `static instance`. `GameObject get(prefab,pos,rot)`, `void ret(GameObject)`. Adds `pooledObject` marker (`source`). |
| `gameController.cs` | `Start()` creates `worldState.instance`, sets `worldState.instance.player`. Single obvious host for new manager wiring. |
| `chaserBehaviour.cs` / `slimeBehaviour.cs` | Enemy movement, both tagged `Enemy`, both carry `enemyHealth`. |
| `playerMovement.cs` | On `player`, moves via `worldState.instance.moveSpeed`. |
| `xpBarUI.cs` | Reads `lvlUpXP`/`currentXP` for the fill bar. |

**Physics note (confirmed by task brief + code):** all colliders are TRIGGERS. Damage/pickup detection is `OnTriggerEnter2D` today. Contact damage (Feature 2) therefore uses `OnTriggerStay2D` / enter+exit tracking, NOT `OnCollisionStay2D`.

---

## Feature 1 — Projectile range (distance-based fade + cull)

**Goal:** Replace the lifetime timer with a distance-traveled budget. Projectile fades (sprite alpha) as traveled distance approaches `worldState.range`, and pool-returns exactly when it reaches the limit.

### worldState change
- Add `public float range = 8f;` — the max distance (world units) a projectile may travel. New player stat, upgrade-eligible later.

### Scripts to EDIT
- **`projectileBehaviour.cs`** (edit, do not create new):
  - New fields: `Vector3 spawnOrigin` (captured in `OnEnable`), `SpriteRenderer sr` (cached in `OnEnable` via `GetComponent`), `Color baseColor` (cached from `sr.color`, so alpha resets cleanly on reuse).
  - `OnEnable`: set `spawnOrigin = transform.position`; keep the existing velocity-zero; **remove the `lifeTimer=0` reset and remove the `lifeSeconds` timer logic from `Update`** (replaced — see coexistence note). Reset `sr.color = baseColor` (full alpha) so a reused pooled star isn't stuck faded.
  - `Update`: compute `float traveled = Vector3.Distance(transform.position, spawnOrigin);` and `float limit = worldState.instance != null ? worldState.instance.range : fallbackRange;`
    - Fade: `float t = Mathf.Clamp01(traveled / limit);` map to alpha via a **fade curve** — proposed `alpha = 1f` until `t >= fadeStart` (e.g. 0.6), then linear ramp to 0 over the last 40%: `alpha = Mathf.InverseLerp(1f, fadeStart, t)` clamped. Keep an `[SerializeField] AnimationCurve fadeCurve` as the tunable so the artist can reshape without code (default = the linear tail). Apply `sr.color = new Color(baseColor.r,g,b, alpha)`.
    - Cull: `if (traveled >= limit) objectPool.instance.ret(gameObject);`
  - Keep a `[SerializeField] float fallbackRange = 8f` for when `worldState.instance` is null (early frames), mirroring existing null-guards.

### Coexistence with the lifetime timer
- **Replace, don't stack.** The distance cull is the new authority. Recommendation: keep `lifeSeconds` as a **safety backstop only** (e.g. 10s) so a stuck/zero-velocity projectile can never leak from the pool, but the *primary* cull is distance. Flag as open question whether to keep the backstop at all (default: keep, value 10s).

### Interactions
- worldState: reads `range`. Pool: same `ret` path — no pool changes.
- Wall/enemy trigger returns are unchanged and still take priority (a projectile that hits a wall before max range still returns immediately).

### Editor prerequisites
- `star.prefab`: confirm it has a `SpriteRenderer` (fade target). Assign `fadeCurve` default in inspector. No new prefab. Material must respect vertex/sprite alpha (default Sprites/Default does). — **shader-artist (Editor) check** if alpha doesn't visibly fade.

---

## Feature 2 — Player HP + contact damage

**Goal:** While the player overlaps an enemy, the player loses HP on a fixed 2s tick. Each enemy has a configurable damage-per-tick.

### worldState
- No new field required — reuse `currentHP` / `maxHP` (already 100/100). `playerHealth` reads/writes `worldState.instance.currentHP`.

### Scripts to CREATE
- **`playerHealth.cs`** (new MonoBehaviour, on `player`):
  - Fields: `[SerializeField] float damageInterval = 2f;` , `float tickTimer;` , `readonly HashSet<Collider2D> touchingEnemies = new();` (tracks current overlaps).
  - `OnTriggerEnter2D(other)`: if `other.CompareTag("Enemy")` add to `touchingEnemies`.
  - `OnTriggerExit2D(other)`: remove from `touchingEnemies`.
  - `Update`: if `touchingEnemies.Count > 0`, advance `tickTimer += Time.deltaTime`; when `tickTimer >= damageInterval`, `tickTimer -= damageInterval` and call `ApplyContactDamage()`. When count is 0, reset `tickTimer = 0` (proposed: no partial-charge carryover so stepping away resets the clock — flag as decision).
  - `ApplyContactDamage()`: sum `enemyDamage` over all live enemies in `touchingEnemies` (prune nulls/inactive first — pooled enemies deactivate without firing `OnTriggerExit2D` reliably), subtract from `worldState.instance.currentHP`, clamp `>= 0`, raise/`if (currentHP <= 0) Die()`.
  - `Die()`: default behavior = reload the active scene via `SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex)`. (Open question — see defaults.) Also resets `worldState.instance = null` path is NOT needed if reload reinitializes; note that `worldState` is a plain static — **on scene reload the static `instance` persists**, so `Die()` must explicitly reset HP/level/stats or null the instance so `gameController.Start()` rebuilds it. Proposed default: in `Die()`, set `worldState.instance = null` before reload so a fresh run starts clean.
  - **Pooled-enemy edge case:** because enemies return to the pool (SetActive false) without a guaranteed `OnTriggerExit2D`, `ApplyContactDamage` and the tick guard MUST prune `touchingEnemies` of null/`!activeInHierarchy` colliders every tick. Document this explicitly for csharp-dev.

- **`enemyDamage` field** — add to **`enemyHealth.cs`** (avoids a second component per enemy): `[SerializeField] int enemyDamage = 5;`. Expose a `public int EnemyDamage => enemyDamage;` getter so `playerHealth` can read it via `GetComponent<enemyHealth>()`. (Alternative: a standalone `enemyDamage.cs` component — rejected to keep enemy prefabs lean; flag if separation preferred.)

### HP UI (optional but recommended, mirrors xpBarUI)
- **`playerHealthUI.cs`** (new, optional): reads `currentHP/maxHP`, drives a Filled `Image`. Parallels `xpBarUI`. Editor: an HP bar Image on the HUD Canvas.

### Interactions
- worldState: `currentHP`. Enemy prefabs: new `enemyDamage` field. Pool: relies on prune-on-tick since pool deactivation bypasses trigger-exit.

### Editor prerequisites
- `player`: add `playerHealth` component (scene-architect). Ensure player has a trigger `Collider2D` + `Rigidbody2D` sized to represent the "contact" hurtbox (confirm it exists from pickup work).
- `slime.prefab` / `chaser.prefab`: set `enemyDamage` values in inspector (defaults below).
- HUD Canvas: HP bar Image (scene-architect) if `playerHealthUI` is used.
- Game-over: default reload needs no new scene; if a Game Over screen is chosen instead, that's an added Editor task (flagged).

---

## Feature 3 — Level-up upgrade menu + juice

### THE LEVEL-UP EVENT FLOW (single source of truth — used by all of Feature 3)

1. **Event source.** Refactor `worldState.addXP`: it no longer applies level effects silently. When `currentXP >= lvlUpXP`, it performs the XP bookkeeping (`currentXP -= lvlUpXP; level++; lvlUpXP = RoundToInt(lvlUpXP*1.5f)`) **and raises an event** `public static event System.Action OnLevelUp;` — invoked once per level gained. Because `addXP` loops, multiple gains in one pickup raise the event multiple times (each queued — see 4).
   - `worldState` is a plain class, so a `static event Action OnLevelUp` on it is the cleanest publish point; subscribers are MonoBehaviours that `+=` in `OnEnable` and `-=` in `OnDisable`.
2. **Menu.** A `levelUpMenuController` listens to `OnLevelUp`, shows a UI panel with **3 buttons**, each labeled with a stat and "+0.1". Clicking a button applies `+0.1f` to that stat on `worldState.instance`, plays UI particles, then closes/hides the panel and signals the manager the choice is resolved.
3. **Pause behavior.** While the menu is open, **`Time.timeScale = 0f`** (proposed default — a survivor game must pause for the pick). Restored to `1f` when the choice resolves AND no more level-ups are queued. All timers using `Time.deltaTime` (shooter, contact-damage tick, projectile travel) naturally freeze — acceptable. **Caveat for csharp-dev:** anything that must animate during pause (UI particles, floating text tween) must use unscaled time (`ParticleSystem.MainModule.useUnscaledTime = true`; text lerps on `Time.unscaledDeltaTime`).
4. **Queued level-ups.** A `levelUpManager` (single owner) keeps `int pendingLevelUps`. `OnLevelUp` increments it. If the menu is not already open, it opens the menu and sets `timeScale=0`. On each resolved choice it decrements; if `pendingLevelUps > 0` it immediately re-opens the menu for the next pick (still paused); when it hits 0 it restores `timeScale=1`. This handles the "gained 3 levels from one big XP orb" case cleanly.
5. **Particles.** On level-up trigger: play a burst ParticleSystem **at the player** (world-space) AND a burst **from the UI** (screen-space canvas) when the menu opens.
6. **Floating text.** On level-up trigger, spawn a world-space "Level Up" text above the player's head that rises and fades.

### Scripts to CREATE / EDIT
- **EDIT `worldState.cs`:** change `addXP` to stop applying stat/level *effects* silently and instead raise `static event Action OnLevelUp` per level gained (bookkeeping stays). Add `range` (from Feature 1) here too. No stat auto-boost on level (boosts now come from menu choices).
- **CREATE `levelUpManager.cs`** (MonoBehaviour, on a persistent object or the gameController object): owns `pendingLevelUps`, subscribes to `worldState.OnLevelUp`, drives menu open/close + `Time.timeScale`, triggers player particles + floating text. Public `ApplyChoiceAndAdvance()` called by the menu controller.
- **CREATE `levelUpMenuController.cs`** (on the menu panel): holds 3 `Button` refs + label `TMP_Text`s; on click calls a `UpgradeStat(StatId)` that does `worldState.instance.<stat> += 0.1f`, plays UI particles, then calls back into `levelUpManager.ApplyChoiceAndAdvance()`. Use an enum `enum UpgradeStat { AttackDamage, MoveSpeed, AttackSpeed }` mapped in inspector per button.
- **CREATE `floatingText.cs`** (on a small world-space text prefab): on `OnEnable` set text, lerp position up + alpha down over ~1s using `Time.unscaledDeltaTime`, then pool-return or `Destroy`. Can reuse `objectPool` (pooled) or be a self-destroying one-shot — proposed: pooled via `objectPool` for consistency with the rest of the project (flag).

### Which 3 stats (proposed — see open questions)
`attackDamage +0.1`, `moveSpeed +0.1`, `attackSpeed +0.1`. Note: **`attackSpeed` semantics** — in `playerProjectileShooter`, the fire cooldown is `attackSpeed * baseAttackSpeed`, so a *larger* `attackSpeed` = *slower* firing. If the button is meant to make the player shoot faster, it must **subtract** or the manager must invert the meaning. Flag this explicitly: proposed default = the "attack speed" button reduces the cooldown (i.e. `attackSpeed -= 0.1f` with a floor, OR redefine to a rate). **This is a real correctness trap — call it out to csharp-dev.**

### Interactions
- worldState: event source + stat mutation target. Pool: floating-text prefab (if pooled). Shooter/contact-damage: frozen by `timeScale=0` (intended).

### Editor prerequisites (asset work — NOT csharp-dev)
- **UI menu prefab** (scene-architect): a Canvas panel with 3 Buttons + labels (TMP), wired to `levelUpMenuController`. Hidden by default.
- **Player level-up ParticleSystem** (shader-artist, Editor): world-space burst prefab, `useUnscaledTime = true`.
- **UI ParticleSystem** (shader-artist, Editor): screen-space/canvas particle burst on menu open, `useUnscaledTime = true`.
- **Floating "Level Up" text prefab** (scene-architect): world-space TMP text + `floatingText` script; if pooled, register with the pool usage pattern.
- Button-to-stat enum mapping set in inspector (scene-architect).

---

## Feature 4 — Enemy death blood particles

**Goal:** On `enemyHealth.die()`, emit a short blood-droplet burst at the enemy position.

### Scripts to EDIT
- **EDIT `enemyHealth.cs`:** in `die()`, before returning the enemy to the pool, spawn the blood effect at `transform.position`. Add `[SerializeField] GameObject bloodPrefab;`.

### Spawn approach (recommended)
- **Pool the blood effect** through the existing `objectPool` for consistency: `objectPool.instance.get(bloodPrefab, transform.position, Quaternion.identity)`. The blood prefab's ParticleSystem should be `Play On Awake = true` (or a tiny `bloodBurst.cs` that plays in `OnEnable`) with a short duration, then a mechanism returns it to the pool after it finishes.
  - Because pooled objects are reused via `SetActive`, a one-shot burst needs a small **`bloodBurst.cs`** helper: `OnEnable` → `particleSystem.Play()`; return to pool after `main.duration + max lifetime` (a cached `WaitForSeconds`-style timer in `Update`, like `projectileBehaviour`'s original timer). This mirrors the pooling discipline already in the codebase.
- Alternative (simpler, rejected as default): plain `Instantiate` + `Destroy(go, 2f)` one-shot, no pooling. Cheap to write but violates the project's "everything pools" convention. Flag as the fallback if pooling the VFX is deemed overkill.

### Interactions
- Pool: adds one more pooled prefab type (`bloodPrefab`). worldState: none.
- Ordering: spawn blood **before** `objectPool.instance.ret(gameObject)` so `transform.position` is still valid.

### Editor prerequisites
- **Blood ParticleSystem prefab** (shader-artist, Editor): red/dark droplet burst, short duration (~0.5s), gravity-affected, small count. Attach `bloodBurst` if pooled.
- Assign `bloodPrefab` on `slime.prefab` and `chaser.prefab` (scene-architect).

---

## Cross-cutting: worldState field additions (single list)
- `float range = 8f;` (Feature 1)
- `static event System.Action OnLevelUp;` (Feature 3)
- `addXP` refactor: keep bookkeeping, remove silent effect, raise event (Feature 3)
- (`currentHP`/`maxHP` already exist — Feature 2 reuses them)

---

## Dependency-ordered task list

### C# — Docker / `csharp-dev` (`run_agent_in_docker`)
1. **[worldState] Add `range` field + refactor `addXP` to raise `static event Action OnLevelUp`** (bookkeeping preserved, no silent stat effect). *Blocks: F1, F3.*
2. **[Feature 1] Edit `projectileBehaviour.cs`** — spawnOrigin capture, distance-traveled cull, alpha fade via `SpriteRenderer` + `fadeCurve`, demote `lifeSeconds` to backstop. *Depends on 1 (range).* 
3. **[Feature 2] Create `playerHealth.cs`** — trigger overlap set, 2s tick, prune-on-tick for pooled enemies, `Die()` → scene reload + reset `worldState.instance`. *Depends on nothing new; needs `enemyDamage` (task 4).* 
4. **[Feature 2] Edit `enemyHealth.cs`** — add `enemyDamage` field + getter; (also task 8 blood hook lands here). *Blocks 3.*
5. **[Feature 2, optional] Create `playerHealthUI.cs`** — HP bar, parallels `xpBarUI`.
6. **[Feature 3] Create `levelUpManager.cs`** — subscribe `OnLevelUp`, `pendingLevelUps`, `Time.timeScale` control, trigger particles + floating text. *Depends on 1.*
7. **[Feature 3] Create `levelUpMenuController.cs`** — 3 buttons, `UpgradeStat` enum, `+0.1f` apply (mind the `attackSpeed` inversion trap), callback to manager. *Depends on 6.*
8. **[Feature 3] Create `floatingText.cs`** — unscaled-time rise+fade, pool-return. *Depends on 6.*
9. **[Feature 4] Edit `enemyHealth.die()`** — spawn `bloodPrefab` via pool before `ret`. **Create `bloodBurst.cs`** helper (OnEnable play + timed pool-return). *Depends on 4 (same file — sequence after task 4 to avoid edit conflict).* 

> **Merge-order note:** tasks 4 and 9 both edit `enemyHealth.cs` — assign to the same csharp-dev run or sequence them to avoid conflicting edits.

### Editor — host `Agent` tool (scene-architect / shader-artist / build-validator)
- **[scene-architect]** Add `playerHealth` (+ optional `playerHealthUI`) to `player`; confirm trigger hurtbox collider + Rigidbody2D.
- **[scene-architect]** Set `enemyDamage` + assign `bloodPrefab` on `slime.prefab` & `chaser.prefab`.
- **[scene-architect]** Build **UI level-up menu prefab** (Canvas panel, 3 Buttons + TMP labels), wire `levelUpMenuController`, map button→stat enum; hidden by default. Wire `levelUpManager` onto the gameController/persistent object.
- **[scene-architect]** Build **world-space "Level Up" floating-text prefab** (TMP + `floatingText`), register with pool.
- **[scene-architect]** HP bar Image on HUD Canvas (if `playerHealthUI` used).
- **[shader-artist — Editor]** Author **player level-up ParticleSystem** (world-space, `useUnscaledTime=true`).
- **[shader-artist — Editor]** Author **UI/canvas ParticleSystem** for menu-open (screen-space, `useUnscaledTime=true`).
- **[shader-artist — Editor]** Author **blood-droplet ParticleSystem prefab** (short burst, gravity, small count); attach `bloodBurst`.
- **[shader-artist — Editor]** Verify `star.prefab` sprite material fades on alpha (Feature 1) — sprite/vertex alpha check.
- **[build-validator]** After each C# batch: compile check, Play-Mode smoke — level-up menu opens/pauses/resolves, contact damage ticks at 2s, projectiles fade+cull at range, blood emits on death, no NREs, no pool leaks.

**Particle/VFX classification:** *authoring* ParticleSystems and materials is **shader-artist Editor work** (needs a live Editor). The C# hooks that *trigger* them (`bloodBurst`, particle `.Play()` calls, `useUnscaledTime` flags set in code) are **csharp-dev file work**. Prefab wiring (dragging the ParticleSystem prefab into serialized fields) is **scene-architect Editor work**.

---

## Open questions / decisions for the user (all have safe defaults — implementation is NOT blocked)

| # | Question | Proposed default (used if no answer) |
|---|---|---|
| 1 | **The 3 upgradeable stats** | `attackDamage +0.1`, `moveSpeed +0.1`, `attackSpeed` (see #2). |
| 2 | **`attackSpeed` button direction** (larger `attackSpeed` = *slower* fire in current shooter math) | Button *reduces* fire cooldown: `attackSpeed -= 0.1f` with a floor of `0.2f`. Alternatively relabel to "Attack Rate". |
| 3 | **Player death behavior at 0 HP** | Reload active scene AND null `worldState.instance` so stats/level reset. (Alt: dedicated Game Over screen — extra Editor task.) |
| 4 | **Default `range` value** | `8f` world units. |
| 5 | **Projectile fade curve** | Full alpha until 60% of range, linear fade to 0 over final 40% (tunable `AnimationCurve` on `star.prefab`). |
| 6 | **Keep lifetime backstop on projectiles?** | Yes — `lifeSeconds` retained as a 10s safety cull; distance is primary. |
| 7 | **Per-enemy `enemyDamage` (per 2s tick)** | slime = `5`, chaser = `8` (chaser is faster/more aggressive). |
| 8 | **Contact-damage timer carryover** when player steps out of contact | Reset `tickTimer` to 0 on leaving all enemies (no partial-charge carryover). |
| 9 | **Does the level-up menu pause the game?** | Yes — `Time.timeScale = 0` while open; UI particles + floating text use unscaled time. |
| 10 | **Queued multi-level-up handling** | Re-open menu once per pending level before restoring `timeScale`. |
| 11 | **Particle style / counts** | Blood: ~12 dark-red droplets, gravity, ~0.5s. Level-up (player): ~20 gold sparkle burst. UI: ~15 confetti burst. |
| 12 | **Floating text: pooled or one-shot?** | Pooled via `objectPool` (project convention); ~1s rise+fade. |
| 13 | **Blood VFX pooled or `Instantiate`+`Destroy`?** | Pooled via `objectPool` + `bloodBurst` helper (convention). |
| 14 | **HP bar UI** (`playerHealthUI`)? | Include it (mirrors `xpBarUI`); low cost. |

---

## Self-validation
- Per-feature sections present: **Feature 1** (projectile range), **Feature 2** (HP + contact damage), **Feature 3** (level-up menu + juice), **Feature 4** (blood particles). ✅
- Single level-up event-flow design stated once (the numbered 1–6 block under Feature 3) and referenced by all F3 sub-items. ✅
- Docker (csharp-dev) vs Editor (scene-architect / shader-artist / build-validator) task lists separated, with particle authoring flagged as shader-artist Editor work vs C# trigger file work. ✅
- Open-questions section: 14 items, each with a safe default. ✅
- No project source files modified — design doc only. ✅
