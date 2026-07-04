using UnityEngine;
using UnityEngine.InputSystem;

public class playerMovement : MonoBehaviour
{
    Animator playerAnimator;
    float x, y;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        playerAnimator = GetComponent<Animator>();   
    }

    // Update is called once per frame
    void Update()
    {
        
        x = Input.GetAxisRaw("Horizontal");
        y = Input.GetAxisRaw("Vertical");
        playerAnimator.SetFloat("x", x);
        playerAnimator.SetFloat("y", y);

    }


    private void FixedUpdate()
    {
        if ((x != 0) || (y != 0))
        {
            transform.Translate(new Vector3(x,y).normalized * Time.deltaTime * worldState.instance.moveSpeed);
        }        
        
    }
}
