using UnityEngine;

public class projectileBehaviour : MonoBehaviour
{
    [SerializeField] private float lifeSeconds = 10f;
    [SerializeField] private LayerMask wallLayer;
    [SerializeField] private float fadeStart = 0.6f;
    [SerializeField] private float fallbackRange = 8f;
    private float lifeTimer;
    private Vector3 spawnOrigin;
    private SpriteRenderer sr;
    private Color baseColor;

    void OnEnable()
    {
        lifeTimer = 0f;
        spawnOrigin = transform.position;
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null) rb.linearVelocity = Vector2.zero;
        if (sr == null)
        {
            sr = GetComponent<SpriteRenderer>();
            if (sr != null) baseColor = sr.color;
        }
        if (sr != null) sr.color = baseColor;
    }

    void Update()
    {
        float limit = worldState.instance != null ? worldState.instance.range : fallbackRange;
        float traveled = Vector3.Distance(transform.position, spawnOrigin);
        if (sr != null)
        {
            float t = limit > 0f ? Mathf.Clamp01(traveled / limit) : 1f;
            float alpha = Mathf.InverseLerp(1f, fadeStart, t); // 1 until fadeStart, ramps to 0 at the limit
            sr.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
        }
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
                int dmg = worldState.instance != null ? Mathf.RoundToInt(worldState.instance.attackDamage) : 1;
                eh.takeDamage(dmg);
                Debug.Log("hit");
            }
            if (objectPool.instance != null) objectPool.instance.ret(gameObject);
        }
    }
}
