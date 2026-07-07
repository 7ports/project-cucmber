# Design: Lock Shooter Aim Direction at AIM Entry

**Date:** 2026-07-07
**Feature (verbatim):** "when the shooter takes aim, the direction in which its aiming shouldn't change after it pauses and choses the initial angle"
**Target file (only):** `Assets/Scripts/shooterBehaviour.cs` (96 lines)
**Scope:** DESIGN ONLY — hand off to `@agent-csharp-dev` for implementation.

---

## 1. Interpretation

The shooter runs `Chase -> Aim -> Fire -> Cooldown`. Today:

- The telegraph line is redrawn every frame during `Aim` **toward the player's live position**, so it rotates to track the player during the telegraph pause.
- `aimDir` is (re)computed at the **Fire instant** from the player's then-current position.

Desired behavior: the moment the shooter **enters `Aim`** it picks its angle **once**, and that angle drives (2) the telegraph for the whole pause and (3) the projectile. Player movement during the pause must **not** change the aim.

---

## 2. Ground-truth anchors (`shooterBehaviour.cs`)

| Concern | Location | Current code |
|---|---|---|
| `aimDir` field declaration | `shooterBehaviour.cs:19` | `private Vector2 aimDir;` |
| `aimDir` reset on enable | `shooterBehaviour.cs:27` | `aimDir = Vector2.right;` |
| Chase → Aim transition (angle should be locked HERE) | `shooterBehaviour.cs:41` | `if (dist <= aimRange) { state = State.Aim; stateTimer = 0f; EnableTelegraph(true); }` |
| Telegraph drawn live toward player during Aim | `shooterBehaviour.cs:45` | `UpdateTelegraph(transform.position, playerPos);` |
| `aimDir` recomputed at Fire instant (BUG source) | `shooterBehaviour.cs:51` | `aimDir = ((Vector2)(playerPos - transform.position)).normalized;` |
| Fire reads `aimDir` | `shooterBehaviour.cs:52`, `:70` | `FireProjectile();` → `proj.Launch(aimDir, projectileSpeed);` |
| Telegraph draw helper (endpoint = `to`) | `shooterBehaviour.cs:75-81` | `SetPosition(1, to);` |

**Key insight:** `FireProjectile()` (line 70) already reads the `aimDir` field. So the fix is entirely about *when* `aimDir` is set (once, at Aim entry) and making the telegraph render along that fixed vector instead of toward `playerPos`. No change to `FireProjectile` itself is required.

---

## 3. Precise edits (exact drop-in C#)

### Edit A — Capture `aimDir` ONCE at Chase → Aim transition (line 41)

Compute and freeze the direction the instant the shooter enters `Aim`.

**BEFORE** (`shooterBehaviour.cs:41`):
```csharp
                if (dist <= aimRange) { state = State.Aim; stateTimer = 0f; EnableTelegraph(true); }
```

**AFTER:**
```csharp
                if (dist <= aimRange)
                {
                    aimDir = ((Vector2)(playerPos - transform.position)).normalized;
                    if (aimDir == Vector2.zero) aimDir = Vector2.right; // guard: player exactly on top
                    state = State.Aim; stateTimer = 0f; EnableTelegraph(true);
                }
```

### Edit B — Telegraph renders along the FIXED `aimDir`, not toward the player (line 45)

The telegraph endpoint must be derived from the locked `aimDir`, so it stops tracking the player during the pause.

**BEFORE** (`shooterBehaviour.cs:44-48`):
```csharp
            case State.Aim:
                UpdateTelegraph(transform.position, playerPos);
                stateTimer += Time.deltaTime;
                if (stateTimer >= aimDuration) state = State.Fire;
                break;
```

**AFTER:**
```csharp
            case State.Aim:
                UpdateTelegraph(transform.position, transform.position + (Vector3)(aimDir * aimRange));
                stateTimer += Time.deltaTime;
                if (stateTimer >= aimDuration) state = State.Fire;
                break;
```

> `aimDir * aimRange` gives the telegraph a stable visible length. `UpdateTelegraph` (lines 75-81) is unchanged — it just draws `from`→`to`. The shooter may still be stationary during Aim (it does not move in the Aim case), so `transform.position` for endpoint 0 stays correct.

### Edit C — Remove the Fire-instant recompute (line 51)

`aimDir` is now already locked from Edit A; recomputing here re-introduces the exact bug.

**BEFORE** (`shooterBehaviour.cs:50-55`):
```csharp
            case State.Fire:
                aimDir = ((Vector2)(playerPos - transform.position)).normalized;
                FireProjectile();
                EnableTelegraph(false);
                state = State.Cooldown; stateTimer = 0f;
                break;
```

**AFTER:**
```csharp
            case State.Fire:
                FireProjectile();
                EnableTelegraph(false);
                state = State.Cooldown; stateTimer = 0f;
                break;
```

### Edit D — (No change) FireProjectile already uses the locked `aimDir`

`shooterBehaviour.cs:65-71` is correct as-is; `proj.Launch(aimDir, projectileSpeed)` consumes the field set in Edit A. **Do not modify.** Listed only to confirm the projectile fires along the locked direction.

---

## 4. Grep-based acceptance checklist (csharp-dev self-validation)

Run from repo root. All must pass.

```bash
# 1. aimDir is captured inside the Chase→Aim transition block (exactly one assignment there).
grep -nA3 'if (dist <= aimRange)' Assets/Scripts/shooterBehaviour.cs | grep -c 'aimDir ='
# EXPECT: 1

# 2. The Fire-instant recompute is GONE (no aimDir assignment adjacent to FireProjectile).
grep -nB2 'FireProjectile();' Assets/Scripts/shooterBehaviour.cs | grep -c 'aimDir ='
# EXPECT: 0

# 3. Telegraph in Aim no longer draws to the live player position.
grep -n 'UpdateTelegraph(transform.position, playerPos)' Assets/Scripts/shooterBehaviour.cs
# EXPECT: no output (exit 1)

# 4. Telegraph now draws along the fixed aimDir.
grep -n 'UpdateTelegraph(transform.position, transform.position + (Vector3)(aimDir' Assets/Scripts/shooterBehaviour.cs
# EXPECT: one match

# 5. Zero-vector guard present.
grep -c 'if (aimDir == Vector2.zero)' Assets/Scripts/shooterBehaviour.cs
# EXPECT: 1

# 6. FireProjectile still launches along aimDir (unchanged).
grep -c 'proj.Launch(aimDir, projectileSpeed)' Assets/Scripts/shooterBehaviour.cs
# EXPECT: 1
```

**Editor validation:** After edits, confirm 0 compile errors in the Unity console (build-validator / Coplay MCP). In Play Mode, when the shooter enters Aim the red telegraph line should freeze its angle for the full `aimDuration` even as the player strafes, and the projectile should travel along that frozen line.

---

## 5. Risk note

- **Low risk, self-contained** — all edits are within `Update()` and touch only the `aimDir` field and telegraph endpoint; no signature, pooling, or `enemyProjectile` changes.
- **Degenerate direction:** if the player is exactly on the shooter at Aim entry, `normalized` yields `Vector2.zero`; the guard in Edit A falls back to `Vector2.right` (matching the existing `OnEnable` default at line 27).
- **Behavioral change is intended:** the shooter becomes easier to dodge (it no longer re-aims at fire time). This is the requested design, not a regression.
- **Telegraph length:** endpoint uses `aimDir * aimRange` for a stable visual. If a different telegraph length is desired (e.g. projectile travel distance), swap `aimRange` for the preferred scalar — cosmetic only, does not affect fire direction.

---

## Handoff

Implementation task for `@agent-csharp-dev`: apply Edits A, B, C to `Assets/Scripts/shooterBehaviour.cs` exactly as specified; leave `FireProjectile` (Edit D) untouched; self-validate with the grep checklist in §4; confirm 0 compile errors.
