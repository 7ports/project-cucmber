using System.Collections;
using UnityEngine;

public class levelUpManager : MonoBehaviour
{
    public static levelUpManager instance;

    [SerializeField] private GameObject menuPanel;
    [SerializeField] private GameObject slotMenuPanel;
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
    // the slot-machine panel on every 5th level, otherwise the normal menu.
    // Recomputed at each open because the level can change between queued level-ups.
    private GameObject SelectPanelForCurrentLevel()
    {
        if (slotMenuPanel != null && worldState.instance != null &&
            worldState.instance.level % 5 == 0)
            return slotMenuPanel;
        return menuPanel;
    }

    private void OpenMenu()
    {
        _activePanel = SelectPanelForCurrentLevel();
        if (_activePanel != null)
            _activePanel.SetActive(true);
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
            if (_activePanel != null)
                _activePanel.SetActive(true);
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
