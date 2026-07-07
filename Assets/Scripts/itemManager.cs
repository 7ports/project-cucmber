using UnityEngine;

public static class itemManager
{
    // Placeholder: real item pool + effects arrive next sprint.
    public static void GrantRandomItem()
    {
        // TODO(items-sprint): replace with real weighted item selection + effect application.
        string placeholder = "PlaceholderItem";
        if (playerInventory.instance != null)
            playerInventory.instance.Add(placeholder);
        Debug.Log("[itemManager] Granted placeholder item. Inventory size: " +
                  (playerInventory.instance != null ? playerInventory.instance.items.Count : 0));
    }
}
