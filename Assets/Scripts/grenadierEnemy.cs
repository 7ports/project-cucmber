using UnityEngine;

/// <summary>
/// On-death grenade attack for a chaser-style enemy. This component ONLY handles the
/// death behaviour — normal movement (chaserBehaviour) and HP (enemyHealth) live on the
/// same GameObject and are wired separately by scene-architect.
///
/// When this enemy dies it: (1) captures the player's CURRENT position and (2) launches a
/// dedicated `enemyGrenade` aimed at that captured position. The enemyGrenade OWNS its own
/// telegraph (the red circle IS its blast radius), so this component no longer spawns or
/// manages a telegraph itself.
///
/// Death hook: enemyHealth exposes NO death event/Action — its die() is private and simply
/// returns the GameObject to the pool (which deactivates it, firing OnDisable). So we hook
/// OnDisable and gate on enemyHealth.CurrentHp &lt;= 0 to distinguish a real death from a pool
/// prewarm / scene teardown. (If a proper death event is added to enemyHealth later, this
/// should subscribe to it in OnEnable / unsubscribe in OnDisable instead.)
///
/// Because the enemy GameObject is being deactivated as it dies, the enemyGrenade projectile
/// (and the telegraph it spawns) are INDEPENDENT objects — never children of the dying enemy —
/// so they survive, and the flight/detonation coroutine runs on the surviving projectile, not
/// on this already-inactive enemy.
/// </summary>
[RequireComponent(typeof(enemyHealth))]
public class grenadierEnemy : MonoBehaviour
{
    [SerializeField] private GameObject _enemyGrenadePrefab;   // dedicated enemyGrenade projectile (Assets/Prefabs/enemies/enemyGrenade.prefab)

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
        if (_enemyGrenadePrefab == null) return;

        // (1) Capture the player's CURRENT position at the moment of death.
        Vector3 target = worldState.instance.player.position;
        target.z = 0f;   // ground plane

        Vector3 origin = transform.position;
        origin.z = 0f;

        // (2) Launch a dedicated enemyGrenade toward the captured position, as an INDEPENDENT
        //     object (pool-get at our death position, else Instantiate). The enemyGrenade owns
        //     its own telegraph and runs its own flight/detonation coroutine, so it survives
        //     this enemy going inactive.
        GameObject go = objectPool.instance != null
            ? objectPool.instance.get(_enemyGrenadePrefab, origin, Quaternion.identity)
            : Instantiate(_enemyGrenadePrefab, origin, Quaternion.identity);
        if (go == null) return;
        enemyGrenade g = go.GetComponent<enemyGrenade>();
        if (g != null) g.Launch((Vector2)target);
    }
}
