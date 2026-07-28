using UnityEngine;

public class playerInventory : MonoBehaviour
{
    public static playerInventory instance;

    public System.Collections.Generic.List<string> items = new System.Collections.Generic.List<string>();

    /// <summary>Fired AFTER an item id is added to the inventory (used by item weapon components to self-register).</summary>
    public event System.Action<string> OnItemAdded;

    void Awake()
    {
        instance = this;
    }

    public void Add(string item)
    {
        items.Add(item);

        // Cone Shot tradeoff: fires a 3-shot spread but reduces ALL outgoing player
        // damage by 1/3 via the global damage modifier (2/3 output). Set-based +
        // guarded on this single grant chokepoint, so it can never double-apply.
        if (item == ItemId.Cone && worldState.instance != null)
            worldState.instance.SetDamageModifier(1f / 3f);

        OnItemAdded?.Invoke(item);
    }

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
}
