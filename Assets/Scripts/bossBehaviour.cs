using UnityEngine;

public class bossBehaviour : MonoBehaviour
{
    [SerializeField] private float chaseSpeed = 0.7f;        // ~0.7x the chaser's real speed of 1.0
    [SerializeField] private float leashDistance = 14f;      // world units
    [SerializeField] private float leashOffscreenPad = 1f;   // units past the visible edge
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
        if (_health != null && _health.IsFrozen) return;   // leash+chase freeze gate preserved
        Vector3 target = worldState.instance.player.position;

        if (((Vector3)_rb.position - target).sqrMagnitude > leashDistance * leashDistance)
        {
            _rb.position = ComputeOffscreenPoint(target);  // CHANGED: teleport, not a swept move
            return;                                        // leash OR chase, never both (preserved)
        }

        Vector2 next = Vector2.MoveTowards(_rb.position, target, chaseSpeed * Time.fixedDeltaTime);
        _rb.MovePosition(next);                            // CHANGED from transform.position = ...
    }

    Vector3 ComputeOffscreenPoint(Vector3 player)
    {
        Camera cam = Camera.main;
        if (cam == null) return player;
        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;
        Vector3 d = transform.position - player;
        if (d.sqrMagnitude < 0.0001f) d = Vector3.right;
        Vector3 point;
        if (Mathf.Abs(d.x) * halfH >= Mathf.Abs(d.y) * halfW)
            point = player + new Vector3(Mathf.Sign(d.x) * (halfW + leashOffscreenPad), Mathf.Clamp(d.y, -halfH, halfH), 0f);
        else
            point = player + new Vector3(Mathf.Clamp(d.x, -halfW, halfW), Mathf.Sign(d.y) * (halfH + leashOffscreenPad), 0f);
        point.z = 0f;
        return point;
    }
}
