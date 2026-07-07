using UnityEngine;

public class damageFlash : MonoBehaviour
{
    [SerializeField] private SpriteRenderer sr;
    [SerializeField] private Color flashColor = Color.red;
    [SerializeField] private float flashDuration = 0.12f;
    private Color baseColor;
    private float timer;

    void Awake()
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (sr != null) baseColor = sr.color;
    }

    void OnEnable()   // pool reset: never spawn mid-flash
    {
        timer = 0f;
        if (sr != null) sr.color = baseColor;
    }

    public void Flash()
    {
        timer = flashDuration;
        if (sr != null) sr.color = flashColor;
    }

    void Update()
    {
        if (timer <= 0f || sr == null) return;
        timer -= Time.deltaTime;
        float t = flashDuration > 0f ? Mathf.Clamp01(timer / flashDuration) : 0f;
        sr.color = Color.Lerp(baseColor, flashColor, t);
        if (timer <= 0f) sr.color = baseColor;
    }
}
