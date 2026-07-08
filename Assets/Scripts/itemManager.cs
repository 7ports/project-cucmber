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
