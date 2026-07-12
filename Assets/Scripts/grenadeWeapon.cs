using System.Collections;
using UnityEngine;

/// <summary>
/// Grenadier item weapon (sits on the player). Once the player owns ItemId.Grenade,
/// lobs a pooled grenade every worldState.grenadeInterval seconds, launched OPPOSITE
/// the player's current move direction (falls back to the last-faced direction when idle).
/// The throw loop is a coroutine with an explicit stop path in OnDisable.
/// </summary>
public class grenadeWeapon : MonoBehaviour
{
    [SerializeField] private GameObject _grenadePrefab;   // wired in Editor; null-guarded
    [SerializeField] private float _throwDistance = 3f;   // how far opposite move dir the grenade lands

    private Vector2 _lastFacing = Vector2.down;   // fallback aim when idle
    private Coroutine _routine;
    private bool _active;

    void Start()
    {
        if (playerInventory.instance != null)
        {
            playerInventory.instance.OnItemAdded += OnItemAdded;
            if (playerInventory.instance.Has(ItemId.Grenade)) Activate();
        }
    }

    void OnDisable()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }
    }

    void OnDestroy()
    {
        if (playerInventory.instance != null)
            playerInventory.instance.OnItemAdded -= OnItemAdded;
    }

    void Update()
    {
        // Mirror playerMovement's Input read so we track the current move direction
        // for aim + a persisted last-facing fallback (playerMovement exposes no accessor).
        Vector2 move = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        if (move.sqrMagnitude > 1e-4f) _lastFacing = move.normalized;
    }

    private void OnItemAdded(string id)
    {
        if (id == ItemId.Grenade) Activate();
    }

    private void Activate()
    {
        if (_active) return;
        _active = true;
        if (_routine == null) _routine = StartCoroutine(ThrowLoop());
    }

    private IEnumerator ThrowLoop()
    {
        while (true)
        {
            float interval = worldState.instance != null ? worldState.instance.grenadeInterval : 2f;
            yield return new WaitForSeconds(Mathf.Max(0.05f, interval));
            ThrowOne();
        }
    }

    private void ThrowOne()
    {
        if (_grenadePrefab == null || objectPool.instance == null) return;

        Vector2 aim = _lastFacing.sqrMagnitude > 1e-4f ? _lastFacing.normalized : Vector2.down;
        Vector2 opposite = -aim;   // launch OPPOSITE the move direction

        GameObject go = objectPool.instance.get(_grenadePrefab, transform.position, Quaternion.identity);
        if (go == null) return;
        grenade g = go.GetComponent<grenade>();
        if (g != null) g.Throw(opposite * _throwDistance);
    }
}
