using UnityEngine;
using UnityEngine.UI;

public class bossHealthBar : MonoBehaviour
{
    [SerializeField] private enemyHealth health;      // the boss's enemyHealth
    [SerializeField] private Image fill;              // BarFill, Image Type = Filled
    [SerializeField] private Transform followTarget;  // the boss root transform
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.2f, 0f);
    [SerializeField] private bool billboard = true;

    void OnEnable()
    {
        if (fill != null) fill.fillAmount = 1f;
        UpdateFollow();
    }

    void LateUpdate()
    {
        if (health != null && fill != null && health.MaxHp > 0)
            fill.fillAmount = Mathf.Clamp01((float)health.CurrentHp / health.MaxHp);
        UpdateFollow();
    }

    void UpdateFollow()
    {
        if (followTarget != null)
            transform.position = followTarget.position + worldOffset;
        if (billboard && Camera.main != null)
            transform.rotation = Camera.main.transform.rotation;
    }
}
