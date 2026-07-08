using UnityEngine;

public class chaserBehaviour : MonoBehaviour
{
    [SerializeField] private float chaseSpeed = 1.5f;
    private enemyHealth _health;                 // ADDED

    void Awake() { _health = GetComponent<enemyHealth>(); }   // ADDED

    // Update is called once per frame
    void Update()
    {
        if (worldState.instance == null || worldState.instance.player == null) return;
        if (_health != null && _health.IsFrozen) return;       // ADDED: freeze gate

        transform.position = Vector3.MoveTowards(
            transform.position,
            worldState.instance.player.position,
            chaseSpeed * Time.deltaTime);
    }
}
