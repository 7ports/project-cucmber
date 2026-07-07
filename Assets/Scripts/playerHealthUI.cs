using UnityEngine;
using UnityEngine.UI;

public class playerHealthUI : MonoBehaviour
{
    [SerializeField] private Image hpBar;
    [SerializeField] private Text hpLabel;

    void Update()
    {
        if (hpBar == null || worldState.instance == null) return;
        float denom = worldState.instance.MaxHP();
        hpBar.fillAmount = denom > 0f ? Mathf.Clamp01(worldState.instance.currentHP / denom) : 0f;
        if (hpLabel != null) hpLabel.text = worldState.instance.currentHP + "/" + worldState.instance.MaxHP();
    }
}
