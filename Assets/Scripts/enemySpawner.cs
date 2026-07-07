using UnityEngine;

public class enemySpawner : MonoBehaviour
{
    [System.Serializable]
    private struct SpawnEntry
    {
        public GameObject prefab;
        public int minLevel;   // eligible when worldState.level >= minLevel
        public float weight;    // relative base weight; <= 0 is treated as 1 (default for existing entries)
    }

    [SerializeField] private SpawnEntry[] spawnTable;
    // Rebuilt every spawn: eligible prefabs + their computed (ramped) weights, index-aligned.
    private readonly System.Collections.Generic.List<GameObject> eligible = new System.Collections.Generic.List<GameObject>();
    private readonly System.Collections.Generic.List<float> eligibleWeights = new System.Collections.Generic.List<float>();
    [SerializeField] private float spawnInterval = 2f;
    [SerializeField] private float edgeMargin = 0.08f;
    // Levels a newly-unlocked type takes to ramp from 1/ramp of its base weight up to full weight.
    [SerializeField] private int unlockRampLevels = 4;
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
            eligibleWeights.Clear();
            float totalWeight = 0f;
            int ramp = Mathf.Max(1, unlockRampLevels);   // guards against a deserialized 0
            for (int i = 0; i < spawnTable.Length; i++)
            {
                if (spawnTable[i].prefab == null || lvl < spawnTable[i].minLevel) continue;

                float baseWeight = spawnTable[i].weight > 0f ? spawnTable[i].weight : 1f;
                // 1/ramp of base weight at the unlock level, growing linearly to full weight over `ramp` levels.
                float t = Mathf.Clamp01((lvl - spawnTable[i].minLevel + 1) / (float)ramp);
                float w = baseWeight * t;
                if (w <= 0f) continue;

                eligible.Add(spawnTable[i].prefab);
                eligibleWeights.Add(w);
                totalWeight += w;
            }
            if (eligible.Count == 0 || totalWeight <= 0f) return;

            // Weighted pick over the eligible set.
            float r = Random.value * totalWeight;
            int pick = eligible.Count - 1;   // fallback guards float rounding at the top of the range
            for (int i = 0; i < eligibleWeights.Count; i++)
            {
                r -= eligibleWeights[i];
                if (r <= 0f) { pick = i; break; }
            }
            GameObject prefab = eligible[pick];

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
