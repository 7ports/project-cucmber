using UnityEngine;

public class bossSpawner : MonoBehaviour
{
    // Preconfigure one or more boss prefabs here; the spawner picks one at random.
    // NOTE: replacing the old single `bossPrefab` field DROPS its inspector reference —
    // this array must be re-populated in the Editor (see EDITOR MIGRATION in the design doc).
    [SerializeField] private GameObject[] bossPrefabs;
    [SerializeField] private float edgeMargin = 0.08f;   // same as enemySpawner
    private GameObject activeBoss;
    private bool spawned;

    void Start() { }

    void Update()
    {
        if (spawned) return;
        if (worldState.instance == null || worldState.instance.player == null) return;
        if (worldState.instance.level < 5) return;   // boss only at level 5+
        SpawnBoss();
    }

    // === FUTURE TIMED CADENCE HOOK ===
    // TODO(boss-cadence): add [SerializeField] float bossInterval + a spawnTimer in Update()
    //   (mirror enemySpawner's spawnTimer pattern), calling SpawnBoss() on interval.
    //   The "already alive" guard below makes repeat calls safe.

    void SpawnBoss()
    {
        if (activeBoss != null && activeBoss.activeInHierarchy) return;
        if (worldState.instance == null || worldState.instance.player == null) return;

        // Fail safe: no bosses configured -> do nothing (no null-ref spam).
        if (bossPrefabs == null || bossPrefabs.Length == 0) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        // Pick a random preconfigured boss; skip if the chosen slot is empty.
        GameObject chosen = bossPrefabs[Random.Range(0, bossPrefabs.Length)];
        if (chosen == null) return;

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
        spawned = true;
    }
}
