using UnityEngine;

public class playerPickupRadius : MonoBehaviour
{
    [SerializeField] private LayerMask xpLayer;
    [SerializeField] private float scanInterval = 0.1f;
    private float scanTimer;

    void Update()
    {
        scanTimer += Time.deltaTime;
        if (scanTimer >= scanInterval)
        {
            scanTimer = 0f;
            scan();
        }
    }

    void scan()
    {
        float radius = worldState.instance != null ? worldState.instance.PickupRadius() : 4f;
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, radius, xpLayer);
        foreach (Collider2D hit in hits)
        {
            pickupBehaviour pb = hit.GetComponent<pickupBehaviour>();
            if (pb != null)
                pb.pickup = true;
        }
    }

    void OnDrawGizmosSelected()
    {
        float radius = (Application.isPlaying && worldState.instance != null) ? worldState.instance.PickupRadius() : 4f;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
