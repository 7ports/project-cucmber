using UnityEngine;

public class chaserBehaviour : MonoBehaviour
{
    [SerializeField] private float chaseSpeed = 1.5f;
    private enemyHealth _health;                 // ADDED
    private Rigidbody2D _rb;   // ADD

    void Awake()
    {
        _health = GetComponent<enemyHealth>();
        _rb = GetComponent<Rigidbody2D>();          // ADD
    }

    void FixedUpdate()                               // CHANGED from Update()
    {
        if (worldState.instance == null || worldState.instance.player == null) return;
        if (_health != null && _health.IsFrozen) return;   // freeze gate preserved

        Vector2 target = worldState.instance.player.position;
        Vector2 next = Vector2.MoveTowards(_rb.position, target, chaseSpeed * Time.fixedDeltaTime);
        _rb.MovePosition(next);                       // CHANGED from transform.position = ...
    }
}
