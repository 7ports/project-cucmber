using UnityEngine;

public class bossSpawner : MonoBehaviour
{
    [SerializeField] private GameObject bossPrefab;
    [SerializeField] private float edgeMargin = 0.08f;   // same as enemySpawner
    private GameObject activeBoss;
    private bool spawned;

    void Start()
    {
        SpawnBoss();
    }

    void Update()
    {
        if (spawned) return;
        if (worldState.instance == null || worldState.instance.player == null) return;
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
        Camera cam = Camera.main;
        if (cam == null || bossPrefab == null) return;

        int side = Random.Range(0, 4);
        Vector3 vp;
        if (side == 0)      vp = new Vector3(-edgeMargin, Random.value, 0f);
        else if (side == 1) vp = new Vector3(1f + edgeMargin, Random.value, 0f);
        else if (side == 2) vp = new Vector3(Random.value, -edgeMargin, 0f);
        else                vp = new Vector3(Random.value, 1f + edgeMargin, 0f);
        vp.z = Mathf.Abs(cam.transform.position.z);
        Vector3 point = cam.ViewportToWorldPoint(vp);
        point.z = 0f;

        activeBoss = Instantiate(bossPrefab, point, Quaternion.identity);
        spawned = true;
    }
}
