using UnityEngine;

public class pooledObject : MonoBehaviour
{
    public GameObject source;

    private void OnEnable()
    {
        if (source != null)
        {
            EnemyRegistry.Add(source, transform);
        }
    }

    private void OnDisable()
    {
        if (source != null)
        {
            EnemyRegistry.Remove(source, transform);
        }
    }
}
