using UnityEngine;

public class chaserBehaviour : MonoBehaviour
{
    [SerializeField] private float chaseSpeed = 1.5f;

    // Update is called once per frame
    void Update()
    {
        if (worldState.instance == null || worldState.instance.player == null) return;

        transform.position = Vector3.MoveTowards(
            transform.position,
            worldState.instance.player.position,
            chaseSpeed * Time.deltaTime);
    }
}
