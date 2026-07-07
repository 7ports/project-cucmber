using UnityEngine;
using UnityEngine.UI;

public class pauseStatsView : MonoBehaviour
{
    [SerializeField] private Text statsText;

    void OnEnable()
    {
        if (statsText != null && worldState.instance != null)
        {
            float fireRate = (1f / worldState.instance.FireCooldown());

            string s = "Level: " + worldState.instance.level + "\n" +
                       "HP: " + worldState.instance.currentHP + "/" + worldState.instance.MaxHP() + "\n" +
                       "Damage: " + worldState.instance.AttackDamage().ToString("0.0") + "\n" +
                       "Move Speed: " + worldState.instance.MoveSpeed().ToString("0.0") + "\n" +
                       "Fire Rate: " + fireRate.ToString("0.0") + "/s" + "\n" +
                       "Range: " + worldState.instance.Range().ToString("0.0") + "\n" +
                       "Pickup Radius: " + worldState.instance.PickupRadius().ToString("0.0");

            statsText.text = s;
        }
    }
}
