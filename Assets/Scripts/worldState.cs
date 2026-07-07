using System.Xml.XPath;
using UnityEngine;

public class worldState
{
    public static worldState instance;
    public Transform player;
    public float attackSpeed = 1f;
    public float attackDamage = 10;
    public float moveSpeed = 1;
    public float baseAttackSpeed = 0.8f;
    public float range = 4f;

    public int lvlUpXP = 16, currentXP = 0;
    public int level = 1;

    public int maxHP = 100, currentHP = 100;

    public float baseSpawnInterval = 2.5f;
    public float spawnIntervalCoefficient = 0.3f;
    public float minSpawnInterval = 0.6f;
    public float currentSpawnInterval = 2f;

    public static event System.Action OnLevelUp;

    public void addXP(int amount)
    {
        currentXP += amount;
        while (currentXP >= lvlUpXP)
        {
            currentXP -= lvlUpXP;
            level++;
            currentSpawnInterval = Mathf.Max(minSpawnInterval, currentSpawnInterval - spawnIntervalCoefficient * (1f / level));
            lvlUpXP = Mathf.RoundToInt(lvlUpXP * 1.5f);   // each level costs more
            if (OnLevelUp != null) OnLevelUp();
        }
    }
}
