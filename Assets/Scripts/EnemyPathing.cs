using UnityEngine;

public static class EnemyPathing
{
    // Returns a point just past the visible screen edge, in the direction the player is HEADING,
    // so an enemy teleported/leashed there lands in the player's path. Falls back to the enemy->player
    // direction when the player is nearly stationary. Adds small lateral jitter so enemies don't stack.
    public static Vector2 ComputeInterceptPoint(Vector2 enemyPos, Vector2 playerPos, Vector2 playerVelocity,
                                                Camera cam, float pad, float jitter)
    {
        Vector2 heading = playerVelocity;
        if (heading.sqrMagnitude < 0.0001f) heading = (playerPos - enemyPos);
        if (heading.sqrMagnitude < 0.0001f) heading = Vector2.right;
        heading.Normalize();
        float halfH = cam != null ? cam.orthographicSize : 5f;
        float halfW = halfH * (cam != null ? cam.aspect : 1.777f);
        // project from player along heading to just past the screen edge
        float tx = heading.x != 0f ? (halfW + pad) / Mathf.Abs(heading.x) : float.MaxValue;
        float ty = heading.y != 0f ? (halfH + pad) / Mathf.Abs(heading.y) : float.MaxValue;
        float t = Mathf.Min(tx, ty);
        Vector2 edge = playerPos + heading * t;
        Vector2 perp = new Vector2(-heading.y, heading.x);
        edge += perp * Random.Range(-jitter, jitter);
        return edge;
    }
}
