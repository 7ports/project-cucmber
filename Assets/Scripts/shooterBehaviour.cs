using UnityEngine;

public class shooterBehaviour : MonoBehaviour
{
    [SerializeField] private float moveSpeed    = 1.5f;
    [SerializeField] private float aimRange     = 4f;
    [SerializeField] private float aimDuration  = 0.75f;
    [SerializeField] private float fireCooldown = 2f;

    [SerializeField] private GameObject enemyProjectilePrefab;
    [SerializeField] private float projectileSpeed = 6f;
    [SerializeField] private LineRenderer telegraph;
    [SerializeField] private Color lineColor = new Color(1f, 0.2f, 0.2f, 0.8f);
    [SerializeField] private float lineWidth = 0.06f;

    [SerializeField] private float leashDistance = 14f;      // world units before leash can trigger
    [SerializeField] private float stuckSeconds = 1.5f;      // time wedged before leashing closer
    [SerializeField] private float leashOffscreenPad = 1f;   // units past the visible edge

    private const float _stuckMoveEpsilon = 0.05f;           // min per-step progress to count as "moving"

    private enum State { Chase, Aim, Fire, Cooldown }
    private State state;
    private float stateTimer;
    private Vector2 aimDir;
    private enemyHealth _health;                 // ADDED
    private Rigidbody2D _rb;   // ADD
    private float _stuckTimer = 0f;
    private Vector2 _lastLeashPos;

    void Awake()
    {
        ConfigureTelegraph();
        _health = GetComponent<enemyHealth>();
        _rb = GetComponent<Rigidbody2D>();          // ADD
        _lastLeashPos = _rb.position;
    }

    void OnEnable()
    {
        state = State.Chase;
        stateTimer = 0f;
        aimDir = Vector2.right;
        if (telegraph != null) telegraph.enabled = false;
    }

    void FixedUpdate()                               // CHANGED from Update()
    {
        if (worldState.instance == null || worldState.instance.player == null) return;
        if (_health != null && _health.IsFrozen) return;       // freeze pauses the state machine
        Vector3 playerPos = worldState.instance.player.position;
        float dist = Vector2.Distance(_rb.position, playerPos);

        // Wall-stuck leash: far from the player AND making no positional progress => wedged on a wall.
        if (((Vector2)_rb.position - (Vector2)playerPos).sqrMagnitude > leashDistance * leashDistance)
        {
            if (((Vector2)_rb.position - _lastLeashPos).sqrMagnitude < _stuckMoveEpsilon * _stuckMoveEpsilon)
                _stuckTimer += Time.fixedDeltaTime;
            else
                _stuckTimer = 0f;
            _lastLeashPos = _rb.position;

            if (_stuckTimer >= stuckSeconds)
            {
                _rb.position = ComputeOffscreenPoint(playerPos);
                _stuckTimer = 0f;
                _lastLeashPos = _rb.position;
                state = State.Chase; stateTimer = 0f; EnableTelegraph(false);
                return;
            }
        }
        else
        {
            _stuckTimer = 0f;
            _lastLeashPos = _rb.position;
        }

        switch (state)
        {
            case State.Chase:
                _rb.MovePosition(Vector2.MoveTowards(_rb.position, playerPos, moveSpeed * Time.fixedDeltaTime));
                if (dist <= aimRange)
                {
                    aimDir = ((Vector2)(playerPos - transform.position)).normalized;
                    if (aimDir == Vector2.zero) aimDir = Vector2.right; // guard: player exactly on top
                    state = State.Aim; stateTimer = 0f; EnableTelegraph(true);
                }
                break;

            case State.Aim:
                UpdateTelegraph(transform.position, transform.position + (Vector3)(aimDir * aimRange));
                stateTimer += Time.fixedDeltaTime;
                if (stateTimer >= aimDuration) state = State.Fire;
                break;

            case State.Fire:
                FireProjectile();
                EnableTelegraph(false);
                state = State.Cooldown; stateTimer = 0f;
                break;

            case State.Cooldown:
                _rb.MovePosition(Vector2.MoveTowards(_rb.position, playerPos, moveSpeed * Time.fixedDeltaTime));
                stateTimer += Time.fixedDeltaTime;
                if (stateTimer >= fireCooldown) state = State.Chase;
                break;
        }
    }

    void FireProjectile()
    {
        if (objectPool.instance == null || enemyProjectilePrefab == null) return;
        GameObject go = objectPool.instance.get(enemyProjectilePrefab, transform.position, Quaternion.identity);
        enemyProjectile proj = go.GetComponent<enemyProjectile>();
        if (proj != null) proj.Launch(aimDir, projectileSpeed);
    }

    void EnableTelegraph(bool on) { if (telegraph != null) telegraph.enabled = on; }

    void UpdateTelegraph(Vector3 from, Vector3 to)
    {
        if (telegraph == null) return;
        telegraph.positionCount = 2;
        telegraph.SetPosition(0, from);
        telegraph.SetPosition(1, to);
    }

    void ConfigureTelegraph()
    {
        if (telegraph == null) return;
        telegraph.useWorldSpace  = true;
        telegraph.positionCount  = 2;
        telegraph.startWidth     = lineWidth;
        telegraph.endWidth       = lineWidth;
        telegraph.numCapVertices = 0;
        telegraph.startColor     = lineColor;
        telegraph.endColor       = lineColor;
        telegraph.sortingOrder   = 6;
        telegraph.enabled        = false;
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
