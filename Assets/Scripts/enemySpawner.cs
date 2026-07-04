using UnityEngine;

public class enemySpawner : MonoBehaviour
{
    [SerializeField] private GameObject[] enemyPrefabs;
    [SerializeField] private float spawnInterval = 2f;
    [SerializeField] private float edgeMargin = 0.08f;
    private float spawnTimer;

    void Update()
    {
        if (worldState.instance == null || worldState.instance.player == null) return;
        if (objectPool.instance == null || enemyPrefabs == null || enemyPrefabs.Length == 0) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        spawnTimer += Time.deltaTime;
        if (spawnTimer >= spawnInterval)
        {
            spawnTimer = 0f;
            GameObject prefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];

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
