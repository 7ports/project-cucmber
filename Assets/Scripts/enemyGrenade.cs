using System.Collections;
using UnityEngine;

/// <summary>
/// Dedicated enemy grenade projectile. Completely independent of the player's `grenade`
/// weapon: its blast radius IS the telegraph indicator, so the red circle the player sees
/// is exactly the area that damages them (no more "big player radius pretty much always
/// hits"). It owns its own telegraph, sizes that telegraph to _blastRadius, arcs to the
/// landing spot, and on arrival deals flat damage to the player ONLY if the player is
/// actually inside the circle.
///
/// Spawned by grenadierEnemy at the enemy's death position as an INDEPENDENT object (never
/// a child of the dying enemy). The flight coroutine therefore runs on THIS surviving
/// projectile, and the telegraph is a separate top-level object so both outlive the enemy
/// that threw them. It does NOT route through the shared explosion helper or the big player
/// blast radius — the whole point is a small, self-contained radius that matches the telegraph.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class enemyGrenade : MonoBehaviour
{
    [Header("Blast (radius == telegraph size)")]
    [SerializeField] private float _blastRadius = 1.5f;   // SMALL — both the damage radius AND the visible telegraph radius
    [SerializeField] private int _playerDamage = 15;      // flat damage to the player on a direct hit
    [SerializeField] private GameObject _telegraphPrefab; // red circle marker (Assets/Prefabs/vfx/grenadeTelegraph.prefab)

    [Header("Flight")]
    [SerializeField] private float _flightTime = 0.5f;    // seconds to reach the landing spot
    [SerializeField] private float _fuse = 0.6f;          // hard cap; detonate even if flight stalls
    [SerializeField] private float _arcHeight = 0.5f;     // visual pop height along the toss

    private Coroutine _routine;
    private GameObject _telegraph;
    private bool _detonated;

    void OnEnable()
    {
        // pooled reset
        _detonated = false;
        _routine = null;
        _telegraph = null;
    }

    void OnDisable()
    {
        // Stop path for the flight coroutine + clean up the telegraph if we go inactive early.
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }
        RemoveTelegraph();
    }

    /// <summary>
    /// Public launch method the grenadier calls. Spawns + sizes the telegraph at
    /// targetWorldPos, then arcs from the current position to that spot and detonates.
    /// </summary>
    public void Launch(Vector2 targetWorldPos)
    {
        if (_routine != null) StopCoroutine(_routine);
        _detonated = false;

        SpawnTelegraph(targetWorldPos);

        _routine = StartCoroutine(FlightRoutine(targetWorldPos));
    }

    private void SpawnTelegraph(Vector2 targetWorldPos)
    {
        RemoveTelegraph();
        if (_telegraphPrefab == null) return;

        _telegraph = Instantiate(_telegraphPrefab, new Vector3(targetWorldPos.x, targetWorldPos.y, 0f), Quaternion.identity);

        // Scale the telegraph so its VISIBLE world radius equals _blastRadius. Mirror the
        // ring-sizing math from auraWeapon: divide the target diameter by the sprite's own
        // unscaled bounds size so the drawn circle's world radius matches the gameplay radius.
        SpriteRenderer sr = _telegraph.GetComponentInChildren<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
        {
            Vector2 baseSize = sr.sprite.bounds.size;
            float spriteBase = Mathf.Max(baseSize.x, baseSize.y);
            if (spriteBase <= 0f) spriteBase = 1f;
            float scale = (2f * _blastRadius) / spriteBase;
            _telegraph.transform.localScale = new Vector3(scale, scale, 1f);
        }
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
        Detonate(landing);
    }

    private void Detonate(Vector2 landPos)
    {
        if (_detonated) return;
        _detonated = true;

        // Damage the player ONLY if they are actually inside the telegraph circle. Tag-independent
        // seam: reach the player through worldState.instance.player and its playerHealth.
        if (worldState.instance != null && worldState.instance.player != null)
        {
            Transform player = worldState.instance.player;
            if (Vector2.Distance(landPos, player.position) <= _blastRadius)
            {
                playerHealth ph = player.GetComponentInChildren<playerHealth>();
                if (ph == null) ph = player.GetComponentInParent<playerHealth>();
                if (ph != null) ph.TakeHit(_playerDamage);
            }
        }

        RemoveTelegraph();

        // Return/destroy self. Prefer the pool if this was pool-spawned; otherwise destroy.
        if (objectPool.instance != null && GetComponent<pooledObject>() != null)
            objectPool.instance.ret(gameObject);
        else
            Destroy(gameObject);
    }

    private void RemoveTelegraph()
    {
        if (_telegraph != null)
        {
            Destroy(_telegraph);
            _telegraph = null;
        }
    }
}
