using UnityEngine;
using UnityEngine.Tilemaps;

// Spawns a configurable number of starter powerup pickups on random floor tiles
// anywhere on the map (like quest items) the moment XP-doubling activates.
// One-shot spawn — no pooling. Guarded by _spawned so it only ever fires once.
public class startPickupSpawner : MonoBehaviour
{
    [SerializeField] private GameObject _vacuumPickupPrefab;
    [SerializeField] private GameObject _itemPickupPrefab;

    [SerializeField] private int _vacuumCount = 3;
    [SerializeField] private int _itemCount = 1;

    // Wired to the SAME ground tilemap questManager uses.
    [SerializeField] private Tilemap _groundTilemap;

    // Bounded retries to find a floored cell before giving up on a single pickup.
    private const int _maxPlacementAttempts = 64;

    private bool _spawned;

    private void Update()
    {
        if (_spawned)
        {
            return;
        }

        // Fire once the moment XP-doubling activates (time threshold, not an event).
        if (worldState.instance != null
            && worldState.instance.xpDoubleThreshold > 0f
            && Time.timeSinceLevelLoad >= worldState.instance.xpDoubleThreshold)
        {
            _spawned = true;
            SpawnPickups(_vacuumPickupPrefab, _vacuumCount, "vacuum");
            SpawnPickups(_itemPickupPrefab, _itemCount, "item");
        }
    }

    private void SpawnPickups(GameObject prefab, int count, string label)
    {
        if (prefab == null)
        {
            Debug.LogWarning($"[startPickupSpawner] {label} pickup prefab is not assigned — skipping {count} spawn(s).");
            return;
        }

        if (_groundTilemap == null)
        {
            Debug.LogWarning($"[startPickupSpawner] _groundTilemap is not assigned — skipping {count} {label} spawn(s).");
            return;
        }

        for (int i = 0; i < count; i++)
        {
            if (TryGetRandomFloorPosition(out Vector3 pos))
            {
                Instantiate(prefab, pos, Quaternion.identity);
            }
            else
            {
                Debug.LogWarning($"[startPickupSpawner] no floor tile found for {label} pickup after {_maxPlacementAttempts} attempts — skipping.");
            }
        }
    }

    // Picks a random cell within the ground tilemap bounds that HasTile(...),
    // mirroring questManager's floor-tile scatter. Returns the cell center in world space.
    private bool TryGetRandomFloorPosition(out Vector3 world)
    {
        BoundsInt bounds = _groundTilemap.cellBounds;

        for (int attempt = 0; attempt < _maxPlacementAttempts; attempt++)
        {
            int x = Random.Range(bounds.xMin, bounds.xMax);
            int y = Random.Range(bounds.yMin, bounds.yMax);
            Vector3Int cell = new Vector3Int(x, y, 0);

            if (_groundTilemap.HasTile(cell))
            {
                world = _groundTilemap.GetCellCenterWorld(cell);
                return true;
            }
        }

        world = Vector3.zero;
        return false;
    }
}
