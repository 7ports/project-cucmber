using System.Xml.XPath;
using UnityEngine;

public class worldState
{
    public static worldState instance;
    public Transform player;
    public float attackSpeed = 1f;
    public float attackDamage = 1;
    public float moveSpeed = 1;
    public float baseAttackSpeed = 1.2f;

    public int lvlUpXP = 16, currentXP = 0;
    public int level = 1;

    public int maxHP = 100, currentHP = 100;

    public void addXP(int amount)
    {
        currentXP += amount;
        while (currentXP >= lvlUpXP)
        {
            currentXP -= lvlUpXP;
            level++;
            lvlUpXP = Mathf.RoundToInt(lvlUpXP * 1.5f);   // each level costs more
        }
    }
}
