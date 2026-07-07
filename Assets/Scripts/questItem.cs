using UnityEngine;

public class questItem : MonoBehaviour
{
    public Sprite icon;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (worldState.instance != null &&
            (other.transform == worldState.instance.player || other.transform.root == worldState.instance.player))
        {
            if (questManager.instance != null)
            {
                questManager.instance.Collect(this);
            }
            gameObject.SetActive(false);
        }
    }
}
