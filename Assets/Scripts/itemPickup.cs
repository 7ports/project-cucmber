using UnityEngine;

public class itemPickup : MonoBehaviour
{
    void OnTriggerEnter2D(Collider2D other)
    {
        if (worldState.instance == null) return;
        if (other.transform == worldState.instance.player ||
            other.transform.root == worldState.instance.player)
        {
            itemManager.GrantRandomItem();
            if (objectPool.instance != null) objectPool.instance.ret(gameObject);
        }
    }
}
