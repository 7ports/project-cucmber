# Design — XP / Enemy / Pooling Feature (6 parts)

- **Topic:** xp-system-design
- **Date:** 2026-07-04
- **Actor:** project-planner (design only — no implementation)
- **Builds on:** `.voltron/analyses/2026-07-04-xp-system-baseline.md` (read first; not re-audited here)
- **Scope:** 6 features — enemy health→XP drop, XP lerp-on-pickup, pickup radius, XP contact→currentXP, slow-chaser enemy, basic object pooling.

---

## 0. Global design decisions (apply everywhere)

### 0.1 Chosen XP-vs-player contact mechanism (stated ONCE, used consistently)

**Mechanism: kinematic-trigger on the XP, `OnTriggerEnter2D` on the XP script, player identified by reference-equality against `worldState.instance.player` (no new "Player" tag needed).**

- The **XP prefab** carries a **`Rigidbody2D` set to Body Type = Kinematic** and a **`CircleCollider2D` with `isTrigger = true`**. Because one body in the pair has a Rigidbody2D, Unity 2D will fire trigger callbacks even though the player has none.
- The player keeps its existing non-trigger `BoxCollider2D` (unchanged).
- Contact is handled inside `pickupBehaviour.OnTriggerEnter2D(Collider2D other)`: it compares `other.transform == worldState.instance.player || other.transform.root == worldState.instance.player`. On match → award XP + return to pool.
- **Why this over `Physics2D.OverlapCircle` polling for contact:** the XP already needs a collider to be discoverable by the pickup-radius scan (Feature 3), and it already moves every frame (Feature 2), so a trigger callback is zero extra per-frame cost and fires exactly once. Reference-equality avoids adding a "Player" tag and avoids a per-frame distance check inside every XP.
- **Consistency note:** `OverlapCircle` IS still used — but only by **Feature 3 (the player's pickup-radius scan to *find* XP)**, never for player-contact. The two never overlap in responsibility: Feature 3 = "is this XP close enough to start homing?"; Feature 4 = "did a homing XP physically touch the player?". Kept distinct on purpose.

### 0.2 Style conventions (match existing code exactly)
- lowercase camelCase class names matching filenames; **no namespaces**; flat `Assets/Scripts/`.
- camelCase fields, no underscore prefix; mix of `public` (inspector) and `[SerializeField] private` — follow whichever the neighbouring file uses.
- Movement via `transform`/`Vector3.MoveTowards` lerp, not physics forces (matches slime/player).
- Player lookup is always `worldState.instance.player` — never `Find*`.

### 0.3 worldState null-order hazard
`worldState.instance` is assigned only in `gameController.Start()`. Every new script that reads `worldState.instance` must tolerate it being `null` on the very first frame (guard with `if (worldState.instance == null) return;`) OR rely on being spawned from the pool (which only happens after gameplay starts). Scene-placed objects (initial slimes, the player's pickup-radius component) take the guard.

---

## 1. Pooling architecture (Feature 6 foundation — build FIRST)

Kept deliberately **basic** per the user's ask: one manager + one marker component. No generic `<T>`, no per-type prewarm config assets, no coroutine reclaim.

### 1.1 New script: `Assets/Scripts/objectPool.cs`
- **Type:** `objectPool : MonoBehaviour`, scene-placed on a `pooling` GameObject. Static access `public static objectPool instance;` assigned in `Awake()` (mirrors the `worldState` static-instance idiom, but MonoBehaviour so it lives in the scene).
- **State:** `Dictionary<GameObject, Queue<GameObject>> pools` keyed by **source prefab**.
- **Public API:**
  | Method | Purpose |
  |---|---|
  | `GameObject get(GameObject prefab, Vector3 position, Quaternion rotation)` | Dequeue an inactive instance for that prefab (or `Instantiate` a fresh one if the queue is empty); set its transform; tag it with a `pooledObject` marker pointing back at `prefab`; `SetActive(true)`; return it. |
  | `void ret(GameObject instance)` | Read the instance's `pooledObject.source`, `SetActive(false)`, enqueue it back under that prefab. If the instance has no `pooledObject` marker (was scene-placed, not pooled) → fall back to `Destroy` and log a warning. |
- **Reset-on-spawn contract:** the pool does NOT know per-type state. Each pooled behaviour resets itself in **`OnEnable()`** (which re-runs every time `get` calls `SetActive(true)`). This is the single reset mechanism for the whole feature — documented per-feature below. On `ret`, `SetActive(false)` fires `OnDisable`, where behaviours stop coroutines / zero velocity.

### 1.2 New script: `Assets/Scripts/pooledObject.cs`
- **Type:** `pooledObject : MonoBehaviour` — tiny marker.
- **Field:** `public GameObject source;` (the prefab it was cloned from). Added/updated by `objectPool.get`; read by `objectPool.ret`. No logic.

### 1.3 Spawn / despawn sites that change to use the pool
| Site | Today | After |
|---|---|---|
| `playerProjectileShooter.cs:32` | `Instantiate(projectile, …)` | `objectPool.instance.get(projectile, transform.position, transform.rotation)` — then set velocity as before. |
| Projectile cleanup (**new**, `projectileBehaviour.cs`) | never destroyed | on enemy hit or lifetime expiry → `objectPool.instance.ret(gameObject)`. |
| Enemy death (Feature 1, `enemyHealth.cs`) | n/a | `objectPool.instance.ret(gameObject)` for the enemy, then `objectPool.instance.get(xpPrefab, transform.position, …)` for the drop. |
| XP consumed (Feature 4, `pickupBehaviour.cs`) | n/a | `objectPool.instance.ret(gameObject)` instead of `Destroy`. |
| Slime spawning (**new**, `enemySpawner.cs`) | slimes are scene-placed, no spawner | spawner calls `objectPool.instance.get(slimePrefab, …)` on an interval. |

> **Slime pooling requires a spawner that does not exist today.** Pooling a scene-placed object is meaningless, so Feature 6's "pool slimes" sub-goal implies creating `enemySpawner.cs` (see Feature 6 below) and removing the hand-placed slimes from the scene. Flagged as an Editor decision in §Open Questions.

---

## Feature 1 — Enemy health: HP reaches 0 → despawn → drop XP

- **File edited:** `Assets/Scripts/enemyHealth.cs` (empty stub, already on `slime.prefab`).
- **New supporting file:** `Assets/Scripts/projectileBehaviour.cs` (damage delivery — see below; also a Feature-6 pool client).
- **Fields to add (enemyHealth):**
  | Field | Purpose |
  |---|---|
  | `[SerializeField] private int maxHp = 3;` | starting hit points |
  | `private int currentHp;` | live HP |
  | `[SerializeField] private GameObject xpPrefab;` | prefab spawned on death (pool key) |
  | `[SerializeField] private int xpDropCount = 1;` | how many XP to drop |
- **Methods to add (enemyHealth):**
  - `void OnEnable()` → `currentHp = maxHp;` (pool reset contract).
  - `public void takeDamage(int amount)` → subtract; if `currentHp <= 0` call `die()`.
  - `void die()` → for each of `xpDropCount`: `objectPool.instance.get(xpPrefab, transform.position, Quaternion.identity)`; then `objectPool.instance.ret(gameObject)`.
- **Damage source — `projectileBehaviour.cs`:** the star projectile currently has no script, so nothing delivers damage. Add `projectileBehaviour : MonoBehaviour` on `star.prefab`:
  - `void OnCollisionEnter2D(Collision2D col)` (star has a **Dynamic Rigidbody2D** + non-trigger `CircleCollider2D`, slime has a non-trigger collider → `OnCollisionEnter2D` fires on the star): if `col.collider.CompareTag("Enemy")`, get `enemyHealth` via `col.collider.GetComponent<enemyHealth>()`, call `takeDamage(Mathf.RoundToInt(worldState.instance.attackDamage))`, then `objectPool.instance.ret(gameObject)`.
  - `[SerializeField] private float lifeSeconds = 3f;` + `OnEnable()` (re)starts a lifetime that returns the star to the pool if it hits nothing (prevents leaks; also the pool-reset hook — zero the Rigidbody2D velocity in `OnEnable`).
- **worldState interaction:** reads `worldState.instance.attackDamage` for damage amount.
- **Pool interaction:** returns the enemy, gets the XP — both via `objectPool.instance`.
- **Prerequisites (Editor):** XP prefab must exist (Feature 4 defines it); `enemyHealth.xpPrefab` reference assigned on `slime.prefab`; `projectileBehaviour` added to `star.prefab`; slime keeps `Enemy` tag.

---

## Feature 2 — XP lerps toward the player once its pickup flag is set

- **File edited:** `Assets/Scripts/pickupBehaviour.cs` (empty stub; will live on the XP prefab). Features 2 and 4 share this one script.
- **Fields to add:**
  | Field | Purpose |
  |---|---|
  | `public bool pickup;` | the flag set externally by Feature 3 |
  | `[SerializeField] private float homeSpeed = 8f;` | lerp/move speed toward player |
- **Methods to add:**
  - `void OnEnable()` → `pickup = false;` (pool reset contract — a recycled XP must start un-homed).
  - `void Update()` → `if (!pickup) return;` guard, `if (worldState.instance == null) return;` guard, then `transform.position = Vector3.MoveTowards(transform.position, worldState.instance.player.position, homeSpeed * Time.deltaTime);`
- **worldState interaction:** reads `worldState.instance.player.position` (same pattern as slime).
- **Pool interaction:** none directly in Feature 2 (spawn is Feature 1, despawn is Feature 4); only the `OnEnable` reset matters for reuse.
- **Prerequisites (Editor):** XP prefab (see Feature 4).

---

## Feature 3 — playerPickupRadius sets the pickup flag on nearby XP

- **File edited:** `Assets/Scripts/playerPickupRadius.cs` (empty stub; goes on the **player** GameObject).
- **Fields to add:**
  | Field | Purpose |
  |---|---|
  | `[SerializeField] private float radius = 4f;` | pickup detection radius |
  | `[SerializeField] private LayerMask xpLayer;` | filter — only scan the XP layer |
  | `[SerializeField] private float scanInterval = 0.1f;` | throttle (don't scan every frame) |
  | `private float scanTimer;` | interval accumulator |
- **Methods to add:**
  - `void Update()` → accumulate `scanTimer`; when `>= scanInterval`, reset and call `scan()`.
  - `void scan()` → `Physics2D.OverlapCircleAll(transform.position, radius, xpLayer)`; for each hit, `GetComponent<pickupBehaviour>()?.pickup = true`.
  - `void OnDrawGizmosSelected()` → draw the radius (dev aid).
- **worldState interaction:** none required (component lives on the player itself, uses `transform.position`).
- **Pool interaction:** none — only flips a flag; pooling handles spawn/despawn.
- **Prerequisites (Editor):**
  - **A dedicated `XP` layer** (ProjectSettings → Tags & Layers), assigned to the XP prefab; `xpLayer` mask set to it. (Layer chosen over a tag so `OverlapCircleAll` can filter in the physics query rather than post-filtering by tag.)
  - `playerPickupRadius` component **added to the player** (currently not attached anywhere).

---

## Feature 4 — XP on contact with player: +currentXP and despawn

- **File edited:** `Assets/Scripts/pickupBehaviour.cs` (same script as Feature 2 — contact half).
- **Fields to add:**
  | Field | Purpose |
  |---|---|
  | `[SerializeField] private int xpValue = 1;` | amount added to `currentXP` per pickup |
- **Methods to add:**
  - `void OnTriggerEnter2D(Collider2D other)` → `if (worldState.instance == null) return;`; `if (other.transform == worldState.instance.player || other.transform.root == worldState.instance.player)` → `worldState.instance.currentXP += xpValue;` then `objectPool.instance.ret(gameObject);`
- **worldState interaction:** increments `worldState.instance.currentXP` (field already exists; `lvlUpXP` also exists but **no level-up logic is in scope** — see Open Questions).
- **Pool interaction:** returns itself via `objectPool.instance.ret(gameObject)` (NOT `Destroy`).
- **Contact mechanism:** exactly §0.1 — XP has kinematic Rigidbody2D + trigger collider; player identified by reference to `worldState.instance.player`.
- **Prerequisites (Editor) — the XP prefab itself (new asset):**
  - Create `Assets/xp.prefab`: SpriteRenderer, **`Rigidbody2D` Body Type = Kinematic**, **`CircleCollider2D` `isTrigger = true`**, on the **`XP` layer** (Feature 3), with `pickupBehaviour` attached and `xpValue`/`homeSpeed` set.
  - No "XP tag" is required (layer + reference-equality cover both needs); do not add one unless a later feature needs tag-based queries.

---

## Feature 5 — New enemy type: slow continuous chaser (clean, not a slime copy)

- **File created:** `Assets/Scripts/chaserBehaviour.cs` (`chaserBehaviour : MonoBehaviour`). **Does NOT reuse `slimeBehaviour`** — avoids its two known bugs (overlapping-coroutine start; `.normalized` operator-precedence error at slimeBehaviour.cs:25).
- **Fields to add:**
  | Field | Purpose |
  |---|---|
  | `[SerializeField] private float chaseSpeed = 1.5f;` | slow constant homing speed |
- **Methods to add:**
  - `void Update()` → `if (worldState.instance == null) return;` then `transform.position = Vector3.MoveTowards(transform.position, worldState.instance.player.position, chaseSpeed * Time.deltaTime);` — frame-based, no coroutine, no in-flight guard needed, no precedence trap.
- **worldState interaction:** reads `worldState.instance.player.position` (established enemy pattern).
- **Pool interaction:** carries `enemyHealth` (Feature 1) so it despawns→drops XP through the pool identically to the slime; poolable as a distinct prefab key.
- **Prerequisites (Editor):**
  - Create a **prefab variant of `slime.prefab`** (or a sibling prefab): keep `Enemy` tag, keep `enemyHealth` (assign its `xpPrefab`), **replace `slimeBehaviour` with `chaserBehaviour`**. Give it a distinguishing sprite/color if desired.
  - Assign this prefab as a spawnable in `enemySpawner` (Feature 6) if it should spawn at runtime.

---

## Feature 6 — Basic object pooling for projectiles, slimes, and XP

Architecture is §1 above (`objectPool.cs` + `pooledObject.cs`). This feature = **create the pool + convert every spawn/despawn site + add the slime spawner**.

- **Files created:** `Assets/Scripts/objectPool.cs`, `Assets/Scripts/pooledObject.cs`, `Assets/Scripts/enemySpawner.cs`.
- **Files edited:** `playerProjectileShooter.cs` (Instantiate→`get`), plus the pool calls already specified inside `enemyHealth` (F1), `pickupBehaviour` (F4), `projectileBehaviour` (F1).
- **`enemySpawner.cs`** (new — needed because slimes are scene-placed with no spawner):
  - **Fields:** `[SerializeField] private GameObject[] enemyPrefabs;` (slime + chaser), `[SerializeField] private float spawnInterval = 2f;`, `[SerializeField] private float spawnRadius = 8f;` (spawn ring around player), `private float spawnTimer;`.
  - **Methods:** `Update()` accumulates timer; on interval, pick a prefab + a point around `worldState.instance.player.position` and `objectPool.instance.get(prefab, point, Quaternion.identity)`. Guard on `worldState.instance == null`.
- **Reset-on-spawn (whole feature):** every pooled behaviour resets its per-spawn state in `OnEnable` (`enemyHealth.currentHp`, `pickupBehaviour.pickup`, `projectileBehaviour` velocity/lifetime). `OnDisable` stops any running coroutine and zeroes Rigidbody2D velocity.
- **worldState interaction:** spawner reads `worldState.instance.player` for spawn placement.
- **Prerequisites (Editor):**
  - A `pooling` GameObject in the scene carrying `objectPool`.
  - A spawner GameObject carrying `enemySpawner` with prefab refs assigned.
  - **Remove the hand-placed slime(s) from the scene** (or leave one and let the spawner add more — a decision; see Open Questions). Scene-placed instances are NOT pool-managed and `ret` will `Destroy` them with a warning.

---

## Dependency-ordered task list (for scrum-master → csharp-dev + Editor passes)

### Phase A — Pool foundation (Docker / csharp-dev) — no dependencies
1. **A1** Create `objectPool.cs` (§1.1).
2. **A2** Create `pooledObject.cs` (§1.2).  *(A1 references it — do A2 with or before A1.)*

### Phase B — Core behaviours (Docker / csharp-dev) — depend on Phase A
3. **B1** Fill `pickupBehaviour.cs` — Features 2 + 4 (lerp + `OnTriggerEnter2D` + `ret`). *(needs A1)*
4. **B2** Fill `enemyHealth.cs` — Feature 1 (HP, `takeDamage`, `die`→get XP + ret self). *(needs A1)*
5. **B3** Create `projectileBehaviour.cs` — Feature 1 damage delivery + projectile pool-return. *(needs A1, B2)*
6. **B4** Fill `playerPickupRadius.cs` — Feature 3 (`OverlapCircleAll` → set `pickup`). *(needs B1 for the `pickupBehaviour.pickup` field)*
7. **B5** Create `chaserBehaviour.cs` — Feature 5 (clean continuous chase). *(independent; can run parallel to B1–B4)*
8. **B6** Edit `playerProjectileShooter.cs:32` — Instantiate → `objectPool.instance.get`. *(needs A1)*
9. **B7** Create `enemySpawner.cs` — Feature 6 slime/chaser spawning. *(needs A1)*

> B1, B2, B5, B6, B7 are largely independent given Phase A; B3 depends on B2; B4 depends on B1. Dispatch B1/B2/B5/B6/B7 in parallel, then B3 and B4.

### Phase C — Editor / asset setup (Host — scene-architect / asset-manager) — depend on Phase B scripts compiling
10. **C1** Add an **`XP` layer** (Tags & Layers). *(prereq for C2, B4 mask)*
11. **C2** Create **`Assets/xp.prefab`**: SpriteRenderer + Kinematic `Rigidbody2D` + trigger `CircleCollider2D`, on `XP` layer, `pickupBehaviour` attached, values set. *(needs B1, C1)*
12. **C3** Add `projectileBehaviour` to `star.prefab`; verify Dynamic Rigidbody2D present. *(needs B3)*
13. **C4** On `slime.prefab`, assign `enemyHealth.xpPrefab = xp.prefab`, set `maxHp`. *(needs B2, C2)*
14. **C5** Create the **chaser prefab variant** of slime: swap `slimeBehaviour`→`chaserBehaviour`, keep `Enemy` tag + `enemyHealth`, assign its `xpPrefab`. *(needs B5, C2)*
15. **C6** Add `playerPickupRadius` **to the player**; set `radius`, `xpLayer = XP`. *(needs B4, C1)*
16. **C7** Add a **`pooling`** GameObject with `objectPool`; add a **spawner** GameObject with `enemySpawner`, assign `enemyPrefabs = [slime, chaser]`; remove/reduce hand-placed slimes. *(needs A1, B7, C5)*

### Phase D — Validation (Host — build-validator)
17. **D1** Compile check (no errors), then Play Mode smoke: projectile kills slime → XP drops → walks into pickup radius → homes → contact increments `currentXP` → both slime and XP return to pool (Hierarchy shows reuse, not unbounded growth); chaser homes slowly. *(needs all above)*

### Docker vs Editor split (explicit)
- **C# file work (Docker / csharp-dev):** A1, A2, B1–B7. Pure `.cs` create/edit; no Editor needed.
- **Editor work (Host — scene-architect / asset-manager for prefab/component/layer setup):** C1–C7 (layer, XP prefab, component attachment, prefab variant, inspector references, scene wiring).
- **Validation (Host — build-validator):** D1 (Play Mode + console).

---

## Open questions / decisions for the user (defaults proposed so nothing blocks)

| # | Question | Proposed default (used if no answer) |
|---|---|---|
| Q1 | XP awarded per pickup (`xpValue`)? | **1** (`lvlUpXP` is 16 → ~16 kills to level) |
| Q2 | XP dropped per enemy death (`xpDropCount`)? | **1** |
| Q3 | Enemy max HP (`enemyHealth.maxHp`)? | **3** (with `attackDamage` 1 → 3 hits) |
| Q4 | Pickup radius (`playerPickupRadius.radius`)? | **4** world units |
| Q5 | XP homing speed (`homeSpeed`)? | **8** (clearly faster than the chaser so pickups feel snappy) |
| Q6 | Chaser speed (`chaseSpeed`)? | **1.5** (slow, per "constantly lerps slowly") |
| Q7 | Pool prewarm sizes? | **None** — grow on demand (basic pooling); pool `Instantiate`s lazily when a queue is empty |
| Q8 | Projectile lifetime before auto-return (`lifeSeconds`)? | **3s** |
| Q9 | Spawner cadence & placement (`spawnInterval`, `spawnRadius`)? | **2s**, ring of **radius 8** around player |
| Q10 | Do we keep the hand-placed scene slime, or fully switch to the spawner? | **Remove hand-placed slimes; spawner owns all enemies** (so every enemy is pool-managed — avoids the `ret`→`Destroy` warning path) |
| Q11 | Level-up when `currentXP >= lvlUpXP`? | **Out of scope** — Feature 4 only increments `currentXP`; no level-up logic requested. Flag if wanted as a follow-up. |
| Q12 | Should the chaser also use a Rigidbody2D, or stay transform-based like the slime? | **Transform-based** (`Vector3.MoveTowards`), matching slime/player convention |

---

## Prerequisites summary (assets that do NOT exist yet — must be created in Phase C)
- **`XP` layer** (ProjectSettings) — none exists.
- **`Assets/xp.prefab`** — no XP prefab exists.
- **Chaser prefab variant** — no file/prefab exists.
- **`pooling` + spawner GameObjects** in the scene — none exist.
- `projectileBehaviour` on `star.prefab`; `playerPickupRadius` on the player — currently attached to nothing.
- No new **tag** is required (layer + reference-equality cover detection & contact).
