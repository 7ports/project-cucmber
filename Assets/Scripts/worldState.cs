using UnityEngine;

public class worldState
{
    public static worldState instance;
    public Transform player;

    // --- CSV tuning overlay (Assets/StreamingAssets/tuning.csv) ---------------
    // worldState.instance is re-created each run (new worldState()). C# runs the
    // field initializers below FIRST (setting the in-code defaults), THEN this
    // constructor body — so the overlay always applies on top of the defaults.
    // Key == field name; a missing/malformed CSV key leaves the field default
    // (TryGet returns false). xpBonusCap is intentionally NOT overlaid (const-like
    // cap = 3; see tuning.csv note).
    public worldState()
    {
        ApplyTuningOverrides();
    }

    // One TryGet per tunable field. int-typed fields (pierceBase, fireStackCap,
    // xpBonusPerPickup, xpBonusStep, pierceFlatStep) use TryGetInt; every other
    // field uses TryGetFloat. Assignment happens only on a successful lookup.
    private void ApplyTuningOverrides()
    {
        // Core player stats
        if (tuningTable.TryGetFloat("attackDamageBase", out var v)) attackDamageBase = v;
        if (tuningTable.TryGetFloat("moveSpeedBase", out v)) moveSpeedBase = v;
        if (tuningTable.TryGetFloat("fireRateBase", out v)) fireRateBase = v;
        if (tuningTable.TryGetFloat("rangeBase", out v)) rangeBase = v;
        if (tuningTable.TryGetFloat("maxHPBase", out v)) maxHPBase = v;
        if (tuningTable.TryGetFloat("defenseBase", out v)) defenseBase = v;
        if (tuningTable.TryGetFloat("regenBase", out v)) regenBase = v;
        if (tuningTable.TryGetFloat("pickupRadiusBase", out v)) pickupRadiusBase = v;
        if (tuningTable.TryGetFloat("projectileSizeBase", out v)) projectileSizeBase = v;
        if (tuningTable.TryGetInt("pierceBase", out var i)) pierceBase = i;
        if (tuningTable.TryGetFloat("critChanceBase", out v)) critChanceBase = v;
        if (tuningTable.TryGetFloat("critDamageBase", out v)) critDamageBase = v;

        // Status effects (Fire DoT / Freeze)
        if (tuningTable.TryGetFloat("fireDpsFactor", out v)) fireDpsFactor = v;
        if (tuningTable.TryGetInt("fireStackCap", out i)) fireStackCap = i;
        if (tuningTable.TryGetFloat("fireTickInterval", out v)) fireTickInterval = v;
        if (tuningTable.TryGetFloat("fireBurnDuration", out v)) fireBurnDuration = v;
        if (tuningTable.TryGetFloat("freezeDefaultDuration", out v)) freezeDefaultDuration = v;

        // Item projectile/weapon mods (Cone / Bounce / Explode / Freeze)
        if (tuningTable.TryGetFloat("coneHalfAngleDeg", out v)) coneHalfAngleDeg = v;
        if (tuningTable.TryGetFloat("bounceSearchRadius", out v)) bounceSearchRadius = v;
        if (tuningTable.TryGetFloat("explosionRadiusFactor", out v)) explosionRadiusFactor = v;
        if (tuningTable.TryGetFloat("freezeChance", out v)) freezeChance = v;
        if (tuningTable.TryGetFloat("freezeItemDuration", out v)) freezeItemDuration = v;

        // Aura / Bot / Trail / Grenadier item stats
        if (tuningTable.TryGetFloat("auraDpsFactor", out v)) auraDpsFactor = v;
        if (tuningTable.TryGetFloat("auraRadiusBase", out v)) auraRadiusBase = v;
        if (tuningTable.TryGetFloat("auraTickInterval", out v)) auraTickInterval = v;
        if (tuningTable.TryGetFloat("robotDamageFactor", out v)) robotDamageFactor = v;
        if (tuningTable.TryGetFloat("robotSpeedFactor", out v)) robotSpeedFactor = v;
        if (tuningTable.TryGetFloat("robotHitInterval", out v)) robotHitInterval = v;
        if (tuningTable.TryGetFloat("trailDpsFactor", out v)) trailDpsFactor = v;
        if (tuningTable.TryGetFloat("trailSegmentLifetime", out v)) trailSegmentLifetime = v;
        if (tuningTable.TryGetFloat("trailEmitDistance", out v)) trailEmitDistance = v;
        if (tuningTable.TryGetFloat("trailTickInterval", out v)) trailTickInterval = v;
        if (tuningTable.TryGetFloat("grenadeInterval", out v)) grenadeInterval = v;
        if (tuningTable.TryGetFloat("grenadeDamageFactor", out v)) grenadeDamageFactor = v;

        // XP pickup bonus
        if (tuningTable.TryGetInt("xpBonusPerPickup", out i)) xpBonusPerPickup = i;
        if (tuningTable.TryGetInt("xpBonusStep", out i)) xpBonusStep = i;
        if (tuningTable.TryGetInt("xpBonusCap", out i)) xpBonusCap = i;

        // Level-up flat additive steps (per stat)
        if (tuningTable.TryGetFloat("attackDamageFlatStep", out v)) attackDamageFlatStep = v;
        if (tuningTable.TryGetFloat("moveSpeedFlatStep", out v)) moveSpeedFlatStep = v;
        if (tuningTable.TryGetFloat("fireRateFlatStep", out v)) fireRateFlatStep = v;
        if (tuningTable.TryGetFloat("rangeFlatStep", out v)) rangeFlatStep = v;
        if (tuningTable.TryGetFloat("maxHPFlatStep", out v)) maxHPFlatStep = v;
        if (tuningTable.TryGetFloat("defenseFlatStep", out v)) defenseFlatStep = v;
        if (tuningTable.TryGetFloat("regenFlatStep", out v)) regenFlatStep = v;
        if (tuningTable.TryGetFloat("pickupRadiusFlatStep", out v)) pickupRadiusFlatStep = v;
        if (tuningTable.TryGetFloat("projectileSizeFlatStep", out v)) projectileSizeFlatStep = v;
        if (tuningTable.TryGetInt("pierceFlatStep", out i)) pierceFlatStep = i;

        // Level-up shared percent step
        if (tuningTable.TryGetFloat("levelUpPercentStep", out v)) levelUpPercentStep = v;
        if (tuningTable.TryGetFloat("lvlUpCostGrowth", out v)) lvlUpCostGrowth = v;

        // Enemy spawning
        if (tuningTable.TryGetFloat("baseSpawnInterval", out v)) baseSpawnInterval = v;
        if (tuningTable.TryGetFloat("spawnIntervalCoefficient", out v)) spawnIntervalCoefficient = v;
        if (tuningTable.TryGetFloat("minSpawnInterval", out v)) minSpawnInterval = v;
        if (tuningTable.TryGetFloat("spawnIntervalTimeCoefficient", out v)) spawnIntervalTimeCoefficient = v;
        if (tuningTable.TryGetFloat("spawnIntervalTimeFloor", out v)) spawnIntervalTimeFloor = v;

        // Time-based boss scaling
        if (tuningTable.TryGetFloat("bossFireRateTimeCoefficient", out v)) bossFireRateTimeCoefficient = v;
        if (tuningTable.TryGetFloat("bossBulletSpeedTimeCoefficient", out v)) bossBulletSpeedTimeCoefficient = v;
        if (tuningTable.TryGetFloat("bossVolleyBonusPerMinute", out v)) bossVolleyBonusPerMinute = v;
        if (tuningTable.TryGetFloat("bossStatTimeCoefficient", out v)) bossStatTimeCoefficient = v;

        // Time-based type progression
        if (tuningTable.TryGetFloat("shooterStartTime", out v)) shooterStartTime = v;
        if (tuningTable.TryGetFloat("unlockRampSeconds", out v)) unlockRampSeconds = v;

        // Repeating boss cadence
        if (tuningTable.TryGetFloat("bossFirstTime", out v)) bossFirstTime = v;
        if (tuningTable.TryGetFloat("bossInterval", out v)) bossInterval = v;

        // Enemy HP scaling over time
        if (tuningTable.TryGetFloat("hpScaleInterval", out v)) hpScaleInterval = v;
        if (tuningTable.TryGetFloat("hpScalePerTier", out v)) hpScalePerTier = v;

        // Time-based XP doubling
        if (tuningTable.TryGetFloat("xpDoubleThreshold", out v)) xpDoubleThreshold = v;
        if (tuningTable.TryGetFloat("xpDoubleFactor", out v)) xpDoubleFactor = v;
    }

    // Base + multiplier stat system. Effective value = base * mult (via getters).
    public float attackDamageBase = 100f;
    public float attackDamageMult = 1f;
    public float moveSpeedBase = 1f;
    public float moveSpeedMult = 1f;
    public float fireRateBase = 1.2f;   // = 1 / (attackSpeed 1 * baseAttackSpeed 0.8)
    public float fireRateMult = 1f;
    public float rangeBase = 3f;
    public float rangeMult = 1f;
    public float maxHPBase = 1000f;
    public float maxHPMult = 1f;
    public float defenseBase = 0f;
    public float defenseMult = 1f;
    public float regenBase = 1f;
    public float regenMult = 1f;
    public float pickupRadiusBase = 1.5f;   // = current playerPickupRadius default -> behavior unchanged
    public float pickupRadiusMult = 1f;

    // Projectile visual+collider scale multiplier. Effective = base * mult, applied to
    // the player bullet's localScale at spawn (OnEnable). Default 1.0 -> size unchanged.
    public float projectileSizeBase = 1f;
    public float projectileSizeMult = 1f;

    public int pierceBase = 2;   // enemies a bullet passes through before despawning; 1 = "pierce through 1 enemy" default

    // --- Critical hit stats (base+mult). critChanceBase 0 -> no crits until an item/upgrade raises it (behavior-neutral). ---
    public float critChanceBase = 0.1f;
    public float critChanceMult = 1f;
    public float critDamageBase = 2.0f;   // crit deals 2x by default
    public float critDamageMult = 1f;

    // --- Enemy status effects (Fire DoT / Freeze). New (post-x10) damage scale. ---
    public float fireDpsFactor = 0.05f;   // fire DPS per stack = attackDamageBase * this
    public float   fireDpsPerStack() => attackDamageBase * fireDpsFactor;    // damage per second PER burning stack
    public int   fireStackCap      = 5;     // max simultaneous burning stacks
    public float fireTickInterval  = 0.1f;    // seconds between burn ticks (1s -> dmg/tick == dps)
    public float fireBurnDuration  = 3f;    // seconds a burn lasts; refreshed by each ApplyFire()
    public float freezeDefaultDuration = 2f;// fallback freeze seconds when hit site passes <= 0

    // --- Item projectile/weapon mods (Cone / Bounce / Explode / Freeze) ---
    // Single-home tunables read by playerProjectileShooter + projectileBehaviour at the hit/spawn site.
    public float coneHalfAngleDeg     = 15f;   // Cone: ± spread of the 3-shot cone
    public float bounceSearchRadius   = 6f;    // Bounce: OverlapCircle radius to find next target
    public float explosionRadiusFactor = 0.5f;   // Explode: AoE radius = Range() * this
    public float freezeChance         = 0.2f;  // Freeze: per-hit probability to freeze
    public float freezeItemDuration   = 2f;    // Freeze: seconds passed to ApplyFreeze

    // --- Phase 2 item weapon stats (base+mult, mirroring existing style). Registered but inert until their components exist. ---
    // Damage Aura: constant DPS in a radius around the player.
    public float auraDpsFactor = 0.1f;   // aura DPS = attackDamageBase * this
    public float auraDpsBase()        => attackDamageBase * auraDpsFactor;
    public float auraDpsMult         = 1f;
    public float auraRadiusBase     = 1.5f;
    public float auraRadiusMult      = 1f;
    public float auraTickInterval    = 0.1f;  // seconds between aura damage ticks
    // Attack Bot: a bot dealing a fraction of attack damage on an interval.
    public float robotDamageFactor   = 0.5f;   // bot damage = AttackDamage() * this
    public float robotSpeedFactor    = 1f;     // bot move/orbit speed multiplier
    public float robotHitInterval    = 0.5f;   // seconds between bot hits
    // Searing Trail: damaging trail segments left behind the player.
    public float trailDpsFactor = 0.05f;   // trail DPS = attackDamageBase * this
    public float trailDpsBase()        => attackDamageBase * trailDpsFactor;
    public float trailDpsMult         = 1f;
    public float trailSegmentLifetime = 2f;    // seconds a trail segment persists
    public float trailEmitDistance    = 0.5f;  // player travel distance between emitted segments
    public float trailTickInterval    = 0.1f; // seconds between trail damage ticks
    // Grenadier: periodically lobs a grenade that explodes for AoE damage.
    public float grenadeInterval     = 2f;     // seconds between grenade throws
    public float grenadeDamageFactor = 2f;   // grenade damage = attackDamageBase * this
    public float grenadeDamageBase()   => attackDamageBase * grenadeDamageFactor;
    public float grenadeDamageMult    = 1f;
    public float grenadeRadiusBase   => rangeBase;
    public float grenadeRadiusMult    = 1f;

    // Flat bonus XP added to EVERY pickup's xpValue at collection time.
    // Base 0 (behavior unchanged); each XP-Gain upgrade adds +xpBonusStep; hard cap 3.
    public int xpBonusPerPickup = 0;

    // Level-up increment magnitudes. Single source of truth for BOTH the
    // "+X" / "+Y%" labels and the actual mutations in levelUpMenuController.
    // Defaults equal the previously hardcoded values -> behavior unchanged.

    // Flat additive steps (per stat).
    public float attackDamageFlatStep = 50f;
    public float moveSpeedFlatStep    = 0.2f;
    public float fireRateFlatStep     = 0.2f;
    public float rangeFlatStep        = 0.5f;
    public float maxHPFlatStep        = 200f;
    public float defenseFlatStep      = 5f;
    public float regenFlatStep        = 2f;
    public float pickupRadiusFlatStep = 1f;
    public float projectileSizeFlatStep = 0.2f;   // +0.2 size per Flat upgrade
    public int   pierceFlatStep       = 1;   // flat-only Pierce upgrade step
    public int   xpBonusStep          = 1;   // flat-only XP-Gain upgrade step
    public int xpBonusCap              = 3;  // max total bonus (base 1 pickup -> 4 XP)

    // Percent step, shared across all stats. 0.1 = +10% (mult factor = 1 + step).
    public float levelUpPercentStep = 0.2f;

    public float AttackDamage() => attackDamageBase * attackDamageMult;
    public float MoveSpeed() => moveSpeedBase * moveSpeedMult;
    public float FireRate() => fireRateBase * fireRateMult;
    public float FireCooldown() => 1f / FireRate();
    public float Range() => rangeBase * rangeMult;
    public int MaxHP() => Mathf.RoundToInt(maxHPBase * maxHPMult);
    public float Defense() => defenseBase * defenseMult;
    public float Regen() => regenBase * regenMult;
    public float PickupRadius() => pickupRadiusBase * pickupRadiusMult;
    public float ProjectileSize() => projectileSizeBase * projectileSizeMult;
    public int Pierce() => pierceBase;
    public int XpBonus() => xpBonusPerPickup;

    // --- Critical hit getters + shared damage roll ---
    public float CritChance() => Mathf.Clamp01(critChanceBase * critChanceMult);
    public float CritMultiplier() => critDamageBase * critDamageMult;

    // --- Phase 2 item weapon getters (effective = base * mult) ---
    public float AuraDps() => auraDpsBase() * auraDpsMult;
    public float AuraRadius() => auraRadiusBase * auraRadiusMult;
    public float TrailDps() => trailDpsBase() * trailDpsMult;
    public float GrenadeDamage() => grenadeDamageBase() * grenadeDamageMult;
    public float GrenadeRadius() => grenadeRadiusBase * grenadeRadiusMult;

    // Single shared damage roll: rounds to int, applies a crit roll (crit base 0 -> never crits).
    public int RollDamage(float baseDamage, out bool isCrit)
    {
        isCrit = Random.value < CritChance();
        float d = isCrit ? baseDamage * CritMultiplier() : baseDamage;
        return Mathf.RoundToInt(d);
    }

    public int lvlUpXP = 4, currentXP = 0;
    public int level = 1;
    public float lvlUpCostGrowth = 1.3f;   // each level costs this * previous XP requirement

    public int currentHP = 1000;

    // Run-scoped pity flag for the slot-machine level-up menu. worldState.instance is
    // re-created each play session, so this auto-resets to false at the start of every run.
    public bool slotPityPending = false;

    public float baseSpawnInterval = 1.5f;
    public float spawnIntervalCoefficient = 0.3f;
    public float minSpawnInterval = 0.3f;
    public float currentSpawnInterval = 1.5f;

    // --- Time-based SPAWN VOLUME ramp (seconds of elapsed run time) ---
    // Shrinks the consumed spawn interval as the run goes on so enemy volume
    // ramps with TIME (not level), harder than the level-based currentSpawnInterval curve.
    public float spawnIntervalTimeCoefficient = 0.1f;   // -5% of base interval per elapsed minute
    public float spawnIntervalTimeFloor       = 0.1f;    // never shrink below 10% of the base interval

    // --- Time-based BOSS scaling coefficients (per elapsed minute) ---
    public float bossFireRateTimeCoefficient    = 0.15f; // +15% fire rate per minute (bosses fire faster)
    public float bossBulletSpeedTimeCoefficient = 0.10f; // +10% bullet speed per minute
    public float bossVolleyBonusPerMinute       = 0.5f;  // +1 extra bullet every 2 minutes (floored)
    public float bossStatTimeCoefficient        = 0.10f; // +10% boss HP & damage per minute

    // --- Time-based TYPE progression (seconds of elapsed run time) ---
    public float shooterStartTime  = 120f;   // reference: shooters begin at 2:00 (set on the shooter SpawnEntry)
    public float unlockRampSeconds = 30f;    // newly-unlocked type ramps 0 -> full weight over this window

    // --- Repeating boss cadence (seconds of elapsed run time) ---
    public float bossFirstTime = 200f;   // first boss at 5:00
    public float bossInterval  = 200f;   // then every 5:00

    // --- Time-based ENEMY HP scaling (seconds of elapsed run time) ---
    // Every hpScaleInterval seconds, NEWLY-spawned enemies (and bosses) get
    // +hpScalePerTier of their BASE hp. ADDITIVE: mult = 1 + perTier * tier.
    public float hpScaleInterval = 300f;   // 7 minutes per tier
    public float hpScalePerTier  = 1f;   // +50% of base per tier

    // Multiplier for HP applied AT SPAWN, from elapsed run time.
    // Uses Time.timeSinceLevelLoad — the run-time source already adopted by
    // enemySpawner/bossSpawner — so all time-based systems agree.
    public float EnemyHpTimeMultiplier()
    {
        if (hpScaleInterval <= 0f) return 1f;   // guard: no divide-by-zero / disable
        int tier = Mathf.FloorToInt(Time.timeSinceLevelLoad / hpScaleInterval);
        if (tier < 0) tier = 0;
        return 1f + hpScalePerTier * tier;
        // COMPOUNDING alternative (retune): return Mathf.Pow(1f + hpScalePerTier, tier);
    }

    // --- Time-based XP doubling (seconds of elapsed run time) ---
    // After xpDoubleThreshold seconds, ALL earned XP is doubled (single ×2, permanent).
    // Separate field from hpScaleInterval so it can be retuned independently, default
    // equal (420 = 7 min) so it fires with the same milestone as EnemyHpTimeMultiplier().
    public float xpDoubleThreshold = 300f;   // 7 minutes
    public float xpDoubleFactor     = 2f;    // the "double"

    // Multiplier applied to earned XP at the grant site. Uses Time.timeSinceLevelLoad,
    // the same run-time source as the HP/spawn/boss systems, so all time-gates agree.
    public float XpTimeMultiplier()
    {
        if (xpDoubleThreshold <= 0f) return 1f;                 // guard/disable
        return (Time.timeSinceLevelLoad >= xpDoubleThreshold) ? xpDoubleFactor : 1f;
    }

    // --- Time-based BOSS scaling getters (pull-based; Time.timeSinceLevelLoad -> minutes) ---
    // Bosses fire faster, throw faster & more bullets, and hit harder the longer the run lasts.
    // Mirror EnemyHpTimeMultiplier(): additive ramp, guarded, always >= 1.
    public float BossFireRateTimeMultiplier()
    {
        float minutes = Mathf.Max(0f, Time.timeSinceLevelLoad / 60f);
        return 1f + bossFireRateTimeCoefficient * minutes;
    }

    public float BossBulletSpeedTimeMultiplier()
    {
        float minutes = Mathf.Max(0f, Time.timeSinceLevelLoad / 60f);
        return 1f + bossBulletSpeedTimeCoefficient * minutes;
    }

    public int BossVolleyBonus()
    {
        float minutes = Mathf.Max(0f, Time.timeSinceLevelLoad / 60f);
        return Mathf.FloorToInt(bossVolleyBonusPerMinute * minutes);
    }

    public float BossStatTimeMultiplier()
    {
        float minutes = Mathf.Max(0f, Time.timeSinceLevelLoad / 60f);
        return 1f + bossStatTimeCoefficient * minutes;
    }

    // Multiplier applied to the consumed spawn interval so enemy VOLUME ramps with
    // elapsed time. Shrinks over time (more enemies), floored so spawns stay sane.
    public float SpawnIntervalTimeMultiplier()
    {
        float minutes = Mathf.Max(0f, Time.timeSinceLevelLoad / 60f);
        float mult = 1f - spawnIntervalTimeCoefficient * minutes;
        return Mathf.Max(spawnIntervalTimeFloor, mult);
    }

    public static event System.Action OnLevelUp;

    public void addXP(int amount)
    {
        currentXP += amount;
        while (currentXP >= lvlUpXP)
        {
            currentXP -= lvlUpXP;
            level++;
            currentSpawnInterval = Mathf.Max(minSpawnInterval, currentSpawnInterval - spawnIntervalCoefficient * (1f / level));
            lvlUpXP = Mathf.RoundToInt(lvlUpXP * lvlUpCostGrowth);   // each level costs more
            if (OnLevelUp != null) OnLevelUp();
        }
    }
}
