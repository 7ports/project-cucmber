using UnityEngine;

public class gameController : MonoBehaviour
{
    public Transform player;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if(worldState.instance == null)
        {
            worldState.instance = new worldState();
        } 
        worldState.instance.player = player;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
