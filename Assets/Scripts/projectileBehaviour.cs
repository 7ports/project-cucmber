using UnityEngine;

public class projectileBehaviour : MonoBehaviour
{
    [SerializeField] private float lifeSeconds = 3f;
    private float lifeTimer;

    void OnEnable()
    {
        lifeTimer = 0f;
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null) rb.linearVelocity = Vector2.zero;
    }

    void Update()
    {
        lifeTimer += Time.deltaTime;
        if (lifeTimer >= lifeSeconds)
        {
            if (objectPool.instance != null) objectPool.instance.ret(gameObject);
        }
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        if (col.collider.CompareTag("Enemy"))
        {
            enemyHealth eh = col.collider.GetComponent<enemyHealth>();
            if (eh != null)
            {
                int dmg = worldState.instance != null ? Mathf.RoundToInt(worldState.instance.attackDamage) : 1;
                eh.takeDamage(dmg);
            }
            if (objectPool.instance != null) objectPool.instance.ret(gameObject);
        }
    }
}
