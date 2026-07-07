using UnityEngine;
using UnityEngine.InputSystem;

public class playerMovement : MonoBehaviour
{
    Animator playerAnimator;
    [SerializeField] private float animThreshold = 0.1f;
    float x, y;
    float oldX, oldY;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        oldX = oldY = float.NaN;
        playerAnimator = GetComponent<Animator>();
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
            transform.Translate(new Vector3(x,y).normalized * Time.deltaTime * worldState.instance.MoveSpeed());
        }        
        
    }
}
