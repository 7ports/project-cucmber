using UnityEngine;

// Spawns a configurable number of starter powerup pickups at random points
// within a play-area when the run begins. One-shot spawn on Start() — no pooling.
public class startPickupSpawner : MonoBehaviour
{
    [SerializeField] private GameObject _vacuumPickupPrefab;
    [SerializeField] private GameObject _itemPickupPrefab;

    [SerializeField] private int _vacuumCount = 3;
    [SerializeField] private int _itemCount = 1;

    [SerializeField] private Vector2 _areaCenter = Vector2.zero;
    [SerializeField] private Vector2 _areaSize = new Vector2(30f, 20f);

    // Starter powerups are delayed until the player's FIRST level-up ('xp scale up')
    // rather than spawned at run start. Guarded one-shot via _spawned.
    private bool _spawned;

    private void OnEnable()
    {
        worldState.OnLevelUp += HandleFirstLevelUp;
    }

    private void OnDisable()
    {
        worldState.OnLevelUp -= HandleFirstLevelUp;
    }

    private void HandleFirstLevelUp()
    {
        if (_spawned)
        {
            return;
        }
        _spawned = true;

        // Only ever fires once — unsubscribe immediately so the handler never leaks.
        worldState.OnLevelUp -= HandleFirstLevelUp;

        SpawnPickups(_vacuumPickupPrefab, _vacuumCount, "vacuum");
        SpawnPickups(_itemPickupPrefab, _itemCount, "item");
    }

    private void SpawnPickups(GameObject prefab, int count, string label)
    {
        if (prefab == null)
        {
            Debug.LogWarning($"[startPickupSpawner] {label} pickup prefab is not assigned — skipping {count} spawn(s).");
            return;
        }

        for (int i = 0; i < count; i++)
        {
            Vector2 pos = RandomPointInArea();
            Instantiate(prefab, pos, Quaternion.identity);
        }
    }

    private Vector2 RandomPointInArea()
    {
        Vector2 extents = _areaSize * 0.5f;
        float x = _areaCenter.x + Random.Range(-extents.x, extents.x);
        float y = _areaCenter.y + Random.Range(-extents.y, extents.y);
        return new Vector2(x, y);
    }
}
