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
            float fireRate = (1f / w.FireCooldown());

            // Per stat: effective value, then the base value and the percent-upgrade
            // multiplier (Mult starts at 1.0; flat upgrades grow the base). worldState
            // base/mult fields are public, so this is an exact breakdown.
            string s = "Level: " + w.level + "\n" +
                       "HP: " + w.currentHP + "/" + w.MaxHP() + "  (base " + w.maxHPBase.ToString("0") + " x" + w.maxHPMult.ToString("0.00") + ")\n" +
                       "Damage: " + w.AttackDamage().ToString("0.0") + "  (base " + w.attackDamageBase.ToString("0.0") + " x" + w.attackDamageMult.ToString("0.00") + ")\n" +
                       "Move Speed: " + w.MoveSpeed().ToString("0.0") + "  (base " + w.moveSpeedBase.ToString("0.0") + " x" + w.moveSpeedMult.ToString("0.00") + ")\n" +
                       "Fire Rate: " + fireRate.ToString("0.0") + "/s  (x" + w.fireRateMult.ToString("0.00") + ")\n" +
                       "Range: " + w.Range().ToString("0.0") + "  (base " + w.rangeBase.ToString("0.0") + " x" + w.rangeMult.ToString("0.00") + ")\n" +
                       "Pickup Radius: " + w.PickupRadius().ToString("0.0") + "  (base " + w.pickupRadiusBase.ToString("0.0") + " x" + w.pickupRadiusMult.ToString("0.00") + ")\n" +
                       "Crit Chance: " + (w.CritChance() * 100f).ToString("0") + "%\n" +
                       "Crit Damage: x" + w.CritMultiplier().ToString("0.00") + "\n" +
                       "Total Damage: " + runStats.TotalDamage + "\n" +
                       "Average DPS: " + runStats.AvgDps().ToString("0.0") + "\n" +
                       "Enemies Killed: " + runStats.EnemiesKilled;

            statsText.text = s;
        }
    }
}
