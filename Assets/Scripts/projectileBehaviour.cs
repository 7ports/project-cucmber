using UnityEngine;

public class projectileBehaviour : MonoBehaviour
{
    [SerializeField] private float lifeSeconds = 3f;
    [SerializeField] private LayerMask wallLayer;
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
