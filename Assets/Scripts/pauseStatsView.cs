using UnityEngine;
using UnityEngine.UI;

public class pauseStatsView : MonoBehaviour
{
    [SerializeField] private Text statsText;

    void OnEnable()
    {
        if (statsText != null && worldState.instance != null)
        {
            worldState w = worldState.instance;

            // Per stat: "<starting> + <gained>", where starting is the base value and
            // gained (effective - base) is highlighted in yellow rich-text. worldState
            // base fields are public and every getter is base * mult, so this is an
            // exact split of what the player started with vs. earned from levels/items.
            string s = "Level: " + w.level + "\n" +
                       "HP: " + w.currentHP + "/" + Gain(w.maxHPBase, w.MaxHP(), "0") + "\n" +
                       "Damage: " + Gain(w.attackDamageBase, w.AttackDamage(), "0.0") + "\n" +
                       "Move Speed: " + Gain(w.moveSpeedBase, w.MoveSpeed(), "0.0") + "\n" +
                       "Fire Rate: " + Gain(w.fireRateBase, w.FireRate(), "0.0") + "/s\n" +
                       "Range: " + Gain(w.rangeBase, w.Range(), "0.0") + "\n" +
                       "Pickup Radius: " + Gain(w.pickupRadiusBase, w.PickupRadius(), "0.0") + "\n" +
                       "Crit Chance: " + Gain(w.critChanceBase * 100f, w.CritChance() * 100f, "0") + "%\n" +
                       "Crit Damage: x" + Gain(w.critDamageBase, w.CritMultiplier(), "0.00") + "\n" +
                       "Total Damage: " + runStats.TotalDamage + "\n" +
                       "Average DPS: " + runStats.AvgDps().ToString("0.0") + "\n" +
                       "Enemies Killed: " + runStats.EnemiesKilled;

            statsText.text = s;
        }
    }

    // Formats a stat as "<starting> + <gained>", with the gained portion in yellow.
    private static string Gain(float start, float effective, string format)
    {
        float gained = effective - start;
        return start.ToString(format) + " + <color=yellow>" + gained.ToString(format) + "</color>";
    }
}
