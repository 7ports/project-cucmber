using UnityEngine;

public class pickupBehaviour : MonoBehaviour
{
    public bool pickup;
    [SerializeField] private float homeSpeed = 8f;
    [SerializeField] private int xpValue = 1;
    [SerializeField] private float _vacuumSpeed = 5f;

    // Fired by the XP-vacuum powerup — every active pickup subscribes and force-homes on pickup.
    private static System.Action _onVacuum;
    private bool _attracted;

    void OnEnable()
    {
        pickup = false;
        _attracted = false; // pooled reset
        _onVacuum += Attract;
    }

    void OnDisable()
    {
        // Required so pooled/destroyed pickups don't leak the static subscription.
        _onVacuum -= Attract;
    }

    void Update()
    {
        // Normal homing is gated by `pickup` (the pickup-radius system); the vacuum forces
        // homing regardless of that gate, at a boosted speed.
        if (!pickup && !_attracted) return;
        if (worldState.instance == null || worldState.instance.player == null) return;
        float speed = _attracted ? _vacuumSpeed : homeSpeed;
        transform.position = Vector3.MoveTowards(transform.position, worldState.instance.player.position, speed * Time.deltaTime);
    }

    private void Attract()
    {
        _attracted = true;
    }

    // Called by xpVacuumPickup on collection — pulls every active XP pickup to the player.
    public static void VacuumAll()
    {
        if (_onVacuum != null) _onVacuum();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (worldState.instance == null) return;
        if (other.transform == worldState.instance.player || other.transform.root == worldState.instance.player)
        {
            // xpValue is baked into the dropped prefab (XP1..XP4), which already encodes
            // base + XpGain bonus via enemyHealth's prefab choice — do NOT add the flat XP
            // bonus here or it double-counts. XpTimeMultiplier() applies the 7-min ×2 at collection.
            int earned = Mathf.RoundToInt(xpValue * worldState.instance.XpTimeMultiplier());
            worldState.instance.addXP(earned);
            objectPool.instance.ret(gameObject);
        }
    }
}
