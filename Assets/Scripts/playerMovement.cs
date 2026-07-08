using UnityEngine;
using UnityEngine.InputSystem;

public class playerMovement : MonoBehaviour
{
    Animator playerAnimator;
    [SerializeField] private float animThreshold = 0.1f;
    float x, y;
    float oldX, oldY;
    private Rigidbody2D _rb;   // ADD

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        oldX = oldY = float.NaN;
        playerAnimator = GetComponent<Animator>();
        _rb = GetComponent<Rigidbody2D>();          // ADD
    }

    // Update is called once per frame
    void Update()
    {
        
        x = Input.GetAxisRaw("Horizontal");
        y = Input.GetAxisRaw("Vertical");


        if (float.IsNaN(oldX) || Mathf.Abs(x - oldX) > animThreshold)
        {
            playerAnimator.SetFloat("x", x);
            oldX = x;
        }
        if (float.IsNaN(oldY) || Mathf.Abs(y - oldY) > animThreshold)
        {
            playerAnimator.SetFloat("y", y);
            oldY = y;
        }

    }


    private void FixedUpdate()
    {
        if ((x != 0) || (y != 0))
        {
            Vector2 delta = new Vector2(x, y).normalized
                            * Time.fixedDeltaTime * worldState.instance.MoveSpeed();
            _rb.MovePosition(_rb.position + delta);  // CHANGED from transform.Translate(...)
        }
        
    }
}
