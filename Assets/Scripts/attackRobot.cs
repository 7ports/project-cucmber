using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attack Robot ally (ItemId.Robot). An autonomous world-space unit that chases the
/// NEAREST enemy and deals contact damage equal to half the player's attack damage,
/// scaling with all player stats through worldState. Trigger-only: it never pushes
/// the player or walls physically.
///
/// One component, two wiring modes:
///  - ROBOT mode (no _robotPrefab assigned): this object IS the robot. It starts inert
///    and activates itself when the player owns ItemId.Robot.
///  - ACTIVATOR mode (_robotPrefab assigned, e.g. sits on the player): on acquiring the
///    item it spawns exactly one robot prefab and does nothing else. Wired in the Editor.
/// </summary>
public class attackRobot : MonoBehaviour
{
    [SerializeField] private GameObject _robotPrefab;   // ACTIVATOR mode: prefab to spawn; null => this IS the robot
    [SerializeField] private SpriteRenderer _sprite;    // placeholder visual (robot mode)
    [SerializeField] private string _enemyTag = "Enemy";
    [SerializeField] private float _leashDistance = 12f; // teleport back to player if farther than this

    private bool _active;                                // robot mode: chasing + dealing damage
    private GameObject _spawnedRobot;                    // activator guard: only ever spawn one
    private readonly Dictionary<int, float> _nextHitTime = new Dictionary<int, float>();

    void Awake()
    {
        // Only coerce trigger-only physics in ROBOT mode. In ACTIVATOR mode this component
        // sits on the player, so we must NOT turn the player's own collider into a trigger.
        if (_robotPrefab != null) return;
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null) rb.bodyType = RigidbodyType2D.Kinematic;
    }

    void Start()
    {
        if (playerInventory.instance != null)
        {
            playerInventory.instance.OnItemAdded += HandleItemAdded;
            if (playerInventory.instance.Has(ItemId.Robot))
                Activate();
            else
                SetInert();
        }
        else
        {
            SetInert();
        }
    }

    void OnDestroy()
    {
        if (playerInventory.instance != null)
            playerInventory.instance.OnItemAdded -= HandleItemAdded;
    }

    private void HandleItemAdded(string id)
    {
        if (id == ItemId.Robot)
            Activate();
    }

    private void Activate()
    {
        // ACTIVATOR mode: spawn exactly one robot prefab, then stay inert.
        if (_robotPrefab != null)
        {
            if (_spawnedRobot == null)
            {
                // Spawn AT THE PLAYER, not at the activator's own transform (which may be
                // anywhere). Fall back to this transform only if the singleton is missing.
                Vector3 spawnPos = playerInventory.instance != null
                    ? playerInventory.instance.transform.position
                    : transform.position;
                _spawnedRobot = Instantiate(_robotPrefab, spawnPos, Quaternion.identity);
            }
            return;
        }

        // ROBOT mode: become the live ally.
        _active = true;
        if (_sprite != null) _sprite.enabled = true;
    }

    private void SetInert()
    {
        _active = false;
        if (_robotPrefab == null && _sprite != null) _sprite.enabled = false;
    }

    void Update()
    {
        if (!_active) return;

        // Leash FIRST — this must run every frame the robot is active, BEFORE any early
        // return for "no enemy" or a missing worldState. Previously it sat behind the
        // worldState-null guard and ahead of the chase flow, so on any frame that bailed
        // early the snap-back never executed and the robot drifted forever chasing enemies.
        // Reuse the existing player singleton reference (playerInventory sits on the player).
        Transform player = playerInventory.instance != null ? playerInventory.instance.transform : null;
        if (player != null &&
            Vector2.Distance(transform.position, player.position) > _leashDistance)
        {
            transform.position = player.position;
        }

        if (worldState.instance == null) return;

        Transform target = FindNearestEnemy();
        if (target == null) return;

        float speed = worldState.instance.MoveSpeed() * worldState.instance.robotSpeedFactor;
        transform.position = Vector2.MoveTowards(transform.position, target.position, speed * Time.deltaTime);
    }

    // Nearest enemy by squared distance — same scan pattern as chaserBehaviour / playerProjectileShooter.
    private Transform FindNearestEnemy()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag(_enemyTag);
        Transform nearest = null;
        float bestSqr = float.MaxValue;
        Vector2 here = transform.position;
        for (int i = 0; i < enemies.Length; i++)
        {
            GameObject e = enemies[i];
            if (e == null || !e.activeInHierarchy) continue;
            float sqr = ((Vector2)e.transform.position - here).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                nearest = e.transform;
            }
        }
        return nearest;
    }

    void OnTriggerEnter2D(Collider2D other) => TryHit(other);
    void OnTriggerStay2D(Collider2D other) => TryHit(other);

    // Contact damage: half the player's attack damage, routed through the shared crit roll,
    // gated by a per-enemy cooldown so a boss isn't melted in a single frame of overlap.
    private void TryHit(Collider2D other)
    {
        if (!_active || worldState.instance == null) return;
        if (!other.CompareTag(_enemyTag)) return;

        // Stable per-enemy identity (multi-collider enemies share their rigidbody).
        int enemyId = other.attachedRigidbody != null
            ? other.attachedRigidbody.gameObject.GetInstanceID()
            : other.transform.root.gameObject.GetInstanceID();

        float now = Time.time;
        if (_nextHitTime.TryGetValue(enemyId, out float next) && now < next) return;

        float interval = worldState.instance.robotHitInterval > 0f
            ? worldState.instance.robotHitInterval
            : worldState.instance.FireCooldown();
        _nextHitTime[enemyId] = now + interval;

        enemyHealth eh = other.GetComponent<enemyHealth>();
        if (eh == null) return;

        int dmg = Mathf.FloorToInt(worldState.instance.robotDamageFactor * worldState.instance.AttackDamage());
        int finalDmg = worldState.instance.RollDamage(dmg, out bool crit);
        eh.takeDamage(finalDmg, crit);
    }
}
