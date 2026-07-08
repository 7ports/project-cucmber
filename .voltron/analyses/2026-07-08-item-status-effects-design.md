# Enemy Status-Effect Subsystem Design — project-cucumber (Fire + Freeze)

**Date:** 2026-07-08
**Author:** project-planner (Tier 2, design only — no implementation)
**Scope:** The enemy-side status MECHANISM + its two public apply methods that the projectile-hit-site design will CALL. Covers Fire (burning DoT, stacks up to 3) and Freeze (immobilize). Grounded in the five real source files below.

**Contract dependency:** Ownership (`playerInventory.instance.Has(ItemId.Fire)` / `Has(ItemId.Freeze)`) and the freeze random roll happen at the **hit site** (separate design). That code calls the methods defined here. This doc does NOT touch inventory, projectile, or roll logic.

---

## 0. TL;DR decisions

- **Host component:** `enemyHealth` (already on every enemy incl. bosses). **No new component, no prefab/Editor change.**
- **Public contract:** `public void ApplyFire()` and `public void ApplyFreeze(float seconds)` live on **`enemyHealth`**.
- **Fire interpretation:** **10 dmg/sec PER STACK** (10 at 1 stack → 30 at 3 stacks). Justified in §2.
- **Freeze mechanism:** a single `public bool IsFrozen` flag on `enemyHealth`; **each of the three movers early-returns** at the top of its movement code when `IsFrozen` is true. No velocity code (all three move via `transform.position`/`MoveTowards`), so gating = an early `return`.
- **Pooled reset:** stacks cleared + freeze lifted in `enemyHealth.OnEnable` (the existing pool-reset hook).

---

## 1. Anchors (real file references)

### `Assets/Scripts/enemyHealth.cs`
- **Class:** `public class enemyHealth : MonoBehaviour` (L3). Present on **every** enemy incl. boss (`_isBoss` computed L26). No `Update()` currently exists — we add one.
- **Pool-reset hook:** `void OnEnable()` (L29–34) recomputes `scaledMaxHp` and `currentHp`. **This is the reset anchor** — add status reset here.
- **Damage entry point:** `public void takeDamage(int amount)` (L36–50). Fire DoT calls this exact method each tick. `die()` (L52) returns the object to the pool via `objectPool.instance.ret(gameObject)` (L67), which disables it → next `OnEnable` re-resets.

### `Assets/Scripts/chaserBehaviour.cs`
- **Movement line:** L12–15, `transform.position = Vector3.MoveTowards(...)` inside `Update()` (L8). Guarded only by the null-check at L10. **Gate anchor:** immediately after L10's guard.

### `Assets/Scripts/bossBehaviour.cs`
- **Movement lines:** `Update()` (L9) does either a leash teleport (L16) OR a chase `MoveTowards` (L20). **Gate anchor:** immediately after the null-check at L11 — one early-return blocks both leash and chase.

### `Assets/Scripts/shooterBehaviour.cs`
- **Movement lines:** `Update()` (L31) state machine. Movement happens in `State.Chase` (L40) and `State.Cooldown` (L62) via `MoveTowards`. **Gate anchor:** immediately after the null-check at L33 — a frozen shooter should also stop advancing its aim/fire timers, so gate the whole `Update` body (freeze pauses the whole state machine).

### `Assets/Scripts/worldState.cs`
- Plain static-instance config class (`public static worldState instance;` L5). Effective-value getter pattern (e.g. `AttackDamage()` L58). **This is where the status config numbers + getters go.**

---

## 2. Fire design — burning DoT, stacks up to 3

### Per-stack decision (STATED)
**10 damage/second PER STACK.** At 1 stack → 10 dps; 2 stacks → 20 dps; 3 stacks (cap) → 30 dps.

**Justification:** The spec phrase is *"fire stacks up to 3 times and deals 10 damage per second."* If the total were a flat 10 dps regardless of stacks, the "stacks up to 3 times" clause would be mechanically meaningless — stacking would change nothing. Per-stack scaling is the only reading under which the stack count matters, so it is the intended design. (Rejected: 10 dps total — makes stacking a no-op.) Numbers are already in the post-x10 NEW scale (enemy base HP `maxHp=30` L5, `EnemyDamage=50` L10), so 10/tick-second is a meaningful but non-instant DoT against 30-HP chasers (~3s to kill at 1 stack, ~1s at 3 stacks).

### Tick / stack / expire model
- **Stacks:** integer `_burnStacks`, `ApplyFire()` does `_burnStacks = Min(_burnStacks + 1, cap)`.
- **Tick:** a fixed accumulator in `Update()`. Every `FireTickInterval` seconds of burn, deal `fireDpsPerStack * _burnStacks` via `takeDamage(...)`. Interval default **1s** → damage-per-tick equals dps directly.
- **Refresh:** every `ApplyFire()` resets `_burnTimeRemaining = FireBurnDuration` (default 3s). New hits refresh duration; they do NOT reset the tick accumulator (burn keeps ticking smoothly).
- **Expire:** when `_burnTimeRemaining` reaches 0, `_burnStacks = 0` and ticking stops.
- **Rounding:** damage is `int` already (dps × stacks are ints), so `takeDamage(int)` gets a clean value.

### Drop-in code (added to `enemyHealth`)

Add fields (near L16–17):
```csharp
    // --- Status effect state (Fire / Freeze). Reset every OnEnable (pool respawn). ---
    private int   _burnStacks;          // 0..fireStackCap
    private float _burnTimeRemaining;   // seconds left before burn fully expires
    private float _burnTickAccum;       // accumulates toward one FireTickInterval
    private float _freezeTimeRemaining; // seconds left immobilized

    public bool IsFrozen => _freezeTimeRemaining > 0f;   // read by the three movers
```

Add an `Update()` (new method — enemyHealth has none today):
```csharp
    void Update()
    {
        // Read config once; fall back to constants if worldState not ready.
        float tickInterval = (worldState.instance != null) ? worldState.instance.fireTickInterval : 1f;
        int   dpsPerStack  = (worldState.instance != null) ? worldState.instance.fireDpsPerStack : 10;

        // --- Burning DoT ---
        if (_burnStacks > 0 && _burnTimeRemaining > 0f)
        {
            _burnTimeRemaining -= Time.deltaTime;
            _burnTickAccum     += Time.deltaTime;
            while (_burnTickAccum >= tickInterval)
            {
                _burnTickAccum -= tickInterval;
                takeDamage(dpsPerStack * _burnStacks);   // 10 dmg/sec PER STACK
                if (currentHp <= 0) return;              // die() already recycled us
            }
            if (_burnTimeRemaining <= 0f)                // burn expired
            {
                _burnStacks = 0;
                _burnTickAccum = 0f;
            }
        }

        // --- Freeze countdown ---
        if (_freezeTimeRemaining > 0f)
            _freezeTimeRemaining -= Time.deltaTime;
    }
```

Add the public contract method `ApplyFire()`:
```csharp
    /// <summary>
    /// CONTRACT (called by the projectile-hit site when the player owns ItemId.Fire).
    /// Adds one burning stack (capped) and refreshes burn duration.
    /// </summary>
    public void ApplyFire()
    {
        int cap = (worldState.instance != null) ? worldState.instance.fireStackCap : 3;
        _burnStacks = Mathf.Min(_burnStacks + 1, cap);
        _burnTimeRemaining = (worldState.instance != null) ? worldState.instance.fireBurnDuration : 3f;
    }
```

> **Note on `takeDamage` inside the tick:** `takeDamage` already spawns damage numbers / flash / boss-blood and calls `die()` at `currentHp <= 0` (L48). Reusing it means burn ticks show floating damage numbers and can kill — the `if (currentHp <= 0) return;` guard after the call prevents ticking a corpse that `die()` already returned to the pool.

---

## 3. Freeze design — immobilize across all three movers

### Chosen approach (STATED): shared `IsFrozen` flag + mover early-return
`enemyHealth.IsFrozen` (a `bool` property backed by `_freezeTimeRemaining > 0f`) is the single source of truth. **Each mover caches its `enemyHealth` in `Awake()` and early-returns from `Update()` while frozen.** Rationale for early-return vs zeroing velocity: **all three movers translate `transform.position` directly (no Rigidbody velocity is used for movement)** — there is no velocity to zero, so the clean universal gate is "skip the movement code this frame." The freeze countdown itself lives in `enemyHealth.Update()` (§2) so it ticks independently of whether the mover runs.

**What each mover must check (exactly):** after its existing `worldState`/`player` null-guard, add
`if (_health != null && _health.IsFrozen) return;`

### Drop-in code — `chaserBehaviour.cs`
```csharp
public class chaserBehaviour : MonoBehaviour
{
    [SerializeField] private float chaseSpeed = 1.5f;
    private enemyHealth _health;                 // ADDED

    void Awake() { _health = GetComponent<enemyHealth>(); }   // ADDED

    void Update()
    {
        if (worldState.instance == null || worldState.instance.player == null) return;
        if (_health != null && _health.IsFrozen) return;       // ADDED: freeze gate

        transform.position = Vector3.MoveTowards(
            transform.position,
            worldState.instance.player.position,
            chaseSpeed * Time.deltaTime);
    }
}
```

### Drop-in code — `bossBehaviour.cs` (blocks both leash + chase)
```csharp
    private enemyHealth _health;                 // ADDED (field)

    void Awake() { _health = GetComponent<enemyHealth>(); }   // ADDED

    void Update()
    {
        if (worldState.instance == null || worldState.instance.player == null) return;
        if (_health != null && _health.IsFrozen) return;       // ADDED: one gate stops leash AND chase
        Vector3 target = worldState.instance.player.position;
        // ... existing leash/chase body unchanged ...
    }
```
> Boss already has `enemyHealth` (that's how `_isBoss` is detected). `Awake` here is new — bossBehaviour has no `Awake` today, so adding one is safe.

### Drop-in code — `shooterBehaviour.cs` (pauses whole state machine)
`shooterBehaviour` already has `void Awake()` (L21) and `void OnEnable()` (L23). Add the field + cache in the existing `Awake`, and gate the whole `Update` body so a frozen shooter neither moves nor advances aim/fire timers:
```csharp
    private enemyHealth _health;                 // ADDED (field)

    void Awake() { ConfigureTelegraph(); _health = GetComponent<enemyHealth>(); }   // CHANGED: added cache

    void Update()
    {
        if (worldState.instance == null || worldState.instance.player == null) return;
        if (_health != null && _health.IsFrozen) return;       // ADDED: freeze pauses the state machine
        // ... existing switch(state) body unchanged ...
    }
```

### `ApplyFreeze` public contract method (added to `enemyHealth`)
```csharp
    /// <summary>
    /// CONTRACT (called by the projectile-hit site AFTER its freeze-chance roll succeeds
    /// and the player owns ItemId.Freeze). Immobilizes the enemy for `seconds`.
    /// Passing seconds <= 0 uses the configured default duration.
    /// </summary>
    public void ApplyFreeze(float seconds)
    {
        if (seconds <= 0f)
            seconds = (worldState.instance != null) ? worldState.instance.freezeDefaultDuration : 2f;
        // Refresh-to-longest: a new freeze never shortens an active one.
        _freezeTimeRemaining = Mathf.Max(_freezeTimeRemaining, seconds);
    }
```
> The CHANCE is applied at the hit site (per the contract), not here. The hit site may pass its own duration or `0` to use the default. Refresh-to-longest avoids a rapid second hit cutting an existing freeze short.

---

## 4. Pooled-reset code (recycled enemy never starts burning/frozen)

Enemies are pooled: `die()` → `objectPool.instance.ret(gameObject)` disables the GO; next spawn re-enables → `OnEnable` fires. Add the reset to the **existing** `OnEnable` (L29–34):

```csharp
    void OnEnable()
    {
        float mult = (worldState.instance != null) ? worldState.instance.EnemyHpTimeMultiplier() : 1f;
        scaledMaxHp = Mathf.Max(1, Mathf.RoundToInt(maxHp * mult));
        currentHp = scaledMaxHp;

        // ADDED: clear all status so a recycled enemy never starts burning/frozen.
        _burnStacks = 0;
        _burnTimeRemaining = 0f;
        _burnTickAccum = 0f;
        _freezeTimeRemaining = 0f;
    }
```
Because `IsFrozen` derives from `_freezeTimeRemaining` and burn ticking derives from `_burnStacks`/`_burnTimeRemaining`, zeroing these four fields fully clears status. No mover state needs resetting (they read `IsFrozen` live each frame).

---

## 5. Config fields + defaults (worldState)

Add to `worldState.cs` (new-scale numbers). Public fields + effective getters, matching the file's existing style:

```csharp
    // --- Enemy status effects (Fire DoT / Freeze). New (post-x10) damage scale. ---
    public int   fireDpsPerStack   = 10;    // damage per second PER burning stack
    public int   fireStackCap      = 3;     // max simultaneous burning stacks
    public float fireTickInterval  = 1f;    // seconds between burn ticks (1s -> dmg/tick == dps)
    public float fireBurnDuration  = 3f;    // seconds a burn lasts; refreshed by each ApplyFire()
    public float freezeDefaultDuration = 2f;// fallback freeze seconds when hit site passes <= 0
```

| Field | Default | Meaning |
|-------|---------|---------|
| `fireDpsPerStack` | 10 | dmg/sec per stack → 30 dmg/sec at the 3-stack cap |
| `fireStackCap` | 3 | matches "stacks up to 3 times" |
| `fireTickInterval` | 1f | one tick per second |
| `fireBurnDuration` | 3f | refreshed on every `ApplyFire()` |
| `freezeDefaultDuration` | 2f | used when hit site passes `0`; chance lives at hit site |

All read defensively (`worldState.instance != null ? … : constant`) so status still works if `worldState.instance` is momentarily null.

---

## 6. THE CONTRACT (restated for the hit-site design)

Both methods live on **`enemyHealth`** (the component already on every enemy, incl. bosses). The hit site, after resolving ownership + rolls, gets the enemy's `enemyHealth` (e.g. `hitCollider.GetComponent<enemyHealth>()`) and calls:

```csharp
// Fire: player owns ItemId.Fire -> add a stack (auto-capped, auto-refreshed)
enemyHealth eh = hit.GetComponent<enemyHealth>();
if (eh != null) eh.ApplyFire();

// Freeze: player owns ItemId.Freeze AND the freeze-chance roll (rolled at hit site) succeeded
if (eh != null) eh.ApplyFreeze(0f);   // 0 -> use worldState.freezeDefaultDuration, or pass an explicit seconds
```

Exact signatures (do not change):
- `public void ApplyFire()` — on `enemyHealth`
- `public void ApplyFreeze(float seconds)` — on `enemyHealth`
- `public bool IsFrozen { get; }` — on `enemyHealth` (movers read this; hit site does not need it)

---

## 7. Grep acceptance checklist

Run after implementation lands:

```bash
# Contract methods exist on enemyHealth
grep -n 'public void ApplyFire()'            Assets/Scripts/enemyHealth.cs
grep -n 'public void ApplyFreeze(float'      Assets/Scripts/enemyHealth.cs
grep -n 'public bool IsFrozen'               Assets/Scripts/enemyHealth.cs

# Burn state + per-stack tick present
grep -n '_burnStacks\|fireDpsPerStack \* _burnStacks' Assets/Scripts/enemyHealth.cs

# Pooled reset clears status in OnEnable
grep -n '_burnStacks = 0\|_freezeTimeRemaining = 0f' Assets/Scripts/enemyHealth.cs

# All three movers gate on IsFrozen
grep -n 'IsFrozen) return' Assets/Scripts/chaserBehaviour.cs Assets/Scripts/bossBehaviour.cs Assets/Scripts/shooterBehaviour.cs

# Config in worldState
grep -n 'fireDpsPerStack\|fireStackCap\|fireBurnDuration\|freezeDefaultDuration' Assets/Scripts/worldState.cs
```

Expected: all present; `IsFrozen) return` appears once in each of the three mover files.

---

## 8. Editor / prefab change needed?

**No.** Status lives on `enemyHealth`, which is **already on every enemy prefab and every boss** (proven by `enemyHealth.Awake` computing `_isBoss` via `GetComponent<bossBehaviour>()`, L26). No new component is added to any prefab; no serialized/inspector fields are introduced (all config lives in the plain-class `worldState`, not `[SerializeField]`). The three mover scripts gain a cached `GetComponent<enemyHealth>()` at runtime — no wiring. **Script-only change** across five files: `enemyHealth.cs`, `chaserBehaviour.cs`, `bossBehaviour.cs`, `shooterBehaviour.cs`, `worldState.cs`.

---

## 9. Open questions for human input

1. **Frozen enemies still deal contact damage?** This design only stops *movement*. If a frozen enemy touching the player should also stop dealing contact damage, that's a separate gate at the damage-dealing site (not in scope here).
2. **Shooter freeze scope:** design pauses the shooter's *entire* state machine (no move, no aim progress, no fire). Confirm a mid-aim frozen shooter should not complete its shot. Alternative: freeze only movement but let an in-progress aim/fire resolve.
3. **Visual feedback:** no burn/freeze tint or icon is specified — burn reuses the existing damage-number popups via `takeDamage`. A freeze tint (e.g. blue `damageFlash`) would need shader-artist/scene work if desired.
