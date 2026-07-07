# Design Doc: Shooter Enemy Variant + Shared `enemyProjectile`

**Date:** 2026-07-07
**Author:** project-planner
**Status:** DESIGN ONLY — no implementation. Consumed by scrum-master → csharp-dev (scripts) + scene-architect (prefab/scene wiring).
**Scope:** A `shooterBehaviour` chaser variant that pauses to aim (telegraph line) and fires a straight-line `enemyProjectile`, PLUS the shared, pooled `enemyProjectile` that a sibling boss bullet-hell design will reuse.

---

## Overview

A new enemy — the **shooter** — is a chaser variant. It walks toward the player like `chaserBehaviour`, but when the player enters `aimRange` it **stops**, renders a red **telegraph `LineRenderer`** from itself toward the player for `aimDuration` seconds, then **fires one `enemyProjectile`** in a straight line, hides the line, and enters a `fireCooldown` before it may aim again (resuming chase meanwhile).

The `enemyProjectile` is owned by this design and is **shared**: the boss bullet-hell design calls the same `Launch` method to spray bullets. It is pooled via the existing `objectPool`, travels straight via `Rigidbody2D` velocity, damages the player on trigger-enter, and returns to the pool after `lifetime` seconds.

Everything follows the codebase's existing idioms: `objectPool.get/ret` with `OnEnable` state reset (as in `projectileBehaviour`/`enemyHealth`/`enemyAnimator`), `worldState.instance.player` for the player transform, 2D trigger colliders, legacy `UnityEngine.UI`/Input, URP 2D.

---

## Grounding (what the code already does)

| Fact | Source | Consequence for this design |
|---|---|---|
| Chase = `Vector3.MoveTowards(pos, player.position, speed*dt)` in `Update` | `chaserBehaviour.cs:12` | Reuse verbatim for the CHASE state. |
| Player transform = `worldState.instance.player` (plain static, may be null) | `worldState.cs:6`, `chaserBehaviour.cs:10` | Always null-guard `worldState.instance` and `.player`. |
| Player contact damage: `playerHealth.OnTriggerEnter2D` matches `CompareTag("Enemy")` then reads `enemyHealth.EnemyDamage` | `playerHealth.cs:17-30` | Shooter must keep tag **Enemy** + `enemyHealth` + trigger collider so body-contact still hurts the player. |
| `playerHealth.ApplyDamage(int)` is **private** — no public damage entrypoint exists | `playerHealth.cs:65` | The projectile cannot damage the player without EITHER a new public method on `playerHealth` (recommended) OR piggybacking the Enemy-tag path. See §5 + Open Questions. |
| Player identified by `other.transform == worldState.instance.player || other.transform.root == worldState.instance.player` | `pickupBehaviour.cs:24`, `questItem.cs:10` | Reuse this exact pattern for the projectile's player detection. |
| Pooling: `objectPool.get(prefab,pos,rot)` / `objectPool.ret(go)`; adds a `pooledObject{source}` marker; `ret` just `SetActive(false)` + enqueue | `objectPool.cs`, `pooledObject.cs` | Pooled objects MUST reset all mutable state in `OnEnable` (nothing else re-initializes them on reuse). |
| Existing player projectile: `OnEnable` zeroes `rb.linearVelocity` + resets `lifeTimer`; `Update` returns to pool on range/lifetime; damages on `OnTriggerEnter2D` | `projectileBehaviour.cs` | `enemyProjectile` mirrors this structure (mirror image: damages player not enemy; expires on lifetime, no range cap by default). |
| Player shooter launches by `objectPool.get` then `rb.linearVelocity = dir*speed` | `playerProjectileShooter.cs:32-37` | `enemyProjectile.Launch` follows the same velocity model. |
| `chaser.prefab` is a prefab-instance variant of base guid `a3f0c52240fc14f4fbcb9e1d3c7451b1`; tag Enemy; trigger `CircleCollider2D` (`m_IsTrigger 1`); `SpriteRenderer` sortingOrder 3; Animator+`enemyAnimator`+`enemyHealth`; maxHp 30, enemyDamage 8 | `chaser.prefab` | `shooter.prefab` is built by the same recipe, swapping the behaviour and adding a `LineRenderer`. |
| Boss chases with a leash and reads `worldState.instance.player` | `bossBehaviour.cs` | Boss will separately call `enemyProjectile.Launch(...)` — signature must satisfy it (§ enemyProjectile). |

---

## 1. `enemyProjectile` component (SHARED — owned here, reused by boss)

New file: `Assets/Scripts/enemyProjectile.cs`. Mirror of `projectileBehaviour`, inverted to hit the player.

### Serialized fields
```csharp
public class enemyProjectile : MonoBehaviour
{
    [SerializeField] private int   damage   = 8;    // dealt to the player on hit
    [SerializeField] private float lifetime = 4f;   // seconds before auto-return to pool
    // No wallLayer/range cap by default (boss bullet-hell wants full-lifetime travel).
    // Velocity is set by Launch(), NOT serialized.

    private float lifeTimer;
    private Rigidbody2D rb;
}
```

### Canonical launch signature (COORDINATE POINT with boss design)

> **CANONICAL:** `public void Launch(Vector2 direction, float speed)`
> Lifetime comes from the serialized `lifetime` field.
> **Overload (optional, for per-shot lifetime):** `public void Launch(Vector2 direction, float speed, float lifetimeSeconds)`

Rationale: matches the requested signature and the existing velocity model (`rb.linearVelocity = dir*speed`). The boss can call the 2-arg form for uniform bullets, or the 3-arg overload when it needs pattern-specific lifetimes. `direction` is normalized inside `Launch` so callers may pass an un-normalized vector safely.

```csharp
void Awake() { rb = GetComponent<Rigidbody2D>(); }

void OnEnable()                       // pool-reset: nothing else re-inits on reuse
{
    lifeTimer = 0f;
    if (rb != null) rb.linearVelocity = Vector2.zero;
}

public void Launch(Vector2 direction, float speed)
{
    lifeTimer = 0f;                                   // restart the expiry clock at launch
    if (rb == null) rb = GetComponent<Rigidbody2D>();
    Vector2 d = direction.sqrMagnitude > 1e-8f ? direction.normalized : Vector2.right;
    if (rb != null) rb.linearVelocity = d * speed;
    else transform.position += (Vector3)(d * speed * Time.deltaTime); // fallback if non-RB variant
}

public void Launch(Vector2 direction, float speed, float lifetimeSeconds)
{
    lifetime = lifetimeSeconds;
    Launch(direction, speed);
}
```

### Lifetime / expiry
```csharp
void Update()
{
    lifeTimer += Time.deltaTime;
    if (lifeTimer >= lifetime)
    {
        if (objectPool.instance != null) objectPool.instance.ret(gameObject);
    }
}
```
Expiry = time-based only (no range cap), so boss curtain bullets live their full flight. `ret` sets inactive + enqueues; next `get` re-activates and `OnEnable` zeroes velocity + timer.

### Damaging the player (player detection)
```csharp
void OnTriggerEnter2D(Collider2D other)
{
    if (worldState.instance == null || worldState.instance.player == null) return;
    if (other.transform == worldState.instance.player ||
        other.transform.root == worldState.instance.player)      // reuse pickup/questItem idiom
    {
        playerHealth ph = worldState.instance.player.GetComponentInChildren<playerHealth>();
        if (ph != null) ph.TakeHit(damage);                      // NEW public method — see §5
        if (objectPool.instance != null) objectPool.instance.ret(gameObject);   // consumed on hit
    }
}
```
> **Dependency:** this requires a **public** entrypoint on `playerHealth` (`TakeHit(int)`), because `ApplyDamage` is private. That is a small shared-file EDIT — see §5 and the NEW/EDIT table. The alternative (tag the projectile "Enemy" so `playerHealth`'s existing trigger hits it) is rejected in Open Questions.

### Poolability
Pool it via `objectPool` (recommended over Instantiate+self-destruct): reuse avoids per-shot GC, which matters for boss bullet-hell spray. It already carries the `pooledObject` marker automatically the first time `objectPool.get` returns it; nothing extra is needed on the prefab. Spawner pattern (shooter & boss both):
```csharp
GameObject go = objectPool.instance.get(enemyProjectilePrefab, firePos, Quaternion.identity);
go.GetComponent<enemyProjectile>().Launch(aimDir, projectileSpeed);
```

---

## 2. `shooterBehaviour` component (new chaser variant)

New file: `Assets/Scripts/shooterBehaviour.cs`. A 4-state machine in `Update`.

### Serialized fields
```csharp
public class shooterBehaviour : MonoBehaviour
{
    [SerializeField] private float moveSpeed    = 1.5f;  // chase speed (chaser uses 1.0)
    [SerializeField] private float aimRange     = 4f;    // enter AIM when player within this
    [SerializeField] private float aimDuration  = 0.75f; // telegraph hold before firing
    [SerializeField] private float fireCooldown = 2f;    // after firing, before it may aim again

    [SerializeField] private GameObject enemyProjectilePrefab; // the §1 prefab
    [SerializeField] private float projectileSpeed = 6f;
    [SerializeField] private LineRenderer telegraph;           // child LineRenderer (§3)
    [SerializeField] private Color lineColor = new Color(1f, 0.2f, 0.2f, 0.8f);
    [SerializeField] private float lineWidth = 0.06f;

    private enum State { Chase, Aim, Fire, Cooldown }
    private State state;
    private float stateTimer;
    private Vector2 aimDir;   // locked at the fire instant (see decision)
}
```

### Aim-lock decision (JUSTIFIED)
**The telegraph line tracks the player live throughout AIM; the fire direction is locked at the moment the shot fires** (= wherever the line points at that instant).

Why lock-at-fire, not lock-at-aim-start:
- The task requires the telegraph to *track until fire*. A tracking line that then fired a stale aim-start direction would be a **lying telegraph** (shows one thing, fires another) — the worst UX.
- Locking at fire keeps the telegraph **truthful**: what the line shows at the fire frame is exactly the shot direction.
- Fairness/counterplay lives in the **projectile flight**: the bullet commits at fire and travels straight with a finite `lifetime`, so the player dodges the *in-flight bullet* by sidestepping after the flash, not by out-running the aim. This is the standard readable-telegraph pattern.

### Per-state logic
```csharp
void OnEnable()   // pool-reset ALL state (enemies are pooled by enemySpawner)
{
    state = State.Chase;
    stateTimer = 0f;
    aimDir = Vector2.right;
    if (telegraph != null) telegraph.enabled = false;
}

void Update()
{
    if (worldState.instance == null || worldState.instance.player == null) return;
    Vector3 playerPos = worldState.instance.player.position;
    float dist = Vector2.Distance(transform.position, playerPos);

    switch (state)
    {
        case State.Chase:
            transform.position = Vector3.MoveTowards(transform.position, playerPos, moveSpeed * Time.deltaTime);
            if (dist <= aimRange) { state = State.Aim; stateTimer = 0f; EnableTelegraph(true); }
            break;

        case State.Aim:
            // stop moving; telegraph tracks the player live
            UpdateTelegraph(transform.position, playerPos);
            stateTimer += Time.deltaTime;
            if (stateTimer >= aimDuration) state = State.Fire;
            break;

        case State.Fire:
            aimDir = ((Vector2)(playerPos - transform.position)).normalized; // LOCK at fire instant
            FireProjectile();
            EnableTelegraph(false);
            state = State.Cooldown; stateTimer = 0f;
            break;

        case State.Cooldown:
            // resume chasing during cooldown
            transform.position = Vector3.MoveTowards(transform.position, playerPos, moveSpeed * Time.deltaTime);
            stateTimer += Time.deltaTime;
            if (stateTimer >= fireCooldown) state = State.Chase;
            break;
    }
}

void FireProjectile()
{
    if (objectPool.instance == null || enemyProjectilePrefab == null) return;
    GameObject go = objectPool.instance.get(enemyProjectilePrefab, transform.position, Quaternion.identity);
    var proj = go.GetComponent<enemyProjectile>();
    if (proj != null) proj.Launch(aimDir, projectileSpeed);
}
```
Notes:
- CHASE reuses `chaserBehaviour`'s exact `MoveTowards` call; AIM freezes movement (no `MoveTowards`), which is what makes the enemy visibly "pause to aim."
- `enemyAnimator` keeps working untouched — during AIM the enemy is stationary so its directional floats simply hold their last value (it derives direction from position delta; zero delta → `lastDir`). No coupling needed.
- FIRE is a single-frame state; it immediately transitions to COOLDOWN so exactly one projectile spawns per cycle.

---

## 3. Telegraph line (`LineRenderer`)

A child `LineRenderer` on the shooter, 2 positions in **world space**: `[0]=shooter`, `[1]=aim point (current player pos)`.

Setup (done once in `OnEnable`/`Awake` helper, or pre-configured on the prefab):
```csharp
void EnableTelegraph(bool on) { if (telegraph != null) telegraph.enabled = on; }

void UpdateTelegraph(Vector3 from, Vector3 to)
{
    if (telegraph == null) return;
    telegraph.positionCount = 2;
    telegraph.SetPosition(0, from);
    telegraph.SetPosition(1, to);
}

void ConfigureTelegraph()   // call from Awake
{
    if (telegraph == null) return;
    telegraph.useWorldSpace   = true;
    telegraph.positionCount   = 2;
    telegraph.startWidth      = lineWidth;
    telegraph.endWidth        = lineWidth;
    telegraph.numCapVertices  = 0;
    telegraph.startColor      = lineColor;
    telegraph.endColor        = lineColor;
    telegraph.sortingOrder    = 6;          // above enemies (chaser SpriteRenderer sortingOrder = 3)
    telegraph.enabled         = false;      // hidden until AIM
    // Material: a Sprites/Default (or default-line) material tinted white so start/endColor show.
}
```
- **Widths:** ~0.06 world units (thin but visible).
- **Color:** semi-transparent red `(1, 0.2, 0.2, 0.8)` — reads as "danger."
- **sortingOrder:** 6, above enemy sprites (sortingOrder 3) and body, so the line is never occluded.
- **Enabled** only during AIM (`EnableTelegraph(true)` on entering AIM, `false` on FIRE). Also force-disabled in `OnEnable` so a pooled reuse never shows a stale line.

---

## 4. Prefab structure

### 4a. `shooter.prefab` (variant of `chaser.prefab`)
Build by the same recipe as `chaser.prefab`; scene-architect creates it (Editor pass).
- **Root GameObject:** tag **Enemy**, same layer as chaser (keeps `playerHealth` body-contact damage working — `playerHealth.cs:19` matches tag "Enemy").
- **SpriteRenderer** — same sprite as chaser (placeholder OK), sortingOrder 3.
- **Animator + `enemyAnimator`** — unchanged (kept; wire `anim` ref like chaser).
- **`enemyHealth`** — kept (e.g. maxHp 30, enemyDamage 8, xp drop wired) so it drops XP on death and deals contact damage.
- **`damageFlash`** — kept (enemyHealth calls it).
- **Trigger `CircleCollider2D`** (`m_IsTrigger = 1`) — kept, so player projectiles (`projectileBehaviour`) can hit it and it can touch the player.
- **REMOVE** `chaserBehaviour`; **ADD** `shooterBehaviour`.
- **ADD child (or same-GO) `LineRenderer`** — the telegraph; assign to `shooterBehaviour.telegraph`.
- Wire `shooterBehaviour.enemyProjectilePrefab` → `enemyProjectile.prefab`.

### 4b. `enemyProjectile.prefab` (new)
- **Root GameObject:** does **NOT** need tag Enemy under the recommended design (it damages the player itself; it is not the player-shooter's target). Layer: a bullet layer that only interacts with the player, or default with trigger filtering — keep simple: default layer, trigger collider.
- **SpriteRenderer** — a small bullet sprite (placeholder OK), sortingOrder ≥ 4 so it reads above enemies.
- **Trigger `Collider2D`** (small `CircleCollider2D`, `isTrigger = true`).
- **`Rigidbody2D`** — `bodyType = Dynamic`, `gravityScale = 0`, `Continuous` collision (fast bullet), freeze rotation Z. Required for the velocity model + trigger callbacks.
- **`enemyProjectile`** component — set `damage`, `lifetime`, defaults.
- Poolable automatically (objectPool adds `pooledObject` on first `get`).

---

## 5. Shared-file edit: `playerHealth` public damage entrypoint

`enemyProjectile` needs to damage the player, but `playerHealth.ApplyDamage(int)` is private. **Add a thin public wrapper** (minimal, non-breaking):
```csharp
// in playerHealth.cs
public void TakeHit(int amount) => ApplyDamage(amount);   // ranged/projectile damage entry
```
This reuses the existing `ApplyDamage` (which already applies Defense reduction, flash, camera shake, screen flash, and game-over check — `playerHealth.cs:65-78`), so projectile hits feel identical to contact hits. **This is the only edit to an existing gameplay script** and is additive (no behaviour change to existing paths). Flagged in the NEW/EDIT table and ordering.

---

## 6. Editor-wiring checklist (for scene-architect)

**`enemyProjectile.prefab`:**
- [ ] Create prefab with SpriteRenderer (bullet sprite), trigger `CircleCollider2D`, `Rigidbody2D` (Dynamic, gravityScale 0, freeze Z rotation, Continuous).
- [ ] Add `enemyProjectile` component; set `damage` (≈8), `lifetime` (≈4).
- [ ] Confirm no leftover `projectileBehaviour`/Enemy tag.

**`shooter.prefab`:**
- [ ] Duplicate/vary `chaser.prefab`; confirm tag **Enemy** + layer unchanged.
- [ ] Remove `chaserBehaviour`; add `shooterBehaviour`.
- [ ] Add a child `LineRenderer`; assign a Sprites/Default (white) material; assign to `shooterBehaviour.telegraph`.
- [ ] Set `LineRenderer`: useWorldSpace ON, positionCount 2, width ≈0.06, sortingOrder 6, enabled OFF.
- [ ] Assign `shooterBehaviour.enemyProjectilePrefab` → `enemyProjectile.prefab`.
- [ ] Set `moveSpeed`, `aimRange`, `aimDuration`, `fireCooldown`, `projectileSpeed`, `lineColor`, `lineWidth`.
- [ ] Verify `enemyHealth` (maxHp/enemyDamage/xpPrefab), `damageFlash`, `enemyAnimator.anim` wired (as chaser).

**Scene / spawner:**
- [ ] Register `shooter.prefab` in the enemy spawn set (`enemySpawner`) or a wave config as desired.
- [ ] Ensure `objectPool` and a player with `playerHealth` (assigned to `worldState.instance.player` via `gameController`) exist in the scene.

---

## 7. NEW vs EDIT table + ordering / risk

| Item | Type | File / Asset | Owner | Risk |
|---|---|---|---|---|
| `enemyProjectile` script | **NEW** | `Assets/Scripts/enemyProjectile.cs` | csharp-dev | Low — self-contained, mirrors `projectileBehaviour`. Shared contract with boss. |
| `shooterBehaviour` script | **NEW** | `Assets/Scripts/shooterBehaviour.cs` | csharp-dev | Low — self-contained; reuses chaser move + pool idioms. |
| `playerHealth.TakeHit(int)` | **EDIT (shared)** | `Assets/Scripts/playerHealth.cs` | csharp-dev | **Flagged** — shared gameplay file; additive one-liner wrapping private `ApplyDamage`. No existing-path change. Verify compile. |
| `enemyProjectile.prefab` | **NEW** | `Assets/enemyProjectile.prefab` | scene-architect (Editor) | Low. |
| `shooter.prefab` | **NEW** | `Assets/shooter.prefab` | scene-architect (Editor) | Low — variant recipe of chaser. |
| Spawner/scene registration | **EDIT** | spawner asset/scene | scene-architect | Low — optional gating by wave. |

**Ordering (dependencies):**
1. `enemyProjectile.cs` + `playerHealth.TakeHit` (scripts compile first; boss design blocks on the `Launch` signature — freeze it now).
2. `shooterBehaviour.cs` (references `enemyProjectile`).
3. `enemyProjectile.prefab` (Editor) → then `shooter.prefab` (Editor, references the projectile prefab).
4. Spawner/scene wiring + Play-Mode validation (build-validator).

**Shared-file risk callouts:**
- `playerHealth.cs` is the only existing gameplay script edited — additive, but coordinate if another in-flight task touches it.
- `enemyProjectile.Launch(Vector2, float)` is a **cross-design contract** with the boss bullet-hell design — DO NOT rename/reshape without updating the boss design. Canonical signature frozen in §1.

---

## 8. Open questions (with safe defaults)

1. **How does the projectile damage the player — new `playerHealth.TakeHit` (recommended) vs. tag the projectile "Enemy"?**
   *Default:* add `playerHealth.TakeHit(int)` and have the projectile detect the player itself. Rejected alternative: giving the projectile tag "Enemy" + an `enemyHealth` so `playerHealth`'s existing trigger damages it — this pollutes player-shooter targeting (`FindGameObjectsWithTag("Enemy")` would aim at bullets), lets player projectiles "kill" enemy bullets, and couples damage to `enemyHealth.EnemyDamage`. Avoid.
2. **RB velocity vs transform movement for the projectile?**
   *Default:* `Rigidbody2D` velocity (matches `projectileBehaviour`/player shooter, and trigger callbacks need a rigidbody in the pair). `Launch` includes a transform fallback if a non-RB variant is ever used.
3. **Aim lock timing.** Resolved in §2: telegraph tracks live, direction locks at fire. If designers want a dodge-during-aim window instead, switch to lock-at-aim-start — but then the telegraph must be a fixed line (contradicts "track until fire"); keep default.
4. **Does the shot stop chasing during COOLDOWN?**
   *Default:* it keeps chasing during cooldown (feels aggressive, avoids a passive enemy). Toggle to "hold still during cooldown" if too oppressive.
5. **Projectile range cap?** Player projectile caps travel by `worldState.Range()`. *Default:* enemy projectile uses **time-only** expiry (`lifetime`) with no range cap, because the boss bullet-hell needs full-flight bullets. Add an optional range cap later if shooter bullets feel too long-range.
6. **Does the shooter fire while off-screen / behind walls?** *Default:* fires whenever player is within `aimRange` regardless of line-of-sight (no wall layer in this design). Add a `wallLayer` raycast gate later if needed.
7. **Prefab location.** *Default:* place `shooter.prefab` and `enemyProjectile.prefab` next to `chaser.prefab` in `Assets/` (matches existing enemy prefab placement). Move under a folder if the team reorganizes.

---

## Handoff

- **csharp-dev:** implement `enemyProjectile.cs`, `shooterBehaviour.cs`, and the `playerHealth.TakeHit` one-liner (§1, §2, §5). Freeze `Launch(Vector2 direction, float speed)`.
- **scene-architect (Editor):** build `enemyProjectile.prefab` and `shooter.prefab`, wire per §6.
- **build-validator (Editor):** Play-Mode check — shooter pauses & shows red line in range, fires one bullet, bullet hurts player and expires after `lifetime`, pooled reuse shows no stale line/velocity.
- **boss bullet-hell design (sibling):** consume `enemyProjectile.Launch(Vector2, float)` (+ 3-arg overload).

> Plan saved to `.voltron/analyses/2026-07-07-shooter-enemy-design.md`. Invoke `/scrum-master` with this doc to generate a work breakdown.
