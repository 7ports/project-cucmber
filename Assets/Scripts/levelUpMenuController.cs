using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class levelUpMenuController : MonoBehaviour
{
    private enum StatKind { MaxHP, FireRate, AttackDamage, MoveSpeed, Range, Defense, Regen, PickupRadius }
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
            StatKind.PickupRadius
        };

        bool defenseHasBase = worldState.instance != null && worldState.instance.defenseBase > 0f;
        bool regenHasBase = worldState.instance != null && worldState.instance.regenBase > 0f;

        List<Upgrade> pool = new List<Upgrade>();
        foreach (StatKind k in stats)
        {
            // Flat is always offered.
            pool.Add(new Upgrade { kind = k, mode = Mode.Flat });

            // Percent is inert on a 0 base for Defense/Regen — only offer once seeded.
            if (k == StatKind.Defense && !defenseHasBase) continue;
            if (k == StatKind.Regen && !regenHasBase) continue;

            pool.Add(new Upgrade { kind = k, mode = Mode.Percent });
        }

        return pool;
    }

    private string LabelFor(Upgrade u)
    {
        if (u.mode == Mode.Flat)
        {
            switch (u.kind)
            {
                case StatKind.AttackDamage: return "+2 Damage";
                case StatKind.MoveSpeed: return "+0.2 Move Speed";
                case StatKind.FireRate: return "+0.25 Fire Rate";
                case StatKind.Range: return "+0.5 Range";
                case StatKind.MaxHP: return "+15 Max HP";
                case StatKind.Defense: return "+2 Defense";
                case StatKind.Regen: return "+0.1 HP/s Regen";
                case StatKind.PickupRadius: return "+0.5 Pickup Radius";
                default: return "";
            }
        }

        switch (u.kind)
        {
            case StatKind.AttackDamage: return "+10% Damage";
            case StatKind.MoveSpeed: return "+10% Move Speed";
            case StatKind.FireRate: return "+10% Fire Rate";
            case StatKind.Range: return "+10% Range";
            case StatKind.MaxHP: return "+10% Max HP";
            case StatKind.Defense: return "+10% Defense";
            case StatKind.Regen: return "+10% Regen";
            case StatKind.PickupRadius: return "+10% Pickup Radius";
            default: return "";
        }
    }

    private void Choose(Upgrade u)
    {
        if (worldState.instance == null) return;

        if (u.mode == Mode.Flat)
        {
            switch (u.kind)
            {
                case StatKind.AttackDamage:
                    worldState.instance.attackDamageBase += 2f;
                    break;
                case StatKind.MoveSpeed:
                    worldState.instance.moveSpeedBase += 0.2f;
                    break;
                case StatKind.FireRate:
                    worldState.instance.fireRateBase += 0.25f;
                    break;
                case StatKind.Range:
                    worldState.instance.rangeBase += 0.5f;
                    break;
                case StatKind.MaxHP:
                {
                    int before = worldState.instance.MaxHP();
                    worldState.instance.maxHPBase += 15f;
                    worldState.instance.currentHP += (worldState.instance.MaxHP() - before);
                    break;
                }
                case StatKind.Defense:
                    worldState.instance.defenseBase += 2f;
                    break;
                case StatKind.Regen:
                    worldState.instance.regenBase += 0.1f;
                    break;
                case StatKind.PickupRadius:
                    worldState.instance.pickupRadiusBase += 0.5f;
                    break;
            }
        }
        else // Percent
        {
            switch (u.kind)
            {
                case StatKind.AttackDamage:
                    worldState.instance.attackDamageMult *= 1.1f;
                    break;
                case StatKind.MoveSpeed:
                    worldState.instance.moveSpeedMult *= 1.1f;
                    break;
                case StatKind.FireRate:
                    worldState.instance.fireRateMult *= 1.1f;
                    break;
                case StatKind.Range:
                    worldState.instance.rangeMult *= 1.1f;
                    break;
                case StatKind.MaxHP:
                {
                    int before = worldState.instance.MaxHP();
                    worldState.instance.maxHPMult *= 1.1f;
                    worldState.instance.currentHP += (worldState.instance.MaxHP() - before);
                    break;
                }
                case StatKind.Defense:
                    worldState.instance.defenseMult *= 1.1f;
                    break;
                case StatKind.Regen:
                    worldState.instance.regenMult *= 1.1f;
                    break;
                case StatKind.PickupRadius:
                    worldState.instance.pickupRadiusMult *= 1.1f;
                    break;
            }
        }

        if (levelUpManager.instance != null)
        {
            levelUpManager.instance.ApplyChoiceAndAdvance();
        }
    }
}
