using UnityEngine;

public class enemyProjectile : MonoBehaviour
{
    [SerializeField] private int   damage   = 80;
    [SerializeField] private float lifetime = 4f;

    private float lifeTimer;
    private Rigidbody2D rb;

    void Awake() { rb = GetComponent<Rigidbody2D>(); }

    void OnEnable()
    {
        lifeTimer = 0f;
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (rb != null) rb.linearVelocity = Vector2.zero;
    }

    public void Launch(Vector2 direction, float speed)
    {
        lifeTimer = 0f;
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        Vector2 d = direction.sqrMagnitude > 1e-8f ? direction.normalized : Vector2.right;
        if (rb != null) rb.linearVelocity = d * speed;
        else transform.position += (Vector3)(d * speed * Time.deltaTime);
    }

    public void Launch(Vector2 direction, float speed, float lifetimeSeconds)
    {
        lifetime = lifetimeSeconds;
        Launch(direction, speed);
    }

    void Update()
    {
        lifeTimer += Time.deltaTime;
        if (lifeTimer >= lifetime)
        {
            if (objectPool.instance != null) objectPool.instance.ret(gameObject);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (worldState.instance == null || worldState.instance.player == null) return;
        if (other.transform == worldState.instance.player || other.transform.root == worldState.instance.player)
        {
            playerHealth ph = worldState.instance.player.GetComponentInChildren<playerHealth>();
            if (ph != null) ph.TakeHit(damage);
            if (objectPool.instance != null) objectPool.instance.ret(gameObject);
        }
    }
}
