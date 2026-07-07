# Boss Bullet-Hell Emission System — Design Doc

**Date:** 2026-07-07
**Author:** project-planner
**Scope:** DESIGN ONLY — no implementation. Grounds every decision in the existing top-down survivors codebase.
**Feature:** The boss fires projectiles in a customizable, randomizable emission pattern, shipping several classic bullet-hell patterns with a cheap path to add more.

---

## 1. Grounding — what the codebase already gives us

Verified by reading source (file:line):

| Fact | Source |
|---|---|
| Boss is **transform-based**, lives on `boss.prefab`, chases/leashes `worldState.instance.player.position` | `Assets/Scripts/bossBehaviour.cs:9-21` |
| Pooling API: `objectPool.instance.get(GameObject prefab, Vector3 pos, Quaternion rot)` → `GameObject`; `objectPool.instance.ret(GameObject)` | `Assets/Scripts/objectPool.cs:15,42` |
| Pooled objects **reset state in `OnEnable()`** (timer + velocity zeroing), because `get()` calls `SetActive(true)` and reuses instances | `Assets/Scripts/projectileBehaviour.cs:11-17` |
| Player projectile reference model: `[SerializeField] lifeSeconds`, distance-from-origin cap via `worldState.Range()`, wall LayerMask, `OnTriggerEnter2D` damage + `ret()` | `Assets/Scripts/projectileBehaviour.cs:5-55` |
| `worldState` is a **plain static class** (not MonoBehaviour): `worldState.instance.player` is a `Transform`; effective stats via getters | `Assets/Scripts/worldState.cs:3-34` |
| Player takes damage on trigger contact with `"Enemy"`-tagged colliders through `playerHealth` (contact-tick model, `Reduce()` applies Defense) | `Assets/Scripts/playerHealth.cs:17-78` |
| Spawners use `Camera.main` viewport math + `Time.deltaTime` accumulator timers | `Assets/Scripts/enemySpawner.cs:15-36`, `Assets/Scripts/bossSpawner.cs:31-45` |
| `enemyHealth.takeDamage(int)` handles hit flash + damage numbers + death/XP | `Assets/Scripts/enemyHealth.cs:30-57` |
| Codebase idiom: **plain C# classes + `[SerializeField]` fields**; ScriptableObjects are NOT used for behavior config anywhere in `Assets/Scripts/` | whole tree |

**Implication:** the natural, low-friction extension shape here is an **enum + switch (strategy) inside a MonoBehaviour**, matching every other system in the project. A ScriptableObject pattern catalog would be idiomatically foreign and add asset-wiring overhead the team doesn't otherwise carry.

---

## 2. Shared dependency — the `enemyProjectile` contract (CANONICAL)

The straight-line enemy projectile is being designed in parallel. This emission system depends only on its **launch entry point**. One canonical signature — both designs MUST match this exactly:

```csharp
// On the enemyProjectile MonoBehaviour (component on the pooled enemyProjectile prefab):
//   dir       : desired travel direction (need NOT be pre-normalized; enemyProjectile normalizes internally)
//   speed     : world units / second
//   lifetime  : seconds before auto-return to pool (also caps travel)
public void Launch(Vector2 dir, float speed, float lifetime);
```

**Contract the sibling `enemyProjectile` design must satisfy:**
- It is a **pooled prefab** obtained via `objectPool.instance.get(enemyProjectilePrefab, spawnPos, Quaternion.identity)`.
- It exposes `public void Launch(Vector2 dir, float speed, float lifetime)`.
- `Launch` normalizes `dir` internally, sets its own velocity (Rigidbody2D `linearVelocity` per the player-projectile idiom) = `dir.normalized * speed`.
- It **resets its own state in `OnEnable()`** (velocity zero, timers reset) so pooled reuse is clean — `bossShooter` does NOT reset projectile internals.
- It damages the player on `OnTriggerEnter2D` (routes through `playerHealth`, so Defense/flash/shake apply) and returns itself to the pool. **Player damage is entirely the projectile's job — `bossShooter` never touches player HP.**
- It self-returns to the pool on `lifetime` expiry or wall hit.

**Exact call `bossShooter` will make per bullet:**

```csharp
GameObject go = objectPool.instance.get(enemyProjectilePrefab, transform.position, Quaternion.identity);
enemyProjectile p = go.GetComponent<enemyProjectile>();   // cache-friendly; see §3 note
if (p != null) p.Launch(dir, bulletSpeed, bulletLifetime);
```

> If the sibling prefers `Init(...)` + a separate velocity set, this doc's canonical choice overrides for consistency: **`Launch(Vector2 dir, float speed, float lifetime)`**.

---

## 3. Pattern architecture — enum + switch strategy (RECOMMENDED)

### Decision

Use an **`enum BulletPatternType`** plus a **`switch` dispatch** inside the `bossShooter` MonoBehaviour. Each pattern is a small private method `EmitRing()`, `EmitSpiral()`, etc. `Fire()` switches on the current `patternType` and calls the matching emit method.

### Why this over a ScriptableObject catalog (for THIS codebase)

| Criterion | enum + switch (chosen) | ScriptableObject pattern list (rejected) |
|---|---|---|
| Matches existing idioms | ✅ every system is plain class + `[SerializeField]` + enum-ish switches | ❌ no SO-driven behavior exists here |
| Wiring cost | ✅ zero extra assets; tune in Inspector on the prefab | ❌ must author + assign SO assets per pattern |
| Add-a-pattern effort | ✅ 1 enum member + 1 method + 1 switch case (localized) | ⚠️ new SO subclass/asset + polymorphic dispatch |
| Randomize among patterns | ✅ `(BulletPatternType)Random.Range(0, Count)` | ⚠️ needs a curated list asset |
| Discoverability for later agents | ✅ all logic in one file | ❌ split across assets + scripts |

**Alternatives considered:**
- *Strategy interface `IBulletPattern` + `List<IBulletPattern>`*: cleanest OOP, but overkill — introduces indirection the rest of the codebase never uses, and pattern params would need per-strategy state objects. Rejected for altitude mismatch.
- *ScriptableObject catalog*: best when designers author many data variants without code; this project has one boss and a handful of patterns, so the asset overhead isn't repaid. Rejected.

### Extension point — exactly where "add a pattern later" happens

Adding `DOUBLE_RING` (or any new pattern) is a **3-line-of-intent, single-file change** in `bossShooter.cs`:

1. **Add enum member** (one line):
   ```csharp
   public enum BulletPatternType { Ring, Spiral, AimedSpread, RandomScatter, /* NEW: */ DoubleRing }
   ```
2. **Add a switch case** in `Fire()` (one line):
   ```csharp
   case BulletPatternType.DoubleRing: EmitDoubleRing(); break;
   ```
3. **Add the emit method** (one small private method using the shared `EmitBullet(dir)` helper).

No other file changes. `RandomizePattern()` automatically includes it (it enumerates the enum — see §4). This is the whole cost of a new pattern.

### Shared emission helper (keeps every pattern tiny)

```csharp
// Single funnel: every pattern computes directions and calls this.
void EmitBullet(Vector2 dir)
{
    if (objectPool.instance == null || enemyProjectilePrefab == null) return;
    GameObject go = objectPool.instance.get(enemyProjectilePrefab, transform.position, Quaternion.identity);
    enemyProjectile p = go.GetComponent<enemyProjectile>();
    if (p != null) p.Launch(dir, bulletSpeed, bulletLifetime);
}
```

Direction convention: bullets travel in world XY. Angle `θ` (degrees, CCW from +X) → `dir = new Vector2(Mathf.Cos(θ*Mathf.Deg2Rad), Mathf.Sin(θ*Mathf.Deg2Rad))`.

Aim helper (angle toward player, degrees):
```csharp
float AimAngleDeg()
{
    Transform pl = worldState.instance != null ? worldState.instance.player : null;
    Vector2 to = pl != null ? (Vector2)(pl.position - transform.position) : Vector2.right;
    if (to.sqrMagnitude < 0.0001f) to = Vector2.right;
    return Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg;
}
```

---

## 4. `bossShooter` component spec

MonoBehaviour, added to `boss.prefab` alongside `bossBehaviour`. Plain-class idiom, `[SerializeField]` private fields.

### Serialized fields (Inspector-tunable)

```csharp
[Header("Pattern")]
[SerializeField] private BulletPatternType patternType = BulletPatternType.Ring;
[SerializeField] private GameObject enemyProjectilePrefab;   // the pooled enemyProjectile prefab

[Header("Cadence")]
[SerializeField] private float fireInterval = 1.2f;          // seconds between volleys

[Header("Bullet")]
[SerializeField] private float bulletSpeed = 3.5f;           // world units / sec
[SerializeField] private float bulletLifetime = 6f;          // seconds; passed to Launch

[Header("Volley shape")]
[SerializeField] private int bulletsPerVolley = 12;          // N — ring count / fan count / scatter count
[SerializeField] private float spreadAngle = 45f;            // degrees — total fan width (AimedSpread)
[SerializeField] private float spinStep = 13f;               // degrees added to emitter angle each volley (Spiral)

[Header("Randomize")]
[SerializeField] private bool randomizeOnEnable = false;     // if true, OnEnable also rolls a pattern
```

### Runtime state
```csharp
private float fireTimer;
private float spinAngle;   // accumulates for Spiral
```

### Lifecycle

```csharp
void OnEnable()
{
    // Pool-reset contract: boss.prefab may itself be reused. Clear volley state.
    fireTimer = 0f;
    spinAngle = 0f;
    if (randomizeOnEnable) RandomizePattern();
}

void Update()
{
    // Fire only while boss is alive & player exists. Boss aliveness == this GO active
    // (enemyHealth.die() -> objectPool.ret / Destroy deactivates it, so Update stops).
    if (worldState.instance == null || worldState.instance.player == null) return;
    if (objectPool.instance == null || enemyProjectilePrefab == null) return;

    fireTimer += Time.deltaTime;
    if (fireTimer >= fireInterval)
    {
        fireTimer = 0f;
        Fire();
    }
}
```

### `Fire()` — dispatch
```csharp
void Fire()
{
    switch (patternType)
    {
        case BulletPatternType.Ring:         EmitRing();        break;
        case BulletPatternType.Spiral:       EmitSpiral();      break;
        case BulletPatternType.AimedSpread:  EmitAimedSpread(); break;
        case BulletPatternType.RandomScatter:EmitRandomScatter();break;
        // NEW patterns: add one case here (see §3 extension point)
    }
}
```

### `RandomizePattern()` — the spawner hook (EXACT name)

Public, parameterless. This is the exact method `bossSpawner` calls when spawning the boss at level 5 so each boss appearance uses a different pattern.

```csharp
public void RandomizePattern()
{
    int count = System.Enum.GetValues(typeof(BulletPatternType)).Length;
    patternType = (BulletPatternType)Random.Range(0, count);
    spinAngle = 0f;   // fresh spiral phase per roll
}
```

> Because it enumerates the enum, **any newly added pattern is automatically eligible** — no edit to `RandomizePattern()` when adding patterns.

---

## 5. Starter pattern set (≥4 classic bullet-hell patterns)

All angles in degrees, CCW from +X. All use the `EmitBullet(dir)` helper. Directions computed from an angle via `Ang2Dir(θ)`.

### 5.1 RING — N bullets evenly around 360°
- **Params:** `bulletsPerVolley` (N), `bulletSpeed`.
- **Math:** for `i` in `0..N-1`: `θ_i = i * (360 / N)`.
- **Defaults:** N = 12, speed = 3.5.
```csharp
void EmitRing()
{
    int n = Mathf.Max(1, bulletsPerVolley);
    float step = 360f / n;
    for (int i = 0; i < n; i++) EmitBullet(Ang2Dir(i * step));
}
```

### 5.2 SPIRAL — rotating emitter, N arms per volley
- **Params:** `bulletsPerVolley` (arms per volley), `spinStep` (degrees added each volley), `bulletSpeed`.
- **Math:** base angle `spinAngle` advances by `spinStep` every volley; emit N bullets evenly split around 360° offset by `spinAngle`. Over successive volleys the offset rotates → spiral.
- **Defaults:** N = 3, spinStep = 13° (a non-divisor of 360 gives long, non-repeating spirals), speed = 3.5.
```csharp
void EmitSpiral()
{
    int n = Mathf.Max(1, bulletsPerVolley);
    float step = 360f / n;
    for (int i = 0; i < n; i++) EmitBullet(Ang2Dir(spinAngle + i * step));
    spinAngle = Mathf.Repeat(spinAngle + spinStep, 360f);
}
```

### 5.3 AIMED_SPREAD — fan of K bullets centered on the player
- **Params:** `bulletsPerVolley` (K), `spreadAngle` (total fan width, degrees), `bulletSpeed`.
- **Math:** center = `AimAngleDeg()` (toward player). For K bullets spanning `spreadAngle`: bullet `i` angle = `center - spreadAngle/2 + i * (spreadAngle/(K-1))`. K==1 → single aimed shot at `center`.
- **Defaults:** K = 5, spreadAngle = 45°, speed = 4.0 (aimed shots read as faster/threatening).
```csharp
void EmitAimedSpread()
{
    int k = Mathf.Max(1, bulletsPerVolley);
    float center = AimAngleDeg();
    if (k == 1) { EmitBullet(Ang2Dir(center)); return; }
    float half = spreadAngle * 0.5f;
    float inc = spreadAngle / (k - 1);
    for (int i = 0; i < k; i++) EmitBullet(Ang2Dir(center - half + i * inc));
}
```

### 5.4 RANDOM_SCATTER — N bullets in random directions (chosen 4th pattern)
- **Params:** `bulletsPerVolley` (N), `bulletSpeed`. Optional bias toward player via `spreadAngle` (0 = full 360 random).
- **Math:** for each of N bullets: `θ = Random.Range(0f, 360f)` (or `AimAngleDeg() + Random.Range(-spreadAngle, spreadAngle)` if a biased cone is desired). Uses `Random` (allowed — spawners already use `Random.Range`/`Random.value`, `enemySpawner.cs:23-30`).
- **Defaults:** N = 10, speed = 3.0.
```csharp
void EmitRandomScatter()
{
    int n = Mathf.Max(1, bulletsPerVolley);
    for (int i = 0; i < n; i++) EmitBullet(Ang2Dir(Random.Range(0f, 360f)));
}
```

> Alternate 4th (DOUBLE_RING) documented as the §3 worked example if the team prefers a deterministic pattern instead: emit two concentric rings, the second offset by `step/2`, for a denser wall — trivially added later without touching this doc's four.

### Shared math helpers
```csharp
static Vector2 Ang2Dir(float deg)
{
    float r = deg * Mathf.Deg2Rad;
    return new Vector2(Mathf.Cos(r), Mathf.Sin(r));
}
```

---

## 6. Integration

- `bossShooter` is added to `boss.prefab` **alongside** `bossBehaviour` (both transform-based, both read `worldState.instance.player`). No coupling between the two components — movement and firing are independent.
- **Aims relative to `worldState.instance.player`** via `AimAngleDeg()` (used by AimedSpread; other patterns are player-agnostic but still fire from the boss's transform, which chases the player).
- **Fires while alive:** the component fires every `fireInterval` in `Update()`. When the boss dies, `enemyHealth.die()` (`enemyHealth.cs:44-57`) deactivates/returns the GameObject → `Update()` stops → firing stops. No explicit "am I alive" flag needed, matching the leash/chase model.
- **Spawner hook (level 5):** the boss is spawned by `bossSpawner` (`bossSpawner.cs:44` `Instantiate`). To randomize per appearance, the spawner should call `RandomizePattern()` on the freshly spawned boss:
  ```csharp
  activeBoss = Instantiate(bossPrefab, point, Quaternion.identity);
  var shooter = activeBoss.GetComponent<bossShooter>();
  if (shooter != null) shooter.RandomizePattern();
  ```
  This is the **only** edit to a shared/existing file, and it is additive (2 lines after the existing `Instantiate`). Gating on level 5 is a separate concern already tracked by the boss-cadence TODO (`bossSpawner.cs:22-25`) and is out of scope for this shooter design — `RandomizePattern()` is the hook regardless of when the spawn fires.
- **Pooling:** bullets are pooled `enemyProjectile` instances (§2). `bossShooter` only calls `get()` + `Launch()`; the projectile owns its own `ret()`. No new pool registration needed — `objectPool` is prefab-keyed and lazily creates queues (`objectPool.cs:19-25,54-58`).
- **Player damage path:** entirely inside `enemyProjectile` (routes through `playerHealth` so Defense/flash/shake apply). `bossShooter` never references player HP.

---

## 7. Editor-wiring checklist (for a later scene-architect pass)

1. Open `boss.prefab`.
2. **Add Component → `bossShooter`** (sits next to `bossBehaviour`, `enemyHealth`, `bossHealthBar`).
3. Assign **`Enemy Projectile Prefab`** = the pooled `enemyProjectile` prefab (from the sibling design).
4. Set defaults on the prefab: `patternType = Ring`, `fireInterval = 1.2`, `bulletSpeed = 3.5`, `bulletLifetime = 6`, `bulletsPerVolley = 12`, `spreadAngle = 45`, `spinStep = 13`, `randomizeOnEnable = false`.
5. Confirm the `enemyProjectile` prefab has the `"Enemy"`-independent trigger setup its own design specifies (its collider must hit the player, not other enemies). *(Sibling design owns this.)*
6. In `bossSpawner` (if per-appearance randomization is wanted now): the 2-line `GetComponent<bossShooter>() + RandomizePattern()` addition after `Instantiate` (§6).
7. Play-mode smoke test: boss spawns → volleys emit on interval → bullets travel → return to pool after lifetime. Verify no `enemyProjectile` leak (pool count stable across many volleys).

---

## 8. NEW vs EDIT — files, ordering, risk

| File | New/Edit | Purpose | Risk |
|---|---|---|---|
| `Assets/Scripts/bossShooter.cs` | **NEW** | Emission component: enum, serialized fields, `Update` timer, `Fire()`, four `Emit*` methods, `EmitBullet` helper, `RandomizePattern()` | Low — self-contained, no shared state |
| `Assets/Scripts/enemyProjectile.cs` | **NEW (sibling)** | Pooled straight-line enemy bullet with `Launch(Vector2 dir, float speed, float lifetime)` | Owned by sibling design — this doc only defines the contract |
| `boss.prefab` | **EDIT (scene-architect)** | Add `bossShooter`, assign prefab + tunables | Low — additive component |
| `enemyProjectile.prefab` | **NEW (sibling)** | The pooled bullet asset | Sibling |
| `Assets/Scripts/bossSpawner.cs` | **EDIT (optional, 2 lines)** | Call `RandomizePattern()` after `Instantiate` | Low — additive, guarded by null check |

**Shared-file edits:** only `bossSpawner.cs` (optional, additive) and `boss.prefab`. **`bossShooter.cs` edits no existing script.** No changes to `worldState`, `objectPool`, `projectileBehaviour`, `enemyHealth`, or `playerHealth`.

**Ordering:**
1. Sibling `enemyProjectile` (prefab + script) — the hard dependency.
2. `bossShooter.cs` (this design) — compiles independently; runtime needs the prefab.
3. Prefab wiring (scene-architect).
4. Optional `bossSpawner` randomize hook.

---

## 9. Open questions — with chosen safe defaults

| # | Question | Safe default (chosen) |
|---|---|---|
| 1 | Should firing be gated on boss being on-screen / not leashed? | **No** — fire whenever active; leash already keeps the boss near the visible edge. Off-screen volleys just travel in. Revisit only if it feels unfair. |
| 2 | Does `enemyProjectile.Launch` take normalized or raw direction? | **Raw** — `Launch` normalizes internally (§2). Callers pass unit `Ang2Dir` vectors anyway. |
| 3 | Bullet lifetime vs. distance cap? | Use **`lifetime` seconds** (passed to `Launch`), mirroring `projectileBehaviour.lifeSeconds`. Distance cap is the projectile's optional internal concern. |
| 4 | Should `RandomizePattern()` avoid repeating the previous pattern? | **No** — plain uniform roll for simplicity/idiom (spawners roll uniformly, `enemySpawner.cs:23`). Add anti-repeat later if desired (localized to the method). |
| 5 | Per-pattern tuned defaults vs. one shared field set? | **One shared field set** on the prefab; if RandomizePattern picks Spiral, the same `bulletsPerVolley` (12) yields a dense spiral — acceptable. Per-pattern presets would need SOs (rejected in §3). Document as a future enhancement. |
| 6 | Where does level-5 gating live? | **In `bossSpawner`** (existing boss-cadence TODO, `bossSpawner.cs:22-25`), NOT in `bossShooter`. This design is spawn-timing-agnostic. |
| 7 | `GetComponent<enemyProjectile>()` per bullet — perf? | Acceptable at these volley sizes (≤~20/volley); pool reuse means no GC. If profiling flags it, cache the component on the projectile prefab and have `get()` return typed — out of scope. |

---

## 10. Summary

- **Architecture:** `enum BulletPatternType` + `switch` in a `bossShooter` MonoBehaviour — matches the project's plain-class idiom; adding a pattern = 1 enum member + 1 switch case + 1 method, single file.
- **Patterns (4):** RING, SPIRAL, AIMED_SPREAD, RANDOM_SCATTER (DOUBLE_RING documented as the drop-in example).
- **Contract:** `enemyProjectile.Launch(Vector2 dir, float speed, float lifetime)` — the canonical signature both designs share; projectile owns normalization, pooling return, and player damage.
- **Hook:** `public void RandomizePattern()` — enumerates the enum and rolls uniformly; called by `bossSpawner` after `Instantiate`.
- **Risk:** contained — one NEW script, additive prefab/spawner edits, zero changes to shared runtime scripts.
