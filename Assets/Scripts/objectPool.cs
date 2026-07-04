using UnityEngine;
using System.Collections.Generic;

public class objectPool : MonoBehaviour
{
    public static objectPool instance;

    private Dictionary<GameObject, Queue<GameObject>> pools = new Dictionary<GameObject, Queue<GameObject>>();

    void Awake()
    {
        instance = this;
    }

    public GameObject get(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        GameObject instanceObj;

        if (pools.ContainsKey(prefab) && pools[prefab].Count > 0)
        {
            instanceObj = pools[prefab].Dequeue();
        }
        else
        {
            instanceObj = Instantiate(prefab, position, rotation);
        }

        instanceObj.transform.position = position;
        instanceObj.transform.rotation = rotation;

        pooledObject marker = instanceObj.GetComponent<pooledObject>();
        if (marker == null)
        {
            marker = instanceObj.AddComponent<pooledObject>();
        }
        marker.source = prefab;

        instanceObj.SetActive(true);
        return instanceObj;
    }

    public void ret(GameObject instance)
    {
        pooledObject marker = instance.GetComponent<pooledObject>();
        if (marker == null)
        {
            Debug.LogWarning("objectPool.ret: returned a non-pooled object; destroying it.");
            Destroy(instance);
            return;
        }

        instance.SetActive(false);

        if (!pools.ContainsKey(marker.source))
        {
            pools[marker.source] = new Queue<GameObject>();
        }
        pools[marker.source].Enqueue(instance);
    }
}
