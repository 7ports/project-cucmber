using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class pauseItemsView : MonoBehaviour
{
    [SerializeField] private TMP_Text itemsText;

    void OnEnable()
    {
        if (itemsText != null)
        {
            if (playerInventory.instance == null || playerInventory.instance.items == null ||
                playerInventory.instance.items.Count == 0)
            {
                itemsText.text = "No items yet";
            }
            else
            {
                itemsText.text = string.Join("\n", playerInventory.instance.items);
            }
        }
    }
}
