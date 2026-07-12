using System.Collections;
using UnityEngine;

public class slimeBehaviour : MonoBehaviour
{


    float movementTimer = 0;
    public float movementDelay = 1.5f;
    [SerializeField] private float hopDistance = 1f;
    [SerializeField] private float leashDistance = 14f;      // world units before leash can trigger
    [SerializeField] private float stuckSeconds = 3f;        // window length before checking for wall-stuck
    [SerializeField] private float leashOffscreenPad = 1f;   // units past the visible edge

    private const float _stuckProgressThreshold = 0.5f;      // min net movement over the window to count as progress

    private bool isHopping = false;
    private Rigidbody2D _rb;   // ADD
    private float _stuckWindowTimer = 0f;
    private Vector2 _windowStartPos;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();   // ADD
        _windowStartPos = _rb.position;
    }

    // Update is called once per frame
    void Update()
    {
        if (worldState.instance == null || worldState.instance.player == null) return;

        movementTimer += Time.deltaTime;
        if (movementTimer >= movementDelay && !isHopping)
        {
            StartCoroutine(jump());
        }
    }

    void FixedUpdate()
    {
        // Sliding fix: a dynamic Rigidbody2D retains collision-imparted velocity between MovePosition
        // calls, so it drifts. Zero it whenever the slime is not mid-hop so it only ever moves under
        // its own hop control and never slides from collisions.
        if (!isHopping) _rb.linearVelocity = Vector2.zero;

        if (worldState.instance == null || worldState.instance.player == null) return;

        // Wall-stuck leash: measured over a window so periodic hopping isn't mistaken for being stuck.
        Vector2 target = worldState.instance.player.position;
        _stuckWindowTimer += Time.fixedDeltaTime;
        if (_stuckWindowTimer >= stuckSeconds)
        {
            bool far = ((Vector2)_rb.position - target).sqrMagnitude > leashDistance * leashDistance;
            bool littleProgress = ((Vector2)_rb.position - _windowStartPos).sqrMagnitude < _stuckProgressThreshold * _stuckProgressThreshold;
            if (far && littleProgress && !isHopping)
                _rb.position = ComputeOffscreenPoint(target);

            _stuckWindowTimer = 0f;
            _windowStartPos = _rb.position;
        }
    }



    IEnumerator jump()
    {
        isHopping = true;

        // Parenthesize the subtraction, THEN normalize — a fixed direction toward the player.
        Vector3 dir = (worldState.instance.player.position - transform.position).normalized;
        Vector3 initPosition = transform.position;
        // Fixed-length hop target — NOT the player's absolute position (which caused overshoot).
        Vector3 targetPosition = initPosition + dir * hopDistance;

        for (float t = 0; t <= 1; t += 0.1f)
        {
            _rb.MovePosition(Vector3.Lerp(initPosition, targetPosition, t));  // CHANGED from transform.position =
            yield return new WaitForFixedUpdate();                            // CHANGED: land on physics step
        }

        _rb.MovePosition(targetPosition);                                    // CHANGED from transform.position =
        movementTimer = 0;
        isHopping = false;
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
