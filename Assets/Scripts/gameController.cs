using UnityEngine;

public class gameController : MonoBehaviour
{
    public Transform player;
    [SerializeField] private bool aggressiveBlocking = false;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if(worldState.instance == null)
        {
            worldState.instance = new worldState();
        }
        worldState.instance.player = player;
        worldState.instance.aggressiveBlocking = aggressiveBlocking;
    }

    // Update is called once per frame
    void Update()
    {
        if (worldState.instance != null)
            worldState.instance.aggressiveBlocking = aggressiveBlocking;
    }
}
