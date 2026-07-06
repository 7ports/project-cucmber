using UnityEngine;
using UnityEngine.SceneManagement;

public class gameOverManager : MonoBehaviour
{
    public static gameOverManager instance;
    [SerializeField] private GameObject gameOverPanel;   // hidden by default

    void Awake() { instance = this; }

    public void Show()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
        Time.timeScale = 0f;
    }

    // Wire this to the Game Over panel's Restart button OnClick
    public void Restart()
    {
        Time.timeScale = 1f;
        worldState.instance = null;   // force gameController.Start() to rebuild fresh stats
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
