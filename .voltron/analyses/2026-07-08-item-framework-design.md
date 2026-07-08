# Item Framework Design — project-cucumber

**Date:** 2026-07-08
**Author:** project-planner (Tier 2, design only)
**Scope:** Foundational item framework the five weapon-modifying items plug into. Defines the **contract**: five canonical item-id keys + the ownership-query API (`playerInventory.Has`). Grounded in the three real source files.

---

## 1. Overview

Bosses already drop `itemPickup.prefab`. On pickup, `itemPickup.OnTriggerEnter2D` → `itemManager.GrantRandomItem()` → `playerInventory.instance.Add("PlaceholderItem")`. This design replaces the placeholder with a **five-item pool granted random-without-duplicates**, and adds a boolean **ownership query** the weapon/projectile code calls to branch behaviour. No new prefab, scene, or Editor wiring — the existing boss→pickup→grant path is reused verbatim.

---

## 2. Anchors (real file references)

### `Assets/Scripts/playerInventory.cs`
- **Class + singleton:** `public class playerInventory : MonoBehaviour` (L3); `public static playerInventory instance;` (L5); assigned in `Awake()` (L9–12).
- **Storage type (confirmed):** `public System.Collections.Generic.List<string> items = new ...();` (L7) — a `List<string>`.
- **Add method (confirmed):** `public void Add(string item) { items.Add(item); }` (L14–17).
- **No `Has`/query method exists yet** — must be added (§5).

### `Assets/Scripts/itemManager.cs`
- **Class:** `public static class itemManager` (L3).
- **Grant entry point:** `public static void GrantRandomItem()` (L6). Body currently grants the literal `"PlaceholderItem"` (L9) via `playerInventory.instance.Add(placeholder)` (L11), guarded by `instance != null` (L10). This whole method body is replaced (§4).

### `Assets/Scripts/itemPickup.cs`
- **Grant call site (unchanged):** `itemManager.GrantRandomItem();` at **L11**, inside `OnTriggerEnter2D(Collider2D other)` (L5), after the player-identity check against `worldState.instance.player` (L8–9). Pickup is returned to pool on L12. **No change needed here** — it calls the same zero-arg method.

---

## 3. Canonical item-id keys (THE CONTRACT)

Declared as a **single source of truth**: a new static class `ItemId` inside `itemManager.cs` (no new file — keeps the contract co-located with the granter). All later item designs and weapon/projectile code reference these constants, never raw string literals.

| Item | Constant | String value |
|------|----------|--------------|
| Cone 3-shot | `ItemId.Cone` | `"CONE"` |
| Bounce | `ItemId.Bounce` | `"BOUNCE"` |
| Fire (burning DoT) | `ItemId.Fire` | `"FIRE"` |
| Explosion | `ItemId.Explode` | `"EXPLODE"` |
| Freeze | `ItemId.Freeze` | `"FREEZE"` |

The string values are the stable wire format stored in `playerInventory.items` and passed to `Has(...)`. Constants are the compile-time reference. `ItemId.All` exposes the pool for the granter.

---

## 4. Drop-in code — revised `itemManager.cs`

**Decision — random WITHOUT duplicates (justified):** Each boss kill is a meaningful reward; re-granting an already-owned item would feel like a wasted drop. With only five items the player caps out after five distinct boss drops, at which point we grant nothing (log it) rather than a junk duplicate. This is the "prefer no-duplicates so each drop is meaningful" directive. Plain-random was rejected: it produces frequent no-op duplicates and makes the five-item ceiling feel arbitrary.

Replace the entire body of `itemManager.cs` with:

```csharp
using UnityEngine;

/// <summary>Canonical, stable item identifiers — the single source of truth for the item contract.</summary>
public static class ItemId
{
    public const string Cone    = "CONE";    // fires 3 projectiles in a cone instead of 1
    public const string Bounce  = "BOUNCE";  // attack bounces once on final-target hit (not on range expiry)
    public const string Fire    = "FIRE";    // burning DoT, stacks up to 3, 10 dmg/sec
    public const string Explode = "EXPLODE"; // range-scaled explosion for 1/3 attack damage
    public const string Freeze  = "FREEZE";  // chance to freeze enemies in place

    /// <summary>The full grantable pool, in declaration order.</summary>
    public static readonly string[] All = { Cone, Bounce, Fire, Explode, Freeze };
}

public static class itemManager
{
    /// <summary>
    /// Grants one random weapon-upgrade item the player does NOT already own
    /// (random-without-duplicates). If the player owns all five, grants nothing.
    /// Called by itemPickup.OnTriggerEnter2D on boss-drop pickup.
    /// </summary>
    public static void GrantRandomItem()
    {
        var inv = playerInventory.instance;
        if (inv == null)
        {
            Debug.LogWarning("[itemManager] No playerInventory.instance; cannot grant item.");
            return;
        }

        // Build the set of not-yet-owned items.
        var candidates = new System.Collections.Generic.List<string>();
        foreach (string id in ItemId.All)
            if (!inv.Has(id)) candidates.Add(id);

        if (candidates.Count == 0)
        {
            Debug.Log("[itemManager] Player already owns all items; no grant.");
            return;
        }

        string chosen = candidates[Random.Range(0, candidates.Count)];
        inv.Add(chosen);
        Debug.Log("[itemManager] Granted '" + chosen + "'. Inventory size: " + inv.items.Count);
    }
}
```

Notes:
- `Random.Range(0, count)` is `UnityEngine.Random`, upper-bound exclusive — correct for list indexing.
- Duplicate-avoidance uses the same `Has` query defined below, so ownership semantics have one definition.
- `itemPickup.cs` L11 is unchanged — still calls `itemManager.GrantRandomItem()`.

---

## 5. Drop-in code — `playerInventory.Has` query (THE query API)

Add the `Has` method (and a convenience `OwnedCount`) to `playerInventory.cs`. **This is the exact method the five item implementations will call.**

**Exact signature:** `public bool Has(string itemId)`

Insert after the existing `Add` method (after L17), inside the class:

```csharp
    /// <summary>
    /// Returns true if the player currently owns the given item.
    /// itemId must be one of the ItemId.* constants (e.g. ItemId.Fire).
    /// This is THE ownership query weapon/projectile code calls.
    /// </summary>
    public bool Has(string itemId)
    {
        return items.Contains(itemId);
    }

    /// <summary>Number of distinct upgrade items currently owned (used by the granter for the all-owned check).</summary>
    public int OwnedCount => items.Count;
```

Semantics: `items` is a `List<string>`; grants are duplicate-free (§4), so `Contains` is an exact ownership test. Case-sensitive — always pass an `ItemId.*` constant, never a hand-typed literal.

---

## 6. CONTRACT FOR ITEM IMPLEMENTATIONS

Later item designs and their weapon/projectile implementations MUST use exactly this contract:

- **Item keys (reference the constants, never raw strings):**
  `ItemId.Cone` (`"CONE"`), `ItemId.Bounce` (`"BOUNCE"`), `ItemId.Fire` (`"FIRE"`), `ItemId.Explode` (`"EXPLODE"`), `ItemId.Freeze` (`"FREEZE"`) — declared in `Assets/Scripts/itemManager.cs`.
- **Ownership query (the one call that gates every item's behaviour):**
  ```csharp
  if (playerInventory.instance != null && playerInventory.instance.Has(ItemId.Fire))
  {
      // apply burning DoT
  }
  ```
  Exact signature: `bool playerInventory.Has(string itemId)`. Always null-guard `playerInventory.instance` at call sites in shooter/`projectileBehaviour` code.
- **Granting is owned by `itemManager.GrantRandomItem()`** — item implementations never grant; they only read ownership via `Has`.
- **Where each item hooks (for later designs, not this doc):** Cone → shooter/weapon spawn count; Bounce/Fire/Explode/Freeze → `projectileBehaviour` on-hit logic. Each checks `Has(ItemId.X)` at its hook point.

---

## 7. Grep acceptance checklist

Run after implementation lands:

```bash
# Contract constants exist in one place
grep -n 'ItemId' Assets/Scripts/itemManager.cs
grep -n '"CONE"\|"BOUNCE"\|"FIRE"\|"EXPLODE"\|"FREEZE"' Assets/Scripts/itemManager.cs

# Query API exists with exact signature
grep -n 'public bool Has(string itemId)' Assets/Scripts/playerInventory.cs

# Granter rewritten, placeholder gone
grep -n 'GrantRandomItem' Assets/Scripts/itemManager.cs
grep -n 'PlaceholderItem' Assets/Scripts/itemManager.cs   # expect: no matches

# Pickup call site unchanged
grep -n 'itemManager.GrantRandomItem' Assets/Scripts/itemPickup.cs   # expect L11
```

Expected: constants present; `Has` present; `PlaceholderItem` absent; pickup still calls `GrantRandomItem()`.

---

## 8. Editor / prefab change needed?

**No.** `itemPickup.prefab` and the boss `deathDropPrefab` wiring are untouched — the pickup already calls the zero-arg `itemManager.GrantRandomItem()`, and its signature is unchanged. `playerInventory` remains a `MonoBehaviour` singleton on its existing GameObject; adding the `Has`/`OwnedCount` methods requires no inspector fields. This is a **script-only** change (`itemManager.cs` rewrite + two methods added to `playerInventory.cs`). No scene, prefab, or serialized-field edits.

---

## 9. Open questions for human input

1. **All-owned behaviour:** current design grants nothing once all five are owned. Alternative: grant a fallback stat-boost or duplicate for stacking. Confirm "grant nothing" is acceptable end-game behaviour.
2. **Persistence:** `playerInventory.items` is runtime-only (no save/load seen). Confirm items are not expected to persist across runs.
3. **Stacking:** contract assumes one-of-each (boolean ownership). If any item should be stackable (multiple Cone → more projectiles), `Has` is insufficient and we'd need an `int CountOf(itemId)` — flag now before item designs assume boolean.
