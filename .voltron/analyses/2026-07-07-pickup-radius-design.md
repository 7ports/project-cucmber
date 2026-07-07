# Design: XP Pickup Radius as an Upgradeable Stat

**Date:** 2026-07-07
**Author:** project-planner
**Scope:** Design-only. No implementation performed. Hand off to `csharp-dev`.
**Feature:** Make the XP pickup/attraction radius an upgradeable stat (flat + percent), mirroring the existing base+mult stat system.

---

## 1. Overview

The XP pickup radius is currently a fixed `[SerializeField] private float radius = 4f` on
`playerPickupRadius.cs`. This design promotes it to a first-class stat in `worldState` (base + mult
getter) so it can be increased by the level-up menu's Flat/Percent upgrade pool, applied live per
scan, and shown in the pause stats view — exactly matching the established pattern for Damage,
MoveSpeed, FireRate, Range, MaxHP, Defense, and Regen.

---

## 2. Ground-Truth Anchors (verified this session)

| Fact | Value |
|---|---|
| Pickup radius owner | `Assets/Scripts/playerPickupRadius.cs` |
| Field name | `radius` |
| Field line | **line 5** — `[SerializeField] private float radius = 4f;` |
| Default value | **4f** |
| Mechanism | Player-side polling scanner: `Physics2D.OverlapCircleAll(transform.position, radius, xpLayer)` in `scan()` (line 22), every `scanInterval` (0.1s). NOT a collider, NOT hardcoded in `pickupBehaviour`. |
| Radius read sites | Two, both inside `playerPickupRadius.cs`: `scan()` line 22 and `OnDrawGizmosSelected()` line 33. |
| Other readers | **None.** `grep -rn` over `Assets/Scripts/` for `playerPickupRadius` / `PickupRadius` / `pickupRadius` / `.radius` returns only the class declaration. The field is entirely local. |

This confirms the known anchor. The default `4f` matches `rangeBase = 4f` coincidentally; they are independent stats and must not be merged.

---

## 3. worldState.cs — New Stat Fields + Getter

Mirror the existing pattern (base+mult pair, getter = base*mult). Add alongside the other stat
fields (after the `regen` pair, `worldState.cs:22`):

```csharp
public float pickupRadiusBase = 4f;   // = current playerPickupRadius default -> behavior unchanged
public float pickupRadiusMult = 1f;
```

Add the getter alongside the others (after `Regen()`, `worldState.cs:31`):

```csharp
public float PickupRadius() => pickupRadiusBase * pickupRadiusMult;
```

**Default rationale:** `pickupRadiusBase = 4f` × `pickupRadiusMult = 1f` = `4f`, identical to the
current `radius` default, so behavior is unchanged until an upgrade is chosen.

---

## 4. playerPickupRadius.cs — Migration

Read the effective radius from `worldState` on every scan so upgrades apply live. Remove the local
`radius` field (it is no longer the source of truth) and use a local variable in `scan()`.

**Before (`scan()`, lines 20–29):**
```csharp
void scan()
{
    Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, radius, xpLayer);
    ...
}
```

**After:**
```csharp
void scan()
{
    float radius = worldState.instance != null ? worldState.instance.PickupRadius() : 4f;
    Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, radius, xpLayer);
    ...
}
```

**Field removal (line 5):** delete `[SerializeField] private float radius = 4f;`.

**Gizmo (`OnDrawGizmosSelected()`, line 33):** this also reads `radius`. Since the field is removed,
update it to read from worldState with a null-safe fallback (worldState.instance is null in edit
mode):
```csharp
void OnDrawGizmosSelected()
{
    float radius = (Application.isPlaying && worldState.instance != null)
        ? worldState.instance.PickupRadius() : 4f;
    Gizmos.DrawWireSphere(transform.position, radius);
}
```

**Null-safety note:** `scan()` only runs in Play Mode where `worldState.instance` is expected to be
set (same assumption every other stat consumer makes), but the `!= null` guard with `4f` fallback
keeps it defensive and matches the gizmo path.

**Inspector caveat for csharp-dev/scene-architect:** removing the serialized `radius` field drops it
from the `playerPickupRadius` component inspector. This is intentional (worldState is now the source
of truth) and is additive-safe at runtime, but any scene/prefab that had a non-default value set in
the inspector would lose it. Verified default was `4f`; if a scene overrides it, that override is now
ignored — confirm the player object uses the default.

---

## 5. levelUpMenuController.cs — New Upgrade

### 5a. Enum (`StatKind`, line 7)
Add `PickupRadius`:
```csharp
private enum StatKind { MaxHP, FireRate, AttackDamage, MoveSpeed, Range, Defense, Regen, PickupRadius }
```

### 5b. Pool (`BuildPool()`, stats array lines 50–59)
Add `StatKind.PickupRadius` to the `stats` array. **Percent is NOT gated** — `pickupRadiusBase`
defaults to `4f` (> 0), so percent is never inert. No `continue` guard needed (unlike Defense/Regen
which start at 0). Both Flat and Percent are always offered.

### 5c. Labels (`LabelFor()`)
- Flat case (add to switch, lines 84–94): `case StatKind.PickupRadius: return "+0.5 Pickup Radius";`
- Percent case (add to switch, lines 98–107): `case StatKind.PickupRadius: return "+10% Pickup Radius";`

**Flat magnitude choice: +0.5.** Matches the Range flat step (`+0.5 Range`), which is the closest
analogue (both are world-space distances defaulting to 4f). +0.5 = +12.5% of base — a meaningful but
not overpowered increment, consistent with the game's other flat steps.

### 5d. Choose() mutation
- Flat branch (add to switch, lines 116–143):
```csharp
case StatKind.PickupRadius:
    worldState.instance.pickupRadiusBase += 0.5f;
    break;
```
- Percent branch (add to switch, lines 147–174):
```csharp
case StatKind.PickupRadius:
    worldState.instance.pickupRadiusMult *= 1.1f;
    break;
```

No special-case handling needed (unlike MaxHP, which reconciles `currentHP`). Pickup radius has no
dependent runtime state.

---

## 6. pauseStatsView.cs — Display Line

Append a Pickup Radius line to the stats string (`OnEnable()`, lines 14–19). After the Range line:

```csharp
string s = "Level: " + worldState.instance.level + "\n" +
           "HP: " + worldState.instance.currentHP + "/" + worldState.instance.MaxHP() + "\n" +
           "Damage: " + worldState.instance.AttackDamage().ToString("0.0") + "\n" +
           "Move Speed: " + worldState.instance.MoveSpeed().ToString("0.0") + "\n" +
           "Fire Rate: " + fireRate.ToString("0.0") + "/s" + "\n" +
           "Range: " + worldState.instance.Range().ToString("0.0") + "\n" +
           "Pickup Radius: " + worldState.instance.PickupRadius().ToString("0.0");
```

(Add the `+ "\n"` to the current final `Range` line, then append the `Pickup Radius` line.)

---

## 7. Risk Note & File:Line Anchors

**Touched files (4):**

| File | Anchor(s) | Change | Breaking? |
|---|---|---|---|
| `Assets/Scripts/worldState.cs` | after `:22` (regenMult), after `:31` (`Regen()`) | Add `pickupRadiusBase`/`pickupRadiusMult` fields + `PickupRadius()` getter | **Additive, non-breaking.** New public members only. |
| `Assets/Scripts/playerPickupRadius.cs` | `:5` (remove field), `:22` (`scan()` read), `:33` (gizmo read) | Read `worldState.instance.PickupRadius()`; remove local `radius` field | Behavior-preserving at runtime; **removes serialized inspector field** (see §4 caveat). |
| `Assets/Scripts/levelUpMenuController.cs` | `:7` (enum), `:50-59` (stats array), `:84-94` + `:98-107` (`LabelFor`), `:116-143` + `:147-174` (`Choose`) | Add `PickupRadius` StatKind, ungated pool entry, labels, mutations | Additive. |
| `Assets/Scripts/pauseStatsView.cs` | `:19` (Range line) | Append Pickup Radius display line | Additive, display-only. |

**Key risk:** The `worldState` field add is additive and will not break compilation. The *behavioral*
requirement is that **all radius consumers use the getter, not a cached/serialized field**. Since the
only consumer is `playerPickupRadius.cs` (grep-verified, §2), and it is migrated to call
`PickupRadius()` on every scan, upgrades apply live with no stale-cache risk. No pooling or
event-subscription reset is involved.

**Secondary risk (inspector):** Removing the serialized `radius` field means any scene/prefab
override is dropped. Confirmed default is `4f`; csharp-dev should verify no scene sets a non-default
radius on the player before removing the field. If an override exists, migrate that value into
`pickupRadiusBase` instead.

**Ordering for csharp-dev:** Add worldState members first (no dependents), then migrate the three
consumer files in any order. Compile after worldState change to confirm the getter exists before
referencing it.

---

## 8. Out of Scope

- No new gizmo colors, no attraction-speed changes (`pickupBehaviour` unchanged).
- No new UI buttons — reuses the existing 3-button roll.
- No save/load persistence (worldState is a runtime static-instance object, not serialized).
