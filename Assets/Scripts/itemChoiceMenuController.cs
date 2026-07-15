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
    private bool _fromLevelUp;

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
        _fromLevelUp = false;
        if (!PopulateOffers()) return false; // owns all -> no menu, no grant
        if (menuPanel != null) menuPanel.SetActive(true);
        Time.timeScale = 0f; // pause — mirrors levelUpManager.OpenMenu line 69
        return true;
    }

    /// <summary>
    /// Called by levelUpManager to reuse this picker as a level-up reward menu.
    /// Populates and shows the panel but does NOT touch Time.timeScale — the
    /// level-up manager already owns the pause. Returns true if a menu was shown;
    /// false if the player owns everything.
    /// </summary>
    public bool OpenForLevelUp()
    {
        _fromLevelUp = true;
        if (!PopulateOffers()) return false;
        if (menuPanel != null) menuPanel.SetActive(true);
        return true; // no Time.timeScale — levelUpManager already paused
    }

    /// <summary>
    /// True if the player is missing at least one item (i.e. a level-up offer
    /// would have candidates). Pure predicate — no UI, no pause. Used by
    /// levelUpManager as an owns-all guard.
    /// </summary>
    public bool HasOffers()
    {
        var inv = playerInventory.instance;
        if (inv == null) return false;
        foreach (string id in ItemId.All)
            if (!inv.Has(id)) return true;
        return false;
    }

    /// <summary>
    /// Builds the not-yet-owned candidate pool, shuffles it, and populates the
    /// choice buttons/labels. Returns false when the player owns everything
    /// (no candidates), true otherwise. Does NOT touch Time.timeScale and does
    /// NOT call menuPanel.SetActive — callers own visibility and pause.
    /// </summary>
    private bool PopulateOffers()
    {
        var inv = playerInventory.instance;
        if (inv == null) return false;
        if (buttons == null || labels == null) return false;

        // Not-yet-owned pool (same predicate as itemManager.GrantRandomItem).
        List<string> candidates = new List<string>();
        foreach (string id in ItemId.All)
            if (!inv.Has(id)) candidates.Add(id);
        if (candidates.Count == 0) return false; // owns all -> no candidates

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

        if (!_fromLevelUp)
        {
            Close(); // boss-drop: panel off + resume time
        }
        else
        {
            // Level-up: hide panel but leave Time.timeScale to levelUpManager,
            // then advance the level-up queue.
            if (menuPanel != null) menuPanel.SetActive(false);
            if (levelUpManager.instance != null) levelUpManager.instance.ApplyChoiceAndAdvance();
        }
    }

    private void Close()
    {
        if (menuPanel != null) menuPanel.SetActive(false);
        Time.timeScale = timescaleController.RunningTimeScale; // resume — mirrors levelUpManager line 88
    }
}
