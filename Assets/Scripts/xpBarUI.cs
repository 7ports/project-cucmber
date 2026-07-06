using UnityEngine;
using UnityEngine.UI;

public class xpBarUI : MonoBehaviour
{
    [SerializeField] private Image xpBar;   // Image Type = Filled

    void Update()
    {
        if (xpBar == null || worldState.instance == null) return;
        float denom = worldState.instance.lvlUpXP;
        xpBar.fillAmount = denom > 0f ? Mathf.Clamp01(worldState.instance.currentXP / denom) : 0f;
    }
}
