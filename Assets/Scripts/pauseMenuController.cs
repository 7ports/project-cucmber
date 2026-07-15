using UnityEngine;

public class pauseMenuController : MonoBehaviour
{
    [SerializeField] private GameObject pauseRoot;
    [SerializeField] private KeyCode pauseKey = KeyCode.Escape;
    [SerializeField] private GameObject levelUpMenuPanel;
    [SerializeField] private GameObject gameOverPanel;
    private bool isPaused;

    void Update()
    {
        if (Input.GetKeyDown(pauseKey))
        {
            if (OtherPauseActive()) return;
            Toggle();
        }
    }

    bool OtherPauseActive()
    {
        return (levelUpMenuPanel != null && levelUpMenuPanel.activeSelf) ||
               (gameOverPanel != null && gameOverPanel.activeSelf);
    }

    void Toggle()
    {
        if (isPaused) Resume();
        else Pause();
    }

    void Pause()
    {
        isPaused = true;
        if (pauseRoot != null) pauseRoot.SetActive(true);
        Time.timeScale = 0f;
    }

    public void Resume()
    {
        isPaused = false;
        if (pauseRoot != null) pauseRoot.SetActive(false);
        if (!OtherPauseActive()) Time.timeScale = timescaleController.RunningTimeScale;
    }

    public void Quit()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    public void MainMenu()
    {
        Debug.Log("Main menu not implemented yet");
    }
}
