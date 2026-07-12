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
