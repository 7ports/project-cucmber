using UnityEngine;

public class xpVacuumPickup : MonoBehaviour
{
    void OnTriggerEnter2D(Collider2D other)
    {
        if (worldState.instance == null) return;
        if (other.transform == worldState.instance.player ||
            other.transform.root == worldState.instance.player)
        {
            pickupBehaviour.VacuumAll(); // pull every active XP pickup to the player
            if (objectPool.instance != null) objectPool.instance.ret(gameObject);
        }
    }
}
