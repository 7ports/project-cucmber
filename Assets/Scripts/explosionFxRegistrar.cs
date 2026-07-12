using UnityEngine;

// Tiny init hook: pushes the explosion-burst prefab into the static explosionUtil field so the
// static Detonate helper can spawn size-scaled explosion VFX. Attach to an always-present scene
// object (e.g. GameController) and assign the pooled explosion-burst prefab in the Inspector.
public class explosionFxRegistrar : MonoBehaviour
{
    [SerializeField] private GameObject explosionBurstPrefab;

    void Awake()
    {
        explosionUtil.ExplosionBurstPrefab = explosionBurstPrefab;
    }
}
