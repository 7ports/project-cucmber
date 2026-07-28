using UnityEngine;

public class chaserBehaviour : MonoBehaviour
{
    [SerializeField] private float chaseSpeed = 1.5f;
    [SerializeField] private float leashDistance = 14f;      // world units before leash can trigger
    [SerializeField] private float stuckSeconds = 1.5f;      // time wedged before leashing closer
    [SerializeField] private float leashOffscreenPad = 1f;   // units past the visible edge
    [SerializeField] private float interceptInterval = 3f;   // seconds between timed intercept teleports
    [SerializeField] private float interceptJitter = 1f;     // lateral jitter for intercept points
    [SerializeField] private float leadDistance = 2f;        // units ahead to bias the chase target

    private const float _stuckMoveEpsilon = 0.05f;           // min per-step progress to count as "moving"

    private enemyHealth _health;                 // ADDED
    private Rigidbody2D _rb;   // ADD
    private float _stuckTimer = 0f;
    private float _interceptTimer = 0f;
    private Vector2 _lastLeashPos;

    void Awake()
    {
        _health = GetComponent<enemyHealth>();
        _rb = GetComponent<Rigidbody2D>();          // ADD
        _lastLeashPos = _rb.position;
    }

    void FixedUpdate()                               // CHANGED from Update()
    {
        if (worldState.instance == null || worldState.instance.player == null) return;
        if (_health != null && _health.IsFrozen) return;   // freeze gate preserved

        Vector2 target = worldState.instance.player.position;

        // Wall-stuck leash: far from the player AND making no positional progress => wedged on a wall.
        if (((Vector2)_rb.position - target).sqrMagnitude > leashDistance * leashDistance)
        {
            if (((Vector2)_rb.position - _lastLeashPos).sqrMagnitude < _stuckMoveEpsilon * _stuckMoveEpsilon)
                _stuckTimer += Time.fixedDeltaTime;
            else
                _stuckTimer = 0f;
            _lastLeashPos = _rb.position;

            if (_stuckTimer >= stuckSeconds)
            {
                _rb.position = ComputeOffscreenPoint(target);
                _stuckTimer = 0f;
                _lastLeashPos = _rb.position;
                return;
            }
        }
        else
        {
            _stuckTimer = 0f;
            _lastLeashPos = _rb.position;
        }

        // Intercept-leash: when aggressiveBlocking is on and player moving away, teleport on timer
        if (worldState.instance != null && worldState.instance.aggressiveBlocking)
        {
            Vector2 playerVel = worldState.instance.playerVelocity;
            Vector2 toPlayer = (Vector2)(target - _rb.position);
            if (Vector2.Dot(toPlayer, playerVel) > 0)
            {
                _interceptTimer += Time.fixedDeltaTime;
                if (_interceptTimer >= interceptInterval)
                {
                    _rb.position = EnemyPathing.ComputeInterceptPoint(_rb.position, target, playerVel, Camera.main, leashOffscreenPad, interceptJitter);
                    _interceptTimer = 0f;
                    _lastLeashPos = _rb.position;
                    return;
                }
            }
            else
            {
                _interceptTimer = 0f;
            }
        }

        // Chase with optional lead bias
        Vector2 chaseTarget = target;
        if (worldState.instance != null && worldState.instance.aggressiveBlocking)
        {
            Vector2 playerVel = worldState.instance.playerVelocity;
            if (playerVel.sqrMagnitude > 0.01f)
            {
                chaseTarget = target + playerVel.normalized * leadDistance;
            }
        }

        Vector2 next = Vector2.MoveTowards(_rb.position, chaseTarget, chaseSpeed * Time.fixedDeltaTime);
        _rb.MovePosition(next);                       // CHANGED from transform.position = ...
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
