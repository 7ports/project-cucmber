using UnityEngine;
using UnityEngine.UI;

public class pauseStatsView : MonoBehaviour
{
    [SerializeField] private Text statsText;

    void OnEnable()
    {
        if (statsText != null && worldState.instance != null)
        {
            float fireRate = 1f / (worldState.instance.attackSpeed * worldState.instance.baseAttackSpeed);

            string s = "Level: " + worldState.instance.level + "\n" +
                       "HP: " + worldState.instance.currentHP + "/" + worldState.instance.maxHP + "\n" +
                       "Damage: " + worldState.instance.attackDamage.ToString("0.0") + "\n" +
                       "Move Speed: " + worldState.instance.moveSpeed.ToString("0.0") + "\n" +
                       "Fire Rate: " + fireRate.ToString("0.0") + "/s" + "\n" +
                       "Range: " + worldState.instance.range.ToString("0.0");

            statsText.text = s;
        }
    }
}
