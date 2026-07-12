using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Boss-drop item choice menu. On pickup, offers up to 3 random not-yet-owned
/// items; clicking one grants it. Mirrors the level-up menu's pause/populate/
/// click/resume pattern (levelUpManager + levelUpMenuController), folded into
/// one class. This component lives on an ALWAYS-ACTIVE object and toggles a
/// separate, initially-inactive panel — so its Awake runs and `instance` is
/// always available to itemPickup.
/// </summary>
public class itemChoiceMenuController : MonoBehaviour
{
    public static itemChoiceMenuController instance;

    [SerializeField] private GameObject menuPanel; // separate panel, starts INACTIVE
    [SerializeField] private Button[] buttons;     // 3, wired in Editor
    [SerializeField] private Text[] labels;        // 3, one per button (child of each button)

    private readonly string[] offered = new string[3];
    private int offeredCount;

    private void Awake()
    {
        instance = this;
    }

    /// <summary>Human-readable name shown on a choice button for each ItemId.</summary>
    private static string DisplayName(string id)
    {
        switch (id)
        {
            case ItemId.Cone:    return "Cone Shot";
            case ItemId.Bounce:  return "Ricochet";
            case ItemId.Fire:    return "Burning Rounds";
            case ItemId.Explode: return "Explosive Rounds";
            case ItemId.Freeze:  return "Frostbite";
            case ItemId.Aura:    return "Damage Aura";
            case ItemId.Robot:   return "Attack Bot";
            case ItemId.Trail:   return "Searing Trail";
            case ItemId.Grenade: return "Grenadier";
            default:             return id;
        }
    }

    /// <summary>
    /// Called by itemPickup on boss-drop collection. Presents up to 3 random
    /// not-yet-owned items and pauses the game. Returns true if a menu was
    /// shown; false if the player owns everything (caller grants nothing).
    /// </summary>
    public bool Offer()
    {
        var inv = playerInventory.instance;
        if (inv == null) return false;
        if (buttons == null || labels == null) return false;

        // Not-yet-owned pool (same predicate as itemManager.GrantRandomItem).
        List<string> candidates = new List<string>();
        foreach (string id in ItemId.All)
            if (!inv.Has(id)) candidates.Add(id);
        if (candidates.Count == 0) return false; // owns all -> no menu, no grant

        // Fisher-Yates shuffle (mirrors levelUpMenuController lines 31-37).
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            string tmp = candidates[i]; candidates[i] = candidates[j]; candidates[j] = tmp;
        }

        offeredCount = Mathf.Min(3, candidates.Count); // fewer-than-3 case
        for (int i = 0; i < buttons.Length; i++)
        {
            bool active = i < offeredCount;
            if (buttons[i] != null) buttons[i].gameObject.SetActive(active);
            if (!active) continue;

            offered[i] = candidates[i];
            if (labels[i] != null) labels[i].text = DisplayName(offered[i]);

            int idx = i; // capture fix — mirrors line 44
            buttons[i].onClick.RemoveAllListeners();
            buttons[i].onClick.AddListener(() => Choose(idx));
        }

        if (menuPanel != null) menuPanel.SetActive(true);
        Time.timeScale = 0f; // pause — mirrors levelUpManager.OpenMenu line 69
        return true;
    }

    private void Choose(int idx)
    {
        // Grant ONLY on a real choice.
        if (idx >= 0 && idx < offeredCount)
        {
            var inv = playerInventory.instance;
            if (inv != null && !inv.Has(offered[idx]))
                inv.Add(offered[idx]); // grant — playerInventory.cs:14
        }
        Close();
    }

    private void Close()
    {
        if (menuPanel != null) menuPanel.SetActive(false);
        Time.timeScale = 1f; // resume — mirrors levelUpManager line 88
    }
}
