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
            fx.transform.localScale = Vector3.one;
        }

        Collider2D[] near = Physics2D.OverlapCircleAll(pos, radius);
        foreach (Collider2D c in near)
        {
            if (c == null || !c.CompareTag("Enemy")) continue;
            enemyHealth eh = c.GetComponent<enemyHealth>();
            if (eh == null) continue;
            bool crit = false;
            int dmg = worldState.instance != null
                ? worldState.instance.RollDamage(damage, out crit)
                : damage;
            eh.takeDamage(dmg, crit);
        }
    }

    /// <summary>
    /// Player-targeting variant of Detonate: same size-scaled VFX, but the AoE damages the
    /// PLAYER instead of enemies. Used by enemy-thrown grenades. Damage is a flat value passed
    /// in by the thrower (no worldState.GrenadeDamage scaling — that scales with the player's
    /// own attack). The player is only hit when within the blast radius.
    /// </summary>
    public static void DetonateOnPlayer(Vector2 pos, float radius, int damage)
    {
        if (damage <= 0) return;

        if (ExplosionBurstPrefab != null && objectPool.instance != null)
        {
            GameObject fx = objectPool.instance.get(ExplosionBurstPrefab, pos, Quaternion.identity);
            fx.transform.localScale = Vector3.one;
        }

        // Locate the player by the game's canonical reference rather than a collider tag.
        // Tag-based OverlapCircle filtering was unreliable (the player collider may not carry
        // the exact "Player" tag, or the tag may live on a different object in the hierarchy),
        // so grenades never damaged the player. Mirror enemyProjectile's robust seam: check the
        // worldState player transform directly and only hit when inside the blast radius.
        if (worldState.instance == null || worldState.instance.player == null) return;

        if (Vector2.Distance(pos, worldState.instance.player.position) > radius) return;

        playerHealth ph = worldState.instance.player.GetComponentInChildren<playerHealth>();
        if (ph == null) ph = worldState.instance.player.GetComponentInParent<playerHealth>();
        if (ph == null) return;

        ph.TakeHit(damage);
    }
}
