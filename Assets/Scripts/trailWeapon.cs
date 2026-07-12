using UnityEngine;

/// <summary>
/// Searing Trail weapon (lives on the player). While the ItemId.Trail item is owned, drops a
/// pooled damaging trail segment behind the player every worldState.trailEmitDistance units of
/// travel. Emission is DISTANCE-based, not time-based, so standing still never stacks segments.
/// Activates when Trail is granted (playerInventory.OnItemAdded) or is already owned at Start.
/// </summary>
public class trailWeapon : MonoBehaviour
{
    [SerializeField] private GameObject _trailSegmentPrefab;   // wired in the Editor; one pooled instance per emit

    private bool _active;
    private Vector3 _lastEmitPos;

    private void Start()
    {
        if (playerInventory.instance != null)
        {
            playerInventory.instance.OnItemAdded += HandleItemAdded;
            if (playerInventory.instance.Has(ItemId.Trail)) Activate();
        }
    }

    private void OnDestroy()
    {
        // Balanced unsubscribe for the Start subscription.
        if (playerInventory.instance != null)
            playerInventory.instance.OnItemAdded -= HandleItemAdded;
    }

    private void HandleItemAdded(string itemId)
    {
        if (itemId == ItemId.Trail) Activate();
    }

    private void Activate()
    {
        if (_active) return;
        _active = true;
        _lastEmitPos = transform.position;   // start measuring travel from here so we don't dump a burst on pickup
    }

    private void Update()
    {
        if (!_active) return;

        float emitDist = worldState.instance != null ? worldState.instance.trailEmitDistance : 0.5f;
        if (emitDist <= 0f) emitDist = 0.5f;

        // Distance-based emission: drop one segment for every trailEmitDistance of travel since the
        // last drop. The loop keeps the trail evenly spaced on fast frames; a stationary player
        // travels 0 and emits nothing (no time-based stacking).
        float traveled = Vector3.Distance(transform.position, _lastEmitPos);
        while (traveled >= emitDist)
        {
            Vector3 dir = (transform.position - _lastEmitPos).normalized;
            _lastEmitPos += dir * emitDist;
            EmitSegment(_lastEmitPos);
            traveled = Vector3.Distance(transform.position, _lastEmitPos);
        }
    }

    private void EmitSegment(Vector3 pos)
    {
        if (_trailSegmentPrefab == null || objectPool.instance == null) return;   // null-guard: no prefab wired / no pool yet
        objectPool.instance.get(_trailSegmentPrefab, pos, Quaternion.identity);
    }
}
