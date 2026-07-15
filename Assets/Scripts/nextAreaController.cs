using UnityEngine;

/// <summary>
/// Marks the end of the demo. Once all quest items have been collected, this
/// controller subscribes to questManager.OnAllQuestItemsCollected and, on fire,
/// opens a "DEMO OVER" menu panel and pauses the game (Time.timeScale = 0).
///
/// It intentionally does NOT disable the door collider or advance to a next
/// level — reaching the door is the demo's end state.
/// </summary>
public class nextAreaController : MonoBehaviour
{
    [SerializeField] private Collider2D _doorCollider;
    [SerializeField] private GameObject _doorVisual;

    [SerializeField] private GameObject _demoOverPanel;

    /// <summary>Fired once, when the demo-over menu is shown. Harmless hook
    /// retained for anything that wants to react to the demo ending.</summary>
    public event System.Action OnDoorOpened;

    private bool _opened;

    private void OnEnable()
    {
        questManager.OnAllQuestItemsCollected += ShowDemoOver;
    }

    private void OnDisable()
    {
        questManager.OnAllQuestItemsCollected -= ShowDemoOver;
    }

    private void ShowDemoOver()
    {
        if (_opened)
        {
            return;
        }
        _opened = true;

        if (_demoOverPanel != null)
        {
            _demoOverPanel.SetActive(true);
        }

        // Pause the game — the demo has ended.
        Time.timeScale = 0f;

        if (OnDoorOpened != null)
        {
            OnDoorOpened();
        }
    }
}
