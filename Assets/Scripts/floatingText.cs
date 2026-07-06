using UnityEngine;

public class floatingText : MonoBehaviour
{
    [SerializeField] private CanvasGroup group;
    [SerializeField] private float riseSpeed = 1f;
    [SerializeField] private float lifeSeconds = 1f;
    private float timer;

    void OnEnable()
    {
        timer = 0f;
        if (group != null) group.alpha = 1f;
    }

    void Update()
    {
        timer += Time.unscaledDeltaTime;
        transform.position += Vector3.up * riseSpeed * Time.unscaledDeltaTime;

        if (group != null) group.alpha = Mathf.Clamp01(1f - timer / lifeSeconds);

        if (timer >= lifeSeconds)
        {
            if (objectPool.instance != null) objectPool.instance.ret(gameObject);
        }
    }
}
