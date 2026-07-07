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

    private enum State { Chase, Aim, Fire, Cooldown }
    private State state;
    private float stateTimer;
    private Vector2 aimDir;

    void Awake() { ConfigureTelegraph(); }

    void OnEnable()
    {
        state = State.Chase;
        stateTimer = 0f;
        aimDir = Vector2.right;
        if (telegraph != null) telegraph.enabled = false;
    }

    void Update()
    {
        if (worldState.instance == null || worldState.instance.player == null) return;
        Vector3 playerPos = worldState.instance.player.position;
        float dist = Vector2.Distance(transform.position, playerPos);

        switch (state)
        {
            case State.Chase:
                transform.position = Vector3.MoveTowards(transform.position, playerPos, moveSpeed * Time.deltaTime);
                if (dist <= aimRange)
                {
                    aimDir = ((Vector2)(playerPos - transform.position)).normalized;
                    if (aimDir == Vector2.zero) aimDir = Vector2.right; // guard: player exactly on top
                    state = State.Aim; stateTimer = 0f; EnableTelegraph(true);
                }
                break;

            case State.Aim:
                UpdateTelegraph(transform.position, transform.position + (Vector3)(aimDir * aimRange));
                stateTimer += Time.deltaTime;
                if (stateTimer >= aimDuration) state = State.Fire;
                break;

            case State.Fire:
                FireProjectile();
                EnableTelegraph(false);
                state = State.Cooldown; stateTimer = 0f;
                break;

            case State.Cooldown:
                transform.position = Vector3.MoveTowards(transform.position, playerPos, moveSpeed * Time.deltaTime);
                stateTimer += Time.deltaTime;
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
}
