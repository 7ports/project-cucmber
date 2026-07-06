using UnityEngine;

public class levelUpManager : MonoBehaviour
{
    public static levelUpManager instance;

    [SerializeField] private GameObject menuPanel;
    [SerializeField] private GameObject floatingTextPrefab;
    [SerializeField] private ParticleSystem playerLevelUpParticles;
    [SerializeField] private Vector3 textOffset = new Vector3(0f, 1.2f, 0f);

    private int pendingLevelUps;

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
        if (menuPanel != null && !menuPanel.activeSelf)
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

    private void OpenMenu()
    {
        if (menuPanel != null)
            menuPanel.SetActive(true);
        Time.timeScale = 0f;
    }

    public void ApplyChoiceAndAdvance()
    {
        pendingLevelUps--;
        if (pendingLevelUps > 0)
        {
            // re-open for next pick, still paused
            if (menuPanel != null)
            {
                menuPanel.SetActive(false);
                menuPanel.SetActive(true);
            }
        }
        else
        {
            if (menuPanel != null)
                menuPanel.SetActive(false);
            Time.timeScale = 1f;
        }
    }
}
