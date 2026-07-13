using System.Collections.Generic;
using UnityEngine;

public enum StatKind { MaxHP, FireRate, AttackDamage, MoveSpeed, Range, Defense, Regen, PickupRadius, Pierce, XpGain, ProjectileSize, CritChance, CritDamage }

public enum Mode { Flat, Percent }

public struct Upgrade
{
    public StatKind kind;
    public Mode mode;
}

// Shared upgrade generation / labels / apply logic. Used by both the normal
// level-up menu and the slot-machine menu so they stay in lockstep.
public static class upgradePool
{
    // Crit level-up step magnitudes (flat). Percent reuses worldState.levelUpPercentStep.
    private const float critChanceFlatStep = 0.05f;   // +5% crit chance per flat pick
    private const float critDamageFlatStep = 0.25f;   // +0.25x crit damage per flat pick

    public static List<Upgrade> BuildPool()
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
            StatKind.ProjectileSize,
            StatKind.CritChance,
            StatKind.CritDamage
        };

        bool defenseHasBase = worldState.instance != null && worldState.instance.defenseBase > 0f;
        bool regenHasBase = worldState.instance != null && worldState.instance.regenBase > 0f;
        bool xpGainAtCap = worldState.instance != null && worldState.instance.xpBonusPerPickup >= worldState.xpBonusCap;
        bool critChanceHasBase = worldState.instance != null && worldState.instance.critChanceBase > 0f;
        bool critChanceAtCap = worldState.instance != null && worldState.instance.CritChance() >= 1f;

        List<Upgrade> pool = new List<Upgrade>();
        foreach (StatKind k in stats)
        {
            // XP-Gain is capped: once the bonus hits the cap, stop offering it entirely.
            if (k == StatKind.XpGain && xpGainAtCap) continue;

            // Crit Chance soft cap: once effective chance hits 100%, stop offering it entirely.
            if (k == StatKind.CritChance && critChanceAtCap) continue;

            // Flat is always offered.
            pool.Add(new Upgrade { kind = k, mode = Mode.Flat });

            // Pierce and XpGain are flat-only stats — never offer them as a percent.
            if (k == StatKind.Pierce) continue;
            if (k == StatKind.XpGain) continue;

            // Percent is inert on a 0 base for Defense/Regen — only offer once seeded.
            if (k == StatKind.Defense && !defenseHasBase) continue;
            if (k == StatKind.Regen && !regenHasBase) continue;
            if (k == StatKind.CritChance && !critChanceHasBase) continue;

            pool.Add(new Upgrade { kind = k, mode = Mode.Percent });
        }

        return pool;
    }

    public static string LabelFor(Upgrade u)
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
                case StatKind.CritChance:   return "+" + Mathf.RoundToInt(critChanceFlatStep * 100f) + "% Crit Chance";
                case StatKind.CritDamage:   return "+" + critDamageFlatStep.ToString(ci) + "x Crit Damage";
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
            case StatKind.CritChance:   return "+" + pct + "% Crit Chance";
            case StatKind.CritDamage:   return "+" + pct + "% Crit Damage";
            default: return "";
        }
    }

    public static void ApplyUpgrade(Upgrade u)
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
                case StatKind.CritChance:
                    ws.critChanceBase += critChanceFlatStep;
                    break;
                case StatKind.CritDamage:
                    ws.critDamageBase += critDamageFlatStep;
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
                case StatKind.CritChance:
                    ws.critChanceMult *= p;
                    break;
                case StatKind.CritDamage:
                    ws.critDamageMult *= p;
                    break;
            }
        }
    }
}
