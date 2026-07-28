using UnityEngine;

public class enemySpawner : MonoBehaviour
{
    [System.Serializable]
    private struct SpawnEntry
    {
        public GameObject prefab;
        public float minTimeSeconds;   // eligible when elapsed run time >= minTimeSeconds
        public float weight;           // relative base weight; <= 0 is treated as 1
    }

    // A customizable spawn batch: N copies of one enemy prefab, spawned as a group.
    // Prefab identity matches SpawnEntry.prefab (same objectPool.get path).
    [System.Serializable]
    private struct SpawnBatch
    {
        public GameObject prefab;   // enemy prefab to spawn as a group
        public int count;           // how many to instantiate per batch (min 1)
    }

    [SerializeField] private SpawnEntry[] spawnTable;

    [Header("Batch spawning (active from the Nth boss)")]
    // Once worldState.bossSpawnCount >= _batchBossThreshold, each spawn tick emits one of
    // these batches (picked at random) as a group instead of a single enemy. Empty list ->
    // batching never engages (single spawns preserved). Fully inspector-customizable.
    [SerializeField] private SpawnBatch[] spawnBatches;
    // Boss count at/after which batching begins. Default 2 -> batches start after the 2nd boss.
    [SerializeField] private int _batchBossThreshold = 2;
    // Rebuilt every spawn: eligible prefabs + their computed (ramped) weights, index-aligned.
    private readonly System.Collections.Generic.List<GameObject> eligible = new System.Collections.Generic.List<GameObject>();
    private readonly System.Collections.Generic.List<float> eligibleWeights = new System.Collections.Generic.List<float>();
    [SerializeField] private float spawnInterval = 2f;
    [SerializeField] private float edgeMargin = 0.08f;
    private float spawnTimer;

    [Header("Directional spawn bias")]
    // Seconds the player must hold roughly one heading before spawns bias ahead of them.
    [SerializeField] private float _sameDirThreshold = 5f;
    // Probability (0..1) a spawn is thrown into the heading cone once the gate is open; the rest stay evenly distributed.
    [SerializeField] private float _biasStrength = 0.75f;
    // Half-angle of the cone (degrees) around the heading that biased spawns fall within.
    [SerializeField] private float _biasConeDegrees = 55f;
    // dot(currentDir, avgHeading) above this counts as "same direction"; below it resets the timer.
    [SerializeField] private float _directionTolerance = 0.7f;
    // Minimum per-frame position delta to treat the player as moving (below = idle).
    [SerializeField] private float _moveEpsilon = 0.0001f;

    private Vector3 _lastPlayerPos;
    private bool _hasLastPos;
    private Vector2 _headingDir;      // running-average heading (unit) while moving one way
    private bool _hasHeading;
    private float _sameDirTimer;      // time spent continuously moving within tolerance of _headingDir

    void Update()
    {
        if (worldState.instance == null || worldState.instance.player == null) return;
        if (objectPool.instance == null || spawnTable == null || spawnTable.Length == 0) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        // --- Track heading: accumulate _sameDirTimer while the player keeps one general direction. ---
        Vector3 playerPos = worldState.instance.player.transform.position;
        if (_hasLastPos)
        {
            Vector2 delta = (Vector2)(playerPos - _lastPlayerPos);
            float dist = delta.magnitude;
            if (dist > _moveEpsilon)
            {
                Vector2 moveDir = delta / dist;
                if (_hasHeading && Vector2.Dot(moveDir, _headingDir) > _directionTolerance)
                {
                    _sameDirTimer += Time.deltaTime;
                    // Ease the running average toward the current move dir so slow curves don't reset instantly.
                    _headingDir = Vector2.Lerp(_headingDir, moveDir, 0.1f).normalized;
                }
                else
                {
                    _sameDirTimer = 0f;      // changed direction — restart the gate
                    _headingDir = moveDir;
                    _hasHeading = true;
                }
            }
            else
            {
                _sameDirTimer = 0f;          // idle — restart the gate
            }
        }
        _lastPlayerPos = playerPos;
        _hasLastPos = true;

        spawnTimer += Time.deltaTime;
        float interval = (worldState.instance != null)
            ? worldState.instance.currentSpawnInterval * worldState.instance.SpawnIntervalTimeMultiplier()
            : spawnInterval;
        if (spawnTimer >= interval)
        {
            spawnTimer = 0f;
            float elapsed = Time.timeSinceLevelLoad;

            eligible.Clear();
            eligibleWeights.Clear();
            float totalWeight = 0f;
            // Ramp window in SECONDS, from worldState (guards against a deserialized 0).
            float ramp = (worldState.instance != null)
                ? Mathf.Max(0.0001f, worldState.instance.unlockRampSeconds)
                : 30f;
            for (int i = 0; i < spawnTable.Length; i++)
            {
                if (spawnTable[i].prefab == null || elapsed < spawnTable[i].minTimeSeconds) continue;

                float baseWeight = spawnTable[i].weight > 0f ? spawnTable[i].weight : 1f;
                // Linear phase-in: 0 at unlock time, growing to full weight over `ramp` seconds.
                float t = Mathf.Clamp01((elapsed - spawnTable[i].minTimeSeconds) / ramp);
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

            Vector3 vp;
            // When the player has held one heading past the threshold, throw most spawns into a cone
            // pointing the way they're going (edge ahead of them); otherwise keep the even 4-side spread.
            if (_hasHeading && _sameDirTimer > _sameDirThreshold && Random.value < _biasStrength)
            {
                float headingAngle = Mathf.Atan2(_headingDir.y, _headingDir.x);
                float coneRad = _biasConeDegrees * Mathf.Deg2Rad;
                float a = headingAngle + Random.Range(-coneRad, coneRad);
                Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                // Cast from viewport center out to the bordered edge in `dir` (world XY == viewport XY for an unrotated 2D cam).
                float half = 0.5f + edgeMargin;
                float sx = Mathf.Abs(dir.x) > 1e-4f ? half / Mathf.Abs(dir.x) : float.PositiveInfinity;
                float sy = Mathf.Abs(dir.y) > 1e-4f ? half / Mathf.Abs(dir.y) : float.PositiveInfinity;
                float s = Mathf.Min(sx, sy);
                vp = new Vector3(0.5f + dir.x * s, 0.5f + dir.y * s, 0f);
            }
            else
            {
                int side = Random.Range(0, 4);
                if (side == 0)      vp = new Vector3(-edgeMargin, Random.value, 0f);       // left
                else if (side == 1) vp = new Vector3(1f + edgeMargin, Random.value, 0f);   // right
                else if (side == 2) vp = new Vector3(Random.value, -edgeMargin, 0f);       // bottom
                else                vp = new Vector3(Random.value, 1f + edgeMargin, 0f);   // top
            }
            vp.z = Mathf.Abs(cam.transform.position.z);   // distance from camera to the 2D plane
            Vector3 point = cam.ViewportToWorldPoint(vp);
            point.z = 0f;

            // After the batch gate (default: 2nd boss), spawn a customizable batch as a group;
            // otherwise the original single spawn. Batches reuse this same spawn point/path.
            if (worldState.instance != null
                && worldState.instance.bossSpawnCount >= _batchBossThreshold
                && spawnBatches != null && spawnBatches.Length > 0)
            {
                SpawnBatch batch = spawnBatches[Random.Range(0, spawnBatches.Length)];
                GameObject batchPrefab = batch.prefab != null ? batch.prefab : prefab;
                int count = Mathf.Max(1, batch.count);
                for (int n = 0; n < count; n++)
                    objectPool.instance.get(batchPrefab, point, Quaternion.identity);
            }
            else
            {
                objectPool.instance.get(prefab, point, Quaternion.identity);
            }
        }
    }
}
