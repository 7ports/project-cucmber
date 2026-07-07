using UnityEngine;
using UnityEngine.Tilemaps;

public class questManager : MonoBehaviour
{
    public static questManager instance;

    [SerializeField] private GameObject questItemPrefab;
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private LayerMask wallsMask;
    [SerializeField] private int questCount = 3;
    [SerializeField] private float minSeparation = 3f;

    private readonly System.Collections.Generic.List<questItem> active = new System.Collections.Generic.List<questItem>();
    private int collected;

    public static event System.Action OnAllQuestItemsCollected;

    void Awake()
    {
        instance = this;
    }

    void Start()
    {
        SpawnItems();
    }

    void SpawnItems()
    {
        if (questItemPrefab == null || groundTilemap == null)
        {
            Debug.LogWarning("questManager: questItemPrefab or groundTilemap not assigned - cannot spawn quest items.");
            return;
        }

        // Build candidate world positions from ground tiles that are not overlapping a wall.
        System.Collections.Generic.List<Vector3> candidates = new System.Collections.Generic.List<Vector3>();
        BoundsInt bounds = groundTilemap.cellBounds;
        Vector2 boxSize = groundTilemap.cellSize * 0.9f;

        foreach (Vector3Int cellPos in bounds.allPositionsWithin)
        {
            if (!groundTilemap.HasTile(cellPos)) continue;

            Vector3 world = groundTilemap.GetCellCenterWorld(cellPos);
            if (Physics2D.OverlapBox(world, boxSize, 0f, wallsMask) == null)
            {
                candidates.Add(world);
            }
        }

        // Shuffle candidates (Fisher-Yates).
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            Vector3 tmp = candidates[i];
            candidates[i] = candidates[j];
            candidates[j] = tmp;
        }

        // Greedily pick positions at least minSeparation apart.
        System.Collections.Generic.List<Vector3> picked = new System.Collections.Generic.List<Vector3>();
        float minSepSqr = minSeparation * minSeparation;
        foreach (Vector3 candidate in candidates)
        {
            if (picked.Count >= questCount) break;

            bool farEnough = true;
            foreach (Vector3 p in picked)
            {
                if ((p - candidate).sqrMagnitude < minSepSqr)
                {
                    farEnough = false;
                    break;
                }
            }

            if (farEnough)
            {
                picked.Add(candidate);
            }
        }

        if (picked.Count < questCount)
        {
            Debug.LogWarning("questManager: only found " + picked.Count + " valid spawn position(s) out of " + questCount + " requested.");
        }

        foreach (Vector3 pos in picked)
        {
            GameObject go = Instantiate(questItemPrefab, pos, Quaternion.identity);
            questItem qi = go.GetComponent<questItem>();
            if (qi != null)
            {
                active.Add(qi);
            }
        }
    }

    public void Collect(questItem q)
    {
        active.Remove(q);
        collected++;
        if (collected >= questCount)
        {
            Debug.Log("Quest complete - ready for next part");
            if (OnAllQuestItemsCollected != null)
            {
                OnAllQuestItemsCollected();
            }
        }
    }

    public System.Collections.Generic.List<questItem> ActiveItems() => active;
}
