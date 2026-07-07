# Boss Enemy — Design Doc (single test boss)

**Date:** 2026-07-07
**Author:** project-planner
**Scope:** Design only. No implementation. Target: one special BOSS enemy modeled on the chaser, spawned once at scene start, with a world-space floating health bar and a catch-up leash.
**Stack constraints:** Unity URP 2D · legacy `UnityEngine.UI` Text/Image (NOT TMP) · legacy `Input` · 2D trigger colliders · pooled enemies (`SetActive` + `OnEnable` reset) · plain static `worldState.instance.player` (a `Transform`).

---

## 1. Grounding — the real chaser/enemy stack (cited)

Every recommendation below is anchored to these files. Line numbers are current as of this doc.

### chaserBehaviour.cs — movement
- `chaserBehaviour.cs:5` — `[SerializeField] private float chaseSpeed = 1.5f;` — the speed field. **Name: `chaseSpeed`.** Code default is `1.5`, **but the `chaser.prefab` overrides it to `chaseSpeed: 1`** (`Assets/chaser.prefab:109`). So the *real in-game chaser speed is 1.0 world-units/sec.*
- `chaserBehaviour.cs:10` — null-guards `worldState.instance` and `worldState.instance.player`.
- `chaserBehaviour.cs:12-15` — moves via **`transform.position = Vector3.MoveTowards(transform.position, player.position, chaseSpeed * Time.deltaTime)`**. **Transform-based, NOT Rigidbody2D.** No physics velocity is used. This matters: the leash reposition can safely hard-set `transform.position` without fighting a rigidbody.

### enemyHealth.cs — HP / damage / death / XP
- `enemyHealth.cs:5` — **`[SerializeField] private int maxHp = 3;`** — **the serialized max-HP field is named `maxHp`.** It already exists and is per-prefab. The `chaser.prefab` sets `maxHp: 30` (`Assets/chaser.prefab:20-21`). **→ A 250-HP boss needs only `maxHp = 250` set on the boss prefab. No code change to `enemyHealth` is required for HP.**
- `enemyHealth.cs:6` — `private int currentHp;` (runtime HP; not serialized).
- `enemyHealth.cs:23-26` — `OnEnable()` sets `currentHp = maxHp;` — this is the **pool-reset hook**. A pooled boss re-enabled from the pool starts full again automatically.
- `enemyHealth.cs:28-40` — `public void takeDamage(int amount)` decrements `currentHp`, flashes, spawns a pooled damage number, and calls `die()` at `<= 0`. **`takeDamage` and `currentHp` are the signals the health bar must read** (see §3 — `currentHp` is currently `private`; we must expose it).
- `enemyHealth.cs:7-8` — `[SerializeField] private GameObject xpPrefab;` + `[SerializeField] private int xpDropCount = 1;` — **the XP-drop-on-death path.**
- `enemyHealth.cs:42-55` — `die()` loops `xpDropCount` times spawning `xpPrefab` from the pool, spawns `bloodPrefab`, then `objectPool.instance.ret(gameObject)` (returns to pool, does not Destroy).
- `enemyHealth.cs:9,16` — `enemyDamage` + `public int EnemyDamage => enemyDamage;` (contact damage to player; chaser prefab = 8).

### enemyAnimator.cs — directional animation
- `enemyAnimator.cs:24-41` — reads `transform.position - lastPos` each frame and drives `anim.SetFloat("x", dir.x)` / `SetFloat("y", dir.y)`. **Because the boss moves by `transform.position` (same as chaser), `enemyAnimator` works unchanged on the boss** — it just needs an `Animator` + a directional override controller. `OnEnable()` (`:17-20`) resets `lastPos`/`oldX`/`oldY` (pool-safe).

### objectPool.cs — pooling
- `objectPool.cs:15-40` `get(prefab,pos,rot)` — dequeues or Instantiates, stamps a `pooledObject` marker with `source = prefab`, `SetActive(true)`.
- `objectPool.cs:42-59` `ret(instance)` — `SetActive(false)` + enqueue by source prefab. Non-pooled objects are destroyed with a warning.
- **Pattern for every pooled component: all per-spawn state resets in `OnEnable()`, never `Awake`/`Start`.** The boss health bar must follow this.

### enemySpawner.cs — off-screen spawn math (REUSE THIS)
- `enemySpawner.cs:25-35` — the canonical off-screen-near-player point:
  ```csharp
  int side = Random.Range(0, 4);
  Vector3 vp;
  if (side == 0)      vp = new Vector3(-edgeMargin, Random.value, 0f);      // left
  else if (side == 1) vp = new Vector3(1f + edgeMargin, Random.value, 0f);  // right
  else if (side == 2) vp = new Vector3(Random.value, -edgeMargin, 0f);      // bottom
  else                vp = new Vector3(Random.value, 1f + edgeMargin, 0f);  // top
  vp.z = Mathf.Abs(cam.transform.position.z);   // distance to the 2D plane
  Vector3 point = cam.ViewportToWorldPoint(vp);
  point.z = 0f;
  ```
  `edgeMargin` = `0.08f` viewport units. **The boss spawner and the leash reposition both reuse this exact viewport approach.** Note: the camera **follows the player**, so "off-screen in viewport space" is inherently "near the player." (`enemySpawner.cs:19` also shows the timed-cadence pattern via `spawnTimer += Time.deltaTime` — the model for the future boss-cadence TODO.)

### Camera / off-screen confirmation
- `enemySpawner.cs:15` and `questIndicator.cs:14,34` both use **`Camera.main`** and `ViewportToWorldPoint` / `WorldToViewportPoint`. The 2D setup is a standard orthographic camera looking down −Z (`vp.z = Mathf.Abs(cam.transform.position.z)`). **Camera.main is orthographic** → half-extents are `cam.orthographicSize` (vertical) and `cam.orthographicSize * cam.aspect` (horizontal). This is the math the leash uses (§4).

### World-space UI precedent (model for the health bar)
- `Assets/damageNumber.prefab` is a **World-Space Canvas** (`m_RenderMode: 2`) containing a `Text` + `CanvasGroup`, positioned directly in world coordinates (`damageNumber.cs:15,34` set `transform.position` in world space). `floatingText.cs` follows the same shape. **→ A world-space `Canvas` + `Image (Type=Filled)` child is the idiomatic, already-proven pattern in this project for the boss health bar** (mirrors `xpBarUI`'s filled-Image approach at `xpBarUI.cs:6,13`, but placed in world space instead of screen space).

---

## 2. MOVEMENT — decision: NEW `bossBehaviour` (do NOT reuse `chaserBehaviour` as-is)

**Recommendation: create a new `bossBehaviour.cs`**, not reuse `chaserBehaviour`.

Rationale:
- `chaserBehaviour` is a 4-line closed class with no extension point; the boss needs **chase + leash** owned in the same `Update()` so the leash can short-circuit the chase. Bolting leash logic onto `chaserBehaviour` would risk changing behavior for every normal chaser. Keeping the boss in its own component is the non-breaking choice.
- The movement core is copied verbatim (transform-based `Vector3.MoveTowards`) so `enemyAnimator` keeps working identically.

**Spec — `bossBehaviour.cs`:**
```csharp
using UnityEngine;

public class bossBehaviour : MonoBehaviour
{
    [SerializeField] private float chaseSpeed = 0.7f;    // ~0.7x the chaser's real speed of 1.0
    [SerializeField] private float leashDistance = 14f;  // world units; see §4
    [SerializeField] private float leashOffscreenPad = 1f;   // units past the visible edge

    void Update()
    {
        if (worldState.instance == null || worldState.instance.player == null) return;
        Vector3 target = worldState.instance.player.position;

        // Leash first: if too far, reposition just off-screen near the player, then skip chase this frame.
        if ((transform.position - target).sqrMagnitude > leashDistance * leashDistance)
        {
            transform.position = ComputeOffscreenPoint(target);
            return;   // do not also MoveTowards this frame — avoids fighting the reposition
        }

        transform.position = Vector3.MoveTowards(transform.position, target, chaseSpeed * Time.deltaTime);
    }
    // ComputeOffscreenPoint(...) — see §4
}
```

**Speed value:** boss `chaseSpeed = 0.7` — i.e. **0.7× the chaser's real runtime speed of 1.0** (inside the requested 0.6–0.75× band; "slightly slower"). Set on the prefab; tune 0.6–0.75 in play. Note the chaser's *code default* is `1.5` but its *prefab value* is `1.0` — measure "slower" against the **prefab value (1.0)**, so boss ≈ 0.7.

**Animation:** boss keeps `enemyAnimator` + an Animator + a directional Animator Override Controller. Because movement is transform-based, `enemyAnimator` (`:24-41`) derives x/y correctly with zero changes. Override controller may **reuse `chaser.overrideController`** (`Assets/Sprites/character/chaser.overrideController`) or a dedicated `boss.overrideController` for distinct art (see §6).

---

## 3. HEALTH = 250

**No structural change to `enemyHealth` is needed for HP.** `enemyHealth.maxHp` (`enemyHealth.cs:5`) is already a serialized per-prefab int; `OnEnable` resets `currentHp = maxHp` (`:25`). **Set `maxHp = 250` on the boss prefab** — normal enemies are untouched because HP is per-prefab data, not code.

**One required, minimal edit to `enemyHealth.cs`** — the health bar needs to read HP, but `currentHp` (`:6`) and `maxHp` (`:5`) are `private`. Add read-only accessors (additive, non-breaking — no behavior change, no signature change to existing methods):
```csharp
public int MaxHp => maxHp;
public int CurrentHp => currentHp;
```
This is the *only* change to the shared file, and it is purely additive. See §7 for the risk gate. (Alternative that avoids editing the shared file entirely: a `bossHealth : enemyHealth` subclass exposing the getters — rejected because `currentHp`/`maxHp` are `private` not `protected`, so the subclass still can't see them without editing the base anyway. The two-line getter is the cleanest.)

**XP on death:** the boss should feel rewarding. Reuse the existing `die()` path (`enemyHealth.cs:42-55`) — it already loops `xpDropCount` spawning `xpPrefab`. **Set `xpDropCount = 20` on the boss prefab** (a burst of ~20 orbs) with the same `xpPrefab` the chaser uses. Default chosen: **20 orbs** (tune 15–30). No code needed — it's serialized data. On death the boss also returns to the pool via `ret()` and its health bar (a child) deactivates with it (§3 pool-reset).

### World-space health bar — prefab structure
**Recommendation: a child World-Space `Canvas` + a filled `Image`** (matches the proven `damageNumber.prefab` render-mode-2 pattern and reuses `Image.fillAmount` exactly like `xpBarUI.cs:13`). Chosen over stacked SpriteRenderers because the project already standardizes on `UnityEngine.UI.Image` filled bars and has no bar-sprite assets.

Prefab hierarchy (child of the boss root):
```
Boss (root: SpriteRenderer, Animator, enemyAnimator, enemyHealth[maxHp=250,xpDropCount=20], bossBehaviour, CircleCollider2D isTrigger, damageFlash, pooledObject-at-runtime)
└─ HealthBarCanvas   (Canvas, RenderMode=World Space; small scale e.g. 0.01; local pos y=+1.2)
   ├─ BarBG          (Image, dark, full width)   — static frame
   └─ BarFill        (Image, Type=Filled, Horizontal, Origin=Left, red)  ← driven each frame
```

### `bossHealthBar.cs` — spec
```csharp
using UnityEngine;
using UnityEngine.UI;

public class bossHealthBar : MonoBehaviour
{
    [SerializeField] private enemyHealth health;      // ref to the boss's enemyHealth (parent)
    [SerializeField] private Image fill;              // BarFill, Image Type = Filled
    [SerializeField] private Transform followTarget;  // the boss root transform
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.2f, 0f);
    [SerializeField] private bool billboard = true;   // face the camera

    void OnEnable()   // pool-reset: snap to full + correct position immediately
    {
        if (fill != null) fill.fillAmount = 1f;
        UpdateFollow();
    }

    void LateUpdate()  // LateUpdate so it tracks after movement, like questIndicator
    {
        if (health != null && fill != null && health.MaxHp > 0)
            fill.fillAmount = Mathf.Clamp01((float)health.CurrentHp / health.MaxHp);
        UpdateFollow();
    }

    void UpdateFollow()
    {
        if (followTarget != null)
            transform.position = followTarget.position + worldOffset;
        if (billboard && Camera.main != null)
            transform.rotation = Camera.main.transform.rotation;  // 2D ortho: keeps bar upright/aligned
    }
}
```
Notes:
- **Update math:** `fillAmount = currentHP / maxHP`, clamped — identical to `xpBarUI.cs:13`. Read via the new `CurrentHp`/`MaxHp` getters (§3).
- **Follow:** in `LateUpdate` (mirrors `questIndicator.cs:12` `LateUpdate`) so the bar tracks the boss *after* its `Update()` move; offset `+1.2` world units above.
- **Billboard:** for a 2D orthographic camera, matching `Camera.main.transform.rotation` keeps the bar screen-aligned; if the camera never rotates (typical here) this is effectively a no-op and could be omitted — keep it guarded and cheap.
- **Pool-reset:** `OnEnable` forces `fillAmount = 1` and repositions so a recycled boss never shows a stale bar (follows the `OnEnable`-reset rule from `objectPool`/`enemyHealth`/`enemyAnimator`).
- If the bar is a **child** of the boss, it deactivates automatically when `die()`→`ret()` deactivates the boss root; `followTarget` can be the parent transform. (A child canvas moves with the parent for free — `worldOffset`+billboard are the only per-frame work.)

---

## 4. LEASH / CATCH-UP

Lives in **`bossBehaviour.Update()`** (§2). It must **only** trigger past the threshold and must **not** run alongside the chase in the same frame (the `return` after repositioning guarantees this — otherwise `MoveTowards` toward the player would immediately undo part of the teleport and jitter).

**Threshold:** `leashDistance = 14f` world units (default). Chosen so the boss can wander well off-screen (camera half-height is typically ~5 units) before snapping. Tune 10–18.

**Trigger test (cheap, no sqrt):**
```csharp
if ((transform.position - player.position).sqrMagnitude > leashDistance * leashDistance) { reposition; return; }
```

**Reposition math — `ComputeOffscreenPoint(player)`** — place the boss ~1 unit *past* the visible edge, near the player, biased toward the side the boss drifted to:
```csharp
Vector3 ComputeOffscreenPoint(Vector3 player)
{
    Camera cam = Camera.main;
    if (cam == null) return player;                       // fallback: snap onto player
    float halfH = cam.orthographicSize;                   // vertical half-extent
    float halfW = halfH * cam.aspect;                     // horizontal half-extent

    // Direction from player to the boss's current side (so it re-enters from where it wandered).
    Vector3 d = transform.position - player;
    if (d.sqrMagnitude < 0.0001f) d = Vector3.right;      // degenerate guard
    // Pick the dominant axis to choose an edge (keeps it "just off one edge", not a corner).
    Vector3 point;
    if (Mathf.Abs(d.x) * halfH >= Mathf.Abs(d.y) * halfW) // left/right edge dominates
        point = player + new Vector3(Mathf.Sign(d.x) * (halfW + leashOffscreenPad),
                                     Mathf.Clamp(d.y, -halfH, halfH), 0f);
    else                                                  // top/bottom edge dominates
        point = player + new Vector3(Mathf.Clamp(d.x, -halfW, halfW),
                                     Mathf.Sign(d.y) * (halfH + leashOffscreenPad), 0f);
    point.z = 0f;
    return point;
}
```
- Uses **`cam.orthographicSize` + `cam.aspect`** for half-extents (the confirmed ortho camera).
- Places the boss `leashOffscreenPad = 1f` unit **outside** the visible edge, on the side it drifted toward, at the player-relative position → "just off-screen but near the player."
- **Alternative simpler placement** (if edge-biasing proves fiddly): reuse `enemySpawner`'s exact viewport approach — pick a random `side (0–3)` and `cam.ViewportToWorldPoint` with `edgeMargin ≈ 0.08` (see §1). This is the same result via viewport space and keeps parity with normal spawns. Either is acceptable; the ortho-extent version above re-enters from the boss's own side, which reads better. **Chosen default: the ortho-extent version.**
- **Non-interference:** because of the early `return`, in any frame the boss *either* leashes *or* chases, never both.

---

## 5. SPAWN — `bossSpawner.cs`

One boss at scene start; recurring cadence **deferred** but hooked.

```csharp
using UnityEngine;

public class bossSpawner : MonoBehaviour
{
    [SerializeField] private GameObject bossPrefab;
    [SerializeField] private float edgeMargin = 0.08f;   // same as enemySpawner
    private GameObject activeBoss;

    void Start()
    {
        SpawnBoss();
    }

    // === FUTURE TIMED CADENCE HOOK ===
    // TODO(boss-cadence): add [SerializeField] float bossInterval + a spawnTimer in Update()
    //   (mirror enemySpawner.cs:18-21), calling SpawnBoss() on interval.
    //   Guard with the "already alive" check below so we never double-spawn.

    void SpawnBoss()
    {
        if (activeBoss != null && activeBoss.activeInHierarchy) return;  // don't spawn if one exists
        if (worldState.instance == null || worldState.instance.player == null) return;
        Camera cam = Camera.main;
        if (cam == null || bossPrefab == null) return;

        // Reuse enemySpawner off-screen-near-player math (enemySpawner.cs:25-35)
        int side = Random.Range(0, 4);
        Vector3 vp;
        if (side == 0)      vp = new Vector3(-edgeMargin, Random.value, 0f);
        else if (side == 1) vp = new Vector3(1f + edgeMargin, Random.value, 0f);
        else if (side == 2) vp = new Vector3(Random.value, -edgeMargin, 0f);
        else                vp = new Vector3(Random.value, 1f + edgeMargin, 0f);
        vp.z = Mathf.Abs(cam.transform.position.z);
        Vector3 point = cam.ViewportToWorldPoint(vp);
        point.z = 0f;

        // Boss uses Instantiate (single, distinct entity) rather than the shared enemy pool.
        activeBoss = Instantiate(bossPrefab, point, Quaternion.identity);
    }
}
```
Notes:
- **Spawns exactly one** in `Start()`. `SpawnBoss()` guards against a live boss so the future cadence loop can call it freely.
- Uses **`Instantiate`**, not `objectPool`, because there is a single test boss — simpler and avoids the boss ever being handed to code expecting normal enemies. (If cadence + pooling is wanted later, the boss can be pooled like any enemy; `enemyHealth.die()` already calls `ret()`, so pooling would "just work" once the boss prefab is added to the pool flow. Documented as an open question.)
- `activeBoss` tracks the instance for the "already exists" guard and future respawn logic.
- **Clear TODO hook** marked for timed cadence, modeled on `enemySpawner.cs:18-21`.

---

## 6. EDITOR-WIRING CHECKLIST (for a later scene-architect pass)

Create **`boss.prefab`** as a **variant of `chaser.prefab`** (`Assets/chaser.prefab`) — inherits SpriteRenderer, `enemyAnimator`, `enemyHealth`, `damageFlash`, `CircleCollider2D (isTrigger)`, sorting order:
- [ ] **Remove** `chaserBehaviour`; **add** `bossBehaviour` (`chaseSpeed = 0.7`, `leashDistance = 14`, `leashOffscreenPad = 1`).
- [ ] **`enemyHealth`**: `maxHp = 250`, `xpDropCount = 20` (keep chaser's `xpPrefab`, `bloodPrefab`, `damageNumberPrefab`; `enemyDamage` e.g. 15 for a heavier hit — tune).
- [ ] **Animator + Override Controller**: reuse `Assets/Sprites/character/chaser.overrideController`, or make `boss.overrideController` if the boss gets distinct art. Ensure `enemyAnimator.anim` points at the Animator.
- [ ] **Scale up ~1.5–2×** (`m_LocalScale`) for visibility; verify the `CircleCollider2D` radius still reads well after scaling.
- [ ] **World-space health-bar child** (§3): `HealthBarCanvas` (Canvas RenderMode=World Space, small scale ~0.01, local `y ≈ +1.2`) → `BarBG` (Image) + `BarFill` (Image, Type=Filled, Horizontal, Origin=Left). Add `bossHealthBar` on the canvas; wire `health`→boss `enemyHealth`, `fill`→`BarFill`, `followTarget`→boss root.
- [ ] **Tag/Layer**: reuse the existing enemy tag/layer so the player's contact-damage collision (which reads `enemyHealth.EnemyDamage`) still fires. Add a distinct **`Boss` tag only if** future logic needs to find/count bosses — not required now.
- [ ] **Scene**: add a `bossSpawner` GameObject; assign `bossPrefab = boss.prefab`.
- [ ] Confirm the boss's collider is `isTrigger` (chaser is `m_IsTrigger: 1`) so existing 2D trigger contact-damage applies unchanged.

---

## 7. NEW vs EDIT · Ordering · Risk

### New scripts (create)
| Script | Purpose |
|---|---|
| `bossBehaviour.cs` | Slower transform-based chase + leash/catch-up. |
| `bossHealthBar.cs` | World-space filled-Image bar; follows boss; pool/enable-safe. |
| `bossSpawner.cs` | Spawns one boss in `Start()`; TODO hook for cadence. |

### Edit (existing)
| File | Change | Blast radius |
|---|---|---|
| `enemyHealth.cs` | **Additive only:** add `public int MaxHp => maxHp;` and `public int CurrentHp => currentHp;` | Shared by ALL enemies — see risk gate. |

### Ordering
1. Edit `enemyHealth.cs` (add getters) → compile clean.
2. Create `bossBehaviour`, `bossHealthBar`, `bossSpawner` → compile clean.
3. scene-architect: build `boss.prefab` variant + health-bar child + `bossSpawner` in scene (§6).
4. build-validator: Play Mode — one boss spawns off-screen, chases slower than chasers, bar depletes on hit, leash snaps it back when it drifts >14u, dies at 250 dmg dropping ~20 orbs.

### RISK — the only shared-file edit is `enemyHealth.cs`
- **Why low-risk:** the change is **purely additive read-only getters**. No existing field, signature, serialized value, or control-flow path is touched, so normal chasers/slimes are behaviorally identical and no prefab/serialization data changes. Compile-only impact.
- **Keep it non-breaking:** do **not** rename/retype `maxHp` or `currentHp`, do **not** change `takeDamage`/`die`/`OnEnable`. Only append the two expression-bodied properties. If even that is deemed too invasive, fall back to a `bossHealth : enemyHealth` subclass — but note this still requires changing `maxHp`/`currentHp` from `private` to `protected` in the base, which is a *larger* surface than two getters; therefore the getters are preferred.
- **Second-order risk:** boss uses `Instantiate` (not the pool). Confirm nothing assumes every `enemyHealth`/collider is pooled. `enemyHealth.die()` calls `objectPool.instance.ret(gameObject)` on the boss too — `ret` handles a non-pooled object by **destroying it with a warning** (`objectPool.cs:44-49`), which is acceptable for a single Instantiated boss (it just gets destroyed on death instead of pooled). Flagged in Open Questions.

---

## 8. Open Questions (with chosen safe defaults)

1. **Boss speed** — default **0.7×** chaser (0.7 vs prefab 1.0). *Safe default set; tune 0.6–0.75 in play.*
2. **Leash distance** — default **14 world units**. *Safe default; tune 10–18 against actual camera size.*
3. **XP reward** — default **20 orbs** via `xpDropCount`. *Safe default; tune 15–30.*
4. **Contact damage** — default keep or raise `enemyDamage` to **~15** for the boss. *Safe default; product call.*
5. **Death handling with `Instantiate`** — on death `die()`→`ret()` will **destroy** the non-pooled boss (with a pool warning, `objectPool.cs:47`). Acceptable for one test boss. *If the warning is undesirable or cadence/pooling is added later, add the boss prefab to the pool flow so `ret()` recycles it instead.* **Default: accept the destroy-on-death for now.**
6. **Override controller** — reuse `chaser.overrideController` (default) vs new `boss.overrideController` for distinct art. **Default: reuse; swap when boss art exists.**
7. **Boss scale** — default **~1.5–2×**. *Verify trigger collider radius after scaling.*
8. **Billboard** — enabled but effectively a no-op for a non-rotating 2D camera. **Default: keep, cheap and future-proof.**

---

## Handoff
- **Next:** csharp-dev implements the 3 new scripts + the 2-line `enemyHealth` getter edit (design-only here; no code was written). Then scene-architect builds `boss.prefab` + scene wiring (§6), then build-validator runs the Play-Mode checks (§7).
