using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class levelUpMenuController : MonoBehaviour
{
    private enum StatKind { MaxHP, FireRate, AttackDamage, MoveSpeed, Range, Defense, Regen, PickupRadius, Pierce, XpGain, ProjectileSize }
    private enum Mode { Flat, Percent }

    private struct Upgrade
    {
        public StatKind kind;
        public Mode mode;
    }

    private const int OfferCount = 5;

    [SerializeField] private Button[] buttons; // 3
    [SerializeField] private Text[] labels;    // 3, one per button

    private readonly Upgrade[] rolled = new Upgrade[OfferCount];

    private void OnEnable()
    {
        if (buttons == null || buttons.Length < OfferCount) return;
        if (labels == null || labels.Length < OfferCount) return;

        List<Upgrade> pool = BuildPool();

        // Fisher-Yates shuffle
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            Upgrade tmp = pool[i];
            pool[i] = pool[j];
            pool[j] = tmp;
        }

        for (int i = 0; i < OfferCount; i++)
        {
            rolled[i] = pool[i];
            labels[i].text = LabelFor(rolled[i]);

            int idx = i;
            buttons[i].onClick.RemoveAllListeners();
            buttons[i].onClick.AddListener(() => Choose(rolled[idx]));
        }
    }

    private List<Upgrade> BuildPool()
    {
        StatKind[] stats =
        {
            StatKind.MaxHP,
            StatKind.FireRate,
            StatKind.AttackDamage,
            StatKind.MoveSpeed,
            StatKind.Range,
            StatKind.Defense,
            StatKind.Regen,
            StatKind.PickupRadius,
            StatKind.Pierce,
            StatKind.XpGain,
            StatKind.ProjectileSize
        };

        bool defenseHasBase = worldState.instance != null && worldState.instance.defenseBase > 0f;
        bool regenHasBase = worldState.instance != null && worldState.instance.regenBase > 0f;
        bool xpGainAtCap = worldState.instance != null && worldState.instance.xpBonusPerPickup >= worldState.xpBonusCap;

        List<Upgrade> pool = new List<Upgrade>();
        foreach (StatKind k in stats)
        {
            // XP-Gain is capped: once the bonus hits the cap, stop offering it entirely.
            if (k == StatKind.XpGain && xpGainAtCap) continue;

            // Flat is always offered.
            pool.Add(new Upgrade { kind = k, mode = Mode.Flat });

            // Pierce and XpGain are flat-only stats — never offer them as a percent.
            if (k == StatKind.Pierce) continue;
            if (k == StatKind.XpGain) continue;

            // Percent is inert on a 0 base for Defense/Regen — only offer once seeded.
            if (k == StatKind.Defense && !defenseHasBase) continue;
            if (k == StatKind.Regen && !regenHasBase) continue;

            pool.Add(new Upgrade { kind = k, mode = Mode.Percent });
        }

        return pool;
    }

    private string LabelFor(Upgrade u)
    {
        worldState ws = worldState.instance;
        if (ws == null) return "";

        var ci = System.Globalization.CultureInfo.InvariantCulture;

        if (u.mode == Mode.Flat)
        {
            switch (u.kind)
            {
                case StatKind.AttackDamage: return "+" + ws.attackDamageFlatStep.ToString(ci) + " Damage";
                case StatKind.MoveSpeed:    return "+" + ws.moveSpeedFlatStep.ToString(ci) + " Move Speed";
                case StatKind.FireRate:     return "+" + ws.fireRateFlatStep.ToString(ci) + " Fire Rate";
                case StatKind.Range:        return "+" + ws.rangeFlatStep.ToString(ci) + " Range";
                case StatKind.MaxHP:        return "+" + ws.maxHPFlatStep.ToString(ci) + " Max HP";
                case StatKind.Defense:      return "+" + ws.defenseFlatStep.ToString(ci) + " Defense";
                case StatKind.Regen:        return "+" + ws.regenFlatStep.ToString(ci) + " HP/s Regen";
                case StatKind.PickupRadius: return "+" + ws.pickupRadiusFlatStep.ToString(ci) + " Pickup Radius";
                case StatKind.ProjectileSize: return "+" + ws.projectileSizeFlatStep.ToString(ci) + " Projectile Size";
                case StatKind.Pierce:       return "+" + ws.pierceFlatStep.ToString(ci) + " Pierce";
                case StatKind.XpGain:       return "+" + ws.xpBonusStep.ToString(ci) + " XP per Pickup";
                default: return "";
            }
        }

        // Percent: derive "+Y%" from the shared fractional step (0.1 -> 10).
        int pct = Mathf.RoundToInt(ws.levelUpPercentStep * 100f);
        switch (u.kind)
        {
            case StatKind.AttackDamage: return "+" + pct + "% Damage";
            case StatKind.MoveSpeed:    return "+" + pct + "% Move Speed";
            case StatKind.FireRate:     return "+" + pct + "% Fire Rate";
            case StatKind.Range:        return "+" + pct + "% Range";
            case StatKind.MaxHP:        return "+" + pct + "% Max HP";
            case StatKind.Defense:      return "+" + pct + "% Defense";
            case StatKind.Regen:        return "+" + pct + "% Regen";
            case StatKind.PickupRadius: return "+" + pct + "% Pickup Radius";
            case StatKind.ProjectileSize: return "+" + pct + "% Projectile Size";
            default: return "";
        }
    }

    private void Choose(Upgrade u)
    {
        if (worldState.instance == null) return;
        worldState ws = worldState.instance;

        if (u.mode == Mode.Flat)
        {
            switch (u.kind)
            {
                case StatKind.AttackDamage:
                    ws.attackDamageBase += ws.attackDamageFlatStep;
                    break;
                case StatKind.MoveSpeed:
                    ws.moveSpeedBase += ws.moveSpeedFlatStep;
                    break;
                case StatKind.FireRate:
                    ws.fireRateBase += ws.fireRateFlatStep;
                    break;
                case StatKind.Range:
                    ws.rangeBase += ws.rangeFlatStep;
                    break;
                case StatKind.MaxHP:
                {
                    int before = ws.MaxHP();
                    ws.maxHPBase += ws.maxHPFlatStep;
                    ws.currentHP += (ws.MaxHP() - before);
                    break;
                }
                case StatKind.Defense:
                    ws.defenseBase += ws.defenseFlatStep;
                    break;
                case StatKind.Regen:
                    ws.regenBase += ws.regenFlatStep;
                    break;
                case StatKind.PickupRadius:
                    ws.pickupRadiusBase += ws.pickupRadiusFlatStep;
                    break;
                case StatKind.ProjectileSize:
                    ws.projectileSizeBase += ws.projectileSizeFlatStep;
                    break;
                case StatKind.Pierce:
                    ws.pierceBase += ws.pierceFlatStep;
                    break;
                case StatKind.XpGain:
                    ws.xpBonusPerPickup = Mathf.Min(worldState.xpBonusCap, ws.xpBonusPerPickup + ws.xpBonusStep);
                    break;
            }
        }
        else // Percent
        {
            float p = 1f + ws.levelUpPercentStep;
            switch (u.kind)
            {
                case StatKind.AttackDamage:
                    ws.attackDamageMult *= p;
                    break;
                case StatKind.MoveSpeed:
                    ws.moveSpeedMult *= p;
                    break;
                case StatKind.FireRate:
                    ws.fireRateMult *= p;
                    break;
                case StatKind.Range:
                    ws.rangeMult *= p;
                    break;
                case StatKind.MaxHP:
                {
                    int before = ws.MaxHP();
                    ws.maxHPMult *= p;
                    ws.currentHP += (ws.MaxHP() - before);
                    break;
                }
                case StatKind.Defense:
                    ws.defenseMult *= p;
                    break;
                case StatKind.Regen:
                    ws.regenMult *= p;
                    break;
                case StatKind.PickupRadius:
                    ws.pickupRadiusMult *= p;
                    break;
                case StatKind.ProjectileSize:
                    ws.projectileSizeMult *= p;
                    break;
            }
        }

        if (levelUpManager.instance != null)
        {
            levelUpManager.instance.ApplyChoiceAndAdvance();
        }
    }
}
