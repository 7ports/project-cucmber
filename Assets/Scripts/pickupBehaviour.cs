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
            worldState.instance.currentXP += xpValue;
            objectPool.instance.ret(gameObject);
        }
    }
}
