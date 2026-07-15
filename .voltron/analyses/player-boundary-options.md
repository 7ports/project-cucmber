# Player Boundary — Design Options (DESIGN ONLY, no implementation)

**Project:** project-cucumber (2D top-down survivors, Unity 6000.4.2f1, URP 2D)
**Branch:** slot-machine-levlup
**Problem:** Prevent the player from walking off the edges of the map.
**User's framing:** "prevent them from walking off a floor tile" OR "make it loop back on itself" — wants a few options to choose between.

## Grounding — how movement works today

`Assets/Scripts/playerMovement.cs` (global namespace, legacy `Input.GetAxisRaw` despite the `using UnityEngine.InputSystem;` import — legacy Input still drives it):

- `Update()` (l.21–39) reads `x`/`y` from `Input.GetAxisRaw` and feeds the Animator.
- `FixedUpdate()` (l.42–51) computes
  `delta = new Vector2(x, y).normalized * Time.fixedDeltaTime * worldState.instance.MoveSpeed();`
  then applies `_rb.MovePosition(_rb.position + delta)` (l.48).
- The player is a **Dynamic Rigidbody2D** (`_rb`, cached in `Start()`).

This single `MovePosition` call is the one and only place player position is written, so every option below hooks there or around it.

> ⚠ **Hard constraint:** the placeholder **Walls Tilemap is being cleared** by the user (walls will be redrawn later). No option here may depend on the existing placeholder wall tiles or their colliders. Each option stands on its own.

---

## Option A — Clamp position to a rectangular play-area (RECOMMENDED: simplest)

**What:** Each `FixedUpdate`, after (or instead of) computing the target position, clamp X and Y to serialized `minX/maxX/minY/maxY` bounds so the Rigidbody can never be moved outside the rectangle.

**How it hooks in:** In `playerMovement.FixedUpdate`, compute `Vector2 target = _rb.position + delta;` then
`target.x = Mathf.Clamp(target.x, _minX, _maxX); target.y = Mathf.Clamp(target.y, _minY, _maxY);`
and `MovePosition(target)`. Bounds are `[SerializeField]` floats (or a single serialized `Rect`/`Bounds`) set in the inspector. Optionally expose them via `worldState` if other systems (spawners, camera) need the same rectangle.

**Pros:**
- Fewest moving parts; no new colliders, no Tilemap dependency, no scene geometry.
- Deterministic — player physically cannot exit; no tunneling at high speed.
- Trivial to tune (type numbers in inspector); easy to visualize with `OnDrawGizmos`.

**Cons:**
- Rectangle only (no irregular map shapes) without extra work.
- Bounds are hand-authored numbers — must be kept in sync if the map size changes.
- Clamps *position*, not velocity; feels like a hard invisible wall (fine for this genre).

**Effort:** **S** — a few lines in one file + inspector values.
**Script vs Editor:** **Both** — script edit (playerMovement.cs) + set bounds in inspector. No new GameObjects.
**Files touched:** `Assets/Scripts/playerMovement.cs` (+ optional `worldState.cs` if bounds are shared).

---

## Option B — Invisible solid perimeter colliders (four edge walls)

**What:** Four static `BoxCollider2D` (or `EdgeCollider2D`) "walls" arranged as a rectangle enclosing the play area. Because the player is a Dynamic Rigidbody2D, the physics solver blocks it — no script change at all.

**How it hooks in:** No change to `playerMovement.cs`. In the scene, create a `Boundary` parent GameObject with four child colliders on a solid (non-trigger) layer that collides with the Player layer per the Physics2D matrix. Movement still uses `MovePosition`; the solver resolves penetration.

**Pros:**
- Zero script changes — pure scene/Editor work; keeps movement code untouched.
- Reuses Unity physics; also stops enemies/projectiles if desired (layer-dependent).
- Easy to reshape later (add angled/extra colliders for non-rectangular arenas).

**Cons:**
- `MovePosition` on a Dynamic body can **jitter or tunnel** against static walls at high `MoveSpeed` / low tick rate (depenetration happens next physics step). May need thicker colliders or continuous collision detection.
- Introduces physical contact — a fast player can "stick"/slide along walls; contact could interact with other Rigidbody logic.
- Requires the Physics2D layer matrix to be correct (Player ↔ Boundary must collide); easy to misconfigure.

**Effort:** **S–M** — quick to place, but tuning against MovePosition jitter can add time.
**Script vs Editor:** **Editor only** (scene work). Optionally a tiny helper script to auto-size walls to the play area.
**Files touched:** `Assets/Scenes/SampleScene.unity` (new Boundary GameObject + colliders). No script required.

---

## Option C — Wrap-around / loop (exit one edge, appear on the opposite edge)

**What:** When the player crosses a boundary, teleport them to the opposite side (toroidal map), matching the user's "loop back on itself" idea.

**How it hooks in:** In `playerMovement.FixedUpdate` after `MovePosition`, or in a dedicated `worldWrap` component, test `_rb.position` against `minX/maxX/minY/maxY`; if past an edge, set the crossing axis to the opposite bound (`_rb.position = wrapped;` via `MovePosition`/`_rb.position`). Bounds are serialized like Option A.

**Pros:**
- Distinct gameplay feel (endless arena); no invisible wall the player bumps into.
- Also rectangle-bounded and script-only; no Tilemap dependency.
- Small, self-contained logic.

**Cons:**
- **Only cosmetically "keeps player in"** — enemies, projectiles, XP pickups, off-screen indicators, and the camera all assume a bounded world; wrapping the player alone looks broken (enemies don't wrap, camera jumps). Wrapping *everything* is a much bigger change (L).
- Teleport can desync homing/leash logic (boss catch-up leash, XP homing) and camera follow (Cinemachine snap).
- Not what most survivors-likes do; may feel disorienting.

**Effort:** **M** for player-only wrap; **L** if the whole world must wrap to feel right.
**Script vs Editor:** **Both** — script (new/edited) + inspector bounds; camera/Cinemachine likely needs tuning.
**Files touched:** `Assets/Scripts/playerMovement.cs` (or new `worldWrap.cs`), camera config in `SampleScene.unity`; potentially spawner/enemy scripts if world-wrap.

---

## Option D — Floor-tile-gated movement (walkability check against the ground Tilemap)

**What:** Before committing a move, sample the **ground/floor** Tilemap at the target cell; if that cell is empty (not a floor tile), reject or slide the movement — the player literally cannot step off the floor. Matches the user's "prevent walking off a floor tile" idea.

**How it hooks in:** `playerMovement` (or a `tilemapWalkability` helper) gets a serialized reference to the **floor** `Tilemap`. In `FixedUpdate`, compute `target`, convert with `tilemap.WorldToCell(target)`, and check `tilemap.HasTile(cell)`. If no tile, clamp to the current cell or zero the offending axis (axis-separated check gives wall-sliding). Uses the *floor* map, **not** the placeholder walls being cleared.

**Pros:**
- Supports **arbitrary map shapes** (non-rectangular arenas, cut-outs, corridors) for free.
- Boundary always matches what the player visually sees as floor — self-maintaining as the map is redrawn.
- No invisible colliders; independent of the deleted walls Tilemap.

**Cons:**
- Most complex: cell sampling, player-radius handling (checking one center cell lets corners clip; need to sample the player's footprint / multiple points), and axis-separated sliding to avoid getting stuck.
- Couples movement to Tilemap layout and cell size; needs a correctly-authored, gap-free floor map (holes = invisible walls).
- Slightly more per-frame cost (tile lookups) — negligible at this scale but non-zero.

**Effort:** **M–L** — depends on how robust the footprint/sliding handling needs to be.
**Script vs Editor:** **Both** — script (walkability logic) + assign the floor Tilemap reference in inspector.
**Files touched:** `Assets/Scripts/playerMovement.cs` (or new `tilemapWalkability.cs`); floor Tilemap reference wired in `SampleScene.unity`.

---

## Option E — Composite collider from the floor Tilemap outline (bonus)

**What:** Add a `TilemapCollider2D` + `CompositeCollider2D` to the **floor** Tilemap so its outer edge becomes one solid physics boundary; the Dynamic player collides with the arena perimeter automatically.

**How it hooks in:** Editor-only. On the floor Tilemap GameObject, add `TilemapCollider2D`, `CompositeCollider2D` (Geometry Type = Polygons/Outlines), and a static `Rigidbody2D`. Player blocked by physics; no `playerMovement.cs` change. Independent of the deleted walls map (uses floor tiles).

**Pros:**
- Arbitrary map shape like Option D, but handled by the physics engine (no custom sampling code).
- Regenerates automatically when the floor map is redrawn.
- No script changes.

**Cons:**
- Same `MovePosition`-vs-static jitter/tunneling risk as Option B at high speed.
- If floor tiles have interior gaps, they become collidable pockets; need a clean floor outline.
- Composite collider geometry regen can be a minor cost when tiles change at runtime (not a concern if the map is static).

**Effort:** **S–M** — mostly component setup.
**Script vs Editor:** **Editor only.**
**Files touched:** `Assets/Scenes/SampleScene.unity` (components on the floor Tilemap). No script.

---

## Quick comparison

| Option | Approach | Shape support | New colliders | Script change | Effort | Notes |
|---|---|---|---|---|---|---|
| **A. Clamp** | Clamp position to rect each FixedUpdate | Rectangle | No | Yes | **S** | **Simplest & most robust** |
| **B. Perimeter walls** | 4 invisible solid colliders | Rectangle (+extensible) | Yes | No | S–M | Editor-only; watch MovePosition jitter |
| **C. Wrap-around** | Teleport to opposite edge | Rectangle | No | Yes | M (L if world-wrap) | Player-only wrap looks broken; enemies/camera don't wrap |
| **D. Floor-tile-gated** | Reject moves onto non-floor cells | **Any shape** | No | Yes | M–L | Best fit for "don't walk off a floor tile"; needs footprint/slide care |
| **E. Floor composite collider** | Physics collider from floor outline | **Any shape** | Yes (auto) | No | S–M | Editor-only version of D; same jitter risk as B |

## Recommendation framing (user decides)

- **Simplest / most robust:** **Option A (Clamp)** — one small script change, no physics edge cases, no scene geometry, no Tilemap dependency. Best if a rectangular arena is acceptable.
- **Closest to "don't walk off a floor tile":** **Option D** (script walkability) or **Option E** (composite collider) — both support irregular maps and key off the floor map that survives the walls-clear.
- **Closest to "loop back on itself":** **Option C**, but be aware player-only wrap feels broken; a true toroidal world is a large change.
- **Zero-code:** **Option B** or **Option E**, at the cost of `MovePosition`-vs-static jitter tuning.

All five are decomposition-ready; file targets are listed per option above. No approach is chosen here — this is for the user to pick.
