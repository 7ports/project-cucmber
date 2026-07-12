using UnityEngine;
using System.Collections.Generic;

public class projectileBehaviour : MonoBehaviour
{
    [SerializeField] private float lifeSeconds = 10f;
    [SerializeField] private LayerMask wallLayer;
    [SerializeField] private float fallbackRange = 8f;
    private float lifeTimer;
    private Vector3 spawnOrigin;
    private int enemiesHit;   // per-shot pierce counter; reset in OnEnable (pooled reset)
    private int bounceCount;  // Bounce: bounces used this shot; budget = Pierce(); reset in OnEnable (pooled reset)
    private readonly HashSet<int> hitEnemyIds = new HashSet<int>();  // distinct enemies struck this shot; de-dups enemies carrying multiple colliders; reset in OnEnable (pooled reset)
    private Vector3 baseScale = Vector3.one;   // authored prefab scale, captured once
    private bool baseScaleCaptured;

    void Awake()
    {
        baseScale = transform.localScale;   // capture the prefab's authored scale ONCE
        baseScaleCaptured = true;
    }

    void OnEnable()
    {
        lifeTimer = 0f;
        enemiesHit = 0;
        bounceCount = 0;
        hitEnemyIds.Clear();
        spawnOrigin = transform.position;

        // Re-apply size EVERY spawn from the stored base, reading the CURRENT stat so
        // upgrades taken mid-run apply to the very next bullet. Never multiply the live
        // localScale (that would compound across pooled reuse).
        if (!baseScaleCaptured) { baseScale = transform.localScale; baseScaleCaptured = true; } // safety if OnEnable precedes Awake
        float size = worldState.instance != null ? worldState.instance.ProjectileSize() : 1f;
        transform.localScale = baseScale * size;

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
            // De-dup: an enemy may carry multiple colliders (a solid body collider plus a
            // damage trigger); OnTriggerEnter2D fires once per collider. Resolve a STABLE
            // per-enemy identity and skip any collider whose enemy was already processed
            // this shot, so damage AND the pierce/bounce counters advance at most once per
            // distinct enemy, no matter how many colliders it has.
            int enemyId = other.attachedRigidbody != null
                ? other.attachedRigidbody.gameObject.GetInstanceID()
                : other.transform.root.gameObject.GetInstanceID();
            if (!hitEnemyIds.Add(enemyId)) return;

            enemyHealth eh = other.GetComponent<enemyHealth>();
            if (eh != null)
            {
                int dmg = worldState.instance != null
                    ? worldState.instance.RollDamage(worldState.instance.AttackDamage(), out bool crit)
                    : 1;
                eh.takeDamage(dmg);
            }

            // STATUS: apply on-hit effects to the enemy just struck (damage already applied).
            if (eh != null && playerInventory.instance != null)
            {
                if (playerInventory.instance.Has(ItemId.Fire))
                    eh.ApplyFire();

                float freezeChance = worldState.instance != null ? worldState.instance.freezeChance : 0.2f;
                if (playerInventory.instance.Has(ItemId.Freeze) && Random.value < freezeChance)
                {
                    float freezeDur = worldState.instance != null ? worldState.instance.freezeItemDuration : 2f;
                    eh.ApplyFreeze(freezeDur);
                }
            }

            // EXPLODE: range-scaled AoE dealing 1/3 attack damage to every enemy nearby.
            if (playerInventory.instance != null && playerInventory.instance.Has(ItemId.Explode))
            {
                float baseDmg = worldState.instance != null ? worldState.instance.AttackDamage() : 3f;
                int splash = Mathf.FloorToInt(baseDmg / 3f);   // the 1/3-damage item; relative, x10-safe
                if (splash > 0)
                {
                    float range = worldState.instance != null ? worldState.instance.Range() : 2.5f;
                    float factor = worldState.instance != null ? worldState.instance.explosionRadiusFactor : 1f;
                    float radius = range * factor;
                    explosionUtil.Detonate(transform.position, radius, splash);
                }
            }

            int pierce = worldState.instance != null ? worldState.instance.Pierce() : 1;

            bool hasBounce = playerInventory.instance != null
                             && playerInventory.instance.Has(ItemId.Bounce);

            if (hasBounce)
            {
                // BOUNCE REPLACES PIERCE: the bullet never passes through. On each enemy
                // hit it redirects to the nearest OTHER enemy, up to Pierce() bounces
                // (upgrading Pierce raises the bounce budget). enemiesHit is intentionally
                // NOT touched here, so pierce and bounce are mutually exclusive when owned.
                // Range/lifetime expiry lives in Update() and never reaches here.
                if (bounceCount < pierce && TryBounce(other))
                {
                    bounceCount++;
                    return;   // keep flying toward the new target; do NOT despawn
                }
                if (objectPool.instance != null) objectPool.instance.ret(gameObject);
            }
            else
            {
                // PIERCE (unchanged for non-owners): pass through Pierce() enemies, then despawn.
                enemiesHit++;
                if (enemiesHit > pierce)
                {
                    if (objectPool.instance != null) objectPool.instance.ret(gameObject);
                }
            }
        }
    }

    // Finds the nearest enemy other than `justHit` within bounceSearchRadius and redirects
    // this projectile toward it, preserving current speed. Returns false if none found.
    private bool TryBounce(Collider2D justHit)
    {
        Vector2 pos = transform.position;
        float searchRadius = worldState.instance != null ? worldState.instance.bounceSearchRadius : 6f;
        Collider2D[] hits = Physics2D.OverlapCircleAll(pos, searchRadius);
        Transform best = null;
        float bestSqr = Mathf.Infinity;
        foreach (Collider2D c in hits)
        {
            if (c == null || c == justHit) continue;
            if (!c.CompareTag("Enemy")) continue;
            float sqr = ((Vector2)c.transform.position - pos).sqrMagnitude;
            if (sqr < bestSqr) { bestSqr = sqr; best = c.transform; }
        }
        if (best == null) return false;

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb == null) return false;
        float speed = rb.linearVelocity.magnitude;
        if (speed <= 0.001f) speed = 10f;   // fallback if velocity was zeroed
        Vector2 dir = ((Vector2)best.position - pos).normalized;
        rb.linearVelocity = dir * speed;

        // Re-anchor the range odometer so the post-bounce leg gets a fresh Range() budget
        // (otherwise the bullet may instantly exceed traveled>=limit and despawn next frame).
        spawnOrigin = transform.position;
        return true;
    }
}
