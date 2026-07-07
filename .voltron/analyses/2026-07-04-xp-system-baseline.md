# Code Analysis Report — xp-system-baseline

- **Topic:** xp-system-baseline
- **Date:** 2026-07-04
- **Actor:** code-analyst (read-only inventory + gap analysis)
- **Scope:** Assets/Scripts/{enemyHealth, pickupBehaviour, playerPickupRadius, slimeBehaviour, playerProjectileShooter, worldState, gameController}.cs — readiness baseline for the 6-part XP feature.
- **Note:** `mcp__project-voltron__submit_analysis` / `append_journal` MCP tools were unavailable in this environment; report persisted directly to this file. No stringer baseline exists (`.voltron/stringer/` absent) — delta check skipped.

## Summary

All 7 target files exist and were read. Three of them (`enemyHealth.cs`, `pickupBehaviour.cs`, `playerPickupRadius.cs`) are **empty Unity template stubs** — class + empty `Start()`/`Update()` only. The other four are small but real implementations. The project uses 2D physics (Rigidbody2D/Collider2D), a plain non-MonoBehaviour `worldState` static-instance class (lazily constructed by `gameController.Start()`), tag-based enemy lookup (`"Enemy"`), and exactly **one `Instantiate` call and zero `Destroy` calls** in the whole codebase (projectiles are spawned but never destroyed). No XP prefab exists yet. None of the 6 feature areas has its hook implemented; features 1–4 have named stub files waiting, features 5–6 have no files at all.

---

## Per-file inventory

### 1. `Assets/Scripts/enemyHealth.cs` (298 B) — EMPTY STUB
- **Class:** `enemyHealth : MonoBehaviour` ✓ MonoBehaviour
- **Fields:** none
- **Methods:** `void Start()` — empty body; `void Update()` — empty body
- **Status:** default Unity script template, zero logic:
  ```csharp
  public class enemyHealth : MonoBehaviour
  {
      void Start() { }
      void Update() { }
  }
  ```
- **Attached to:** `Assets/slime.prefab` (script guid `dd2bffd5…` present at slime.prefab:178).
- **Missing for feature 1:** HP field, take-damage entry point, death check, XP-prefab reference, spawn-on-death.

### 2. `Assets/Scripts/pickupBehaviour.cs` (302 B) — EMPTY STUB
- **Class:** `pickupBehaviour : MonoBehaviour` ✓ MonoBehaviour
- **Fields:** none
- **Methods:** empty `Start()`, empty `Update()` (identical template to above)
- **Attached to:** nothing — guid `8ba64ca6…` appears in no scene or prefab.
- **Missing for features 2 & 4:** pickup flag (bool), lerp-toward-player movement, player-contact detection (`OnTriggerEnter2D`), XP increment via `worldState.instance.currentXP`, self-destroy.

### 3. `Assets/Scripts/playerPickupRadius.cs` (313 B) — EMPTY STUB
- **Class:** `playerPickupRadius : MonoBehaviour` ✓ MonoBehaviour
- **Fields:** none
- **Methods:** empty `Start()`, empty `Update()` (identical template)
- **Attached to:** nothing — guid `c8a24dab…` appears in no scene or prefab (not even on the `player` GameObject in SampleScene).
- **Missing for feature 3:** radius field, XP detection (e.g. `Physics2D.OverlapCircleAll` or trigger collider), no XP tag/layer exists to filter on, no call into a pickup flag.

### 4. `Assets/Scripts/slimeBehaviour.cs` (867 B) — REAL IMPLEMENTATION
- **Class:** `slimeBehaviour : MonoBehaviour` ✓ MonoBehaviour
- **Fields:**
  | Name | Type | Access | [SerializeField] |
  |---|---|---|---|
  | `movementTimer` | `float` | private (implicit) | no (init `= 0`) |
  | `movementDelay` | `float` | **public** | no (inspector-exposed via public, `= 1.5f`) |
- **Methods:**
  - `void Update()` — accumulates `movementTimer += Time.deltaTime`; when `>= movementDelay`, `StartCoroutine(jump())`. ⚠ No guard flag: while the coroutine runs, `Update` keeps starting additional `jump()` coroutines every frame until the timer resets (slimeBehaviour.cs:12-19).
  - `IEnumerator jump()` — coroutine; lerps from `initPosition` toward `worldState.instance.player.position - transform.position.normalized` over 10 steps of `WaitForEndOfFrame`, applied via `transform.Translate`; resets `movementTimer = 0` (slimeBehaviour.cs:23-35). ⚠ Line 25 operator-precedence oddity: `.normalized` applies to `transform.position`, not to the difference vector. No stop path for the coroutine (violates project coroutine rule).
- **Player reference pattern:** `worldState.instance.player.position` (slimeBehaviour.cs:25) — this is the established way enemies find the player. Movement is transform-based; **no Rigidbody2D used** (slime.prefab has none).

### 5. `Assets/Scripts/playerProjectileShooter.cs` (1834 B) — REAL IMPLEMENTATION
- **Class:** `playerProjectileShooter : MonoBehaviour` ✓ MonoBehaviour
- **Fields:**
  | Name | Type | Access | [SerializeField] |
  |---|---|---|---|
  | `shootTimer` | `float` | private (implicit) | no |
  | `projectile` | `GameObject` | private | **yes** |
  | `projectileSpeed` | `float` | private | **yes** (`= 10f`) |
- **Methods:**
  - `void Update()` — cooldown from `worldState.instance.attackSpeed * worldState.instance.baseAttackSpeed`; finds nearest enemy via `GameObject.FindGameObjectsWithTag("Enemy")` (line 16); **`Instantiate(projectile, …)` at line 32**; sets `Rigidbody2D.linearVelocity` toward the enemy (lines 33-37); resets `shootTimer` only after firing (deliberate, per comment lines 42-43).
- Attached to `player` in SampleScene. ⚠ Uses `FindGameObjectsWithTag` every eligible frame — contravenes the CLAUDE.md "no Find()" rule (pre-existing).
- **Projectile prefab:** `Assets/star.prefab` — tag `playerBullet`, Dynamic `Rigidbody2D`, `CircleCollider2D` (isTrigger=0), **no script attached** → projectiles are never destroyed or recycled anywhere.

### 6. `Assets/Scripts/worldState.cs` (395 B) — REAL (minimal data holder)
- **Class:** `worldState` — **NOT a MonoBehaviour** (plain C# class; confirmed). Stray `using System.Xml.XPath;` at line 1 (unused).
- **Fields (all `public`, no [SerializeField] — n/a outside MonoBehaviour):**
  | Name | Type | Init |
  |---|---|---|
  | `instance` | `static worldState` | unassigned in this file |
  | `player` | `Transform` | — |
  | `attackSpeed` | `float` | `1` |
  | `attackDamage` | `float` | `1` |
  | `moveSpeed` | `float` | `1` |
  | `baseAttackSpeed` | `float` | `1.2f` |
  | `lvlUpXP` | `int` | `16` |
  | `currentXP` | `int` | `0` |
  | `maxHP` | `int` | `100` |
  | `currentHP` | `int` | `100` |
- **Methods:** none.
- **XP-relevant surface:** `currentXP` and `lvlUpXP` already exist — feature 4 only needs to increment `worldState.instance.currentXP`. No level-up logic exists anywhere.

### 7. `Assets/Scripts/gameController.cs` (484 B) — REAL (bootstrap)
- **Class:** `gameController : MonoBehaviour` ✓ MonoBehaviour
- **Fields:** `public Transform player` (inspector-assigned in SampleScene; no [SerializeField], public field — house style).
- **Methods:**
  - `void Start()` — lazily assigns the singleton: `if (worldState.instance == null) worldState.instance = new worldState();` then `worldState.instance.player = player;` (gameController.cs:9-13). **This is the only place `worldState.instance` is assigned.** ⚠ Script-execution-order hazard: any `Start()`/first-frame consumer running before this NREs.
  - `void Update()` — empty.
- Attached to `gameController` GameObject in SampleScene.

---

## Cross-cutting facts

### Instantiate / Destroy call sites (pooling touchpoints) — exhaustive
| Call | Location | What |
|---|---|---|
| `Instantiate(projectile, transform.position, transform.rotation)` | `playerProjectileShooter.cs:32` | spawns the `star.prefab` projectile |
| `Destroy(...)` | **none found anywhere in Assets/*.cs** | — |

That is the **only** spawn call in the codebase. Nothing destroys projectiles, slimes, or anything else yet — all future Destroy paths (enemy death, XP consumption, projectile cleanup) will be written fresh, so pooling (feature 6) has exactly one existing call site to convert and otherwise constrains new code only. **Slime spawning does not exist in code** — the slime is presumably scene/prefab-placed; no spawner script found.

### worldState access pattern
- Plain static-instance class (not MonoBehaviour, not ScriptableObject, no lazy property). Constructed and wired **only** in `gameController.Start()`.
- Consumers: `slimeBehaviour.cs:25` (`instance.player`), `playerProjectileShooter.cs:13` (`instance.attackSpeed`, `instance.baseAttackSpeed`), `playerMovement.cs:32` (`instance.moveSpeed`).

### Player Transform reference
- Enemies use `worldState.instance.player` (see slimeBehaviour.cs:25). New enemy type (feature 5) and XP lerp (feature 2) should use the same pattern.
- Player GameObject in SampleScene: tag `Untagged`, components include `playerMovement`, `BoxCollider2D`, `playerProjectileShooter`. **No Rigidbody2D found on the player** and player moves via `transform.Translate` (playerMovement.cs:32).

### Physics dimension: 2D (confirmed)
- `Rigidbody2D` referenced in playerProjectileShooter.cs:33 (and `rb.linearVelocity` — Unity 6 API); star.prefab has Dynamic Rigidbody2D + CircleCollider2D; slime.prefab has CircleCollider2D (isTrigger=0, **no Rigidbody2D**); player has BoxCollider2D. No 3D physics anywhere.
- ⚠ Contact-detection caveat for features 3/4: for `OnTrigger/OnCollisionEnter2D` to fire, at least one body in the pair needs a Rigidbody2D — currently neither player nor slime has one; XP pickups will need a trigger collider plus a Rigidbody2D (kinematic) on one side, or radius checks via `Physics2D.OverlapCircle`.

### Tags
- Defined in TagManager: `Enemy`, `playerBullet` (only these two custom tags).
- `Enemy` — on slime.prefab; queried at playerProjectileShooter.cs:16. `playerBullet` — on star.prefab; **never queried in code yet** (enemyHealth stub will presumably check it).
- **No XP/pickup tag exists** — feature 3 likely needs a new tag (e.g. `xp` / `pickup`) added to TagManager, or a layer-based overlap filter.

### Naming / style conventions (match these in new code)
- **lowercase camelCase class names** matching filenames: `enemyHealth`, `slimeBehaviour`, `worldState` — not PascalCase.
- **No namespaces** anywhere; scripts live flat in `Assets/Scripts/` (project does NOT follow the CLAUDE.md `Assets/_Project/` + namespace template).
- Fields: camelCase; mix of `public` fields for inspector exposure (slimeBehaviour, gameController) and `[SerializeField] private` (playerProjectileShooter). Private fields have no underscore prefix.
- Methods/coroutines: lowercase (`jump()`). Movement via `transform.Translate`/coroutine lerp, not physics forces.
- Legacy `Input.GetAxisRaw` in playerMovement.cs:20-21 despite `using UnityEngine.InputSystem` (pre-existing CLAUDE.md violation, noted for awareness only).

### Prefab inventory relevant to the feature
- `Assets/slime.prefab` — tag Enemy; SpriteRenderer, Animator, CircleCollider2D, `slimeBehaviour`, `enemyHealth`. Template for the feature-5 enemy variant.
- `Assets/star.prefab` — tag playerBullet; SpriteRenderer, Animator, CircleCollider2D, Rigidbody2D (Dynamic); no scripts.
- **No XP prefab exists** (only other prefab is `Assets/Sprites/cavePalette.prefab`). Feature 1 needs one created (pickupBehaviour attached to nothing today).

---

## Per-feature readiness (1–6)

| # | Feature | Readiness | Missing hooks |
|---|---|---|---|
| 1 | Enemy death → spawn XP | 🔴 Not started — `enemyHealth.cs` is an empty stub (though already attached to slime.prefab) | HP field, damage intake (projectile hit detection — star has no script and slime has no Rigidbody2D, so no collision callback currently fires), death→`Instantiate(xpPrefab)`, XP prefab reference & the XP prefab itself |
| 2 | XP lerps to player on pickup flag | 🔴 Not started — `pickupBehaviour.cs` empty stub, attached to nothing | pickup flag field, lerp in Update using `worldState.instance.player`, XP prefab to host it |
| 3 | playerPickupRadius sets flag | 🔴 Not started — `playerPickupRadius.cs` empty stub, not on the player object | radius field, `Physics2D.OverlapCircle`-style query or trigger, XP tag/layer to filter, reference to pickupBehaviour's (nonexistent) flag |
| 4 | XP contact → +currentXP, self-destroy | 🟡 Data side ready, behavior missing — `worldState.instance.currentXP`/`lvlUpXP` exist | contact detection (Rigidbody2D missing on player; XP prefab nonexistent), increment call, `Destroy`/pool-release path |
| 5 | Slow-lerp enemy variant of slime | 🔴 Nothing exists — no script, no prefab | new behaviour script (pattern available: copy `slimeBehaviour`'s `worldState.instance.player` targeting, continuous lerp instead of timed jump), prefab variant of slime.prefab, keep `Enemy` tag + `enemyHealth` |
| 6 | Object pooling (projectiles, slimes, XP) | 🔴 Nothing exists — no pool class | pool utility; exactly 1 Instantiate to convert (playerProjectileShooter.cs:32) and 0 Destroy calls today; no slime spawner exists in code (scene-placed), so slime pooling implies also creating a spawner; features 1–4 must route spawn/despawn through the pool |

## Findings (severity-tagged)

| Severity | Finding | File |
|---|---|---|
| info | `enemyHealth` is an empty template stub, already attached to slime.prefab | Assets/Scripts/enemyHealth.cs |
| info | `pickupBehaviour` is an empty template stub, attached to nothing | Assets/Scripts/pickupBehaviour.cs |
| info | `playerPickupRadius` is an empty template stub, not on the player | Assets/Scripts/playerPickupRadius.cs |
| info | Sole Instantiate call site (pooling touchpoint); zero Destroy calls project-wide | Assets/Scripts/playerProjectileShooter.cs:32 |
| info | `worldState` is a plain non-MonoBehaviour static-instance class; `instance` assigned only in gameController.Start() | Assets/Scripts/worldState.cs |
| warn | No Rigidbody2D on player or slime → 2D collision/trigger callbacks won't fire for XP contact as-is | Assets/slime.prefab, SampleScene player |
| warn | No XP prefab and no XP tag/layer exist yet | Assets/, ProjectSettings/TagManager.asset |
| warn | slimeBehaviour.Update can start overlapping `jump()` coroutines (no in-flight guard); `.normalized` precedence bug at line 25 — copying it for feature 5 propagates both | Assets/Scripts/slimeBehaviour.cs:12,25 |
| warn | Projectiles are never destroyed (star.prefab has no script) — unbounded object growth until pooling lands | Assets/star.prefab |
| info | Style: lowercase class names, no namespaces, flat Assets/Scripts/, camelCase fields, mixed public / [SerializeField] private | Assets/Scripts/* |
