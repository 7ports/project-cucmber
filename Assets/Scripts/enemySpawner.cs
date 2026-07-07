using UnityEngine;

public class enemySpawner : MonoBehaviour
{
    [System.Serializable]
    private struct SpawnEntry
    {
        public GameObject prefab;
        public int minLevel;   // eligible when worldState.level >= minLevel
    }

    [SerializeField] private SpawnEntry[] spawnTable;
    private readonly System.Collections.Generic.List<GameObject> eligible = new System.Collections.Generic.List<GameObject>();
    [SerializeField] private float spawnInterval = 2f;
    [SerializeField] private float edgeMargin = 0.08f;
    private float spawnTimer;

    void Update()
    {
        if (worldState.instance == null || worldState.instance.player == null) return;
        if (objectPool.instance == null || spawnTable == null || spawnTable.Length == 0) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        spawnTimer += Time.deltaTime;
        float interval = (worldState.instance != null) ? worldState.instance.currentSpawnInterval : spawnInterval;
        if (spawnTimer >= interval)
        {
            spawnTimer = 0f;
            int lvl = worldState.instance != null ? worldState.instance.level : 1;
            eligible.Clear();
            for (int i = 0; i < spawnTable.Length; i++)
                if (spawnTable[i].prefab != null && lvl >= spawnTable[i].minLevel)
                    eligible.Add(spawnTable[i].prefab);
            if (eligible.Count == 0) return;
            GameObject prefab = eligible[Random.Range(0, eligible.Count)];

            int side = Random.Range(0, 4);
            Vector3 vp;
            if (side == 0)      vp = new Vector3(-edgeMargin, Random.value, 0f);       // left
            else if (side == 1) vp = new Vector3(1f + edgeMargin, Random.value, 0f);   // right
            else if (side == 2) vp = new Vector3(Random.value, -edgeMargin, 0f);       // bottom
            else                vp = new Vector3(Random.value, 1f + edgeMargin, 0f);   // top
            vp.z = Mathf.Abs(cam.transform.position.z);   // distance from camera to the 2D plane
            Vector3 point = cam.ViewportToWorldPoint(vp);
            point.z = 0f;

            objectPool.instance.get(prefab, point, Quaternion.identity);
        }
    }
}
