using UnityEngine;

public class projectileBehaviour : MonoBehaviour
{
    [SerializeField] private float lifeSeconds = 10f;
    [SerializeField] private LayerMask wallLayer;
    [SerializeField] private float fallbackRange = 8f;
    private float lifeTimer;
    private Vector3 spawnOrigin;
    private int enemiesHit;   // per-shot pierce counter; reset in OnEnable (pooled reset)

    void OnEnable()
    {
        lifeTimer = 0f;
        enemiesHit = 0;
        spawnOrigin = transform.position;
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null) rb.linearVelocity = Vector2.zero;
    }

    void Update()
    {
        float limit = worldState.instance != null ? worldState.instance.Range() : fallbackRange;
        float traveled = Vector3.Distance(transform.position, spawnOrigin);
        if (traveled >= limit)
        {
            if (objectPool.instance != null) objectPool.instance.ret(gameObject);
            return;
        }

        lifeTimer += Time.deltaTime;
        if (lifeTimer >= lifeSeconds)
        {
            if (objectPool.instance != null) objectPool.instance.ret(gameObject);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (((1 << other.gameObject.layer) & wallLayer) != 0)
        {
            if (objectPool.instance != null) objectPool.instance.ret(gameObject);
            return;
        }

        if (other.CompareTag("Enemy"))
        {
            enemyHealth eh = other.GetComponent<enemyHealth>();
            if (eh != null)
            {
                int dmg = worldState.instance != null ? Mathf.RoundToInt(worldState.instance.AttackDamage()) : 1;
                eh.takeDamage(dmg);
            }

            enemiesHit++;
            int pierce = worldState.instance != null ? worldState.instance.Pierce() : 1;
            if (enemiesHit > pierce)
            {
                if (objectPool.instance != null) objectPool.instance.ret(gameObject);
            }
        }
    }
}
