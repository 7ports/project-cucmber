using UnityEngine;

public class pickupBehaviour : MonoBehaviour
{
    public bool pickup;
    [SerializeField] private float homeSpeed = 8f;
    [SerializeField] private int xpValue = 1;

    void OnEnable()
    {
        pickup = false;
    }

    void Update()
    {
        if (!pickup) return;
        if (worldState.instance == null || worldState.instance.player == null) return;
        transform.position = Vector3.MoveTowards(transform.position, worldState.instance.player.position, homeSpeed * Time.deltaTime);
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
