using System.Collections;
using UnityEngine;

// Shooting-behaviour component for a slow, larger chaser-style enemy.
// Fires a four-way CROSS volley (up/down/left/right, world axes) on a repeating
// interval. Movement + enemyHealth live on separate components on the prefab;
// scene-architect assembles the prefab (slower move speed + larger scale).
public class crossShooterEnemy : MonoBehaviour
{
    [SerializeField] private GameObject _projectilePrefab;   // wire enemyProjectile.prefab
    [SerializeField] private float _fireInterval = 2.5f;     // seconds between volleys
    [SerializeField] private float _projectileSpeed = 6f;    // launch speed per bolt

    private enemyHealth _health;
    private Coroutine _fireRoutine;

    // World-axis cross pattern: +Y, -Y, -X, +X.
    private static readonly Vector2[] _crossDirs =
    {
        Vector2.up,
        Vector2.down,
        Vector2.left,
        Vector2.right
    };

    void Awake()
    {
        _health = GetComponent<enemyHealth>();
    }

    void OnEnable()
    {
        _fireRoutine = StartCoroutine(FireLoop());
    }

    void OnDisable()
    {
        if (_fireRoutine != null)
        {
            StopCoroutine(_fireRoutine);
            _fireRoutine = null;
        }
    }

    private IEnumerator FireLoop()
    {
        var wait = new WaitForSeconds(_fireInterval);
        while (true)
        {
            yield return wait;
            if (_health != null && _health.IsFrozen) continue;   // freeze pauses shooting
            FireCross();
        }
    }

    private void FireCross()
    {
        if (_projectilePrefab == null) return;

        for (int i = 0; i < _crossDirs.Length; i++)
        {
            GameObject go = objectPool.instance != null
                ? objectPool.instance.get(_projectilePrefab, transform.position, Quaternion.identity)
                : Instantiate(_projectilePrefab, transform.position, Quaternion.identity);

            enemyProjectile proj = go.GetComponent<enemyProjectile>();
            if (proj != null) proj.Launch(_crossDirs[i], _projectileSpeed);
        }
    }
}
