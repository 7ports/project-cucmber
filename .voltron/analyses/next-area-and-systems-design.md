# Design: Next Area + New Systems (items, crit, puzzle/quest, particles)

**Project:** project-cucumber — Unity 6000.4.2f1, 2D top-down survivors, URP 2D, single scene (`Assets/Scenes/SampleScene.unity`), flat scripts in `Assets/Scripts/` (global namespace), New Input System, **no DOTween** (coroutines/Cinemachine only).
**Date:** 2026-07-12
**Author:** project-planner
**Status:** DESIGN — not implementation. No full code below; component names, prefabs, events, and stat fields are specified so scrum-master can decompose into tasks.

---

## 0. Codebase grounding (what already exists)

| Fact | Location | Consequence for this design |
|---|---|---|
| `worldState` is a **plain C# class** held in a `static` field — NOT a MonoBehaviour | `worldState.cs` | It is not destroyed by a scene unload. Player *stats* survive a scene load automatically; scene MonoBehaviours do not. |
| Stat system is **base + mult** with getter methods (`AttackDamage()`, `FireRate()`, …) | `worldState.cs` | New stats (crit, aura, robot, trail, grenade) follow the identical `xBase / xMult / X()` pattern. |
| Item contract = string IDs in `ItemId.All`, ownership via `playerInventory.Has(id)`, granted by boss drop → `itemChoiceMenuController.Offer()` | `itemManager.cs`, `playerInventory.cs`, `itemChoiceMenuController.cs`, `itemPickup.cs` | New items register by (1) adding an `ItemId` constant, (2) adding it to `All`, (3) adding a `DisplayName` case. Behaviour is gated by `Has()`. |
| Damage funnels through one hit site | `projectileBehaviour.OnTriggerEnter2D` → `enemyHealth.takeDamage(int)` | The crit roll and any shared "deal damage" helper must live where every source can reach it — put it on `worldState`. |
| Explosion (EXPLODE item) is **inline** `Physics2D.OverlapCircleAll` | `projectileBehaviour.cs` | Must be extracted to a shared static so the GRENADE reuses it. |
| `questManager` already spawns N `questItem`s on the ground Tilemap and fires `OnAllQuestItemsCollected` — **no subscriber exists** | `questManager.cs` | This is the ready-made hook for the next-area door. |
| `objectPool.get(prefab,pos,rot)` / `ret(go)`, pooled reset in `OnEnable` | `objectPool.cs`, `projectileBehaviour.cs` | Reuse for trail segments, grenades, particle bursts. Pooled objects MUST reset all state in `OnEnable`. |
| Level-up upgrade pool is a `StatKind` enum + `BuildPool`/`LabelFor`/`Choose` triad | `levelUpMenuController.cs` | Crit upgrades slot in by extending these three places, mirroring Defense/Regen gating. |
| `playerInventory.Add` is add-only, no event | `playerInventory.cs` | Persistent auto-weapons (aura/robot/trail/grenade) need an activation signal — add `OnItemAdded`. |
| No `DontDestroyOnLoad` anywhere; ~15 singletons each do `instance = this` in `Awake` | project-wide | A second Unity scene would duplicate every singleton and collide. Central to §1. |

---

## 1. Next-area architecture

### Decision: **Same-scene new *region*** (unlock a gated region of `SampleScene`). Defer additive/multi-scene until a genuine save-load or streaming requirement appears.

### Rationale

**What actually needs to persist across "the next area":** player position/inventory/HP, `worldState` stats, the object pool, the boss/enemy spawners, UI canvas, Cinemachine follow, `itemChoiceMenuController`. Of these, **only `worldState` survives a scene load today** (it is a static plain object). Everything else is a scene MonoBehaviour that would be destroyed and re-instantiated in a second scene.

**Cost of a new Unity scene (rejected for now):**
- A persistence layer is required: `DontDestroyOnLoad` on the player root, `playerInventory`, `objectPool`, the `worldState`-holder, `itemChoiceMenuController`, level-up/pause UI — and an audit of **every** `instance = this` singleton to prevent duplicate-singleton collisions on load (each would overwrite the other's `instance`, and the old scene's copy would linger under DDOL).
- Pooled objects reference scene-bound managers; a surviving pool that outlives a scene would hold stale `pooledObject.source`/parenting. The pool would need explicit carry-over or flush.
- Cinemachine + Canvas + EventSystem re-wiring on every load.
- `worldState.player` is re-assigned in `gameController.Start()` each scene — fine, but it means the *player object itself* must be a DDOL survivor or be re-spawned and re-referenced.
- Net: a large, cross-cutting refactor touching ~15 files, high regression risk against a working prototype, for zero player-visible benefit right now.

**Cost of same-scene region (chosen):** low and local. Nothing unloads, so **stat carryover is automatic and free** and no `DontDestroyOnLoad`/persistence work is needed.

### Design — `nextAreaController` (new MonoBehaviour, single scene)

Region B is a spatially separate part of the existing ground Tilemap (or a second environment root `RegionB_Root` placed off to one side), walled off by a **door**: a GameObject on the `Walls` layer with a solid `Collider2D` that blocks passage.

Flow and events:
```
questManager.OnAllQuestItemsCollected   (already fired; today has no listener)
        │  nextAreaController subscribes
        ▼
OpenDoor():  disable door Collider2D + play door-open placeholder VFX/anim
        │        raise  event Action OnDoorOpened
        ▼
Player walks through → RegionB_EntryTrigger (trigger zone, tag test vs worldState.player)
        │  raises  event Action OnEnteredRegionB
        ▼
nextAreaController.EnterRegionB():
   - (optional) enable RegionB_Root environment
   - retarget enemySpawner/bossSpawner spawn bounds to region B (SerializeField Bounds or Transform[] anchors)
   - trigger the next-area boss via bossSpawner (dedicated SpawnEntry) OR a one-shot NextAreaBoss spawn
```

Components / objects to add:
- `nextAreaController` (MonoBehaviour) — subscribes to `OnAllQuestItemsCollected`; owns `OpenDoor()`, `EnterRegionB()`; exposes `OnDoorOpened`, `OnEnteredRegionB`.
- `Door` GameObject — `Walls`-layer `Collider2D` (disabled on open) + placeholder SpriteRenderer/particle.
- `RegionB_EntryTrigger` — trigger collider; player-only test (reuse the `other.transform == worldState.instance.player || root ==` idiom from `questItem`/`itemPickup`).
- Next-area boss = a new `bossSpawner` SpawnEntry (or existing `boss`/`diggy`/`ziggy` prefab) fired on `OnEnteredRegionB`.

### Migration note (future-proofing, not now)
Keep `nextAreaController` the **single choke point** for the transition. If a future milestone needs true separate scenes (save/load, memory streaming), only that controller changes: `EnterRegionB()` becomes `SceneManager.LoadScene(additive)` behind the *same* event, and the persistence layer is built then. Encapsulating the transition now makes that swap cheap.

### Area-architecture decision table
| Decision | Choice | Rationale | Alternative (rejected) |
|---|---|---|---|
| Next area container | Same-scene region unlock | Zero persistence work; stat/inventory carryover automatic; low regression risk | New Unity scene (needs full DDOL/persistence layer across ~15 singletons) |
| Gate mechanism | `questManager.OnAllQuestItemsCollected` → door collider disable | Hook already exists and fires; no new quest infra | Bespoke trigger counter |
| Region transition | Trigger zone → `nextAreaController.EnterRegionB()` | One choke point; swappable to additive scene later | Direct teleport with no controller (hard to evolve) |
| Stat carryover | Nothing (implicit) | `worldState` static survives; nothing unloads | Serialize/restore stats (unneeded) |

---

## 2. New items (all fold into the boss-drop / item pool)

### Registration pattern (applies to all four)
1. Add `ItemId` constants: `Aura = "AURA"`, `Robot = "ROBOT"`, `Trail = "TRAIL"`, `Grenade = "GRENADE"`; append to `ItemId.All`.
2. Add `DisplayName` cases in `itemChoiceMenuController` (e.g. "Damage Aura", "Attack Bot", "Searing Trail", "Grenadier"). No other change to the boss-drop/choice-menu flow — `Offer()` already draws from `ItemId.All` minus owned.
3. **These four are persistent auto-weapons** (unlike the projectile-hit-site items Fire/Explode/Freeze). They need an activation signal, because ownership is granted mid-run and their effect object must turn on exactly once:
   - Add to `playerInventory`: `public event System.Action<string> OnItemAdded;` invoked at the end of `Add(item)`.
   - Each auto-weapon component lives on the player (or a `PlayerWeapons` child), starts inert, and activates when its ID is added; it also checks `Has(id)` in `Awake`/`Start` for late-join/robustness. Keeps the pattern uniform and save-load-safe.
4. All damage they deal routes through the **shared crit-aware damage helper** from §3 (`worldState.RollDamage`), so crit works everywhere.

New `worldState` stat fields (base+mult + getters, mirroring existing style) are listed per item.

---

### (a) Passive damage AURA
**Component:** `auraWeapon` (MonoBehaviour on player).
**Behaviour:** constant radial DPS around the player. Base **1 dps**, **scales with attack speed** (higher `FireRate()` → more damage/sec). Placeholder **ring visual**.

- **Damage model (integer-safe):** dps and 1-per-second are small vs the x10 damage scale, so accumulate fractionally. Each tick:
  - `effectiveDps = auraDpsBase * (FireRate() / fireRateBaselineForAura)` (baseline = `worldState.fireRateBase` default 1.25 so a fresh player = ~1 dps).
  - Accumulate `_accum += effectiveDps * Time.deltaTime`; when `_accum >= 1`, deal `Mathf.FloorToInt(_accum)` and subtract.
- **Application:** on a fixed tick interval (`auraTickInterval`, e.g. 0.25s) `Physics2D.OverlapCircleAll(player.position, AuraRadius())`, for each `CompareTag("Enemy")` call `worldState.RollDamage(perTickShare)` → `enemyHealth.takeDamage`. Split the accumulated damage across enemies in range or apply full per-enemy (design pick: **full per-enemy per tick**, standard survivors aura).
- **worldState fields:** `auraDpsBase = 1f`, `auraDpsMult = 1f`, `auraRadiusBase = 1.5f`, `auraRadiusMult = 1f`; getters `AuraDps()`, `AuraRadius()`; `auraTickInterval = 0.25f`.
- **Ring visual:** child `AuraRing` GameObject — placeholder SpriteRenderer of a ring sprite (`Assets/Sprites/Square.png` tint as stopgap), `localScale = Vector3.one * AuraRadius() * 2`, semi-transparent, sorting above ground/below UI, updated when radius changes. (Particle shimmer option in §5.)
- **Register:** `ItemId.Aura`; `auraWeapon` enables `AuraRing` + starts ticking on `OnItemAdded`/`Has(Aura)`.

### (b) ATTACK ROBOT
**Component:** `attackRobot` (MonoBehaviour on a pooled/instantiated `attackRobot.prefab`); spawner logic on player (`robotWeapon` or handled by `auraWeapon`-style activator).
**Behaviour:** an autonomous ally that **chases the nearest enemy** and deals **contact damage = ½ player attack damage**, and **scales with all player stats**.

- **Targeting:** reuse the nearest-enemy scan from `playerProjectileShooter`/`chaserBehaviour` (`FindGameObjectsWithTag("Enemy")`, nearest by sqrDist). Move toward target each frame.
- **Stat scaling ("scales with all player stats"):**
  - contact damage = `Mathf.FloorToInt(robotDamageFactor * AttackDamage())`, `robotDamageFactor = 0.5f`;
  - chase speed derived from `MoveSpeed()`;
  - contact re-hit cadence from `FireCooldown()` (a per-enemy hit cooldown so `OnTriggerStay2D` doesn't melt bosses in one frame);
  - visual size optionally from `ProjectileSize()`.
- **Damage application:** `OnTriggerEnter2D`/`OnTriggerStay2D` vs `Enemy` → per-enemy cooldown gate → `worldState.RollDamage(dmg)` → `takeDamage`. Robot must NOT collide with player/walls physically (own layer or trigger-only).
- **worldState fields:** `robotDamageFactor = 0.5f`, `robotSpeedFactor = 1f` (× `MoveSpeed()`), `robotHitInterval` (fallback if not tying to FireCooldown).
- **Register:** `ItemId.Robot`; spawn exactly one robot on grant (`OnItemAdded`), parent to nothing (world-space), despawn/return only on game reset.
- **Placeholder sprite:** simple SpriteRenderer (reuse `Square.png` / a slime sprite tint).

### (c) Damaging TRAIL
**Components:** `trailWeapon` (on player) + `trailSegment` (pooled `trailSegment.prefab`).
**Behaviour:** the player leaves a **damaging trail** behind them; placeholder **particle** per segment.

- **Emission:** `trailWeapon` drops a pooled `trailSegment` at `player.position` every `trailEmitInterval` (e.g. 0.15s) or every `trailEmitDistance` moved (distance-based reads better and avoids stacking when idle — **recommend distance-based**).
- **Segment:** trigger `Collider2D`, lives `trailSegmentLifetime` seconds then `objectPool.ret`. While alive, damages overlapping enemies on a per-enemy tick (`trailTickInterval`) → `worldState.RollDamage(trailDps-share)` → `takeDamage`. Uses the same de-dup-by-enemy-id idea as `projectileBehaviour` to avoid multi-collider double hits.
- **worldState fields:** `trailDpsBase = ?`, `trailDpsMult`, `trailSegmentLifetime`, `trailEmitDistance`, `trailTickInterval`; getter `TrailDps()`. (Scale trail DPS with a chosen stat if desired — keep base flat for now.)
- **Pooled reset:** `trailSegment.OnEnable` resets life timer, tick accumulators, and its hit-enemy set (mirror `projectileBehaviour` discipline).
- **Register:** `ItemId.Trail`; `trailWeapon` starts emitting on `OnItemAdded`/`Has(Trail)`.
- **Placeholder particle:** each segment carries a short-lived colored `ParticleSystem` puff (§5).

### (d) GRENADE
**Components:** `grenadeWeapon` (on player) + `grenade` (pooled `grenade.prefab`).
**Behaviour:** throws a grenade **behind the player every 2s**; it **explodes reusing the explosion system**; placeholder **sprite + size-scaled particle**.

- **Throw:** every `grenadeInterval = 2f`, spawn `grenade` at player, launch **opposite the player's current move direction** (from `playerMovement` velocity; fallback to last-facing if idle). Simple ballistic feel via coroutine lerp of position to a landing offset (no DOTween).
- **Detonation:** after `grenadeFuse` seconds (or on landing), call the **shared explosion helper** (see refactor below) with `grenadeRadius` and `grenadeDamage`, then `objectPool.ret`.
- **Shared-explosion refactor (required, small):** extract the inline EXPLODE block from `projectileBehaviour.OnTriggerEnter2D` into a static helper:
  `static class explosionUtil { void Detonate(Vector2 pos, float radius, int damage) }` (or `worldState.Detonate(...)`). It does `OverlapCircleAll` → for each `Enemy` → `worldState.RollDamage(damage)` → `takeDamage`, and spawns the size-scaled explosion particle (§5). **Both** the EXPLODE item and the grenade call it — single source of truth, and EXPLODE behaviour is unchanged.
- **worldState fields:** `grenadeInterval = 2f`, `grenadeDamageBase`, `grenadeDamageMult`, `grenadeRadiusBase`, `grenadeRadiusMult`; getters `GrenadeDamage()`, `GrenadeRadius()`.
- **Register:** `ItemId.Grenade`; `grenadeWeapon` starts its throw loop (coroutine, with a matching stop path per project rules) on `OnItemAdded`/`Has(Grenade)`.
- **Placeholder:** grenade SpriteRenderer; explosion particle scaled to `GrenadeRadius()` (§5).

### Item summary table
| Item | ID | Component(s) | Prefab(s) | Damage source | Activation |
|---|---|---|---|---|---|
| Damage Aura | `AURA` | `auraWeapon` | AuraRing (child) | radial tick, scales w/ FireRate | `OnItemAdded` |
| Attack Bot | `ROBOT` | `attackRobot`, activator | `attackRobot.prefab` | contact, ½ AttackDamage, scales all stats | `OnItemAdded` |
| Searing Trail | `TRAIL` | `trailWeapon`, `trailSegment` | `trailSegment.prefab` | per-segment tick | `OnItemAdded` |
| Grenadier | `GRENADE` | `grenadeWeapon`, `grenade` | `grenade.prefab` | shared `explosionUtil.Detonate` | `OnItemAdded` |

---

## 3. Crit hits (crit chance + crit damage)

### Stats (base+mult, in `worldState`, mirroring existing style)
```
float critChanceBase = 0f;      float critChanceMult = 1f;   // effective clamped 0..1
float critDamageBase = 2.0f;    float critDamageMult = 1f;   // ×damage on crit (2.0 = double)

float CritChance()     => Mathf.Clamp01(critChanceBase * critChanceMult);
float CritMultiplier() => critDamageBase * critDamageMult;
```
Step fields for upgrades: `critChanceFlatStep = 0.05f`, `critDamageFlatStep = 0.25f`, percent uses the existing shared `levelUpPercentStep`.

### Single integration point — `worldState.RollDamage`
Add ONE helper that every damage source calls, so crit is uniform and future sources inherit it:
```
int RollDamage(float baseDamage, out bool isCrit)
{
    isCrit = Random.value < CritChance();
    float d = isCrit ? baseDamage * CritMultiplier() : baseDamage;
    return Mathf.RoundToInt(d);
}
```
**Call sites converted** (each currently computes an int then calls `takeDamage`):
- `projectileBehaviour` direct hit (replaces the `Mathf.RoundToInt(AttackDamage())` line).
- `projectileBehaviour` EXPLODE splash → now inside `explosionUtil.Detonate`.
- New: aura, robot, trail, grenade (all route through it).
- **Fire DoT / freeze:** leave DoT non-crit for now (design pick — ticking DoT crits feel noisy); document as a knob.

### Crit feedback (damage numbers)
Extend `damageNumber.Set(int amount)` → `Set(int amount, bool isCrit)`; crit renders larger / different color (e.g. yellow, bigger scale). `enemyHealth.takeDamage` currently spawns the number — either pass `isCrit` down through `takeDamage(int amount, bool isCrit = false)` (backward compatible default) or spawn the crit-styled number at the call site. **Recommend** overloading `takeDamage` with an optional `isCrit` so all sources light up crits consistently.

### Level-up pool integration (`levelUpMenuController`)
- Add `StatKind.CritChance`, `StatKind.CritDamage` to the enum and the `stats[]` array in `BuildPool`.
- **CritChance:** offer **Flat** always (`+5% crit chance`); offer **Percent** only once `critChanceBase > 0` (mirror the Defense/Regen "inert on 0 base" gate). Consider a soft cap: stop offering once `CritChance()` ≈ 1.
- **CritDamage:** offer Flat (`+0.25 crit damage`) and Percent (`+10% crit damage`).
- Add `LabelFor` cases (format crit chance as a %: `Mathf.RoundToInt(critChanceFlatStep*100)`), and `Choose` cases mutating `critChanceBase/Mult`, `critDamageBase/Mult`.
- Pause-stats view (`pauseStatsView`) — add crit chance/damage rows for visibility (optional polish).

---

## 4. Floor-tile puzzle + fetch quest

Two independent gates. The **fetch quest** gates the **next area** (§1). The **floor-tile puzzle** gates a **bonus upgrade powerup**.

### 4A. Floor-tile puzzle → gates an upgrade powerup
**Components:** `floorTile` (per tile) + `floorPuzzleController` (one per puzzle).

- **`floorTile`:** a trigger tile the player steps on.
  - `OnTriggerEnter2D` (player test) → `Activate()`: set `activated = true`, swap placeholder visual (SpriteRenderer color/sprite: dim → lit), raise `event Action<floorTile> OnActivated` (or notify controller directly with its index).
  - `Deactivate()` reverts visual (used on sequence reset).
- **`floorPuzzleController`:** holds `floorTile[] tiles` and a solve rule. **Recommended rule: ordered sequence (Simon-style)** — step tiles in the correct order; a wrong step resets all tiles. (Simpler alt: "activate all N tiles simultaneously/within a window.")
  - Tracks `_progressIndex`; on each tile activation checks against `correctOrder[_progressIndex]`.
    - correct → advance; if `_progressIndex == tiles.Length` → **solved**.
    - wrong → `ResetPuzzle()` (deactivate all, `_progressIndex = 0`), optional feedback flash.
  - **Events:** `event Action<int> OnTileActivated(index)`, `event Action OnPuzzleReset`, `event Action OnPuzzleSolved`.
- **Reward on solve:** `OnPuzzleSolved` → enable a `powerupPickup` (a special drop). Options:
  1. Reuse the boss-drop path — spawn an `itemPickup` (guaranteed item choice), or
  2. A dedicated `powerupPickup` that on collection opens the **level-up menu** (guaranteed upgrade pick) or applies a fixed stat boost.
  - **Recommend** option 2 with the level-up menu for consistency with existing UX. Powerup prefab starts inactive; solve enables it (or spawns it at a fixed anchor).
- **Tile activation state:** each `floorTile` owns its `activated` bool + visual; the controller owns solve state. No `worldState` fields required (self-contained), though tunables (reset-on-wrong, window seconds) can be `[SerializeField]`.

### 4B. Fetch quest / puzzle → gates the next area (uses existing `questItem` + `questManager`)
- **Reuse as-is:** `questManager` already spawns `questCount` `questItem` prefabs on valid ground tiles and fires `OnAllQuestItemsCollected`. `questIndicator` already shows off-screen arrows. **No new quest infra needed.**
- **New subscriber = `nextAreaController` (§1):** `OnAllQuestItemsCollected` → `OpenDoor()`. This is the missing link — the event fires today with no listener.
- **Solve-state events (fetch quest):**
  - existing: `questManager.OnAllQuestItemsCollected` (all `questCount` collected).
  - add (nextAreaController): `OnDoorOpened`, `OnEnteredRegionB` (§1).
- **Optional:** make region B's boss require the fetch quest done first (door blocks it), so the ordering is: collect quest items → door opens → enter region B → next-area boss. The floor-tile puzzle (4A) is an *optional side reward* placed in region A or B, independent of the door.

### Puzzle/quest event map
```
[Floor puzzle]  floorTile.OnActivated ─▶ floorPuzzleController
                     (wrong)─▶ OnPuzzleReset ─▶ deactivate tiles
                     (complete)─▶ OnPuzzleSolved ─▶ enable powerupPickup ─▶ (collect) level-up menu

[Fetch quest]   questItem.OnTrigger ─▶ questManager.Collect ─▶ OnAllQuestItemsCollected
                     ─▶ nextAreaController.OpenDoor ─▶ OnDoorOpened
                     ─▶ RegionB_EntryTrigger ─▶ OnEnteredRegionB ─▶ spawn next-area boss
```

---

## 5. Particle approach (placeholders, Unity built-in `ParticleSystem` only — no new packages)

**General rules**
- Built-in `ParticleSystem` only. URP 2D → use a Sprite-Unlit / default-particle material; reuse the existing `Assets/Materials/ParticleColored.mat` as the placeholder material for all of these.
- **Pool them** through `objectPool`. Add a tiny `pooledParticleBurst` helper: `OnEnable` → `ParticleSystem.Clear()` + `Play()`; a small monitor returns the object to the pool when `!ps.IsAlive(true)` (or after `main.duration + startLifetime`). This keeps one-shot bursts allocation-free and matches the project's pooled-reset discipline.
- Sorting: render order above ground, below UI (dedicated sorting layer / order-in-layer). WorldSpace simulation for detached bursts; Local for child systems that must follow an enemy.

| Effect | Prefab / owner | Type | Placeholder recipe | Scaling |
|---|---|---|---|---|
| **Explosion (size-scaled)** | `explosionBurst.prefab`, spawned by `explosionUtil.Detonate` | One-shot burst | Radial burst of N sprites, short `startLifetime`, orange→smoke color-over-lifetime, `emitFromShell` circle shape | Set `transform.localScale = Vector3.one * radius` (or `shape.radius = radius` + `main.startSizeMultiplier ∝ radius`) so a big EXPLODE/grenade visibly bigger |
| **Burning enemy** | child `BurnFX` `ParticleSystem` on each enemy prefab | Looping | Small upward flame stream, orange, low emission | `enemyHealth` toggles emission on while `_burnStacks > 0`; emission rate ∝ `_burnStacks`; cleared in `OnEnable` (pool reset) |
| **Frozen enemy** | child `FrostFX` + sprite tint | Looping + one-shot | Light-blue sparkle loop while `IsFrozen`; a one-shot frost burst on `ApplyFreeze`; tint SpriteRenderer pale blue while frozen | Toggle with `_freezeTimeRemaining > 0`; reset in `OnEnable` |
| **Damaging trail** | `ParticleSystem` on `trailSegment.prefab` | Short one-shot per segment | Small colored puff (red/orange) fading over `trailSegmentLifetime` | Lifetime matches segment lifetime; returns with the pooled segment |
| **Aura ring** | `AuraRing` child of player | Ring — sprite (primary) + optional shimmer | Semi-transparent ring **SpriteRenderer** scaled to `2 × AuraRadius()` (clearest placeholder); optional `ParticleSystem` with `Circle` shape, `emitFromShell`, low emission for a shimmering edge | Ring scale updates when `AuraRadius()` changes; shimmer `shape.radius = AuraRadius()` |

**Why sprite (not particles) for the aura ring:** a scaled ring sprite reads unambiguously as a radius indicator and is the cheapest honest placeholder; the particle shimmer is a nice-to-have layered on top. Everything else (explosion, burn, freeze, trail) is genuinely particulate and uses `ParticleSystem`.

**Coordination with shader-artist / scene-architect:** particle prefabs and their material assignment/preview run through the Editor (Coplay MCP) path per CLAUDE.md; the C# toggling/scaling logic (enemyHealth burn/freeze toggles, `explosionUtil` spawn, pooled-return helper) runs through `csharp-dev` in Docker.

---

## 6. Cross-cutting new/changed surface (for scrum-master decomposition)

**worldState (new fields+getters):** crit (chance/damage base+mult+steps, `RollDamage`), aura (dps/radius/tick), robot (damageFactor/speedFactor/hitInterval), trail (dps/lifetime/emitDistance/tick), grenade (interval/damage/radius). Plus `explosionUtil.Detonate` (or `worldState.Detonate`) extracted from `projectileBehaviour`.

**playerInventory:** add `event Action<string> OnItemAdded` (fired in `Add`).

**itemManager / itemChoiceMenuController:** extend `ItemId.All` + `DisplayName` with AURA/ROBOT/TRAIL/GRENADE. (Boss-drop flow otherwise unchanged.)

**levelUpMenuController:** add CritChance/CritDamage to `StatKind`, `BuildPool` (with 0-base percent gating), `LabelFor`, `Choose`.

**enemyHealth:** optional `takeDamage(int, bool isCrit)` overload; BurnFX/FrostFX child toggles.

**damageNumber:** `Set(int, bool isCrit)` styling.

**New MonoBehaviours:** `nextAreaController`, `auraWeapon`, `attackRobot`, `trailWeapon`, `trailSegment`, `grenadeWeapon`, `grenade`, `floorTile`, `floorPuzzleController`, `powerupPickup`, `pooledParticleBurst`, `explosionUtil` (static).

**New prefabs:** `attackRobot`, `trailSegment`, `grenade`, `explosionBurst`, `AuraRing` (child), `Door`, `RegionB_EntryTrigger`, floor tiles, `powerupPickup`, particle prefabs.

**Editor-side (scene-architect / shader-artist via Agent tool):** door + region-B objects, floor-tile placement, particle prefab authoring + material assignment, inspector wiring of all `[SerializeField]` refs, prefab creation.

---

## 7. Open questions (need human input before implementation)

1. **Next-area layout:** is region B a far corner of the *existing* ground Tilemap, or a separate `RegionB_Root` toggled on entry? (Affects spawner-bounds retargeting vs. simple teleport.)
2. **Powerup reward (4A):** guaranteed level-up menu pick, a fixed stat boost, or a guaranteed *item* drop? 
3. **Floor-puzzle rule:** ordered sequence (Simon) vs. "activate all N"? Reset-on-wrong or forgiving?
4. **Crit tuning:** starting `critChanceBase` (0 vs small nonzero), `critDamageBase` (2.0?), and should Fire DoT be allowed to crit?
5. **Aura scaling baseline:** confirm "scales with attack speed" means `FireRate()/fireRateBase` (fresh player ≈ 1 dps) — or a different curve?
6. **Robot "scales with all stats":** confirm which stats map (damage←AttackDamage, speed←MoveSpeed, cadence←FireRate, size←ProjectileSize) and whether it can crit.
7. **Grenade throw feel:** fixed fuse timer vs. explode-on-landing; throw direction from live move vector vs. last-facing when idle.
8. **Next-area boss:** reuse an existing boss prefab (`boss`/`diggy`/`ziggy`) or a new one?

---

## 8. Suggested milestone ordering (milestone-level, for scrum-master to decompose)

1. **Foundation:** `worldState.RollDamage` + crit stats + `explosionUtil.Detonate` extraction + `playerInventory.OnItemAdded`. (Unblocks everything; behaviour-neutral until wired.)
2. **Crit end-to-end:** convert all damage call sites, crit level-up upgrades, crit damage-number styling.
3. **New items:** aura → robot → trail → grenade (each: worldState fields + component + prefab + registration + placeholder particle).
4. **Puzzle + quest gating:** `floorTile`/`floorPuzzleController` + powerup; `nextAreaController` subscribing to `OnAllQuestItemsCollected` + door + region-B trigger + next-area boss.
5. **Polish:** particle tuning (burn/freeze/aura ring/explosion scaling), pause-stats crit rows, balance pass.

---
*End of design. This document is a blueprint; `/scrum-master` should decompose §6 and §8 into agent-sized tasks.*
