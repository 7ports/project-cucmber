# Down-Left Blend-Tree Freeze — Root-Cause Analysis & Ranked Fixes

**Symptom:** When the player walks down-and-left, the 2D blend-tree animation shows only the
first frame (static) instead of playing. All other 8 directions animate normally.

**Verdict (short):** The down-left slot resolves to a **defective animation clip that contains
only a single sprite frame**. It is an **EDITOR / asset fix**, not a code fix. The movement
script is correctly setting both `x` and `y` for the down-left diagonal.

---

## What I found in playerMovement.cs

File: `Assets/Scripts/playerMovement.cs` (read in full, 52 lines).

```csharp
x = Input.GetAxisRaw("Horizontal");   // returns -1, 0, or +1
y = Input.GetAxisRaw("Vertical");

if (float.IsNaN(oldX) || Mathf.Abs(x - oldX) > animThreshold) { playerAnimator.SetFloat("x", x); oldX = x; }
if (float.IsNaN(oldY) || Mathf.Abs(y - oldY) > animThreshold) { playerAnimator.SetFloat("y", y); oldY = y; }
```

- For down-left the raw axes are `x = -1`, `y = -1`. **Both** are pushed to the animator.
- The two `SetFloat` calls are **independent** and each fires whenever that axis changes by more
  than `animThreshold` (0.1). Going into down-left always changes at least one axis by 1.0, and
  the other axis is already at its correct value from the prior frame. So the animator always
  ends up with `(x=-1, y=-1)` for down-left.
- **There is no down-left-specific conditional, sign flip, clamp, or axis-zeroing.** Nothing here
  singles out down-left. If this code were the cause, *every* diagonal (or every direction) would
  be affected — not down-left alone.

➡️ **Conclusion: the movement script is not the cause.** (Ranked last below, documented only to
rule it out.)

> Side note (unrelated to this bug): the script uses the legacy `Input.GetAxisRaw`, while the
> project standard is the new Input System (`com.unity.inputsystem`). Not causing the freeze;
> worth a separate cleanup ticket.

---

## Player controller / blend-tree findings

**Player object:** GameObject `player` in `Assets/Scenes/SampleScene.unity` (line ~1416).
Its `Animator.m_Controller` → guid `f9a418c1da2ece843b2b18b1cc22cf40` =
`Assets/Sprites/character/chainsaw-guy.overrideController` (an **AnimatorOverrideController**).

**Two-layer setup:**

1. **Base controller** = `chainsaw-guy` → `m_Controller` guid `0c0b926381103d54881b822b554293fd`
   = `Assets/Sprites/character/white-guy.controller`. It contains one **Blend Tree** state driven
   by params `x` / `y`.
2. **Override controller** (`chainsaw-guy`) remaps each base "walk-*" clip to a "run-*" clip.

**Blend tree** (`white-guy.controller`, BlendTree `&3525830624122339103`):

- `m_BlendType: 1` → **2D Simple Directional**.
- 9 motion nodes; every compass direction + center is present:

  | Node | Position (x,y) | Direction | Base clip (original) |
  |---|---|---|---|
  | 1 | (0,-1)  | Down       | walk-down |
  | 2 | (1,-1)  | Down-Right | walk-down-right |
  | 3 | (0,1)   | Up         | walk-up |
  | 4 | (-1,1)  | Up-Left    | walk-up-left |
  | 5 | (-1,0)  | Left       | walk-left |
  | 6 | (1,1)   | Up-Right   | walk-up-right |
  | 7 | (1,0)   | Right      | walk-right |
  | **8** | **(-1,-1)** | **Down-Left** | **walk-down-left** ✅ exists |
  | 9 | (0,0)   | Center/Idle | idle |

  ➡️ A **Down-Left node exists at exactly (-1,-1)**, matching the exact input `(-1,-1)`. So the
  blend math gives that single node ~full weight — the tree is *selecting* the right node. The
  problem is what that node plays.

**Clip resolution for down-left (base → override):**

- Base down-left clip = `walk-down-left.anim` (guid `e645e3cf…62db5`) —
  `Assets/Sprites/character/walk-down-left.anim`.
- Override remaps it → `run-down-left.anim` (guid `7a994d0d…8778`) —
  `Assets/SmallScaleInt/Character creator - Modern/Created Spritesheets/Character_20260704_135624/run-down-left.anim`.
- **So the clip actually playing for down-left is `run-down-left.anim`.**

**Frame-count comparison (the smoking gun):**

| Clip (playing) | Sprite keyframes | m_StopTime | m_LoopTime |
|---|---|---|---|
| **run-down-left.anim (DOWN-LEFT)** | **1** | **0.0833 (1 frame @12fps)** | 0 |
| run-down.anim (down) | 15 | 1.25 | 0 |
| run-down-right.anim | 15 | 1.25 | 0 |
| run-up.anim | 15 | 1.25 | 0 |
| idle.anim | 15 | 1.25 | 0 |

➡️ **`run-down-left.anim` was baked with only ONE sprite frame.** Every other direction's run
clip has 15. That is exactly why down-left shows a single static frame while all others animate.
The spritesheet-generation pass for this one direction dropped/failed to capture its frames.

**Healthy fallback exists:** the *base/original* `walk-down-left.anim`
(`Assets/Sprites/character/walk-down-left.anim`) is **fine** — 12 sprite frames and `m_LoopTime: 1`
(looping). Only the override clip is broken.

---

## Ranked candidate fixes

### #1 — Down-left override clip has only 1 baked frame  ⟵ ROOT CAUSE  · EDITOR / asset
- **Hypothesis:** `run-down-left.anim` (the override target for the down-left node) contains a
  single sprite keyframe, so the animator has nothing to advance to and holds frame 1.
- **Confirm:** `grep -c 'value: {fileID' "…/run-down-left.anim"` → **1** (vs **15** for
  `run-down.anim` and every other run clip). `m_StopTime: 0.083` vs `1.25`. Confirmed above.
- **Fix — choose one:**
  - **(a) Fastest, no third-party edit —** In the `chainsaw-guy` override controller, **clear the
    override** on the `walk-down-left` slot (leave the Override column empty). The animator then
    falls back to the base `walk-down-left.anim`, which is a healthy 12-frame looping clip. One
    inspector change, no re-bake, and it avoids editing anything under `SmallScaleInt/`.
    (Down-left will then match the *walk* look; if the other 8 use *run* clips there will be a
    minor style mismatch — acceptable as an immediate fix, or re-bake per (b) for consistency.)
  - **(b) Proper —** Re-generate/re-bake `run-down-left.anim` so it has the full frame set (15
    frames like its siblings). Re-run the character-creator spritesheet generator
    (`SpritesheetGenerator.cs`) for the down-left run direction, or re-slice its source sheet and
    re-key the sprite curve. Note this file lives under `Assets/SmallScaleInt/…/Created
    Spritesheets/` — a *generated output* folder, but still under the third-party root, so treat
    edits there carefully / regenerate rather than hand-edit.
  - **(c) Relocate —** Bake a correct down-left run clip into `Assets/Sprites/character/` and point
    the override slot at it, keeping all authored clips out of `SmallScaleInt/`.
- **Label:** EDITOR / asset. **This is the fix to do first.**

### #2 — Wrong / misassigned clip on the down-left node  · EDITOR
- **Hypothesis:** the down-left slot points at the wrong clip.
- **Confirm:** verified the override target *is* the intended `run-down-left.anim` (not a
  mismatched direction) — so the assignment is "correct" but the target asset is defective. This
  is effectively the same defect as #1 (bad target), not a separate mis-wire. Listed for
  completeness; **fold into #1.**
- **Label:** EDITOR.

### #3 — Loop Time OFF on the run clips  · EDITOR / import  (secondary polish, not the freeze)
- **Hypothesis:** `m_LoopTime: 0` freezes on the last frame after one pass.
- **Confirm:** all run clips (down, down-right, up, idle) share `m_LoopTime: 0`, yet they visibly
  animate — so loop-off is **not** what makes down-left static (a 15-frame clip still plays its
  pass; a 1-frame clip cannot). The base walk clips use `m_LoopTime: 1`.
- **Fix:** for continuous looping while a direction is held, enable **Loop Time** on the run clips
  (import/clip setting). Do this alongside #1 for smooth held-direction movement, but it will
  **not** fix down-left on its own.
- **Label:** EDITOR / import.

### #4 — Blend type is 2D Simple Directional  · EDITOR (informational, not the cause)
- **Hypothesis:** 2D Simple Directional collapses a quadrant with no motion.
- **Confirm:** the tree already has a valid node at exactly (-1,-1) *and* the exact input is
  (-1,-1), so the down-left node gets full weight — the selection is correct. Not the cause.
- **Optional improvement:** Unity recommends **2D Freeform Directional** for full 8-way movement;
  Simple Directional can produce odd blends on off-axis inputs. Low priority; unrelated to this
  bug. Note the base controller is under `Assets/Sprites/character/` (authored, editable), not
  `SmallScaleInt/`.
- **Label:** EDITOR.

### #5 — Movement-script axis/sign bug  · CODE (playerMovement.cs)  ⟵ RULED OUT / lowest
- **Hypothesis:** `x`/`y` not both set for the down-left diagonal.
- **Confirm:** read `playerMovement.cs` in full — both axes are set independently and both reach
  `(-1,-1)` for down-left; no down-left-specific conditional exists. A code bug would affect more
  than one direction. **Not the cause.**
- **Fix:** none needed for this bug. (Separate, unrelated: migrate `Input.GetAxisRaw` → new Input
  System per project standard.)
- **Label:** CODE — no change required.

---

## Recommended next step

Apply **#1 (a)** as the immediate fix: open `Assets/Sprites/character/chainsaw-guy.overrideController`
in the Inspector and **clear the override on the `walk-down-left` slot** so the down-left node falls
back to the healthy 12-frame looping `walk-down-left.anim`. Verify in Play Mode that walking
down-left now animates. Then, for visual consistency with the other *run* directions, schedule
**#1 (b)** — re-bake `run-down-left.anim` with its full frame set — and optionally **#3** (enable
Loop Time on the run clips).

This is Editor/asset work (override-controller slot + clip re-bake) — dispatch to
**`scene-architect`** (override-controller edit + Play-Mode verify) and, for the re-bake,
**`asset-manager`** (spritesheet/clip regeneration). No `csharp-dev` change is required.

*(Analysis only — per project-planner role, no assets or scripts were modified.)*
