using UnityEngine;

public class bloodBurst : MonoBehaviour
{
    [SerializeField] private float lifeSeconds = 1f;
    private float timer;

    void OnEnable()
    {
        timer = 0f;
        ParticleSystem ps = GetComponent<ParticleSystem>();
        if (ps != null)
        {
            ps.Clear();
            ps.Play();
        }
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= lifeSeconds)
        {
            if (objectPool.instance != null)
                objectPool.instance.ret(gameObject);
        }
    }
}
