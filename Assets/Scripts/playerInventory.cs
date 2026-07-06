using UnityEngine;

public class playerInventory : MonoBehaviour
{
    public static playerInventory instance;

    public System.Collections.Generic.List<string> items = new System.Collections.Generic.List<string>();

    void Awake()
    {
        instance = this;
    }

    public void Add(string item)
    {
        items.Add(item);
    }
}
