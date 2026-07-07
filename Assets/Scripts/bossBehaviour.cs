using UnityEngine;

public class bossBehaviour : MonoBehaviour
{
    [SerializeField] private float chaseSpeed = 0.7f;        // ~0.7x the chaser's real speed of 1.0
    [SerializeField] private float leashDistance = 14f;      // world units
    [SerializeField] private float leashOffscreenPad = 1f;   // units past the visible edge

    void Update()
    {
        if (worldState.instance == null || worldState.instance.player == null) return;
        Vector3 target = worldState.instance.player.position;

        if ((transform.position - target).sqrMagnitude > leashDistance * leashDistance)
        {
            transform.position = ComputeOffscreenPoint(target);
            return;   // leash OR chase in a frame, never both
        }

        transform.position = Vector3.MoveTowards(transform.position, target, chaseSpeed * Time.deltaTime);
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
