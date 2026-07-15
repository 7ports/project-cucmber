using UnityEngine;

/// <summary>
/// Marks the end of the demo. Collecting all quest items only ARMS the demo
/// end: the quest-completion event sets _questComplete = true. The "DEMO OVER"
/// menu panel is opened (and the game paused) only once the player physically
/// REACHES THE DOOR — i.e. walks into the door's trigger collider while the
/// demo is armed.
///
/// A separate door-indicator script guides the player to the door on the same
/// quest-completion event. This controller intentionally does NOT advance to a
/// next level — reaching the door is the demo's end state.
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
    private bool _questComplete;

    private void OnEnable()
    {
        questManager.OnAllQuestItemsCollected += OnQuestComplete;
    }

    private void OnDisable()
    {
        questManager.OnAllQuestItemsCollected -= OnQuestComplete;
    }

    /// <summary>
    /// Fired when all quest items are collected. This only ARMS the demo end;
    /// the DEMO OVER menu opens later, when the player reaches the door trigger.
    /// Deliberately does not activate the panel or touch Time.timeScale.
    /// </summary>
    private void OnQuestComplete()
    {
        _questComplete = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!_questComplete || _opened)
        {
            return;
        }

        if (worldState.instance == null || worldState.instance.player == null)
        {
            return;
        }

        if (other.transform == worldState.instance.player ||
            other.transform.root == worldState.instance.player)
        {
            ShowDemoOver();
        }
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
