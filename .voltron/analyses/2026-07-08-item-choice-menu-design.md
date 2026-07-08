# Item-Choice Menu — Design (2026-07-08)

**Task:** Boss item pickups should present the player a choice of **3 random not-yet-owned items** (click one to grant) instead of auto-granting one. Mirror the existing level-up menu (pause / populate / click / resume). DESIGN ONLY — no implementation here.

---

## (a) Anchors in the real files

| What | File:Line | Detail |
|---|---|---|
| `GrantRandomItem()` (auto-grant, to be replaced) | `Assets/Scripts/itemManager.cs:23` | Builds `candidates` from `ItemId.All` filtering `!inv.Has(id)` (lines 33–41), picks one at random `Random.Range` (line 43), `inv.Add(chosen)` (line 44). |
| Item id constants + `All` pool | `Assets/Scripts/itemManager.cs:4-14` | `ItemId.Cone/Bounce/Fire/Explode/Freeze`, `ItemId.All = {…}`. |
| The pickup grant call (to be rerouted) | `Assets/Scripts/itemPickup.cs:11` | `itemManager.GrantRandomItem();` inside `OnTriggerEnter2D`, then `objectPool.instance.ret(gameObject)` (line 12). |
| Inventory API | `Assets/Scripts/playerInventory.cs:14,24,30` | `Add(string)`, `Has(string)`, `OwnedCount`; `instance` static (line 5). |
| **Populate + wire click** (the template) | `Assets/Scripts/levelUpMenuController.cs:23-48` | `OnEnable` shuffles a pool (Fisher-Yates, 31–37), sets `labels[i].text` (42), `buttons[i].onClick.RemoveAllListeners()` + `AddListener(() => Choose(rolled[idx]))` (45–46) with the **`int idx = i;` capture fix** (44). |
| Serialized button/label refs | `Assets/Scripts/levelUpMenuController.cs:18-19` | `[SerializeField] private Button[] buttons; // 3` and `Text[] labels; // 3`. |
| Apply on click | `Assets/Scripts/levelUpMenuController.cs:137` | `Choose(...)` → `levelUpManager.instance.ApplyChoiceAndAdvance()` (226). |
| **Pause** (timeScale=0 + show panel) | `Assets/Scripts/levelUpManager.cs:65-70` | `OpenMenu()`: `menuPanel.SetActive(true)` (68) then `Time.timeScale = 0f` (69). |
| **Resume** (timeScale=1 + hide panel) | `Assets/Scripts/levelUpManager.cs:72-90` | `ApplyChoiceAndAdvance()`: `menuPanel.SetActive(false)` (87) then `Time.timeScale = 1f` (88). |
| Always-active owner holds `instance` + panel ref | `Assets/Scripts/levelUpManager.cs:6,8,17-20` | `static instance` set in `Awake`; `[SerializeField] GameObject menuPanel`. |

### Critical structural fact from the template
The level-up UI is **two objects**: `levelUpManager` lives on an **always-active** GameObject (so its `Awake` runs and `instance` is always available, and it owns `Time.timeScale`), while `levelUpMenuController` lives on the **panel**, which starts **inactive** and populates in `OnEnable`. If we instead put our controller on an inactive panel, its `Awake` never runs and `instance` stays null — `itemPickup` could never call it. **Our controller must therefore live on an always-active object and toggle a separate panel GameObject** — exactly like `levelUpManager`.

---

## (b) Design decision — NEW controller (not reuse)

**Decision: add a NEW `itemChoiceMenuController.cs` + a NEW UI panel. Do NOT reuse the level-up infrastructure.**

Why not reuse:
- `levelUpMenuController` is hard-wired to stat upgrades: its `BuildPool`, `LabelFor`, and `Choose` operate on `StatKind`/`worldState`, not `ItemId`/`playerInventory`.
- `levelUpManager` carries a `pendingLevelUps` queue (multiple stacked level-ups) that has no analogue for a single boss drop.
- Sharing one panel would force mode-switching branches into proven code and risk regressions in the leveling flow.

Why a single class (fold manager+controller into one) instead of the level-up two-class split:
- Item choice is simpler — no pending queue, no re-open loop. Populating in an explicit `Offer()` (called by the pickup) is cleaner than `OnEnable`, and lets the pickup learn "nothing to offer" via the return value. The one class sits on the always-active object and toggles a child panel — preserving the always-active `instance` guarantee above.

### Drop-in code — `Assets/Scripts/itemChoiceMenuController.cs` (NEW)

```csharp
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
```

### Reroute the pickup — edit `Assets/Scripts/itemPickup.cs:11`

Replace the auto-grant line:

```csharp
// OLD (itemPickup.cs:11):
itemManager.GrantRandomItem();

// NEW:
if (itemChoiceMenuController.instance != null)
    itemChoiceMenuController.instance.Offer(); // opens menu (or no-op if owns all)
```

`objectPool.instance.ret(gameObject)` on line 12 stays — the pickup is consumed on contact regardless (the menu is already populated with the chosen items, so returning the pickup object is safe). `itemManager.GrantRandomItem()` can remain in the file unused (harmless), or be deleted in a later cleanup; leaving it avoids touching a proven path.

---

## (c) Display names for the 5 ItemIds

| ItemId constant | Value | Display name (button) | Effect (from itemManager comments) |
|---|---|---|---|
| `ItemId.Cone` | `CONE` | **Cone Shot** | fires 3 projectiles in a cone |
| `ItemId.Bounce` | `BOUNCE` | **Ricochet** | attack bounces once on final-target hit |
| `ItemId.Fire` | `FIRE` | **Burning Rounds** | burning DoT, stacks to 3, 10 dmg/s |
| `ItemId.Explode` | `EXPLODE` | **Explosive Rounds** | range-scaled explosion for 1/3 damage |
| `ItemId.Freeze` | `FREEZE` | **Frostbite** | chance to freeze enemies in place |

(Optional description sub-labels can reuse the "Effect" column if the panel adds a second `Text` per button; not required.)

---

## (d) EDITOR SETUP  ⚠️ REQUIRES UNITY EDITOR — scene-architect / user

The C# above cannot run until the UI panel exists and the serialized refs are wired **in the Editor** (Docker cannot do this). Mirror the level-up menu's panel exactly.

1. **Manager object (always active).** On an always-active GameObject (e.g. the same object family that carries `levelUpManager`, or a new `ItemChoiceManager` GameObject that is NOT disabled), add the `itemChoiceMenuController` component. This object must stay active so its `Awake` runs and `instance` registers.
2. **Panel (starts inactive).** Create a UI panel `ItemChoicePanel` under the Canvas — clone/mirror the level-up menu panel's layout. **Set it inactive** in the inspector (unchecked). Assign it to the controller's **Menu Panel** field.
3. **3 buttons + 3 labels.** Under the panel, create 3 `Button`s each with a child `Text`. Drag the 3 buttons into the controller's **Buttons** array (size 3) and the 3 child Texts into the **Labels** array (size 3), index-aligned — exactly as `levelUpMenuController.buttons`/`labels` are wired (`levelUpMenuController.cs:18-19`).
4. **Do NOT add persistent onClick listeners in the Editor** — click handlers are wired in code (`buttons[i].onClick.AddListener(...)` in `Offer()`), mirroring `levelUpMenuController` (which also wires at runtime, not via the inspector).
5. Ensure the panel's buttons render above other UI and that the Canvas/EventSystem still process clicks while `Time.timeScale == 0` (level-up menu already proves this works — use the same Canvas setup).

Handoff target: **scene-architect** (Editor exception — live Unity MCP) builds/wires the panel; **csharp-dev** writes the two file changes above.

---

## (e) Grep acceptance (run after implementation)

```bash
# New controller exists with the key surface:
test -f Assets/Scripts/itemChoiceMenuController.cs
grep -q 'public static itemChoiceMenuController instance' Assets/Scripts/itemChoiceMenuController.cs
grep -q 'public bool Offer'                               Assets/Scripts/itemChoiceMenuController.cs
grep -q 'Time.timeScale = 0f'                             Assets/Scripts/itemChoiceMenuController.cs
grep -q 'Time.timeScale = 1f'                             Assets/Scripts/itemChoiceMenuController.cs
grep -q 'Mathf.Min(3'                                     Assets/Scripts/itemChoiceMenuController.cs   # fewer-than-3 clamp
grep -q 'inv.Add(offered'                                 Assets/Scripts/itemChoiceMenuController.cs   # grant on click
grep -q 'onClick.AddListener'                             Assets/Scripts/itemChoiceMenuController.cs
grep -qE 'ItemId.(Cone|Bounce|Fire|Explode|Freeze)'      Assets/Scripts/itemChoiceMenuController.cs   # display-name switch

# Pickup rerouted away from auto-grant:
grep -q 'itemChoiceMenuController.instance.Offer' Assets/Scripts/itemPickup.cs
! grep -q 'itemManager.GrantRandomItem' Assets/Scripts/itemPickup.cs
```

---

## (f) Risks & edge cases

- **Fewer than 3 unowned:** `offeredCount = Mathf.Min(3, candidates.Count)`; unused buttons are `SetActive(false)`. With exactly 1 unowned, 1 button shows. With **0 unowned**, `Offer()` returns `false` → **no menu, no grant, game not paused** (pickup still consumed).
- **Never grant on close-without-choice:** grant happens ONLY in `Choose(idx)` under the `idx < offeredCount` guard. `Close()` grants nothing. If a "skip/cancel" button is later added, wire it to `Close()` — never to `Choose`.
- **Time pause/resume integrity:** `Offer()` sets `timeScale=0`, `Close()` restores `1`. **Collision risk with the level-up menu:** if a level-up menu opens while the item menu is up (or vice-versa), whichever closes first will set `timeScale=1` and unpause under the other. Boss drops and level-ups can plausibly coincide. Mitigation options for the implementer: guard `Offer()` to no-op (and defer) if `levelUpManager` menu is active, or centralize timeScale via a small pause stack. Flag for human decision.
- **`instance` null timing:** only holds if the controller sits on an always-active object (see Editor step 1). If placed on the inactive panel, `Awake` never fires and `itemPickup` silently skips the menu.
- **Duplicate-grant guard:** `Choose` re-checks `!inv.Has(offered[idx])` before `Add`, so a stale button can't double-add. `playerInventory.Add` itself does not dedupe (`playerInventory.cs:14`).
- **Pickup consumed even if menu not shown:** acceptable (matches current behavior where a pickup is always returned), but note the player loses the drop if they already own everything.

---

## Open questions (need human input)
1. Should a boss drop that coincides with a level-up be **queued** behind the level-up menu, or is simple "first-closed-unpauses" acceptable? (timeScale collision above.)
2. Do buttons need a **second Text** for the effect description, or is the display name alone enough?
3. Confirm the **display names** in (c) — these are proposed, not from any existing string table.
