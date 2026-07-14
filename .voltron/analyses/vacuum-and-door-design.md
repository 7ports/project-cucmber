# Design: XP-Vacuum Powerup + Door-to-Next-Area (minimal traversal slice)

> **Status:** DESIGN-ONLY. Nothing here is built yet — this is for the user to read before build.
> **Branch:** `slot-machine-levlup`
> **Scope:** Build (1) the XP-vacuum powerup and (2) a working door gated on quest completion, so the
> user can test the full traversal flow: *collect quest items → door opens → walk through*.
> **Explicitly DEFERRED (design clean hooks only, do NOT build now):** the floor puzzle, the region-B
> next-area boss, and the region-B tilemap itself (the **user** builds region B). We build only the
> door GameObject + gating logic + the vacuum powerup.

Project constraints honored throughout: Unity 6000.4.2f1, URP 2D, **global namespace / no `namespace` blocks**,
`[SerializeField] private` for inspector fields, `_camelCase` privates, **no `Find`/`FindObjectOfType`**
(wire via inspector + static events), **no DOTween** (coroutines only), scripts flat in `Assets/Scripts/`.

---

## Grounding: how the existing code works (verified by reading source)

### `pickupBehaviour.cs` (XP-pickup homing) — the vacuum hook lives here
```csharp
public class pickupBehaviour : MonoBehaviour
{
    public bool pickup;                                   // line 5  <-- THE HOMING SWITCH
    [SerializeField] private float homeSpeed = 8f;        // line 6
    [SerializeField] private int xpValue = 1;             // line 7

    void OnEnable()  { pickup = false; }                  // line 9-12
    void Update()                                         // line 14-19
    {
        if (!pickup) return;                              // homes ONLY while pickup==true
        // MoveTowards player at homeSpeed
    }
    void OnTriggerEnter2D(Collider2D other) { /* addXP + objectPool.instance.ret(gameObject) */ } // line 21-33
}
```
Key facts:
- **`pickup` is a public bool that gates homing.** Something external (`playerPickupRadius.cs`) sets it
  `true` when the pickup enters the player's pickup radius; from then on it homes forever until collected.
- XP pickups are **pooled** (`objectPool.instance.ret(gameObject)` on collect; `OnEnable` resets the flag —
  so a pooled instance reused as a fresh drop correctly starts un-homed).
- **XP pickups are identified by carrying the `pickupBehaviour` component** (prefabs `XP1..XP4`). There is no
  tag/`FindObjectsOfType` scan needed — we use a **static event that each live instance subscribes to**, which
  is both `Find`-free and pool-safe.

> **The vacuum mechanism = flip `pickup = true` on every live pickup at once, regardless of distance.**
> Because `Update()` already homes whenever `pickup` is true, setting the flag *is* the whole effect —
> no new movement code, no distance check, fully reusable.

### `questManager.cs` — the door gate already exists, just needs a subscriber
```csharp
public static event System.Action OnAllQuestItemsCollected;   // line 17
...
public void Collect(questItem q)                              // line 101
{
    active.Remove(q);
    collected++;
    if (collected >= questCount)                             // line 105
    {
        Debug.Log("Quest complete - ready for next part");
        if (OnAllQuestItemsCollected != null)
            OnAllQuestItemsCollected();                       // line 108-110  <-- FIRES, no listener today
    }
}
```
The event is fully wired to fire; **no subscriber exists**. Our `nextAreaController` becomes the first (and,
for this slice, only) subscriber.

### `itemPickup.cs` — the walk-over-trigger pattern to mirror for the powerup
```csharp
void OnTriggerEnter2D(Collider2D other)
{
    if (worldState.instance == null) return;
    if (other.transform == worldState.instance.player ||
        other.transform.root == worldState.instance.player)   // player-or-child match
    {
        /* do the effect */                                    // e.g. itemChoiceMenuController.Offer()
        if (objectPool.instance != null) objectPool.instance.ret(gameObject);
    }
}
```
`xpVacuumPickup` copies this trigger shape exactly (same player-root guard), swapping the effect body.

---

# FEATURE 1 — XP-Vacuum Powerup

**Behaviour:** the player walks over a world-space powerup pickup; on pickup, **every XP pickup currently on
the field is forced to home to the player at once** (a one-shot field-clear "vacuum"), regardless of distance.

## 1A. Data model / components

| Component | Type | Responsibility |
|---|---|---|
| `xpVacuumPickup` | new MonoBehaviour | World-space collectible. On player-trigger, calls `pickupBehaviour.VacuumAll()`, then returns/destroys itself. Mirrors `itemPickup.cs`. |
| `pickupBehaviour` (edit) | existing MonoBehaviour | Add a **static event** + per-instance subscribe/unsubscribe + a static broadcast helper that flips `pickup = true` on all live instances. |
| `xpVacuumPickup.prefab` | prefab (scene-architect) | Sprite + `CircleCollider2D (isTrigger)` + `xpVacuumPickup`. Modeled on `itemPickup.prefab`. |

### New static surface on `pickupBehaviour.cs`
```csharp
// --- vacuum support (added to pickupBehaviour) ---
private static event System.Action _onVacuum;   // fired once to attract every live pickup

void OnEnable()
{
    pickup = false;              // existing line 11 behaviour preserved
    _onVacuum += Attract;        // subscribe this live instance
}

void OnDisable()                 // NEW — pool return / destroy path
{
    _onVacuum -= Attract;        // MUST unsubscribe (pool reuse + no leaked delegates)
}

private void Attract()           // handler: force homing regardless of distance
{
    pickup = true;               // Update() already homes when pickup==true
}

/// <summary>Force EVERY active XP pickup to home to the player at once.</summary>
public static void VacuumAll()
{
    if (_onVacuum != null) _onVacuum();   // no Find/FindObjectsOfType — only live, enabled pickups react
}
```

### `xpVacuumPickup.cs` (new file, mirrors `itemPickup.cs`)
```csharp
using UnityEngine;

public class xpVacuumPickup : MonoBehaviour
{
    void OnTriggerEnter2D(Collider2D other)
    {
        if (worldState.instance == null) return;
        if (other.transform == worldState.instance.player ||
            other.transform.root == worldState.instance.player)
        {
            pickupBehaviour.VacuumAll();
            if (objectPool.instance != null) objectPool.instance.ret(gameObject);
            else Destroy(gameObject);   // safe fallback if this prefab isn't pooled
        }
    }
}
```

## 1B. Exact code hooks (file:line)
- `pickupBehaviour.cs:9-12` — extend existing `OnEnable()` to add `_onVacuum += Attract;` (keep `pickup = false;`).
- `pickupBehaviour.cs` — **add** `OnDisable()` with `_onVacuum -= Attract;` (no existing `OnDisable`).
- `pickupBehaviour.cs` — **add** static `_onVacuum` event, `Attract()`, and public static `VacuumAll()`.
- New file `Assets/Scripts/xpVacuumPickup.cs` (trigger → `pickupBehaviour.VacuumAll()`).

## 1C. Edge cases
- **Pooling correctness:** subscription in `OnEnable`, un-subscription in `OnDisable` — matches the pool
  lifecycle (`objectPool.ret` disables the GO). A pickup reused later re-subscribes fresh; its `pickup`
  flag is reset to `false` in `OnEnable`, so a stale "attracted" state can't carry over. **This is the single
  most important correctness point** — without the `OnDisable` unsubscribe, returned-to-pool instances would
  leak delegate references and could be attracted while inactive.
- **No XP on field:** `VacuumAll()` with no subscribers is a null-check no-op — safe.
- **XP collected mid-flight:** an attracted pickup that reaches the player collects normally via the existing
  `OnTriggerEnter2D`; its `OnDisable` unsubscribes it. No double-collection.
- **Static event across scene reload:** single scene, no additive loads. If the scene is ever reloaded, all
  instances get `OnDisable` → unsubscribe, so `_onVacuum` drains to null. No cross-scene leak, but note this
  if multi-scene is ever added (see Open Questions).
- **Homing speed:** vacuum reuses each pickup's own `homeSpeed` — pickups far away simply take longer to
  arrive, which reads as a satisfying "suck-in". If an instant gather is desired instead, that's a tuning
  follow-up (raise `homeSpeed` on attract) — flagged, not built.
- **Not pooled?** If scene-architect makes `xpVacuumPickup` a plain `Instantiate`d prefab (not registered in
  `objectPool`), the `Destroy` fallback covers it. Decide pooled-vs-not at wiring time.

## 1D. Editor wiring (scene-architect)
1. Create `Assets/Prefabs/pickups/xpVacuumPickup.prefab` (duplicate `itemPickup.prefab` as a starting point):
   sprite (distinct from XP orbs — e.g. a magnet/vacuum icon), `CircleCollider2D` with **Is Trigger = true**,
   `xpVacuumPickup` component. Remove the `itemPickup` component from the duplicate.
2. Place one instance in `SampleScene` at a reachable spot for testing (or hand a drop hook to a spawner later
   — out of scope for this slice; a manually-placed instance is enough to test).
3. Save the scene.

---

# FEATURE 2 — Door to Next Area (minimal slice)

**Behaviour:** collecting all quest items opens a door — the door is a solid `Collider2D` on the **Walls**
layer that blocks the player until `questManager.OnAllQuestItemsCollected` fires, at which point its collider
is disabled (with an optional placeholder open visual) so the player can walk through into the user-built
region-B tilemap.

## 2A. Data model / components

| Component | Type | Responsibility |
|---|---|---|
| `nextAreaController` | new MonoBehaviour | Subscribes to `questManager.OnAllQuestItemsCollected`; on fire runs `OpenDoor()`. Holds a serialized ref to the door's `Collider2D` and optional visual. Exposes `OnDoorOpened` event for deferred hooks. |
| Door GameObject | scene object (scene-architect) | On **Walls** layer, solid (non-trigger) `Collider2D`. Blocks the player. Referenced by `nextAreaController`. |

### `nextAreaController.cs` (new file)
```csharp
using UnityEngine;

public class nextAreaController : MonoBehaviour
{
    [SerializeField] private Collider2D _doorCollider;   // the Walls-layer solid collider that blocks passage
    [SerializeField] private GameObject _doorVisual;     // optional: sprite to toggle/swap as "open" placeholder
    [SerializeField] private bool _disableVisualOnOpen = true;

    // Deferred-feature hook: RegionB_EntryTrigger / next-area-boss spawner subscribe here (NOT built now).
    public static event System.Action OnDoorOpened;

    private bool _opened;

    void OnEnable()  { questManager.OnAllQuestItemsCollected += OpenDoor; }
    void OnDisable() { questManager.OnAllQuestItemsCollected -= OpenDoor; }

    public void OpenDoor()
    {
        if (_opened) return;                 // idempotent — event fires once, but guard anyway
        _opened = true;

        if (_doorCollider != null) _doorCollider.enabled = false;   // stop blocking the Walls layer
        if (_doorVisual != null && _disableVisualOnOpen) _doorVisual.SetActive(false);
        // Optional placeholder open animation would be a coroutine here (NO DOTween).

        if (OnDoorOpened != null) OnDoorOpened();   // fire for deferred subscribers
        Debug.Log("nextAreaController: door opened.");
    }
}
```

## 2B. Exact code hooks (file:line)
- `questManager.cs:17` — event `OnAllQuestItemsCollected` (existing; no edit).
- `questManager.cs:108-110` — fire site (existing; no edit). `nextAreaController` is its first subscriber.
- New file `Assets/Scripts/nextAreaController.cs`.
- **No edits to `questManager.cs` are required** — the gate is already fully wired to fire.

## 2C. Deferred hooks (design only — DO NOT build now)
Attach points are ready so the deferred features drop in without touching this slice:
- **`RegionB_EntryTrigger`** (deferred): a trigger collider placed just past the door in region B. When built,
  it would fire an `OnEnteredRegionB` event on player entry. *Where it attaches:* it's independent of the door
  and just needs region B to exist — no dependency on `nextAreaController` beyond the door being open.
- **Next-area-boss spawn hook** (deferred): subscribe the boss spawner to **either** `nextAreaController.OnDoorOpened`
  (spawn on door open) **or** the future `RegionB_EntryTrigger.OnEnteredRegionB` (spawn on entry — preferred, so
  the boss doesn't spawn before the player crosses). Both are static `System.Action` events, subscribe/unsubscribe
  in `OnEnable`/`OnDisable`, mirroring `nextAreaController`'s own pattern.
- **Floor puzzle** (deferred): would gate `OpenDoor()` behind an *additional* condition. Clean insertion: give
  `nextAreaController` a second boolean (e.g. `_puzzleSolved`) and only call the collider-disable when **both**
  quest-complete and puzzle-solved are satisfied. Not built now — noted so the single-condition open here is
  understood to be intentionally minimal.

## 2D. Edge cases
- **Door ref missing:** null-guarded; logs nothing catastrophic but the door won't open — scene-architect must
  wire `_doorCollider`. Consider a `Debug.LogWarning` if `_doorCollider == null` at `OpenDoor` for diagnosability.
- **Event fires before subscribe:** `questManager.Collect` fires only after all items are collected during play;
  `nextAreaController.OnEnable` runs at scene start, well before. No ordering hazard. (If a save/continue system
  is ever added that could complete the quest off-screen, revisit.)
- **Double fire / idempotency:** `_opened` guard makes `OpenDoor` safe to call more than once.
- **Player stuck in collider:** disabling (not moving) the collider means if the player is overlapping the door
  at open time they're freed cleanly — no teleport needed.
- **Walls layer physics:** disabling the `Collider2D` is enough; the GameObject can stay active (so the visual/
  future animation still runs). Do **not** `SetActive(false)` the whole door GO if `_doorVisual` is a child that
  should animate open.

## 2E. Editor wiring (scene-architect)
1. Create a **Door** GameObject in `SampleScene` at the boundary between the current area and where region B will
   be built. Set its **Layer = Walls**. Add a solid `BoxCollider2D` (**Is Trigger = false**) sized to block the
   passage. Add a child sprite for the door visual (optional).
2. Create an empty **NextAreaController** GameObject (or add the component to an existing systems object). Add
   `nextAreaController`. Wire `_doorCollider` → the Door's `Collider2D`; wire `_doorVisual` → the door sprite GO
   (optional).
3. Ensure a `questManager` is present and configured (it already spawns quest items). No inspector change needed
   on it for this slice.
4. Save the scene.
5. *(User task, out of scope):* build the region-B tilemap on the far side of the door.

---

## Changed / new surface list (for scrum-master decomposition)

**New scripts (csharp-dev, Docker):**
- `Assets/Scripts/xpVacuumPickup.cs` — trigger → `pickupBehaviour.VacuumAll()`.
- `Assets/Scripts/nextAreaController.cs` — subscribes to `questManager.OnAllQuestItemsCollected`, `OpenDoor()`.

**Edited scripts (csharp-dev, Docker):**
- `Assets/Scripts/pickupBehaviour.cs` — add static `_onVacuum` event, `Attract()`, `VacuumAll()`; extend
  `OnEnable`; add `OnDisable`.

**Editor / scene work (scene-architect, host `Agent` tool — Unity Editor):**
- `Assets/Prefabs/pickups/xpVacuumPickup.prefab` (new; trigger collider + component).
- Door GameObject on **Walls** layer + solid `Collider2D` in `SampleScene`.
- `NextAreaController` GameObject + component; wire `_doorCollider` (+ optional `_doorVisual`).
- Place a test `xpVacuumPickup` instance; save scene.

**Validation (build-validator, host `Agent` tool):**
- Compile clean (no console errors).
- Play Mode: pick up vacuum → all field XP homes in; collect all quest items → door collider disables → player
  walks through.

## Phase order (milestone-level; scrum-master decomposes into tasks)

1. **Phase 1 — Scripts.** csharp-dev edits `pickupBehaviour.cs` and creates `xpVacuumPickup.cs` +
   `nextAreaController.cs`. Gate: build-validator confirms clean compile. *(No scene dependency — can run first.)*
2. **Phase 2 — Prefab + scene wiring.** scene-architect creates `xpVacuumPickup.prefab`, the Door (Walls layer +
   collider), and the `NextAreaController` GO; wires refs; places a test vacuum instance; saves scene.
   *Depends on Phase 1 (components must exist to add/wire).*
3. **Phase 3 — Play-Mode validation.** build-validator runs the full-flow test (vacuum gather + quest→door→pass).
   *Depends on Phase 2.*
4. **Deferred (NOT scheduled now):** `RegionB_EntryTrigger`, next-area-boss spawn hook, floor puzzle. Hooks
   (`OnDoorOpened`, future `OnEnteredRegionB`, `_puzzleSolved`) are designed above; build when region B exists.

---

## Open Questions (need human input before build)
1. **Most important:** Should the vacuum **instantly gather** XP (raise `homeSpeed` on attract for a snappy
   suck-in) or let each pickup home at its normal `homeSpeed` (slower, staggered arrival)? This is the one design
   choice that changes game feel — everything else is mechanical. Default assumed: normal `homeSpeed`.
2. Is `xpVacuumPickup` pooled (register in `objectPool`) or a plain `Instantiate`/`Destroy` prefab? Design covers
   both; pooling only matters if vacuums will spawn frequently.
3. Should the boss spawn on **door open** (`OnDoorOpened`) or on **region-B entry** (`OnEnteredRegionB`)? Entry is
   recommended; deferred either way.
