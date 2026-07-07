using UnityEngine;
using UnityEngine.UI;

public class screenFlash : MonoBehaviour
{
    public static screenFlash instance;
    [SerializeField] private Image img;
    [SerializeField] private Color flashColor = new Color(1f, 0f, 0f, 1f);
    [SerializeField] private float maxAlpha = 0.4f;
    [SerializeField] private float flashDuration = 0.35f;
    private float timer, intensity;

    void Awake() { instance = this; if (img != null) SetAlpha(0f); }

    public void Flash() { Flash(1f, flashDuration); }
    public void Flash(float inten, float dur) { intensity = inten; flashDuration = dur > 0f ? dur : flashDuration; timer = flashDuration; }

    void Update()
    {
        if (img == null) return;
        if (timer > 0f)
        {
            timer -= Time.deltaTime;
            float a = maxAlpha * intensity * Mathf.Clamp01(timer / flashDuration);
            SetAlpha(a);
        }
    }

    void SetAlpha(float a) { img.color = new Color(flashColor.r, flashColor.g, flashColor.b, a); }
}
