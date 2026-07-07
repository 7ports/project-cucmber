using UnityEngine;

public class playerProjectileShooter : MonoBehaviour
{
    float shootTimer = 0;
    [SerializeField] private GameObject projectile;
    [SerializeField] private float projectileSpeed = 10f;
    // Update is called once per frame
    void Update()
    {
       shootTimer += Time.deltaTime;

       if(shootTimer >= worldState.instance.FireCooldown())
        {
            // Find the nearest enemy. If none exist, skip firing this frame.
            GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
            GameObject nearest = null;
            float nearestSqrDist = Mathf.Infinity;
            Vector2 myPos = transform.position;
            foreach (GameObject enemy in enemies)
            {
                float sqrDist = ((Vector2)enemy.transform.position - myPos).sqrMagnitude;
                if (sqrDist < nearestSqrDist)
                {
                    nearestSqrDist = sqrDist;
                    nearest = enemy;
                }
            }

            if (nearest != null)
            {
                GameObject shot = objectPool.instance.get(projectile, transform.position, transform.rotation);
                Rigidbody2D rb = shot.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    Vector2 dir = ((Vector2)nearest.transform.position - myPos).normalized;
                    rb.linearVelocity = dir * projectileSpeed;
                }
                // Reset cooldown only after a shot actually fires so the cadence repeats.
                shootTimer = 0;
            }
            // Deliberate choice: when no enemy exists we do NOT reset shootTimer, so the
            // shooter fires immediately the moment an enemy appears instead of waiting a full cooldown.
        }
    }
}
