# Design Doc — XP Pickup-Radius Stat + Test Boss

**Date:** 2026-07-07
**Author:** project-planner
**Status:** DESIGN ONLY — do not implement from this doc directly; hand to scrum-master → csharp-dev / scene-architect.
**Scope:** Two features for the 2D top-down survivors prototype (URP 2D, legacy `UnityEngine.UI` Text/Image, legacy Input, 2D trigger colliders, non-MonoBehaviour static-instance `worldState`).

---

## 0. Ground-Truth Findings (grep-verified)

Everything below is anchored to files actually read, not assumed.

### The REAL pickup / attraction-radius mechanism

> **The XP-orb attraction radius is governed by `playerPickupRadius.cs`, a player-side polling scanner — NOT a collider, and NOT a hardcoded radius inside `pickupBehaviour`.**

- `Assets/Scripts/playerPickupRadius.cs:5` — `[SerializeField] private float radius = 4f;` is the attraction radius.
- `Assets/Scripts/playerPickupRadius.cs:10-18` — `Update()` ticks a `scanTimer`; every `scanInterval` (0.1s, line 7) it calls `scan()`.
- `Assets/Scripts/playerPickupRadius.cs:20-29` — `scan()` runs `Physics2D.OverlapCircleAll(transform.position, radius, xpLayer)`; for every hit with a `pickupBehaviour` it sets `pb.pickup = true`.
- `Assets/Scripts/pickupBehaviour.cs:5,16-19` — once `pickup == true`, the orb homes toward the player at `homeSpeed` (8f, line 6). `homeSpeed` is the **travel** speed, a separate concern from the **attraction radius**. Feature A changes ONLY the radius.
- `Assets/Scripts/pickupBehaviour.cs:21-29` — actual collection happens on `OnTriggerEnter2D` when the orb overlaps the player (independent of the scan radius). We do NOT touch collection.

**Conclusion:** the single source of truth for attraction radius today is `playerPickupRadius.radius` (line 5), read at line 22. That is the one and only consumer to migrate.

### Stat system (worldState.cs) — confirmed pattern

`Assets/Scripts/worldState.cs:9-31`: each stat is `xBase` (float) + `xMult` (float), effective value via a getter `X() => xBase * xMult`. Flat upgrades raise `xBase`; percent upgrades multiply `xMult`. Getters: `AttackDamage()`, `MoveSpeed()`, `FireRate()`, `FireCooldown()`, `Range()`, `MaxHP()`, `Defense()`, `Regen()`.

### Level-up menu (levelUpMenuController.cs) — confirmed pattern

`enum StatKind` (line 7), `enum Mode { Flat, Percent }` (line 8), `struct Upgrade` (line 10-14). `BuildPool()` (48-78) adds a Flat entry for every stat and a Percent entry unless the stat's base is 0 and inert (Defense/Regen gated, lines 61-72). `LabelFor` (80-108) and `Choose` (110-181) switch on `kind`/`mode`.

### Enemy stack — confirmed

- `chaserBehaviour.cs:5` — `chaseSpeed = 1.5f`; `Update()` (8-16) does `Vector3.MoveTowards(pos, player.position, chaseSpeed*dt)`. Pure move-toward, no animation code.
- `enemyAnimator.cs` — reads per-frame movement delta, drives Animator floats `x`/`y`; resets in `OnEnable` (16-20). Boss reuses this unchanged.
- `enemyHealth.cs:5` — `[SerializeField] private int maxHp = 3;` **already serialized** → a boss just sets 250 in the inspector; no code variant needed for HP capacity. `OnEnable` (23-26) resets `currentHp = maxHp` (pooling-safe). `die()` (42-55) drops `xpDropCount` (line 8) XP orbs via `xpPrefab` (line 7). **No public accessor exists for `currentHp`/`maxHp`** — the health bar will need read access (see Feature B / Risk section).
- `objectPool.cs` — `get()`/`ret()` toggle `SetActive`; every pooled behaviour resets in `OnEnable`. Boss health bar must follow this rule.
- `enemySpawner.cs:24-33` — off-screen spawn pattern via `cam.ViewportToWorldPoint` with an `edgeMargin`. Boss spawn + leash reuse this exact viewport math.
- `gameController.cs:13` — sets `worldState.instance.player`. `worldState.instance.player` is the player Transform used everywhere.
- `damageNumber.cs` — existing world-space floating UI (`CanvasGroup` + `Text`), pooled, resets in `OnEnable`. Confirms the project already renders `UnityEngine.UI` elements in world space that follow a position — the health bar follows the same family of pattern.

**Camera note:** the camera is a standard 2D orthographic camera looking down `-Z`. World-space sprites/canvases in the XY plane already face it; **no billboard/LookAt is required** for the health bar. (A `LookAt` would actually be wrong here — it would tilt the bar.)

---

# FEATURE A — XP Pickup Radius as an Upgradeable Stat

## A1. New worldState fields + getter

Add to `worldState.cs` alongside the other stats (after the `regen` block, ~line 22):

```csharp
public float pickupRadiusBase = 4f;   // matches current playerPickupRadius.radius default → behavior unchanged
public float pickupRadiusMult = 1f;
```

Add getter (after `Regen()`, ~line 31):

```csharp
public float PickupRadius() => pickupRadiusBase * pickupRadiusMult;
```

### Stat table (Feature A)

| Item | Value | Notes |
|---|---|---|
| `pickupRadiusBase` (default) | `4f` | **Chosen to equal `playerPickupRadius.radius` (4f) so day-1 behavior is identical.** |
| `pickupRadiusMult` (default) | `1f` | Standard. |
| Getter | `PickupRadius() => base*mult` | Effective radius in world units. |
| Flat magnitude | `+0.5` (base) | Same feel as Range's +0.5 (line 128); radius is in the same world-unit space. |
| Percent magnitude | `×1.1` (mult) | Uniform with every other percent upgrade (+10%). |
| Percent gating | **NOT gated** | Base is 4 (>0), so percent is always meaningful — unlike Defense/Regen which start at 0. |

## A2. Consumer read-sites to migrate (grep-verified, complete)

`Physics2D.OverlapCircleAll` / `radius` reads across the codebase:

| File:line | Current | Change to |
|---|---|---|
| `Assets/Scripts/playerPickupRadius.cs:22` | `Physics2D.OverlapCircleAll(transform.position, radius, xpLayer)` | `Physics2D.OverlapCircleAll(transform.position, worldState.instance != null ? worldState.instance.PickupRadius() : radius, xpLayer)` |
| `Assets/Scripts/playerPickupRadius.cs:33` (gizmo) | `Gizmos.DrawWireSphere(transform.position, radius)` | Optional: leave as-is (editor-only, uses serialized fallback). Harmless. |

**That is the ONLY functional consumer.** Grep for `radius`, `OverlapCircle`, `pickupRadius`, and `playerPickupRadius` returned no other read-sites. The serialized `radius` field (line 5) is kept as a **fallback** for when `worldState.instance` is null (e.g. entering Play Mode before `gameController.Start`), mirroring the `projectileBehaviour.cs:21` pattern (`worldState.instance != null ? worldState.instance.Range() : fallbackRange`).

**No renaming of the serialized field** — keeping `radius` as fallback avoids a serialized-data migration in the player prefab/scene.

## A3. levelUpMenuController.cs changes

1. **enum** (line 7): add `PickupRadius`:
   ```csharp
   private enum StatKind { MaxHP, FireRate, AttackDamage, MoveSpeed, Range, Defense, Regen, PickupRadius }
   ```
2. **BuildPool()** `stats[]` array (lines 50-59): add `StatKind.PickupRadius`. No gating branch needed (base > 0), so it naturally gets both Flat and Percent entries.
3. **LabelFor()** — add to the Flat switch (after line 92):
   ```csharp
   case StatKind.PickupRadius: return "+0.5 Pickup Radius";
   ```
   and to the Percent switch (after line 105):
   ```csharp
   case StatKind.PickupRadius: return "+10% Pickup Radius";
   ```
4. **Choose()** — add to the Flat switch (after line 142):
   ```csharp
   case StatKind.PickupRadius:
       worldState.instance.pickupRadiusBase += 0.5f;
       break;
   ```
   and to the Percent switch (after line 173):
   ```csharp
   case StatKind.PickupRadius:
       worldState.instance.pickupRadiusMult *= 1.1f;
       break;
   ```

## A4. pauseStatsView.cs display line

`pauseStatsView.cs:14-19` builds the stats string. Append a Pickup Radius line (after Range, line 19):

```csharp
string s = "Level: " + worldState.instance.level + "\n" +
           "HP: " + worldState.instance.currentHP + "/" + worldState.instance.MaxHP() + "\n" +
           "Damage: " + worldState.instance.AttackDamage().ToString("0.0") + "\n" +
           "Move Speed: " + worldState.instance.MoveSpeed().ToString("0.0") + "\n" +
           "Fire Rate: " + fireRate.ToString("0.0") + "/s" + "\n" +
           "Range: " + worldState.instance.Range().ToString("0.0") + "\n" +
           "Pickup Radius: " + worldState.instance.PickupRadius().ToString("0.0");
```

No layout change needed — `statsText` is a single multi-line `Text`.

---

# FEATURE B — Test Boss

**User-fixed decisions (not re-litigated):** spawn exactly ONE test boss at scene start; recurring cadence DEFERRED (leave a hook); health bar is a WORLD-SPACE bar floating above the boss that follows it and depletes with HP.

## B1. Movement — recommendation: NEW `bossBehaviour.cs`

**Do NOT reuse `chaserBehaviour` as-is.** `chaserBehaviour` is a bare move-toward with only `chaseSpeed`. The boss needs two extra responsibilities — a distinct (slower) speed AND the catch-up leash — and mixing leash logic into the shared chaser would risk regressing every normal enemy. A dedicated `bossBehaviour` keeps the chaser untouched and colocates leash logic with the only thing that uses it.

`bossBehaviour` duplicates the ~3 lines of chase movement (trivial) and adds the leash. Animation is **not** duplicated — the boss keeps a separate `enemyAnimator` component, exactly like the chaser, so movement drives the Animator floats automatically.

### Speed

| Stat | Chaser | Boss | Rationale |
|---|---|---|---|
| Move speed | `1.5f` (chaser line 5) | **`1.1f`** | "Slightly slower" ≈ 75% of chaser. Slow enough to feel heavy/menacing, fast enough that the leash rarely triggers during normal play. Serialize it so it's tunable. |

### bossBehaviour.cs spec

```csharp
using UnityEngine;

public class bossBehaviour : MonoBehaviour
{
    [SerializeField] private float chaseSpeed = 1.1f;          // slightly slower than chaser's 1.5
    [SerializeField] private float leashDistance = 18f;         // world units from player before catch-up
    [SerializeField] private float leashEdgePadding = 1.0f;     // how far past the visible edge to place the boss

    void Update()
    {
        if (worldState.instance == null || worldState.instance.player == null) return;
        Vector3 playerPos = worldState.instance.player.position;

        // Catch-up leash: if too far, reposition just off-screen near the player.
        if ((transform.position - playerPos).sqrMagnitude > leashDistance * leashDistance)
        {
            transform.position = OffscreenPointNearPlayer(playerPos);
            return; // skip this frame's move; resume chasing next frame
        }

        transform.position = Vector3.MoveTowards(transform.position, playerPos, chaseSpeed * Time.deltaTime);
    }

    // Places the boss just outside the camera view, on the side it was already on relative to the player.
    private Vector3 OffscreenPointNearPlayer(Vector3 playerPos)
    {
        Camera cam = Camera.main;
        Vector3 dir = (transform.position - playerPos);
        if (dir.sqrMagnitude < 0.0001f) dir = Vector3.right; // degenerate guard
        dir.z = 0f; dir = dir.normalized;

        if (cam == null || !cam.orthographic)
        {
            // Fallback: fixed distance in the current direction.
            return playerPos + dir * (leashDistance * 0.5f);
        }

        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;
        // Distance from camera center along dir to the viewport edge, then a touch beyond.
        // Scale dir so it lands just past the rectangular frustum edge.
        float tx = halfW / Mathf.Max(Mathf.Abs(dir.x), 0.0001f);
        float ty = halfH / Mathf.Max(Mathf.Abs(dir.y), 0.0001f);
        float edge = Mathf.Min(tx, ty) + leashEdgePadding;

        Vector3 camPos = cam.transform.position;
        Vector3 p = new Vector3(camPos.x, camPos.y, 0f) + dir * edge;
        p.z = 0f;
        return p;
    }
}
```

Notes:
- Leash is measured from the **player**, but the off-screen point is measured from the **camera center** (which normally tracks the player) so "just off-screen" is exact. If the camera lags the player, `leashEdgePadding` absorbs the slack.
- Placing on the side the boss was already on ("side toward its current position") keeps the re-engage direction intuitive; a random edge is an alternative but feels arbitrary. Recommendation: keep the "same-side" math above.
- `leashDistance = 18f` default: comfortably larger than a screen half-diagonal for a typical ortho size (~5), so it only fires when the boss genuinely falls behind, not during normal play.

## B2. Health — 250 HP, XP on death

- **No new field needed for capacity.** `enemyHealth.maxHp` is already `[SerializeField]` (`enemyHealth.cs:5`). Boss prefab sets `maxHp = 250` in the inspector. `OnEnable` resets `currentHp = maxHp`, so it is pooling-safe.
- **XP on death:** reuse the existing `xpPrefab` + `xpDropCount` (`enemyHealth.cs:7-8,44-48`). Set **`xpDropCount = 20`** on the boss prefab (drops 20 orbs of the standard `xpValue=1` prefab). Rationale: a 250-HP boss is a big investment; 20 orbs ≈ a meaningful level-up burst without a bespoke "big XP orb" prefab. (Open to tuning — see Open Questions.)
- **Accessor gap (shared-file edit):** `enemyHealth` exposes neither `currentHp` nor `maxHp` publicly. The health bar must read the live ratio. Add read-only accessors to `enemyHealth.cs`:
  ```csharp
  public int MaxHp => maxHp;
  public int CurrentHp => currentHp;
  ```
  This is additive and touches a shared file (see Risk section). No behavior change for existing enemies.

## B3. World-space health bar

**Recommendation: two `SpriteRenderer`s (bg + fill) as children of the boss**, NOT a world-space Canvas.

Why sprites over a Canvas here: a fill bar is a pure scale-the-fill operation; two SpriteRenderers need no Canvas/GraphicRaycaster overhead, no camera-render-mode wiring, and scale-from-left is a clean transform op. (The `damageNumber` Canvas pattern exists for text; a bar is simpler as sprites.) Both approaches face the ortho camera automatically.

### Prefab structure

```
boss (root)
 ├─ (Animator + enemyAnimator + enemyHealth[maxHp=250, xpDropCount=20] + bossBehaviour + SpriteRenderer[boss art] + Collider2D + Rigidbody2D as chaser has)
 └─ HealthBar            (empty child at local +Y offset above the sprite, e.g. (0, 1.2, 0))
      ├─ Background      (SpriteRenderer — dark/red backing, full width)
      └─ Fill            (SpriteRenderer — green/bright, anchored so scaling X shrinks from the right)
```

**Anchor-left technique for the fill:** a plain SpriteRenderer scales about its center. To make the bar deplete from one end, put the `Fill` **sprite pivot at its left edge** (import setting) OR nest it: `Fill` parent (pivot point) → `Fill` sprite offset by +halfWidth so scaling the parent's X shrinks toward the left. Simplest for csharp-dev + scene-architect: set the Fill sprite's pivot to Left in import settings, then scale `Fill.localScale.x` from 1→0.

### bossHealthBar.cs spec

Lives on the `HealthBar` child (or the boss root — see fields). Reads the boss's `enemyHealth` each frame and scales `Fill`.

```csharp
using UnityEngine;

public class bossHealthBar : MonoBehaviour
{
    [SerializeField] private enemyHealth health;        // the boss's enemyHealth (assign in prefab)
    [SerializeField] private Transform fill;            // the Fill child transform (scaled on X)
    [SerializeField] private float fullWidth = 1f;      // Fill localScale.x when at full HP
    [SerializeField] private Vector3 followOffset = new Vector3(0f, 1.2f, 0f); // above the boss
    [SerializeField] private bool hideWhenFull = false; // optional: only show once damaged

    private Vector3 baseFillScale;

    void Awake()
    {
        if (health == null) health = GetComponentInParent<enemyHealth>();
        if (fill != null) baseFillScale = fill.localScale;
    }

    void OnEnable()   // pooling-safe reset — boss is a pooled/SetActive object family
    {
        ApplyRatio(1f); // enemyHealth.OnEnable resets currentHp=maxHp; show full immediately
    }

    void LateUpdate()
    {
        if (health == null) return;

        // Follow the boss (if this script sits on a child, offset is already baked via parenting;
        // if it sits on the root, position the bar node here). Keeping bar upright — no billboard
        // needed for a 2D ortho camera looking down -Z.
        // (If HealthBar is a parented child at followOffset, you can omit position code entirely.)

        int max = Mathf.Max(1, health.MaxHp);
        float ratio = Mathf.Clamp01((float)health.CurrentHp / max);
        ApplyRatio(ratio);
    }

    private void ApplyRatio(float ratio)
    {
        if (fill == null) return;
        Vector3 s = baseFillScale;
        s.x = baseFillScale.x * ratio;   // fill sprite has LEFT pivot → depletes from the right
        fill.localScale = s;

        if (hideWhenFull)
        {
            bool show = ratio < 0.999f;
            if (fill.gameObject.activeSelf != show) fill.gameObject.SetActive(show);
        }
    }
}
```

Field/behavior summary:
- `health` — the boss `enemyHealth`; auto-found via `GetComponentInParent` if unset.
- `fill` — the Fill transform, scaled on X.
- `fullWidth` / `baseFillScale` — captured at Awake so we scale relative to the prefab's authored size.
- `followOffset` — if `HealthBar` is a static child, parenting handles following for free; keep the field for the root-mounted variant.
- **Pooling:** `OnEnable` re-shows full; because `enemyHealth.OnEnable` resets `currentHp`, and `LateUpdate` recomputes every frame, a re-used boss instance always shows correct HP.
- **Camera facing:** none needed (2D ortho). Explicitly documented so csharp-dev does NOT add a `LookAt` that would tilt the bar.

## B4. Spawn — single test boss, cadence deferred

New `bossSpawner.cs` MonoBehaviour on a scene GameObject:

```csharp
using UnityEngine;

public class bossSpawner : MonoBehaviour
{
    [SerializeField] private GameObject bossPrefab;
    [SerializeField] private float edgeMargin = 0.08f; // matches enemySpawner off-screen margin

    void Start()
    {
        SpawnOne();

        // TODO(cadence): recurring boss spawns are DEFERRED to a later batch.
        // When implementing, add a timer in Update() gated on worldState (e.g. every N seconds
        // or every M levels) that calls SpawnOne(). Hook intentionally left here.
    }

    private void SpawnOne()
    {
        if (bossPrefab == null) return;
        Vector3 point = OffscreenSpawnPoint();
        // Use objectPool so the boss participates in the pooling lifecycle like other enemies.
        if (objectPool.instance != null)
            objectPool.instance.get(bossPrefab, point, Quaternion.identity);
        else
            Instantiate(bossPrefab, point, Quaternion.identity);
    }

    private Vector3 OffscreenSpawnPoint()
    {
        Camera cam = Camera.main;
        if (cam == null || worldState.instance == null || worldState.instance.player == null)
            return Vector3.zero;

        // Same viewport-edge technique as enemySpawner.cs:24-33.
        int side = Random.Range(0, 4);
        Vector3 vp;
        if (side == 0)      vp = new Vector3(-edgeMargin, Random.value, 0f);
        else if (side == 1) vp = new Vector3(1f + edgeMargin, Random.value, 0f);
        else if (side == 2) vp = new Vector3(Random.value, -edgeMargin, 0f);
        else                vp = new Vector3(Random.value, 1f + edgeMargin, 0f);
        vp.z = Mathf.Abs(cam.transform.position.z);
        Vector3 point = cam.ViewportToWorldPoint(vp);
        point.z = 0f;
        return point;
    }
}
```

Notes:
- Spawns exactly one boss in `Start()`. The cadence hook is a clearly-marked `TODO(cadence)` comment — future batch adds an `Update()` timer without restructuring.
- Uses `objectPool.instance.get` so a boss returned via `enemyHealth.die()` (`objectPool.ret`) can be re-served — consistent with all other enemies and required for the health bar's `OnEnable` reset to matter.
- Off-screen-near-player spawn via the exact `enemySpawner` viewport math.

## B5. Prefab / Editor wiring checklist (for a later scene-architect pass)

Create `boss.prefab` derived from the chaser prefab:

- [ ] Duplicate chaser prefab → `boss.prefab`.
- [ ] Scale root up for visibility (e.g. `localScale = 1.6–2.0`); confirm Collider2D/Rigidbody2D scale sensibly.
- [ ] Assign boss art sprite + Animator **override controller** (distinct boss visuals; `enemyAnimator` drives `x`/`y` unchanged).
- [ ] `enemyHealth`: set `maxHp = 250`, `xpDropCount = 20`, keep `xpPrefab`, `bloodPrefab`, `damageNumberPrefab`, `enemyDamage` (tune boss contact damage as desired).
- [ ] Remove `chaserBehaviour`, add `bossBehaviour` (`chaseSpeed = 1.1`, `leashDistance = 18`, `leashEdgePadding = 1.0`).
- [ ] Add `HealthBar` child at local `(0, 1.2, 0)` (above sprite) with `Background` + `Fill` SpriteRenderers; set Fill sprite pivot to **Left**; put bar sprites on a sorting layer/order that renders above the boss.
- [ ] Add `bossHealthBar` (assign `health` = boss `enemyHealth`, `fill` = Fill transform).
- [ ] Ensure `pooledObject` marker compatibility (objectPool adds it automatically on first `get`).
- [ ] Create a `BossSpawner` GameObject in `SampleScene`, add `bossSpawner`, assign `bossPrefab = boss.prefab`.
- [ ] Verify boss XP layer / collision layers match normal enemies (projectile hits, player contact).

---

## Ordering / Risk — shared-file edits (group these)

Compile-order matters because several edits touch the same shared files. Group by file so csharp-dev makes one coherent pass per file.

| File | Feature | Change | Risk |
|---|---|---|---|
| `worldState.cs` | A | +2 fields, +1 getter | **Low** — additive; nothing references new members until levelUp/pauseStats/playerPickupRadius are updated. Safe to land first. |
| `levelUpMenuController.cs` | A | enum + pool + LabelFor + Choose | **Low** — must land **after** worldState fields exist (references `pickupRadiusBase/Mult`). Compile-breaks if worldState not updated first. |
| `pauseStatsView.cs` | A | +1 display line | **Low** — references `PickupRadius()`; land after worldState. |
| `playerPickupRadius.cs` | A | migrate line 22 read-site | **Low** — references `PickupRadius()`; land after worldState. Keeps `radius` as fallback → no prefab migration. |
| `enemyHealth.cs` | B | +2 public accessors (`MaxHp`, `CurrentHp`) | **Low but SHARED** — additive, no behavior change for existing enemies; required by `bossHealthBar`. Land before/with bossHealthBar. |
| `bossBehaviour.cs` | B | NEW file | **None** — new file, no existing references. |
| `bossHealthBar.cs` | B | NEW file | **Depends on** `enemyHealth` accessors — land after that edit. |
| `bossSpawner.cs` | B | NEW file | **None** — new file; depends only on existing `objectPool`/`enemySpawner` patterns. |

**Recommended landing order:**
1. `worldState.cs` (Feature A foundation) — must be first.
2. `levelUpMenuController.cs`, `pauseStatsView.cs`, `playerPickupRadius.cs` (any order, all after step 1).
3. `enemyHealth.cs` accessors.
4. New files: `bossBehaviour.cs`, `bossHealthBar.cs`, `bossSpawner.cs` (after step 3).
5. Editor/prefab wiring (scene-architect) — after all scripts compile.

**No destructive changes.** Every code edit is additive; the only migrated read-site (`playerPickupRadius.cs:22`) retains its serialized fallback, so a null `worldState.instance` during early Play Mode still works.

---

## Open Questions (with chosen safe defaults)

1. **Boss XP drop amount** — chose `xpDropCount = 20` (20 standard orbs). Alternative: a dedicated high-value orb prefab. *Default stands; tune after playtest.*
2. **Boss speed** — chose `1.1f` (~73% of chaser 1.5). *Default stands; serialized for tuning.*
3. **Leash distance** — chose `18f` world units. Depends on the camera's `orthographicSize` (unread — no camera settings in scripts). *Default is generous; verify against actual ortho size in the scene during wiring and adjust.*
4. **Leash re-entry side** — chose "same side the boss is already on" over a random edge. *Default stands (more intuitive re-engage).*
5. **Health bar tech** — chose two SpriteRenderers over a world-space Canvas. If the team prefers UI consistency with `damageNumber`, a world-space Canvas + two `Image`s is a drop-in alternative (scale the Fill's RectTransform width instead of `localScale.x`). *Sprite default stands for simplicity.*
6. **Pickup-radius flat magnitude** — chose `+0.5` (mirrors Range). If pickup radius should scale faster than combat range, `+0.75/+1.0` is reasonable. *Default stands.*
7. **Does the boss pool or destroy on death?** — chose pool via `objectPool` (consistent with all enemies, and needed for `OnEnable` health-bar reset). Since only one boss spawns now, pooling is harmless. *Default stands.*

---

## Handoff

- **Feature A** → `csharp-dev` (5 files: worldState, levelUpMenuController, pauseStatsView, playerPickupRadius — all additive/low-risk).
- **Feature B scripts** → `csharp-dev` (enemyHealth accessors + 3 new files).
- **Feature B prefab/scene wiring** → `scene-architect` (boss.prefab derivation, HealthBar child, BossSpawner GameObject) — Editor-side, cannot run in Docker.
- **Validation** → `build-validator` after scripts compile: confirm no console errors, boss spawns once at scene start, health bar depletes on hits, boss re-engages after being outrun, and a Pickup Radius upgrade appears in the level-up menu.
