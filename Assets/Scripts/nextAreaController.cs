using UnityEngine;

/// <summary>
/// Unlocks passage into the next area (region B) once all quest items have been
/// collected. Subscribes to questManager.OnAllQuestItemsCollected and, on fire,
/// disables the solid door collider so the player can walk through.
///
/// The DEFERRED region-B boss / entry hook can subscribe to OnDoorOpened — this
/// controller intentionally does NOT spawn the boss or trigger region-B entry.
/// </summary>
public class nextAreaController : MonoBehaviour
{
    [SerializeField] private Collider2D _doorCollider;
    [SerializeField] private GameObject _doorVisual;

    /// <summary>Fired once, after the door opens. Clean hook for the deferred
    /// next-area boss spawn / region-B entry logic.</summary>
    public event System.Action OnDoorOpened;

    private bool _opened;

    private void OnEnable()
    {
        questManager.OnAllQuestItemsCollected += OpenDoor;
    }

    private void OnDisable()
    {
        questManager.OnAllQuestItemsCollected -= OpenDoor;
    }

    private void OpenDoor()
    {
        if (_opened)
        {
            return;
        }
        _opened = true;

        if (_doorCollider != null)
        {
            _doorCollider.enabled = false;
        }

        if (_doorVisual != null)
        {
            // Placeholder: simply hide the door visual. TODO: swap for an
            // open-door sprite or play an open animation once art exists.
            _doorVisual.SetActive(false);
        }

        if (OnDoorOpened != null)
        {
            OnDoorOpened();
        }
    }
}
