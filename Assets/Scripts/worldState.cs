using UnityEngine;

public class worldState
{
    public static worldState instance;
    public Transform player;

    // Base + multiplier stat system. Effective value = base * mult (via getters).
    public float attackDamageBase = 10f;
    public float attackDamageMult = 1f;
    public float moveSpeedBase = 1f;
    public float moveSpeedMult = 1f;
    public float fireRateBase = 1.25f;   // = 1 / (attackSpeed 1 * baseAttackSpeed 0.8)
    public float fireRateMult = 1f;
    public float rangeBase = 4f;
    public float rangeMult = 1f;
    public float maxHPBase = 100f;
    public float maxHPMult = 1f;
    public float defenseBase = 0f;
    public float defenseMult = 1f;
    public float regenBase = 0f;
    public float regenMult = 1f;
    public float pickupRadiusBase = 2f;   // = current playerPickupRadius default -> behavior unchanged
    public float pickupRadiusMult = 1f;

    public float AttackDamage() => attackDamageBase * attackDamageMult;
    public float MoveSpeed() => moveSpeedBase * moveSpeedMult;
    public float FireRate() => fireRateBase * fireRateMult;
    public float FireCooldown() => 1f / FireRate();
    public float Range() => rangeBase * rangeMult;
    public int MaxHP() => Mathf.RoundToInt(maxHPBase * maxHPMult);
    public float Defense() => defenseBase * defenseMult;
    public float Regen() => regenBase * regenMult;
    public float PickupRadius() => pickupRadiusBase * pickupRadiusMult;

    public int lvlUpXP = 4, currentXP = 0;
    public int level = 1;

    public int currentHP = 100;

    public float baseSpawnInterval = 1.75f;
    public float spawnIntervalCoefficient = 0.3f;
    public float minSpawnInterval = 0.6f;
    public float currentSpawnInterval = 1.75f;

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
