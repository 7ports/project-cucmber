using UnityEngine;
using UnityEngine.UI;

public class damageNumber : MonoBehaviour
{
    [SerializeField] private CanvasGroup group;
    [SerializeField] private Text label;
    [SerializeField] private Color color = new Color(0.55f, 0f, 0f, 1f); // dark red
    [SerializeField] private float fadeInSeconds = 0.1f;
    [SerializeField] private float travelSeconds = 0.5f;
    [SerializeField] private float holdSeconds = 0.2f;
    [SerializeField] private float fadeOutSeconds = 0.3f;
    [SerializeField] private Vector3 travelOffset = new Vector3(0f, 0.8f, 0f);
    private float timer;
    private Vector3 startPos;

    void OnEnable()
    {
        timer = 0f;
        startPos = transform.position;
        if (group != null) group.alpha = 0f;
    }

    public void Set(int amount)
    {
        if (label != null) { label.text = "-" + amount; label.color = color; }
    }

    void Update()
    {
        timer += Time.unscaledDeltaTime;
        float tp = travelSeconds > 0f ? Mathf.Clamp01(timer / travelSeconds) : 1f;
        float eased = tp * tp * (3f - 2f * tp);
        transform.position = startPos + travelOffset * eased;
        float life = Mathf.Max(travelSeconds, fadeInSeconds + holdSeconds + fadeOutSeconds);
        float a;
        if (timer < fadeInSeconds) a = fadeInSeconds > 0f ? timer / fadeInSeconds : 1f;
        else if (timer < life - fadeOutSeconds) a = 1f;
        else a = fadeOutSeconds > 0f ? Mathf.Clamp01((life - timer) / fadeOutSeconds) : 0f;
        if (group != null) group.alpha = Mathf.Clamp01(a);
        if (timer >= life) { if (objectPool.instance != null) objectPool.instance.ret(gameObject); }
    }
}
