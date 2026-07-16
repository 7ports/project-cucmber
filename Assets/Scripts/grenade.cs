using System.Collections;
using UnityEngine;

/// <summary>
/// Pooled ballistic grenade. Lerps from its spawn point to a landing offset over a short
/// arc (no DOTween — a coroutine position-lerp with a little sine pop), then on landing
/// (or fuse expiry) detonates via explosionUtil and returns itself to the pool.
/// OnEnable resets all pooled state; OnDisable stops the flight coroutine.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class grenade : MonoBehaviour
{
    [SerializeField] private float _flightTime = 0.5f;   // seconds to reach the landing spot
    [SerializeField] private float _fuse = 0.6f;         // hard cap; detonate even if flight stalls
    [SerializeField] private float _arcHeight = 0.5f;    // visual pop height along the toss

    [Header("Enemy-thrown variant")]
    // Opt-in: when true this grenade damages the PLAYER on detonation instead of enemies.
    // Defaults false so the player's own grenade weapon is completely unaffected.
    [SerializeField] private bool _damagesPlayer = false;
    // Flat damage dealt to the player when _damagesPlayer is true. Deliberately NOT routed
    // through worldState.GrenadeDamage() (which scales with the PLAYER's attack stat).
    [SerializeField] private int _playerDamage = 15;

    private Coroutine _routine;
    private bool _detonated;

    void OnEnable()
    {
        // pooled reset
        _detonated = false;
        _routine = null;
    }

    void OnDisable()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }
    }

    /// <summary>Toss from the current position by the given offset; detonates on landing.</summary>
    public void Throw(Vector2 offset)
    {
        if (_routine != null) StopCoroutine(_routine);
        _detonated = false;
        _routine = StartCoroutine(FlightRoutine((Vector2)transform.position + offset));
    }

    private IEnumerator FlightRoutine(Vector2 landing)
    {
        Vector2 start = transform.position;
        float dur = Mathf.Max(0.01f, _flightTime);
        float fuse = Mathf.Max(dur, _fuse);
        float t = 0f;

        while (t < dur && t < fuse)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / dur);
            Vector2 flat = Vector2.Lerp(start, landing, p);
            float arc = _arcHeight * Mathf.Sin(p * Mathf.PI);   // 0 -> peak -> 0 pop
            transform.position = new Vector3(flat.x, flat.y + arc, transform.position.z);
            yield return null;
        }

        transform.position = new Vector3(landing.x, landing.y, transform.position.z);
        _routine = null;
        Detonate();
    }

    private void Detonate()
    {
        if (_detonated) return;
        _detonated = true;

        float radius = worldState.instance != null ? worldState.instance.GrenadeRadius() : 1.5f;

        if (_damagesPlayer)
        {
            // Enemy-thrown grenade: flat damage to the player if within the blast radius.
            explosionUtil.DetonateOnPlayer(transform.position, radius, _playerDamage);
        }
        else
        {
            // Player's own grenade — unchanged: scales with the player's attack, hits enemies.
            int damage = worldState.instance != null
                ? Mathf.FloorToInt(worldState.instance.GrenadeDamage())
                : 0;
            explosionUtil.Detonate(transform.position, radius, damage);
        }

        if (objectPool.instance != null) objectPool.instance.ret(gameObject);
        else gameObject.SetActive(false);
    }
}
