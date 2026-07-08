# Wall Collision Design — Player & Enemies vs the `Walls` Layer

**Date:** 2026-07-08
**Author:** project-planner (DESIGN ONLY — no code written)
**Task:** "fix the detection for the player and enemies with walls, right now they both walk straight through them but I would like to be able to use the walls layer to make obstacles in the level."

---

## TL;DR — Why walls do nothing today

There are **three independent reasons** movement ignores walls. All three must be fixed:

1. **Every mover writes `transform.position` / `transform.Translate` directly.** This teleports the object each frame. Unity's 2D physics engine does **not** sweep or resolve teleports, so the body passes straight through any collider. This is the root cause on the code side.
2. **Enemies use a *Kinematic* Rigidbody2D and a *trigger* collider.** A Kinematic body is *never* blocked by collisions, and a trigger collider never produces solid blocking. Even after switching to `MovePosition`, enemies would still phase through walls until the body is made **Dynamic** and given a **non-trigger** collider.
3. **The `walls` Tilemap in the scene has no collider at all.** There is nothing physical to hit. (It has a `Tilemap` + `TilemapRenderer` only — no `TilemapCollider2D`.)

The **player** is already physics-ready (Dynamic Rigidbody2D, non-trigger BoxCollider2D, gravity 0, frozen rotation) — it only needs its movement rerouted through the Rigidbody (fix #1) plus real wall colliders (fix #3).

Split: **fix #1 is CODE** (csharp-dev / Docker). **Fixes #2 and #3 are EDITOR** (scene-architect / user — flagged loudly below; Docker cannot do them).

---

## (a) Per-mover anchors + how each currently moves

| Mover | File:line | How it moves today | Physics-aware? |
|---|---|---|---|
| Player | `Assets/Scripts/playerMovement.cs:44` (inside `FixedUpdate` at `:40`) | `transform.Translate(new Vector3(x,y).normalized * Time.deltaTime * worldState.instance.MoveSpeed())` | ❌ bypasses colliders |
| Chaser | `Assets/Scripts/chaserBehaviour.cs:16` (inside `Update` at `:11`) | `transform.position = Vector3.MoveTowards(transform.position, player.position, chaseSpeed*Time.deltaTime)` | ❌ bypasses colliders |
| Boss (chase) | `Assets/Scripts/bossBehaviour.cs:24` (inside `Update` at `:12`) | `transform.position = Vector3.MoveTowards(transform.position, target, chaseSpeed*Time.deltaTime)` | ❌ bypasses colliders |
| Boss (leash) | `Assets/Scripts/bossBehaviour.cs:20` | `transform.position = ComputeOffscreenPoint(target)` (intentional teleport off-screen) | ❌ bypasses colliders (leash teleport — should stay a teleport) |
| Shooter (Chase) | `Assets/Scripts/shooterBehaviour.cs:42` (state machine in `Update` at `:32`) | `transform.position = Vector3.MoveTowards(transform.position, playerPos, moveSpeed*Time.deltaTime)` | ❌ bypasses colliders |
| Shooter (Cooldown) | `Assets/Scripts/shooterBehaviour.cs:64` | `transform.position = Vector3.MoveTowards(transform.position, playerPos, moveSpeed*Time.deltaTime)` | ❌ bypasses colliders |

Shared movement constant: `worldState.MoveSpeed()` at `Assets/Scripts/worldState.cs:74` (`moveSpeedBase * moveSpeedMult`).

**Key physics rule:** `transform.position =` and `transform.Translate(...)` teleport the transform; the Rigidbody2D follows without collision resolution. Only `Rigidbody2D.MovePosition(...)` (or setting `linearVelocity`) performs a *swept* move that walls can stop — **and only on a Dynamic body**. A Kinematic body moved by `MovePosition` still is not blocked.

---

## (b) CODE changes — exact drop-in per mover (csharp-dev / Docker)

**Principle:** cache the `Rigidbody2D` in `Awake`/`Start`, and perform the final position write through `_rb.MovePosition(...)` inside **`FixedUpdate`** (physics timestep). Keep every gameplay decision (leash, aim, freeze, state machine, animation) byte-for-byte identical — only the *how-it-writes-position* changes. Use `Time.fixedDeltaTime` for movement in `FixedUpdate`.

> All four scripts sit on GameObjects that already have (or will have — see Editor) a Rigidbody2D, so `GetComponent<Rigidbody2D>()` resolves on the same object.

### 1. `playerMovement.cs`
Add a cached rigidbody; move `FixedUpdate` off `transform.Translate`:
```csharp
private Rigidbody2D _rb;   // ADD

void Start()
{
    oldX = oldY = float.NaN;
    playerAnimator = GetComponent<Animator>();
    _rb = GetComponent<Rigidbody2D>();          // ADD
}

private void FixedUpdate()
{
    if ((x != 0) || (y != 0))
    {
        Vector2 delta = new Vector2(x, y).normalized
                        * Time.fixedDeltaTime * worldState.instance.MoveSpeed();
        _rb.MovePosition(_rb.position + delta);  // CHANGED from transform.Translate(...)
    }
}
```
Input reading (`Update`) and animator params stay unchanged.

### 2. `chaserBehaviour.cs`
Cache rigidbody in `Awake`; relocate the move from `Update` to `FixedUpdate`:
```csharp
private Rigidbody2D _rb;   // ADD

void Awake()
{
    _health = GetComponent<enemyHealth>();
    _rb = GetComponent<Rigidbody2D>();          // ADD
}

void FixedUpdate()                               // CHANGED from Update()
{
    if (worldState.instance == null || worldState.instance.player == null) return;
    if (_health != null && _health.IsFrozen) return;   // freeze gate preserved

    Vector2 target = worldState.instance.player.position;
    Vector2 next = Vector2.MoveTowards(_rb.position, target, chaseSpeed * Time.fixedDeltaTime);
    _rb.MovePosition(next);                       // CHANGED from transform.position = ...
}
```

### 3. `bossBehaviour.cs`
Cache rigidbody; chase step uses `MovePosition`; **leash uses a direct `_rb.position =` teleport** (NOT `MovePosition`) so the long off-screen jump does not sweep across the whole level and snag on walls:
```csharp
private Rigidbody2D _rb;   // ADD

void Awake()
{
    _health = GetComponent<enemyHealth>();
    _rb = GetComponent<Rigidbody2D>();          // ADD
}

void FixedUpdate()                               // CHANGED from Update()
{
    if (worldState.instance == null || worldState.instance.player == null) return;
    if (_health != null && _health.IsFrozen) return;   // leash+chase freeze gate preserved
    Vector3 target = worldState.instance.player.position;

    if (((Vector3)_rb.position - target).sqrMagnitude > leashDistance * leashDistance)
    {
        _rb.position = ComputeOffscreenPoint(target);  // CHANGED: teleport, not a swept move
        return;                                        // leash OR chase, never both (preserved)
    }

    Vector2 next = Vector2.MoveTowards(_rb.position, target, chaseSpeed * Time.fixedDeltaTime);
    _rb.MovePosition(next);                            // CHANGED from transform.position = ...
}
```
`ComputeOffscreenPoint(...)` is unchanged (it reads `transform.position`, which stays in sync with `_rb.position`).

### 4. `shooterBehaviour.cs`
Cache rigidbody; convert the state machine's `Update()` to `FixedUpdate()` so timers and movement share the physics step; the two movement writes (Chase, Cooldown) go through `MovePosition`. Aim/Fire/telegraph logic is untouched:
```csharp
private Rigidbody2D _rb;   // ADD

void Awake()
{
    ConfigureTelegraph();
    _health = GetComponent<enemyHealth>();
    _rb = GetComponent<Rigidbody2D>();          // ADD
}

void FixedUpdate()                               // CHANGED from Update()
{
    if (worldState.instance == null || worldState.instance.player == null) return;
    if (_health != null && _health.IsFrozen) return;   // freeze gate preserved
    Vector3 playerPos = worldState.instance.player.position;
    float dist = Vector2.Distance(_rb.position, playerPos);

    switch (state)
    {
        case State.Chase:
            _rb.MovePosition(Vector2.MoveTowards(_rb.position, playerPos, moveSpeed * Time.fixedDeltaTime));
            if (dist <= aimRange) { /* aim setup unchanged, uses transform.position */ }
            break;
        // Aim / Fire: unchanged (no movement)
        case State.Cooldown:
            _rb.MovePosition(Vector2.MoveTowards(_rb.position, playerPos, moveSpeed * Time.fixedDeltaTime));
            // timer unchanged, use Time.fixedDeltaTime
            break;
    }
}
```
Replace `Time.deltaTime` with `Time.fixedDeltaTime` for all `stateTimer += ...` lines (fixed step keeps aim/cooldown durations identical). Telegraph `SetPosition` calls stay on `transform.position`.

> **Note for csharp-dev:** if you prefer minimal diff over relocating to `FixedUpdate`, the *smallest* collision-correct change is to swap each `transform.position = Vector3.MoveTowards(...)` for `_rb.MovePosition(Vector2.MoveTowards(_rb.position, ..., ...))` in place. `MovePosition` called from `Update` still collides but is applied at the next physics step and can micro-jitter; the `FixedUpdate` form above is preferred.

---

## (c) EDITOR SETUP checklist ⚠️ (scene-architect / USER — CANNOT be done in Docker)

> **LOUD FLAG:** The code fix alone will **NOT** make walls block anything. The items below are mandatory and require the live Unity Editor (component add/edit, Rigidbody body-type change, layer assignment, Physics2D matrix). Docker/csharp-dev cannot perform them. Dispatch `scene-architect` (Editor exception) or have the user do them.

### Current prefab / scene physics inventory (ground truth, grepped)

| Object | Source | Rigidbody2D | Collider2D | Layer | Notes |
|---|---|---|---|---|---|
| **player** (in `SampleScene.unity`) | scene object | ✅ Dynamic (BodyType 0), gravity 0, `Constraints:4` = freeze-rotation-Z, Simulated | ✅ `BoxCollider2D`, `m_IsTrigger:0` (solid) | 7 = **Player** | **Already physics-ready.** Only needs the code fix + real walls. |
| **slime** (`Assets/Prefabs/enemies/slime.prefab`) — BASE of all enemies | prefab | ⚠️ **Kinematic (BodyType 1)**, gravity 0, `Constraints:4`, Simulated | ⚠️ `CircleCollider2D`, **`m_IsTrigger:1`** (trigger only) | 0 = Default | Tag `Enemy`. Body type + trigger both block wall collision. |
| **chaser** (`chaser.prefab`) | **variant of slime** | inherits slime | inherits slime (trigger) | inherits (0) | override file holds only tuning/behaviour overrides |
| **shooter** (`shooter.prefab`) | **variant of chaser → slime** | inherits | inherits (trigger) | inherits (0) | |
| **boss / diggy / ziggy** (`bosses/*.prefab`) | **variants of chaser → slime** | inherits | inherits (trigger) | inherits (0) | |
| **walls** (Tilemap in `SampleScene.unity`) | scene object | ❌ none | ❌ **none** (`Tilemap` + `TilemapRenderer` only) | 8 = **Walls** ✅ | Correct layer already, but **no collider to hit**. |

Inheritance chain: `slime` (base) → `chaser` → {`shooter`, `boss`, `diggy`, `ziggy`}. **Editing the `slime` base prefab's Rigidbody2D/collider propagates to every enemy** unless a variant overrides it — do the enemy changes on `slime.prefab` once.

### Editor tasks

**E1 — Give the walls a solid collider (root cause #3).**
On the scene `walls` Tilemap (layer 8, Walls):
- Add `TilemapCollider2D` (`m_IsTrigger` = **false**).
- Add `CompositeCollider2D` (+ its auto-added `Rigidbody2D` set to **Static**), and set `TilemapCollider2D.Used By Composite = true` — merges wall tiles into clean, efficient colliders. (A plain `TilemapCollider2D` also works; composite is recommended for tile walls.)
- This same collider is what already lets `projectileBehaviour` (`wallLayer` LayerMask, `projectileBehaviour.cs:58`) stop bullets — so it is consistent with existing wall usage.

**E2 — Make enemies Dynamic (root cause #2a).** On `slime.prefab`'s `Rigidbody2D`: change **Body Type: Kinematic → Dynamic**. Keep Gravity Scale = 0 and Freeze Rotation Z (Constraints 4). A Kinematic body is never blocked; Dynamic + `MovePosition` is.

**E3 — Give enemies a non-trigger wall collider (root cause #2b).** The existing `CircleCollider2D` is a **trigger** used for player contact-damage (`playerHealth.OnTriggerEnter2D` matches tag `Enemy`, `playerHealth.cs:17`). Do **not** simply flip it to non-trigger — that would break contact damage and cause shoving. Instead, on `slime.prefab`:
- **Keep** the existing `CircleCollider2D` as a **trigger** (contact damage unchanged).
- **Add a second, non-trigger** collider (e.g. another `CircleCollider2D`, `m_IsTrigger:0`, roughly body-sized) — this is the one walls block.

**E4 — Layer + Physics2D matrix (controls *what* the solid collider bumps).** The scene's `m_LayerCollisionMatrix` is currently all-`ff` (every layer collides with every layer). With enemies made Dynamic + non-trigger, that default means enemies would also **shove each other and shove the player**. To get *walls-only* blocking (recommended, preserves current feel):
- Assign the enemy **wall-collider** to a dedicated physics layer (an empty slot is free — e.g. layer 3, or add a new `EnemyBody` layer). Put the non-trigger collider from E3 on that layer (a child GameObject on that layer works, since Unity keys the matrix off each collider's own layer).
- Keep the **trigger** collider (E3) on a layer that still collides with **Player** so contact damage keeps firing.
- Set the Physics2D matrix so the enemy wall-collider layer collides with **Walls = ON**, and **OFF** for {itself (no enemy-enemy jostle), Player (no shoving the player)}.
- Verify **Player (7) ↔ Walls (8) = ON** and **enemy-wall-layer ↔ Walls (8) = ON** (both already ON under the all-`ff` default; just don't disable them).

**Simpler fallback (if a dedicated layer is too much):** put a single non-trigger collider on the enemy's existing layer and set only **Enemy-layer ↔ Enemy-layer = OFF** in the matrix (stops self-jostle). Trade-off: enemies will then physically bump the player as well — acceptable in many survivors games, but a behaviour change from today.

---

## (d) Grep acceptance for the CODE

After csharp-dev's changes, all of these must hold:

```bash
# No mover writes transform.position / transform.Translate for locomotion anymore
grep -nE 'transform\.Translate|transform\.position *=' \
  Assets/Scripts/playerMovement.cs Assets/Scripts/chaserBehaviour.cs \
  Assets/Scripts/shooterBehaviour.cs
# EXPECT: no locomotion hits. (bossBehaviour.cs:_rb.position = ComputeOffscreenPoint is the ONLY
# allowed direct write — an intentional leash teleport, not a swept move.)

# Every mover now moves through the Rigidbody2D
grep -l 'MovePosition' Assets/Scripts/playerMovement.cs Assets/Scripts/chaserBehaviour.cs \
  Assets/Scripts/bossBehaviour.cs Assets/Scripts/shooterBehaviour.cs
# EXPECT: all 4 files listed

# Each mover caches a Rigidbody2D
grep -c 'GetComponent<Rigidbody2D>()' Assets/Scripts/playerMovement.cs \
  Assets/Scripts/chaserBehaviour.cs Assets/Scripts/bossBehaviour.cs Assets/Scripts/shooterBehaviour.cs
# EXPECT: 1 in each

# Movement lives in FixedUpdate
grep -l 'FixedUpdate' Assets/Scripts/chaserBehaviour.cs Assets/Scripts/bossBehaviour.cs \
  Assets/Scripts/shooterBehaviour.cs Assets/Scripts/playerMovement.cs
# EXPECT: all 4
```

Editor acceptance (verify in Play Mode / prefab inspector, not greppable from Docker): walls Tilemap has a non-trigger collider; `slime.prefab` Rigidbody2D Body Type = Dynamic; enemy has one trigger + one non-trigger collider; Physics2D matrix has enemy-wall-layer↔Walls = ON, enemy↔enemy = OFF. Then: player and every enemy stop at wall tiles; contact damage still applies; enemies do not visibly jostle each other.

---

## (e) Risk notes

- **Don't break freeze / leash / aim.** The `_health.IsFrozen` early-returns, the boss leash `sqrMagnitude` check + off-screen teleport, and the shooter Chase→Aim→Fire→Cooldown state machine must remain logically identical — only the *position write* is rerouted. The boss leash **must stay a direct `_rb.position =` set** (not `MovePosition`); a swept MovePosition over a 14+ unit jump would collide with walls en route and strand the boss.
- **Kinematic → Dynamic side effects.** Dynamic bodies respond to forces/gravity. Keep **Gravity Scale = 0** and **Freeze Rotation Z** (already `Constraints:4` on slime) so enemies don't drift or spin. Because movement is driven by `MovePosition` (not velocity), enemies won't accumulate unwanted momentum.
- **Enemy-vs-enemy pushing.** With Dynamic bodies + non-trigger colliders on the same layer, `MovePosition` resolves overlaps → visible jostling. The recommended dedicated-layer matrix (E4) with enemy↔enemy = OFF avoids this. If using the fallback, set at least enemy↔enemy = OFF.
- **Enemy-vs-player pushing.** Same mechanism can shove the player. Recommended design keeps the enemy solid collider off the Player-colliding layer so only the existing trigger (contact damage) interacts with the player. Verify contact damage (`playerHealth.cs:17`) still fires after the layer changes — trigger events also respect the collision matrix, so the trigger collider's layer must still collide with Player.
- **Projectiles unaffected.** `projectileBehaviour` already detects walls via the `wallLayer` LayerMask (`projectileBehaviour.cs:6,58`); adding the wall collider (E1) is consistent with and reinforces existing behaviour.
- **`transform.position` reads stay valid.** `Camera`/telegraph/aim code that *reads* `transform.position` keeps working — `MovePosition` keeps the transform in sync each physics step.
