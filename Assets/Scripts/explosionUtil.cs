using UnityEngine;

/// <summary>
/// Shared AoE explosion helper. Extracted verbatim from projectileBehaviour's inline
/// EXPLODE block so item weapons (grenade, etc.) can reuse identical detonation logic.
/// Damage routes through worldState.RollDamage so crits apply uniformly; with crit base 0
/// the result is identical to the previous inline loop (behavior-neutral).
/// </summary>
public static class explosionUtil
{
    // Set once at scene start by explosionFxRegistrar. Null when no VFX prefab is wired.
    public static GameObject ExplosionBurstPrefab;

    public static void Detonate(Vector2 pos, float radius, int damage)
    {
        if (damage <= 0) return;

        // Size-scaled placeholder explosion VFX: a bigger radius spawns a visibly bigger burst.
        // Null-guarded so detonation/damage behavior is unchanged when no prefab is wired.
        if (ExplosionBurstPrefab != null && objectPool.instance != null)
        {
            GameObject fx = objectPool.instance.get(ExplosionBurstPrefab, pos, Quaternion.identity);
            fx.transform.localScale = Vector3.one * radius;
        }

        Collider2D[] near = Physics2D.OverlapCircleAll(pos, radius);
        foreach (Collider2D c in near)
        {
            if (c == null || !c.CompareTag("Enemy")) continue;
            enemyHealth eh = c.GetComponent<enemyHealth>();
            if (eh == null) continue;
            int dmg = worldState.instance != null
                ? worldState.instance.RollDamage(damage, out bool crit)
                : damage;
            eh.takeDamage(dmg);
        }
    }
}
