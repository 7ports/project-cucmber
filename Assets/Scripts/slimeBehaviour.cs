using System.Collections;
using UnityEngine;

public class slimeBehaviour : MonoBehaviour
{


    float movementTimer = 0;
    public float movementDelay = 1.5f;
    [SerializeField] private float hopDistance = 1f;

    private bool isHopping = false;
    private Rigidbody2D _rb;   // ADD

    void Awake() { _rb = GetComponent<Rigidbody2D>(); }   // ADD

    // Update is called once per frame
    void Update()
    {
        if (worldState.instance == null || worldState.instance.player == null) return;

        movementTimer += Time.deltaTime;
        if (movementTimer >= movementDelay && !isHopping)
        {
            StartCoroutine(jump());
        }
    }



    IEnumerator jump()
    {
        isHopping = true;

        // Parenthesize the subtraction, THEN normalize — a fixed direction toward the player.
        Vector3 dir = (worldState.instance.player.position - transform.position).normalized;
        Vector3 initPosition = transform.position;
        // Fixed-length hop target — NOT the player's absolute position (which caused overshoot).
        Vector3 targetPosition = initPosition + dir * hopDistance;

        for (float t = 0; t <= 1; t += 0.1f)
        {
            _rb.MovePosition(Vector3.Lerp(initPosition, targetPosition, t));  // CHANGED from transform.position =
            yield return new WaitForFixedUpdate();                            // CHANGED: land on physics step
        }

        _rb.MovePosition(targetPosition);                                    // CHANGED from transform.position =
        movementTimer = 0;
        isHopping = false;
    }
}
