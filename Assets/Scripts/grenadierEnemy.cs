using UnityEngine;

/// <summary>
/// On-death grenade attack for a chaser-style enemy. This component ONLY handles the
/// death behaviour — normal movement (chaserBehaviour) and HP (enemyHealth) live on the
/// same GameObject and are wired separately by scene-architect.
///
/// When this enemy dies it: (1) captures the player's CURRENT position, (2) drops a red
/// circle telegraph at that ground position, (3) lobs a grenade-style projectile (the same
/// pooled `grenade` the player throws) aimed at that captured position, and (4) removes the
/// telegraph when the grenade reaches / detonates at the target.
///
/// Death hook: enemyHealth exposes NO death event/Action — its die() is private and simply
/// returns the GameObject to the pool (which deactivates it, firing OnDisable). So we hook
/// OnDisable and gate on enemyHealth.CurrentHp &lt;= 0 to distinguish a real death from a pool
/// prewarm / scene teardown. (If a proper death event is added to enemyHealth later, this
/// should subscribe to it in OnEnable / unsubscribe in OnDisable instead.)
///
/// Because the enemy GameObject is being deactivated as it dies, the telegraph and grenade
/// are spawned as INDEPENDENT objects (never children of the dying enemy) so they survive,
/// and the telegraph's removal is scheduled with Object.Destroy(go, delay) — an engine-side
/// timer that runs even though this enemy is already inactive, so no coroutine on the dying
/// object is required.
/// </summary>
[RequireComponent(typeof(enemyHealth))]
public class grenadierEnemy : MonoBehaviour
{
    [SerializeField] private GameObject _grenadeProjectilePrefab;   // player grenade projectile (Assets/Prefabs/grenade.prefab)
    [SerializeField] private GameObject _redCircleTelegraphPrefab;   // red circle ground marker (scene-architect creates)

    // Seconds the red circle stays up before the grenade lands. Keep this matched to the
    // grenade prefab's flight/fuse window (grenade._flightTime ~0.5s, capped by _fuse ~0.6s)
    // so the circle vanishes right as the projectile detonates at the target.
    [SerializeField] private float _telegraphDuration = 0.6f;

    private enemyHealth _health;
    private static bool _appQuitting;   // suppress the attack during application teardown

    void Awake()
    {
        _health = GetComponent<enemyHealth>();
    }

    void OnApplicationQuit()
    {
        _appQuitting = true;
    }

    void OnDisable()
    {
        // Fire ONLY on a genuine death: HP depleted, game world still live, not quitting.
        if (_appQuitting) return;
        if (_health == null || _health.CurrentHp > 0) return;
        if (worldState.instance == null || worldState.instance.player == null) return;
        if (_grenadeProjectilePrefab == null) return;

        // (1) Capture the player's CURRENT position at the moment of death.
        Vector3 target = worldState.instance.player.position;
        target.z = 0f;   // ground plane

        Vector3 origin = transform.position;
        origin.z = 0f;

        // (2) Red circle telegraph at the captured ground position, as an INDEPENDENT object.
        //     Scheduled destroy survives this enemy going inactive, and (4) syncs its removal
        //     to the grenade's landing window.
        if (_redCircleTelegraphPrefab != null)
        {
            GameObject telegraph = Instantiate(_redCircleTelegraphPrefab, target, Quaternion.identity);
            Object.Destroy(telegraph, Mathf.Max(0.05f, _telegraphDuration));
        }

        // (3) Lob the grenade-style projectile toward the captured position. Mirror the
        //     player's grenadeWeapon path: pool-get the grenade at our death position, then
        //     Throw() with the offset to the target (grenade.Throw takes a relative offset).
        if (objectPool.instance == null) return;
        GameObject go = objectPool.instance.get(_grenadeProjectilePrefab, origin, Quaternion.identity);
        if (go == null) return;
        grenade g = go.GetComponent<grenade>();
        if (g != null) g.Throw((Vector2)(target - origin));
    }
}
