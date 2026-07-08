using UnityEngine;

public class itemPickup : MonoBehaviour
{
    void OnTriggerEnter2D(Collider2D other)
    {
        if (worldState.instance == null) return;
        if (other.transform == worldState.instance.player ||
            other.transform.root == worldState.instance.player)
        {
            if (itemChoiceMenuController.instance != null)
                itemChoiceMenuController.instance.Offer(); // opens menu (or no-op if owns all)
            if (objectPool.instance != null) objectPool.instance.ret(gameObject);
        }
    }
}
