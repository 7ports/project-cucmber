using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A single pooled Searing Trail segment. Requires a trigger Collider2D on this GameObject.
/// Lives worldState.trailSegmentLifetime seconds, then returns itself to the object pool.
/// While alive it damages overlapping "Enemy" colliders on a per-enemy cooldown of
/// worldState.trailTickInterval, de-duplicated by a STABLE enemy id so an enemy carrying
/// multiple colliders is hit at most once per tick. Per-tick damage is TrailDps() scaled by
/// the tick interval (dps -> per-tick share). Pooled-reset discipline lives in OnEnable.
/// </summary>
public class trailSegment : MonoBehaviour
{
    private float _life;

    // Per-enemy remaining tick cooldown, keyed by a STABLE enemy id. An entry > 0 means that
    // enemy was already struck this tick window and is not eligible again until it decays to 0.
    // This both de-dups multi-collider enemies within a tick and rate-limits repeat damage.
    private readonly Dictionary<int, float> _cooldowns = new Dictionary<int, float>();
    private readonly List<int> _keyBuffer = new List<int>();   // reused each frame to age cooldowns without alloc churn

    private void OnEnable()
    {
        // Pooled-reset discipline: fresh lifetime, cleared tick accumulators + hit-enemy set.
        _life = worldState.instance != null ? worldState.instance.trailSegmentLifetime : 2f;
        _cooldowns.Clear();
        _keyBuffer.Clear();
    }

    private void Update()
    {
        // Lifetime: expire back to the pool.
        _life -= Time.deltaTime;
        if (_life <= 0f)
        {
            if (objectPool.instance != null) objectPool.instance.ret(gameObject);
            else gameObject.SetActive(false);
            return;
        }

        // Age every per-enemy cooldown; drop expired entries so re-entered enemies re-arm and the
        // dictionary does not accumulate ids for enemies that have left or died.
        if (_cooldowns.Count > 0)
        {
            _keyBuffer.Clear();
            foreach (var kv in _cooldowns) _keyBuffer.Add(kv.Key);
            for (int i = 0; i < _keyBuffer.Count; i++)
            {
                int id = _keyBuffer[i];
                float t = _cooldowns[id] - Time.deltaTime;
                if (t <= 0f) _cooldowns.Remove(id);
                else _cooldowns[id] = t;
            }
        }
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!other.CompareTag("Enemy")) return;

        // Stable per-enemy identity (mirrors projectileBehaviour): prefer the attached
        // rigidbody's GameObject, else the collider hierarchy root, so multiple colliders on
        // one enemy resolve to the SAME id and are de-duped for this tick.
        int enemyId = other.attachedRigidbody != null
            ? other.attachedRigidbody.gameObject.GetInstanceID()
            : other.transform.root.gameObject.GetInstanceID();

        if (_cooldowns.TryGetValue(enemyId, out float cd) && cd > 0f) return;   // still cooling down / already hit this tick

        enemyHealth eh = other.GetComponent<enemyHealth>();
        if (eh == null) return;

        float interval = worldState.instance != null ? worldState.instance.trailTickInterval : 0.25f;
        if (interval <= 0f) interval = 0.25f;

        // dps -> per-tick damage: TrailDps() * tickInterval. crit is rolled by RollDamage (out
        // param) but takeDamage takes only the rolled amount, matching projectileBehaviour.
        bool crit = false;
        int dmg = worldState.instance != null
            ? worldState.instance.RollDamage(worldState.instance.TrailDps() * interval, out crit)
            : 0;
        eh.takeDamage(dmg, crit);

        _cooldowns[enemyId] = interval;   // arm the cooldown; this enemy is eligible again one interval from now
    }
}
