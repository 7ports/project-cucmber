using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class levelUpMenuController : MonoBehaviour
{
    private enum StatKind { MaxHP, FireRate, AttackDamage, MoveSpeed, Range }

    [SerializeField] private Button[] buttons; // 3
    [SerializeField] private Text[] labels;    // 3, one per button

    private readonly StatKind[] rolled = new StatKind[3];

    private void OnEnable()
    {
        if (buttons == null || buttons.Length < 3) return;
        if (labels == null || labels.Length < 3) return;

        List<StatKind> pool = new List<StatKind>
        {
            StatKind.MaxHP,
            StatKind.FireRate,
            StatKind.AttackDamage,
            StatKind.MoveSpeed,
            StatKind.Range
        };

        // Fisher-Yates shuffle
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            StatKind tmp = pool[i];
            pool[i] = pool[j];
            pool[j] = tmp;
        }

        for (int i = 0; i < 3; i++)
        {
            rolled[i] = pool[i];
            labels[i].text = LabelFor(rolled[i]);

            int idx = i;
            buttons[i].onClick.RemoveAllListeners();
            buttons[i].onClick.AddListener(() => Choose(rolled[idx]));
        }
    }

    private string LabelFor(StatKind k)
    {
        switch (k)
        {
            case StatKind.MaxHP: return "+10% Max HP";
            case StatKind.FireRate: return "+10% Fire Rate";
            case StatKind.AttackDamage: return "+10% Damage";
            case StatKind.MoveSpeed: return "+10% Move Speed";
            case StatKind.Range: return "+10% Range";
            default: return "";
        }
    }

    private void Choose(StatKind k)
    {
        if (worldState.instance == null) return;

        switch (k)
        {
            case StatKind.MaxHP:
            {
                int old = worldState.instance.maxHP;
                worldState.instance.maxHP = Mathf.RoundToInt(worldState.instance.maxHP * 1.1f);
                worldState.instance.currentHP += (worldState.instance.maxHP - old);
                break;
            }
            case StatKind.FireRate:
                worldState.instance.attackSpeed = Mathf.Max(0.2f, worldState.instance.attackSpeed * 0.9f);
                break;
            case StatKind.AttackDamage:
                worldState.instance.attackDamage *= 1.1f;
                break;
            case StatKind.MoveSpeed:
                worldState.instance.moveSpeed *= 1.1f;
                break;
            case StatKind.Range:
                worldState.instance.range *= 1.1f;
                break;
        }

        if (levelUpManager.instance != null)
        {
            levelUpManager.instance.ApplyChoiceAndAdvance();
        }
    }
}
