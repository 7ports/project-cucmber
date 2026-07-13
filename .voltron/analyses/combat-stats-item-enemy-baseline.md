# Combat / Stats / Item / Enemy Baseline — project-cucumber

**Date:** 2026-07-12 · **Analyst:** code-analyst · **Purpose:** edit anchors for: crit stats, stats-page base/upgrade split, 4 new items, time-based spawn scaling, boss elapsed-time scaling, sliding fix, explosion/burn/freeze FX scaling, kill/damage/DPS counters.

All paths relative to repo root. All scripts are in the **global namespace** (no `namespace` blocks). Line numbers verified against working tree on 2026-07-12 (uncommitted pierce-fix batch included).

---

## 1. Damage math (crit-multiplier entry points)

**Player → enemy (projectile hit)** — `Assets/Scripts/projectileBehaviour.cs`
- `OnTriggerEnter2D(Collider2D)` at **line 59**; enemy branch begins line 67.
- **Primary damage computation: lines 82–83** — `int dmg = Mathf.RoundToInt(worldState.instance.AttackDamage()); eh.takeDamage(dmg);`. **This is THE crit insertion point** for direct hits (roll crit between line 82 and 83, or wrap the value).
- **Explosion splash: lines 103–104** — `splash = Mathf.FloorToInt(AttackDamage() / 3f)`, applied via `splashEh.takeDamage(splash)` at line 115. Decide whether crit propagates to splash here.
- Per-enemy hit de-dup: `hitEnemyIds` HashSet (line 13, checked line 77) — one damage event per distinct enemy per shot, so a per-hit crit roll here fires exactly once per enemy.

**Fire DoT** — `Assets/Scripts/enemyHealth.cs` **line 65**: `takeDamage(dpsPerStack * _burnStacks)` inside `Update()` burn loop (lines 58–73). Separate damage channel; crit typically excluded, but this is the anchor if included.

**Damage sink (all sources)** — `Assets/Scripts/enemyHealth.cs` `public void takeDamage(int amount)` at **line 104**. Central chokepoint: every point of damage an enemy receives flows through here (projectile, splash, burn). Damage-number spawn is lines 110–115 (`damageNumber.Set(amount)` — a crit-styled number variant would hook at line 114). Death check line 116 → `die()`.
- Note: `takeDamage(int)` has no source/crit parameter today; a "show crits differently" feature needs either an overload or a flag added here.

**Enemy → player (contact damage)** — `Assets/Scripts/playerHealth.cs`
- Entry on first touch: `OnTriggerEnter2D` line 17 → `ApplyDamage(eh.EnemyDamage)` line 26.
- Repeating tick: `Update()` line 48 → `ApplyContactDamage()` (lines 82–92) every `damageInterval` (2 s, line 6).
- Defense reduction: `Reduce(int raw)` at **line 63** — `max(1, raw − Defense())`. All player-incoming damage funnels through `ApplyDamage` (lines 67–80).

**Enemy projectile → player** — `Assets/Scripts/enemyProjectile.cs` `OnTriggerEnter2D` lines 44–53; damage field `[SerializeField] int damage = 80` (line 5); applied via `ph.TakeHit(damage)` line 50 (`playerHealth.TakeHit` line 65).

---

## 2. Stat system (base + mult; where crit-chance / crit-damage stats go)

**Definition:** `Assets/Scripts/worldState.cs` — plain C# class (NOT MonoBehaviour), singleton `worldState.instance` created by `Assets/Scripts/gameController.cs:9–13` in `Start()`.

- **Stat pairs (base + mult), lines 9–31:** `attackDamageBase/Mult` (9–10), `moveSpeed` (11–12), `fireRate` (13–14), `range` (15–16), `maxHP` (17–18), `defense` (19–20), `regen` (21–22), `pickupRadius` (23–24), `projectileSize` (28–29), flat-only `pierceBase` (31).
- **Effective-value getters, lines 73–84:** `AttackDamage() => attackDamageBase * attackDamageMult` etc. **New crit stats follow this exact pattern**: add `critChanceBase/Mult`, `critDamageBase/Mult` fields near line 31 and getters near line 84.
- **Base-vs-upgrade split for a stats page:** the *initial* base values are the field initializers (e.g. `attackDamageBase = 100f` line 9). Flat upgrades **mutate `*Base` in place** (see below), so the original defaults are NOT retained anywhere at runtime — a base-vs-upgrade display needs the initial values captured (e.g. constants or a snapshot at startup). `*Mult` starts at 1 and holds only percent-upgrade contribution, so `mult` cleanly equals "percent upgrades"; `base − initialDefault` equals "flat upgrades".
- **Upgrade step magnitudes, lines 57–71:** `attackDamageFlatStep = 50f` (57) … `projectileSizeFlatStep` (65), `pierceFlatStep` (66), `xpBonusStep`/`xpBonusCap` (67–68), shared `levelUpPercentStep = 0.1f` (71). New crit upgrades need step fields here.

**Where upgrades mutate stats:** `Assets/Scripts/levelUpMenuController.cs`
- `StatKind` enum **line 7** — add `CritChance`, `CritDamage` here.
- Offer pool: `BuildPool()` lines 50–92 (`stats[]` array lines 52–65; flat-only / percent-gating rules lines 74–88).
- Labels: `LabelFor()` lines 94–135 (flat switch 103–117, percent switch 122–134).
- Mutations: `Choose(Upgrade)` lines 137–228 — **flat upgrades do `ws.<stat>Base += ws.<stat>FlatStep`** (e.g. line 147), **percent upgrades do `ws.<stat>Mult *= (1 + levelUpPercentStep)`** (e.g. line 191).

**Current stat consumers** (pattern to copy for crit): `projectileBehaviour.cs:82` (AttackDamage), `:44` (Range), `:35` (ProjectileSize), `:120` (Pierce); `playerProjectileShooter.cs:13` (FireCooldown); `playerMovement.cs:47` (MoveSpeed); `playerHealth.cs:53/58/63` (Regen/MaxHP/Defense); `playerPickupRadius.cs` (PickupRadius).

---

## 3. Boss-drop / item pool (registering 4 new items)

**Item registry:** `Assets/Scripts/itemManager.cs`
- `ItemId` static class **lines 4–14**: 5 string constants (`Cone`, `Bounce`, `Fire`, `Explode`, `Freeze`) + **`ItemId.All` array line 13 — THE grantable pool**. **Register new items here** (new constant + append to `All`).
- `itemManager.GrantRandomItem()` lines 23–46 — legacy random-grant path (superseded by the choice menu but still present; also filters by `All`).

**Choice menu (active grant path):** `Assets/Scripts/itemChoiceMenuController.cs`
- `DisplayName(string id)` **lines 30–41** — switch mapping ItemId → button label. **New items need a case here.**
- `Offer()` lines 48–85 — builds not-yet-owned candidates from `ItemId.All` (lines 55–57), shuffles, shows up to 3, pauses via `Time.timeScale = 0` (line 83). Grant on click: `Choose()` line 87–97 → `playerInventory.Add`.

**Ownership:** `Assets/Scripts/playerInventory.cs` — `List<string> items` (line 7), `Has(itemId)` **line 24** is THE ownership query all weapon code calls.

**Drop chain:** boss dies → `enemyHealth.die()` spawns `deathDropPrefab` (`Assets/Scripts/enemyHealth.cs:144–145`; field line 15, boss-only) → prefab is `Assets/Prefabs/pickups/itemPickup.prefab` (wired as prefab-instance override in `Assets/Prefabs/enemies/bosses/boss.prefab:314–316`, guid `89c7dea22fb13174793c37dea5d776e5`; same pattern in diggy/ziggy) → `Assets/Scripts/itemPickup.cs:11–12` calls `itemChoiceMenuController.instance.Offer()` on player touch.

**Effect hook sites for new items** (where existing 5 plug in):
- On-fire (spawn-time, Cone): `playerProjectileShooter.cs:34–45`.
- On-hit (Fire/Freeze status): `projectileBehaviour.cs:87–98`; (Explode AoE): `:101–118`; (Bounce): `:122–147` + `TryBounce()` :153–180.
- Item tunables live as plain fields in `worldState.cs:40–46` (`coneHalfAngleDeg`, `bounceSearchRadius`, `explosionRadiusFactor`, `freezeChance`, `freezeItemDuration`) — new item tunables go alongside.

---

## 4. Enemy spawn system (volume scaling; time-based driver hook)

**Spawner:** `Assets/Scripts/enemySpawner.cs` (single `Update()` loop, lines 21–81).
- **Current volume driver is LEVEL-based, not time-based:** the interval consumed each spawn is `worldState.instance.currentSpawnInterval` (**enemySpawner.cs:30**), which is decremented ONLY in `worldState.addXP()` on each level-up — `Assets/Scripts/worldState.cs:146`: `currentSpawnInterval = max(minSpawnInterval, currentSpawnInterval − spawnIntervalCoefficient * (1/level))`. Tunables: `baseSpawnInterval = 1.75`, `spawnIntervalCoefficient = 0.3`, `minSpawnInterval = 0.3` (worldState.cs:91–94).
- **Time-based driver hook:** either (a) replace/augment the read at enemySpawner.cs:30 with a function of `Time.timeSinceLevelLoad` (a `worldState.SpawnIntervalTimeMultiplier()`-style getter next to `EnemyHpTimeMultiplier()` at worldState.cs:113–120 matches the established pattern), or (b) hook where the mutation happens at worldState.cs:146. All existing time gates use `Time.timeSinceLevelLoad` (enemySpawner.cs:34, bossSpawner.cs:19, worldState.cs:116/134) — keep that source.
- Type progression is already time-based: `SpawnEntry {prefab, minTimeSeconds, weight}` (lines 5–13, inspector-configured), eligibility + linear ramp lines 43–56 using `worldState.unlockRampSeconds` (worldState.cs:98).
- Spawn position: camera-edge viewport math lines 69–77.

**Boss cadence:** `Assets/Scripts/bossSpawner.cs` — first boss at `worldState.bossFirstTime` (200 s), then every `bossInterval` (200 s) (worldState.cs:101–102; consumed bossSpawner.cs:18–24). One-boss-at-a-time guard line 34. Random pick from inspector array `bossPrefabs` (line 8, pick line 44). Bosses are `Instantiate`d (line 57), NOT pooled. Pattern randomize on spawn line 58–59.

**Boss difficulty definition:** entirely in the three prefabs via serialized values (see §5) plus the global spawn-time HP multiplier `worldState.EnemyHpTimeMultiplier()` (worldState.cs:113–120) applied in `enemyHealth.OnEnable()` (enemyHealth.cs:40–42; bosses included since `OnEnable` runs on Instantiate).

---

## 5. Boss projectile / fire-rate / speed / HP config (elapsed-time scaling anchors)

**Code:** `Assets/Scripts/bossShooter.cs`
- Serialized tuning: `fireInterval` (**line 12**), `bulletSpeed` (15), `bulletLifetime` (16), `bulletsPerVolley` (19), `spreadAngle` (20), `spinStep` (21).
- **Fire cadence check: line 41** (`if (fireTimer >= fireInterval)`) — an elapsed-time fire-rate multiplier hooks here (divide the compared interval) without touching the serialized value.
- **Bullet launch: `EmitBullet()` lines 93–99** — speed passed to `p.Launch(dir, bulletSpeed, bulletLifetime)` at line 98; an elapsed-time speed multiplier wraps `bulletSpeed` here. Volley-size scaling would wrap `bulletsPerVolley` reads at lines 64/71/79/89.
- Boss movement speed: `Assets/Scripts/bossBehaviour.cs` `chaseSpeed` (line 5), consumed in `FixedUpdate` line 29.
- Boss HP: base `maxHp` is a prefab override (2500 for all three bosses) already time-scaled at spawn by `EnemyHpTimeMultiplier()` (enemyHealth.cs:40–42). Additional boss-only scaling would multiply there (gate on `_isBoss`, enemyHealth.cs:35) or in a new hook in `bossSpawner.SpawnBoss()` after line 57.
- Established pattern for new time curves: add a getter beside `EnemyHpTimeMultiplier()` / `XpTimeMultiplier()` in `worldState.cs:113–135`, driven by `Time.timeSinceLevelLoad`.

**Prefab values** (prefab-instance overrides at `boss/diggy/ziggy.prefab:302–307`; MonoBehaviour blocks at :358–380):

| Prefab | maxHp | enemyDamage | chaseSpeed | leash | fireInterval | bulletSpeed | perVolley | spread |
|---|---|---|---|---|---|---|---|---|
| `bosses/boss.prefab` | 2500 | 150 | 0.7 | 14 | 3 | 2 | 10 | 60 |
| `bosses/diggy.prefab` | 2500 | 150 | 0.7 | 14 | 4 | 2 | 6 | 60 |
| `bosses/ziggy.prefab` | 2500 | 150 | 0.7 | 14 | 4 | 2 | 5 | 75 |

Boss XP: `xpDropCount` override = 20 (boss.prefab:310–311).

---

## 6. Enemy AI, leash/catch-up, wall/stuck handling

All movers were recently converted to `Rigidbody2D.MovePosition` in `FixedUpdate` (physics-respecting kinematic-style movement):

- **chaser** — `Assets/Scripts/chaserBehaviour.cs`: straight `MoveTowards` chase, lines 15–23; freeze gate line 18.
- **slime** — `Assets/Scripts/slimeBehaviour.cs`: hop coroutine `jump()` lines 31–50, `_rb.MovePosition(Lerp(...))` per fixed step lines 41–47. **No freeze gate** (unlike the other three movers) and no leash. Coroutine has no explicit stop path (`isHopping` resets at line 49).
- **shooter** — `Assets/Scripts/shooterBehaviour.cs`: Chase/Aim/Fire/Cooldown state machine lines 38–75; freeze gate line 41; aim locked at telegraph start line 51.
- **boss (leash/catch-up)** — `Assets/Scripts/bossBehaviour.cs`: **the only leash logic in the project.** Lines 23–27: if boss is > `leashDistance` (14) from player it teleports to a just-offscreen point via `ComputeOffscreenPoint()` (lines 33–48), else chases (lines 29–30). Freeze gate line 20.
- **player** — `Assets/Scripts/playerMovement.cs`: `FixedUpdate` `MovePosition` lines 42–51 (speed from `worldState.MoveSpeed()`).

**Wall collision / stuck handling:** there is NO dedicated stuck-recovery code. Wall interaction lives in:
- `projectileBehaviour.cs:7` (`wallLayer` mask) + `:61–65` — player bullets despawn on wall hit.
- `questManager.cs:10/47` — `wallsMask` used only to avoid spawning quest items inside walls.
- Physical wall blocking is emergent: enemies/player are Dynamic Rigidbody2Ds moved with `MovePosition`, blocked by Walls-layer colliders per the Physics2D matrix (see §7). Any anti-stuck/steering logic would be new code in the movers' `FixedUpdate` methods above.

---

## 7. Physics: layers, Rigidbody2D, materials (sliding fix)

**Layers** (`ProjectSettings/TagManager.asset:10–21`): 6 = `Pickups`, 7 = `Player`, 8 = `Walls`, 9 = `Enemy`, 10 = `EnemyBody`, 11 = `PlayerProjectile`. Layers 10/11 are from the uncommitted pierce-fix batch (journal `.voltron/journal/2026-07-10.md`). Tags: `Enemy`, `playerBullet`.

**Collision matrix:** `ProjectSettings/Physics2DSettings.asset:56` (`m_LayerCollisionMatrix`, uncommitted edit). Per journal 2026-07-10: EnemyBody no longer collides with Player or PlayerProjectile; contact-damage trigger (Enemy layer) and wall collisions preserved.

**Enemy prefab collider layout** (slime.prefab is the base; bosses/variants inherit):
- Root object: layer 9 `Enemy` (slime.prefab:19), **trigger** collider (`m_IsTrigger: 1`, :167) — the contact-damage/projectile-hit trigger; carries all MonoBehaviours.
- Child "EnemyBody": layer 10 (slime.prefab:250), **solid** collider (`m_IsTrigger: 0`, :302) — collides with Walls only.
- **Rigidbody2D on root (slime.prefab:199–224):** `m_BodyType: 0` (Dynamic), mass 1, **`m_LinearDamping: 0`**, gravityScale 0, **`m_Material: {fileID: 0}` (no physics material)**, `m_Interpolate: 0`, `m_Constraints: 4` (freeze rotation Z), discrete collision.
- **No `PhysicsMaterial2D` assets exist anywhere in Assets/** (verified via find). A zero-friction/zero-bounciness material for a sliding fix would be a new asset, assigned either on the Rigidbody2D `m_Material` (slime.prefab:214) or on the EnemyBody/Walls colliders' `m_Material` fields (e.g. slime.prefab:282).
- Sliding cause context: Dynamic bodies with zero damping retain collision-imparted velocity between `MovePosition` calls; anchors for a fix are the Rigidbody2D blocks above and/or the movers' `FixedUpdate`s (§6).

---

## 8. Explosion / fire / freeze effect systems + particle usage

**Current status: the three item effects are purely mechanical — none has any VFX today.**

- **Explode:** `projectileBehaviour.cs:101–118` — `Physics2D.OverlapCircleAll` at line 110, radius = `Range() * explosionRadiusFactor` (lines 107–109; factor defined worldState.cs:44). **No particle/visual is spawned** — an explosion VFX + radius scaling both anchor at lines 109–110.
- **Fire (burn):** applied `projectileBehaviour.cs:89–90` → state machine in `enemyHealth.cs` — `ApplyFire()` lines 84–89, DoT tick loop lines 57–73, stack fields lines 21–24, tunables worldState.cs:34–37. **No burning visual** — a burn-FX hook belongs in `ApplyFire()`/the tick loop, with cleanup at the `OnEnable` reset (enemyHealth.cs:44–48) and burn-expiry (lines 68–72).
- **Freeze:** applied `projectileBehaviour.cs:92–97` → `ApplyFreeze()` enemyHealth.cs:96–102; `IsFrozen` property line 26 read by chaser/shooter/boss (NOT slime, see §6). **No frozen visual** — hook at `ApplyFreeze()` and the countdown at lines 76–77.

**Existing particle infrastructure (patterns to copy):**
- `Assets/Scripts/bloodBurst.cs` — the pooled one-shot ParticleSystem pattern: `OnEnable` Clear+Play (lines 8–17), timed return to pool (lines 19–27). Used by `Assets/Prefabs/blood.prefab`; spawned from `enemyHealth.takeDamage` (boss-only, :107–108) and `die()` (:141–142) via `objectPool.instance.get`.
- `Assets/Scripts/levelUpManager.cs:50–53` — scene-resident `ParticleSystem.Play()` pattern (`playerLevelUpParticles`, field line 10).
- Only these two scripts touch `ParticleSystem`. Particle prefabs: `blood.prefab`, `levelUpBurst.prefab`, `uiLevelUpBurst.prefab`; material `Assets/Materials/ParticleColored.mat`.
- Pooling: `Assets/Scripts/objectPool.cs` (`objectPool.instance.get/ret`) — all transient FX must go through it.

---

## 9. Stats-page / pause-menu UI (kill / total-damage / avg-DPS counters)

**Pause menu:** `Assets/Scripts/pauseMenuController.cs` — Escape toggles `pauseRoot` (lines 11–18, 32–44); guards against level-up/game-over overlap (lines 20–24). *(Note: uses legacy `Input.GetKeyDown`, line 13, despite the New-Input-System rule — pre-existing.)*

**Stats page:** `Assets/Scripts/pauseStatsView.cs` — the whole display is a single string built in `OnEnable()` (**lines 8–24**, panel-activation refresh). Currently shows: Level, HP/MaxHP, Damage, Move Speed, Fire Rate, Range, Pickup Radius — **effective values only; no base-vs-upgrade split** (see §2 for why initial bases must be snapshotted). New rows (crit stats, kills, total damage, avg DPS) append into the string at lines 14–20.

**Items page:** `Assets/Scripts/pauseItemsView.cs` lines 8–23 — joins raw `playerInventory.items` ids (shows "CONE" etc., not display names).

**Where the counters' data sources live (none exist yet — no kill/damage counters anywhere):**
- **Enemies killed:** increment in `enemyHealth.die()` — `Assets/Scripts/enemyHealth.cs:132` (single death path for all enemies/bosses).
- **Total damage dealt:** accumulate in `enemyHealth.takeDamage()` — enemyHealth.cs:104 (captures projectile + splash + burn), or player-attributed-only at the dealing sites (projectileBehaviour.cs:83/115, enemyHealth.cs:65).
- **Avg DPS:** total damage ÷ `Time.timeSinceLevelLoad` (the established run-clock; formatted example in `Assets/Scripts/timerUI.cs:16`).
- **Storage:** run-scoped fields on `worldState` (worldState.cs:86–94 region holds analogous run state like `level`, `currentHP`); `worldState.instance` is re-created each play session by gameController.cs:9–13, so counters reset naturally.

---

## Gaps / caveats

- Physics2D collision-matrix hex (Physics2DSettings.asset:56) not bit-decoded; layer-pair semantics taken from journal 2026-07-10 (uncommitted batch). Verify in Editor before relying on exact pairs.
- Boss prefab MonoBehaviour line anchors (358–380) are YAML positions in the uncommitted working-tree prefabs; they shift if the prefabs are re-serialized.
- `worldState` is not a MonoBehaviour — it cannot own coroutines/Update; time-based getters must stay pull-based (current pattern) or be driven by a MonoBehaviour.
- Stringer baseline absent (`.voltron/stringer/` empty) — delta check skipped.
- Voltron MCP tools (`submit_analysis`, `append_journal`) unavailable in this harness; report and journal written directly.
