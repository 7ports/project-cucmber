using System.Collections;
using UnityEngine;

public class slimeBehaviour : MonoBehaviour
{


    float movementTimer = 0;
    public float movementDelay = 1.5f;

    // Update is called once per frame
    void Update()
    {
        movementTimer += Time.deltaTime;
        if(movementTimer >= movementDelay)
        {
            StartCoroutine(jump());
        }     
    }



    IEnumerator jump()
    {
        Vector3 moveVector = worldState.instance.player.position - transform.position.normalized;
        Vector3 initPosition = transform.position;
        for (float t = 0; t<=1; t+= 0.1f)
        {
            transform.Translate(-transform.position + Vector3.Lerp(initPosition, moveVector, t));
            yield return new WaitForEndOfFrame();
        }
        movementTimer = 0;
        yield return null;

    }
}
