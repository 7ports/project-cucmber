using UnityEngine;
using UnityEngine.UI;

public class playerHealthUI : MonoBehaviour
{
    [SerializeField] private Image hpBar;

    void Update()
    {
        if (hpBar == null || worldState.instance == null) return;
        float denom = worldState.instance.maxHP;
        hpBar.fillAmount = denom > 0f ? Mathf.Clamp01(worldState.instance.currentHP / denom) : 0f;
    }
}
