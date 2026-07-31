using UnityEngine;
using System.Collections.Generic;

public static class EnemyRegistry
{
    private static Dictionary<GameObject, List<Transform>> _registry = new Dictionary<GameObject, List<Transform>>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry()
    {
        _registry.Clear();
    }

    public static void Add(GameObject type, Transform t)
    {
        if (type == null || t == null)
            return;

        if (!_registry.ContainsKey(type))
        {
            _registry[type] = new List<Transform>();
        }

        _registry[type].Add(t);
    }

    public static void Remove(GameObject type, Transform t)
    {
        if (type == null || t == null)
            return;

        if (_registry.ContainsKey(type))
        {
            _registry[type].Remove(t);
        }
    }

    public static IEnumerable<Transform> OfType(GameObject type)
    {
        if (type == null || !_registry.ContainsKey(type))
            return new List<Transform>();

        return _registry[type];
    }
}
