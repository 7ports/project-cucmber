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
    public float rangeBase = 2.5f;
    public float rangeMult = 1f;
    public float maxHPBase = 100f;
    public float maxHPMult = 1f;
    public float defenseBase = 0f;
    public float defenseMult = 1f;
    public float regenBase = 0.1f;
    public float regenMult = 1f;
    public float pickupRadiusBase = 1.5f;   // = current playerPickupRadius default -> behavior unchanged
    public float pickupRadiusMult = 1f;

    public int pierceBase = 1;   // enemies a bullet passes through before despawning; 1 = "pierce through 1 enemy" default

    // Flat bonus XP added to EVERY pickup's xpValue at collection time.
    // Base 0 (behavior unchanged); each XP-Gain upgrade adds +xpBonusStep; hard cap 3.
    public int xpBonusPerPickup = 0;

    // Level-up increment magnitudes. Single source of truth for BOTH the
    // "+X" / "+Y%" labels and the actual mutations in levelUpMenuController.
    // Defaults equal the previously hardcoded values -> behavior unchanged.

    // Flat additive steps (per stat).
    public float attackDamageFlatStep = 5f;
    public float moveSpeedFlatStep    = 0.2f;
    public float fireRateFlatStep     = 0.2f;
    public float rangeFlatStep        = 0.5f;
    public float maxHPFlatStep        = 50f;
    public float defenseFlatStep      = 5f;
    public float regenFlatStep        = 0.2f;
    public float pickupRadiusFlatStep = 0.5f;
    public int   pierceFlatStep       = 1;   // flat-only Pierce upgrade step
    public int   xpBonusStep          = 1;   // flat-only XP-Gain upgrade step
    public const int xpBonusCap        = 3;  // max total bonus (base 1 pickup -> 4 XP)

    // Percent step, shared across all stats. 0.1 = +10% (mult factor = 1 + step).
    public float levelUpPercentStep = 0.1f;

    public float AttackDamage() => attackDamageBase * attackDamageMult;
    public float MoveSpeed() => moveSpeedBase * moveSpeedMult;
    public float FireRate() => fireRateBase * fireRateMult;
    public float FireCooldown() => 1f / FireRate();
    public float Range() => rangeBase * rangeMult;
    public int MaxHP() => Mathf.RoundToInt(maxHPBase * maxHPMult);
    public float Defense() => defenseBase * defenseMult;
    public float Regen() => regenBase * regenMult;
    public float PickupRadius() => pickupRadiusBase * pickupRadiusMult;
    public int Pierce() => pierceBase;
    public int XpBonus() => xpBonusPerPickup;

    public int lvlUpXP = 4, currentXP = 0;
    public int level = 1;

    public int currentHP = 100;

    public float baseSpawnInterval = 1.75f;
    public float spawnIntervalCoefficient = 0.3f;
    public float minSpawnInterval = 0.6f;
    public float currentSpawnInterval = 1.75f;

    // --- Time-based TYPE progression (seconds of elapsed run time) ---
    public float shooterStartTime  = 120f;   // reference: shooters begin at 2:00 (set on the shooter SpawnEntry)
    public float unlockRampSeconds = 30f;    // newly-unlocked type ramps 0 -> full weight over this window

    // --- Repeating boss cadence (seconds of elapsed run time) ---
    public float bossFirstTime = 300f;   // first boss at 5:00
    public float bossInterval  = 300f;   // then every 5:00

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
