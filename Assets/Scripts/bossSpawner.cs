using UnityEngine;

public class bossSpawner : MonoBehaviour
{
    // Preconfigure one or more boss prefabs here; the spawner picks one at random.
    // NOTE: replacing the old single `bossPrefab` field DROPS its inspector reference —
    // this array must be re-populated in the Editor (see EDITOR MIGRATION in the design doc).
    [SerializeField] private GameObject[] bossPrefabs;
    [SerializeField] private float edgeMargin = 0.08f;   // same as enemySpawner
    private GameObject activeBoss;
    private float nextBossTime = -1f;   // lazily initialized to bossFirstTime on first valid frame

    void Start() { }

    void Update()
    {
        if (worldState.instance == null || worldState.instance.player == null) return;   // player-null retry preserved
        if (nextBossTime < 0f) nextBossTime = worldState.instance.bossFirstTime;          // first boss at bossFirstTime
        if (Time.timeSinceLevelLoad < nextBossTime) return;

        // Only advance the cadence when a boss actually spawns; if one is still alive,
        // SpawnBoss() no-ops via its alive-guard and we retry next frame (spawns as soon as it dies).
        if (SpawnBoss())
            nextBossTime += worldState.instance.bossInterval;   // schedule the next boss (+5:00 default)
    }

    // === FUTURE TIMED CADENCE HOOK ===
    // TODO(boss-cadence): add [SerializeField] float bossInterval + a spawnTimer in Update()
    //   (mirror enemySpawner's spawnTimer pattern), calling SpawnBoss() on interval.
    //   The "already alive" guard below makes repeat calls safe.

    bool SpawnBoss()
    {
        if (activeBoss != null && activeBoss.activeInHierarchy) return false;   // one boss at a time
        if (worldState.instance == null || worldState.instance.player == null) return false;

        // Fail safe: no bosses configured -> do nothing (no null-ref spam).
        if (bossPrefabs == null || bossPrefabs.Length == 0) return false;

        Camera cam = Camera.main;
        if (cam == null) return false;

        // Pick a random preconfigured boss; skip if the chosen slot is empty.
        GameObject chosen = bossPrefabs[Random.Range(0, bossPrefabs.Length)];
        if (chosen == null) return false;

        int side = Random.Range(0, 4);
        Vector3 vp;
        if (side == 0)      vp = new Vector3(-edgeMargin, Random.value, 0f);
        else if (side == 1) vp = new Vector3(1f + edgeMargin, Random.value, 0f);
        else if (side == 2) vp = new Vector3(Random.value, -edgeMargin, 0f);
        else                vp = new Vector3(Random.value, 1f + edgeMargin, 0f);
        vp.z = Mathf.Abs(cam.transform.position.z);
        Vector3 point = cam.ViewportToWorldPoint(vp);
        point.z = 0f;

        activeBoss = Instantiate(chosen, point, Quaternion.identity);
        bossShooter shooter = activeBoss.GetComponent<bossShooter>();
        if (shooter != null) shooter.RandomizePattern();
        return true;
    }
}
