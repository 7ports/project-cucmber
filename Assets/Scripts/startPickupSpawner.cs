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

    private void Start()
    {
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
