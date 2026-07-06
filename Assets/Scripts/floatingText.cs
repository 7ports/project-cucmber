using UnityEngine;

public class floatingText : MonoBehaviour
{
    [SerializeField] private CanvasGroup group;
    [SerializeField] private float fadeInSeconds = 0.25f;
    [SerializeField] private float travelSeconds = 0.6f;
    [SerializeField] private float holdSeconds = 0.4f;
    [SerializeField] private float fadeOutSeconds = 0.3f;
    [SerializeField] private Vector3 travelOffset = new Vector3(0f, 1.0f, 0f);
    private float timer;
    private Vector3 startPos;

    void OnEnable()
    {
        timer = 0f;
        startPos = transform.position;
        if (group != null) group.alpha = 0f;
    }

    void Update()
    {
        timer += Time.unscaledDeltaTime;

        float tp = travelSeconds > 0f ? Mathf.Clamp01(timer / travelSeconds) : 1f;
        float eased = tp * tp * (3f - 2f * tp);
        transform.position = startPos + travelOffset * eased;

        float life = Mathf.Max(travelSeconds, fadeInSeconds + holdSeconds + fadeOutSeconds);
        float a;
        if (timer < fadeInSeconds)
            a = fadeInSeconds > 0f ? timer / fadeInSeconds : 1f;
        else if (timer < life - fadeOutSeconds)
            a = 1f;
        else
            a = fadeOutSeconds > 0f ? Mathf.Clamp01((life - timer) / fadeOutSeconds) : 0f;
        if (group != null) group.alpha = Mathf.Clamp01(a);

        if (timer >= life)
        {
            if (objectPool.instance != null) objectPool.instance.ret(gameObject);
        }
    }
}
