using UnityEngine;

public class worldState
{
    public static worldState instance;
    public Transform player;

    // --- CSV tuning overlay (Assets/StreamingAssets/tuning.csv) ---------------
    // The CSV is the SINGLE SOURCE OF TRUTH for every tuning value below. Those
    // fields are declared WITHOUT in-code literal defaults; worldState.instance is
    // re-created each run (new worldState()) and ApplyTuningOverrides() populates
    // them from the CSV. A field whose CSV key is missing/malformed falls back to
    // the C# type default (0), so keep tuning.csv complete.
    //
    // Item/ability VALUES are intentionally NOT stored or CSV-overlaid here — they
    // are computed getters derived from the player's stats (see "ITEM / ABILITY
    // VALUES" below), so abilities scale with the player instead of carrying their
    // own constants. (bossHpAccelPerBoss has no CSV key yet, so it keeps a literal.)
    public worldState()
    {
        ApplyTuningOverrides();
    }

    // One assignment per CSV-tunable field. int-typed fields use TryGetInt; every
    // other field uses TryGetFloat. Assignment happens only on a successful lookup.
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

        // Derived-weapon factors (scale attack damage for each weapon type)
        if (tuningTable.TryGetFloat("fireDpsFactor", out v)) fireDpsFactor = v;
        if (tuningTable.TryGetFloat("auraDpsFactor", out v)) auraDpsFactor = v;
        if (tuningTable.TryGetFloat("trailDpsFactor", out v)) trailDpsFactor = v;
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
        if (tuningTable.TryGetFloat("bossHpAccelPerBoss", out v)) bossHpAccelPerBoss = v;

        // Time-based XP doubling
        if (tuningTable.TryGetFloat("xpDoubleThreshold", out v)) xpDoubleThreshold = v;
        if (tuningTable.TryGetFloat("xpDoubleFactor", out v)) xpDoubleFactor = v;
    }

    // ============================================================================
    // CORE PLAYER STATS (base + mult). Effective value = base * mult (via getters).
    // Base values are the SINGLE SOURCE OF TRUTH in tuning.csv — declared here with
    // NO literal default so the CSV cannot be masked by a stale in-code number.
    // The runtime *Mult fields are NOT CSV-sourced (they start at 1 and move via
    // level-up upgrades), so they keep their literal.
    // ============================================================================
    public float attackDamageBase;          // CSV: attackDamageBase
    public float attackDamageMult = 1f;
    public float moveSpeedBase;              // CSV: moveSpeedBase
    public float moveSpeedMult = 1f;
    public float fireRateBase;               // CSV: fireRateBase
    public float fireRateMult = 1f;
    public float rangeBase;                  // CSV: rangeBase
    public float rangeMult = 1f;
    public float maxHPBase;                  // CSV: maxHPBase
    public float maxHPMult = 1f;
    public float defenseBase;                // CSV: defenseBase
    public float defenseMult = 1f;
    public float regenBase;                  // CSV: regenBase
    public float regenMult = 1f;
    public float pickupRadiusBase;           // CSV: pickupRadiusBase
    public float pickupRadiusMult = 1f;

    // Projectile visual+collider scale multiplier. Effective = base * mult, applied to
    // the player bullet's localScale at spawn (OnEnable).
    public float projectileSizeBase;         // CSV: projectileSizeBase
    public float projectileSizeMult = 1f;

    public int pierceBase;                   // CSV: pierceBase (enemies a bullet passes through)

    // --- Critical hit stats (base+mult). Base values CSV-sourced. ---
    public float critChanceBase;             // CSV: critChanceBase
    public float critChanceMult = 1f;
    public float critDamageBase;             // CSV: critDamageBase
    public float critDamageMult = 1f;

    // ============================================================================
    // ITEM / ABILITY VALUES — computed from player stats (NOT CSV, NOT constants).
    // Each expression is a PLACEHOLDER the design team will hand-tune. Because these
    // are read-only getters they read the CSV-filled player stats at CALL time, so
    // an ability's damage/cooldown/radius scales with the player rather than owning
    // an independent number. Every external call site reads these exactly as before
    // (a property is source-compatible with a field for reads; none are written to).
    // ============================================================================

    // --- Derived-weapon factors (loaded from CSV; formulas use these to scale with attack damage) ---
    public float fireDpsFactor = 0.05f;     // CSV: fireDpsFactor (fire DoT = attackDamageBase * this)
    public float auraDpsFactor = 0.1f;      // CSV: auraDpsFactor (aura DPS = attackDamageBase * this)
    public float trailDpsFactor = 0.05f;    // CSV: trailDpsFactor (trail DPS = attackDamageBase * this)
    public float grenadeDamageFactor = 2f;  // CSV: grenadeDamageFactor (grenade damage = attackDamageBase * this)

    // --- Enemy status: Fire DoT ---
    public float fireDpsPerStack() => attackDamageBase * fireDpsFactor;         // scales with attack damage via factor field
    public int   fireStackCap      => Mathf.Max(1, Pierce() * 2 + 1);           // PLACEHOLDER formula — tune by hand
    public float fireTickInterval  => FireCooldown() * 0.12f;                   // PLACEHOLDER formula — tune by hand
    public float fireBurnDuration  => Range();                                  // PLACEHOLDER formula — tune by hand

    // --- Enemy status: Freeze ---
    public float freezeDefaultDuration => Range() * 2f / 3f;                    // PLACEHOLDER formula — tune by hand

    // --- Item projectile/weapon mods (Cone / Bounce / Explode / Freeze) ---
    public float coneHalfAngleDeg      => Range() * 5f;                         // PLACEHOLDER formula — tune by hand
    public float bounceSearchRadius    => Range() * 2f;                         // PLACEHOLDER formula — tune by hand
    public float explosionRadiusFactor => ProjectileSize() * 0.5f;             // PLACEHOLDER formula — tune by hand
    public float freezeChance          => Mathf.Clamp01(CritChance() * 2f);     // PLACEHOLDER formula — tune by hand
    public float freezeItemDuration    => Range() * 2f / 3f;                    // PLACEHOLDER formula — tune by hand

    // --- Damage Aura: constant DPS in a radius around the player. ---
    public float auraDpsBase()   => attackDamageBase * auraDpsFactor;           // scales with attack damage via factor field
    public float auraDpsMult      = 1f;
    public float auraRadiusBase  => PickupRadius();                            // PLACEHOLDER formula — tune by hand
    public float auraRadiusMult   = 1f;
    public float auraTickInterval => FireCooldown() * 0.12f;                   // PLACEHOLDER formula — tune by hand

    // --- Attack Bot: a bot dealing a fraction of attack damage on an interval. ---
    public float robotDamageFactor => CritMultiplier() * 0.25f;               // PLACEHOLDER formula — tune by hand
    public float robotSpeedFactor  => ProjectileSize();                       // PLACEHOLDER formula — tune by hand
    public float robotHitInterval  => FireCooldown() * 0.6f;                  // PLACEHOLDER formula — tune by hand

    // --- Searing Trail: damaging trail segments left behind the player. ---
    public float trailDpsBase()   => attackDamageBase * trailDpsFactor;        // scales with attack damage via factor field
    public float trailDpsMult      = 1f;
    public float trailSegmentLifetime => Range() * 2f / 3f;                    // PLACEHOLDER formula — tune by hand
    public float trailEmitDistance    => MoveSpeed() * 0.5f;                   // PLACEHOLDER formula — tune by hand
    public float trailTickInterval    => FireCooldown() * 0.12f;              // PLACEHOLDER formula — tune by hand

    // --- Grenadier: periodically lobs a grenade that explodes for AoE damage. ---
    public float grenadeInterval    => FireCooldown() * 2.4f;                 // PLACEHOLDER formula — tune by hand
    public float grenadeDamageBase() => attackDamageBase * grenadeDamageFactor; // scales with attack damage via factor field
    public float grenadeDamageMult   = 1f;
    public float grenadeRadiusBase  => Range();                              // PLACEHOLDER formula — tune by hand
    public float grenadeRadiusMult   = 1f;

    // Flat bonus XP added to EVERY pickup's xpValue at collection time. CSV-sourced.
    public int xpBonusPerPickup;             // CSV: xpBonusPerPickup (starts 0)

    // ============================================================================
    // LEVEL-UP INCREMENTS — CSV-sourced (single source of truth for both the
    // "+X" / "+Y%" labels and the actual mutations in levelUpMenuController).
    // Declared with NO literal default; the CSV supplies the value.
    // ============================================================================

    // Flat additive steps (per stat).
    public float attackDamageFlatStep;       // CSV: attackDamageFlatStep
    public float moveSpeedFlatStep;          // CSV: moveSpeedFlatStep
    public float fireRateFlatStep;           // CSV: fireRateFlatStep
    public float rangeFlatStep;              // CSV: rangeFlatStep
    public float maxHPFlatStep;              // CSV: maxHPFlatStep
    public float defenseFlatStep;            // CSV: defenseFlatStep
    public float regenFlatStep;              // CSV: regenFlatStep
    public float pickupRadiusFlatStep;       // CSV: pickupRadiusFlatStep
    public float projectileSizeFlatStep;     // CSV: projectileSizeFlatStep
    public int   pierceFlatStep;             // CSV: pierceFlatStep
    public int   xpBonusStep;                // CSV: xpBonusStep
    public int   xpBonusCap;                 // CSV: xpBonusCap (max total bonus per pickup)

    // Percent step, shared across all stats. 0.1 = +10% (mult factor = 1 + step).
    public float levelUpPercentStep;         // CSV: levelUpPercentStep

    // --- Global outgoing-damage modifier ---
    // Single multiplier applied to ALL player damage output via AttackDamage(),
    // so every weapon/attack that reads AttackDamage() is affected uniformly.
    // Default 1.0 = no change. Items trade raw damage for utility by setting this
    // (e.g. Cone Shot -> 2/3, a 1/3 reduction). Set-based (idempotent) so an item
    // that re-applies its value never stacks the reduction. Not CSV-overlaid.
    public float damageModifier = 1f;
    public float DamageModifier() => damageModifier;
    public void SetDamageModifier(float value) => damageModifier = value;

    public float AttackDamage() => attackDamageBase * attackDamageMult * damageModifier;
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
    public float lvlUpCostGrowth;            // CSV: lvlUpCostGrowth (each level costs this * previous XP requirement)

    public int currentHP = 1000;

    // Run-scoped pity flag for the slot-machine level-up menu. worldState.instance is
    // re-created each play session, so this auto-resets to false at the start of every run.
    public bool slotPityPending = false;

    // --- Enemy spawning (CSV-sourced; currentSpawnInterval is runtime state). ---
    public float baseSpawnInterval;          // CSV: baseSpawnInterval
    public float spawnIntervalCoefficient;   // CSV: spawnIntervalCoefficient
    public float minSpawnInterval;           // CSV: minSpawnInterval
    public float currentSpawnInterval = 1.5f;

    // --- Time-based SPAWN VOLUME ramp (seconds of elapsed run time) ---
    // Shrinks the consumed spawn interval as the run goes on so enemy volume
    // ramps with TIME (not level), harder than the level-based currentSpawnInterval curve.
    public float spawnIntervalTimeCoefficient;   // CSV: spawnIntervalTimeCoefficient
    public float spawnIntervalTimeFloor;         // CSV: spawnIntervalTimeFloor

    // --- Time-based BOSS scaling coefficients (per elapsed minute) ---
    public float bossFireRateTimeCoefficient;    // CSV: bossFireRateTimeCoefficient
    public float bossBulletSpeedTimeCoefficient; // CSV: bossBulletSpeedTimeCoefficient
    public float bossVolleyBonusPerMinute;       // CSV: bossVolleyBonusPerMinute
    public float bossStatTimeCoefficient;        // CSV: bossStatTimeCoefficient

    // --- Time-based TYPE progression (seconds of elapsed run time) ---
    public float shooterStartTime;   // CSV: shooterStartTime
    public float unlockRampSeconds;  // CSV: unlockRampSeconds

    // --- Repeating boss cadence (seconds of elapsed run time) ---
    public float bossFirstTime;   // CSV: bossFirstTime
    public float bossInterval;    // CSV: bossInterval

    // Count of bosses actually spawned this run. Incremented by bossSpawner at each
    // successful spawn; drives the per-boss acceleration term in EnemyHpTimeMultiplier()
    // and the batch-spawn gate in enemySpawner. Run-scoped: worldState is re-created each
    // run -> auto-resets to 0.
    public int bossSpawnCount = 0;

    // --- Time-based ENEMY HP scaling (seconds of elapsed run time) ---
    // Every hpScaleInterval seconds, NEWLY-spawned enemies (and bosses) get
    // +hpScalePerTier of their BASE hp. ADDITIVE: mult = 1 + perTier * tier.
    public float hpScaleInterval;   // CSV: hpScaleInterval
    public float hpScalePerTier;    // CSV: hpScalePerTier

    // Per-boss HP acceleration. Each boss that spawns (bossSpawnCount) adds this fraction
    // of base HP ON TOP of the time-based tier scaling. Additive: extra mult += this * count.
    // NOTE: no tuning.csv key yet, so this keeps an in-code literal (overlaid only if a
    // "bossHpAccelPerBoss" row is added to the CSV).
    public float bossHpAccelPerBoss = 0.25f;

    // Multiplier for HP applied AT SPAWN, from elapsed run time.
    // Uses Time.timeSinceLevelLoad — the run-time source already adopted by
    // enemySpawner/bossSpawner — so all time-based systems agree.
    public float EnemyHpTimeMultiplier()
    {
        // Base: time-based tier scaling (existing behavior; guarded against divide-by-zero).
        float mult = 1f;
        if (hpScaleInterval > 0f)
        {
            int tier = Mathf.FloorToInt(Time.timeSinceLevelLoad / hpScaleInterval);
            if (tier < 0) tier = 0;
            mult += hpScalePerTier * tier;
        }
        // Accelerator: each boss spawned steepens the ramp on top of the time base.
        if (bossSpawnCount > 0)
            mult += bossHpAccelPerBoss * bossSpawnCount;
        return mult;
        // COMPOUNDING alternative (retune): Mathf.Pow(1f + hpScalePerTier, tier) then * (1 + accel*count);
    }

    // --- Time-based XP doubling (seconds of elapsed run time) ---
    // After xpDoubleThreshold seconds, ALL earned XP is doubled (single ×2, permanent).
    public float xpDoubleThreshold;   // CSV: xpDoubleThreshold
    public float xpDoubleFactor;      // CSV: xpDoubleFactor

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
