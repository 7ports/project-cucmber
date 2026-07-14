using System.Collections;
using UnityEngine;

public class levelUpManager : MonoBehaviour
{
    public static levelUpManager instance;

    [SerializeField] private GameObject menuPanel;
    [SerializeField] private GameObject slotMenuPanel;
    [SerializeField] private GameObject itemMenuPanel;
    [SerializeField] private GameObject floatingTextPrefab;
    [SerializeField] private ParticleSystem playerLevelUpParticles;
    [SerializeField] private Vector3 textOffset = new Vector3(0f, 1.2f, 0f);
    [SerializeField] private float menuOpenDelay = 1f;

    private int pendingLevelUps;
    private bool menuScheduled;
    private GameObject _activePanel;

    private void Awake()
    {
        instance = this;
    }

    private void OnEnable()
    {
        worldState.OnLevelUp += HandleLevelUp;
    }

    private void OnDisable()
    {
        worldState.OnLevelUp -= HandleLevelUp;
    }

    private void HandleLevelUp()
    {
        pendingLevelUps++;
        PlayJuice();
        if (menuPanel != null && !menuPanel.activeSelf && !menuScheduled)
        {
            menuScheduled = true;
            StartCoroutine(OpenMenuAfterDelay());
        }
    }

    private IEnumerator OpenMenuAfterDelay()
    {
        yield return new WaitForSecondsRealtime(menuOpenDelay);
        menuScheduled = false;
        OpenMenu();
    }

    private void PlayJuice()
    {
        if (playerLevelUpParticles != null)
            playerLevelUpParticles.Play();

        if (floatingTextPrefab != null && objectPool.instance != null &&
            worldState.instance != null && worldState.instance.player != null)
        {
            objectPool.instance.get(
                floatingTextPrefab,
                worldState.instance.player.position + textOffset,
                Quaternion.identity);
        }
    }

    // Returns the panel to show for the CURRENT player level:
    // the item panel on every 10th level (only if there are unowned items to offer),
    // the slot-machine panel on every 3rd level, otherwise the normal menu.
    // Recomputed at each open because the level can change between queued level-ups.
    private GameObject SelectPanelForCurrentLevel()
    {
        var ws = worldState.instance;
        if (ws != null)
        {
            if (itemMenuPanel != null && ws.level % 10 == 0 &&
                itemChoiceMenuController.instance != null && itemChoiceMenuController.instance.HasOffers())
                return itemMenuPanel;
            if (slotMenuPanel != null && ws.level % 3 == 0)   // slot every 3rd level (was 5)
                return slotMenuPanel;
        }
        return menuPanel;
    }

    // Shows a panel. The item panel is populated via the item-choice controller
    // (which activates its own panel but does NOT touch timeScale — this manager
    // remains the sole Time.timeScale owner). Other panels are simply activated.
    private void ActivatePanel(GameObject panel)
    {
        if (panel == itemMenuPanel && itemChoiceMenuController.instance != null)
            itemChoiceMenuController.instance.OpenForLevelUp();
        else if (panel != null)
            panel.SetActive(true);
    }

    private void OpenMenu()
    {
        _activePanel = SelectPanelForCurrentLevel();
        ActivatePanel(_activePanel);
        Time.timeScale = 0f;
    }

    public void ApplyChoiceAndAdvance()
    {
        pendingLevelUps--;
        if (pendingLevelUps > 0)
        {
            // re-open for next pick, still paused; recompute for the new current level
            if (_activePanel != null)
                _activePanel.SetActive(false);
            _activePanel = SelectPanelForCurrentLevel();
            ActivatePanel(_activePanel);
        }
        else
        {
            if (_activePanel != null)
                _activePanel.SetActive(false);
            _activePanel = null;
            Time.timeScale = 1f;
        }
    }
}
