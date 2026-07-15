using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

// Editor-only drift-guard: regenerates Assets/StreamingAssets/tuning.csv from the
// current worldState field values so the CSV keys can never silently drift out of
// sync with the code. Menu: Tools/Tuning/Export tuning.csv from defaults.
//
// The 'key,value,type,comment' layout and the '#' section headers are preserved via
// the ordered Rows descriptor below; the VALUE and TYPE of every row are read from a
// fresh worldState instance via reflection, so changing a field's default in code and
// re-running this exporter always produces a matching, correctly-typed CSV. A row whose
// key has no matching worldState field is logged as a drift warning and skipped.
//
// NOTE: worldState's constructor overlays any existing tuning.csv on top of the in-code
// defaults, so the exported values reflect the CURRENT effective defaults (the in-code
// default when no CSV override is present for that key).
public static class TuningCsvExporter
{
    private readonly struct Row
    {
        public readonly string Section;   // optional "# --- ... ---" header emitted before this row (null = none)
        public readonly string Key;       // must match a public instance field on worldState
        public readonly string Comment;   // author-facing 4th column text

        public Row(string section, string key, string comment)
        {
            Section = section;
            Key = key;
            Comment = comment;
        }
    }

    // Ordered to mirror the hand-authored tuning.csv layout (sections + comments).
    private static readonly Row[] Rows =
    {
        new Row("# --- Core player stats (base values; effective = base * runtime mult) ---", "attackDamageBase", "player base attack damage (drives fire/aura/trail/grenade formulas)"),
        new Row(null, "moveSpeedBase", "player base move speed"),
        new Row(null, "fireRateBase", "shots per second baseline"),
        new Row(null, "rangeBase", "projectile/weapon range (also grenade radius via grenadeRadiusBase alias)"),
        new Row(null, "maxHPBase", "player base max HP"),
        new Row(null, "defenseBase", "flat damage reduction"),
        new Row(null, "regenBase", "HP regen per second baseline"),
        new Row(null, "pickupRadiusBase", "XP pickup homing radius baseline"),
        new Row(null, "projectileSizeBase", "bullet visual+collider scale baseline"),
        new Row(null, "pierceBase", "enemies a bullet passes through before despawning"),
        new Row(null, "critChanceBase", "crit probability 0..1"),
        new Row(null, "critDamageBase", "crit damage multiplier"),

        new Row("# --- Status effect tuning (Fire DoT / Freeze) ---", "fireStackCap", "max simultaneous burning stacks on one enemy"),
        new Row(null, "fireTickInterval", "seconds between burn ticks"),
        new Row(null, "fireBurnDuration", "seconds a burn lasts; refreshed by each ApplyFire()"),
        new Row(null, "freezeDefaultDuration", "fallback freeze seconds when hit site passes 0 or less"),

        new Row("# --- Projectile / weapon item behaviour ---", "coneHalfAngleDeg", "Cone item: +/- spread of the 3-shot cone in degrees"),
        new Row(null, "bounceSearchRadius", "Bounce item: OverlapCircle radius to find next target"),
        new Row(null, "explosionRadiusFactor", "Explode item: AoE radius = Range() * this"),
        new Row(null, "freezeChance", "Freeze item: per-hit probability to apply freeze"),
        new Row(null, "freezeItemDuration", "Freeze item: seconds passed to ApplyFreeze"),

        new Row("# --- Damage Aura item stats ---", "auraRadiusBase", "Aura item: constant-damage radius around the player"),
        new Row(null, "auraTickInterval", "Aura item: seconds between aura damage ticks"),

        new Row("# --- Attack Bot item stats ---", "robotDamageFactor", "Bot: damage = AttackDamage() * this"),
        new Row(null, "robotSpeedFactor", "Bot: move/orbit speed multiplier"),
        new Row(null, "robotHitInterval", "Bot: seconds between hits"),

        new Row("# --- Searing Trail item stats ---", "trailSegmentLifetime", "Trail: seconds a trail segment persists"),
        new Row(null, "trailEmitDistance", "Trail: player travel distance between emitted segments"),
        new Row(null, "trailTickInterval", "Trail: seconds between trail damage ticks"),

        new Row("# --- Grenadier item stats ---", "grenadeInterval", "Grenadier: seconds between grenade throws"),

        new Row("# --- Derived weapon DPS factors (weapon DPS = attackDamageBase * factor) ---", "fireDpsFactor", "burn DPS per stack = attackDamageBase * this"),
        new Row(null, "auraDpsFactor", "aura DPS = attackDamageBase * this"),
        new Row(null, "trailDpsFactor", "trail DPS = attackDamageBase * this"),
        new Row(null, "grenadeDamageFactor", "grenade damage = attackDamageBase * this"),

        new Row("# --- XP pickup bonus ---", "xpBonusPerPickup", "flat bonus XP added to every pickup at collection time (starting value)"),
        new Row(null, "xpBonusStep", "XP-Gain upgrade increment per level-up choice"),

        new Row("# --- Level-up flat additive step sizes (per stat) ---", "attackDamageFlatStep", "flat attack damage added per flat level-up choice"),
        new Row(null, "moveSpeedFlatStep", "flat move speed added per flat level-up choice"),
        new Row(null, "fireRateFlatStep", "flat fire rate added per flat level-up choice"),
        new Row(null, "rangeFlatStep", "flat range added per flat level-up choice"),
        new Row(null, "maxHPFlatStep", "flat max HP added per flat level-up choice"),
        new Row(null, "defenseFlatStep", "flat defense added per flat level-up choice"),
        new Row(null, "regenFlatStep", "flat regen added per flat level-up choice"),
        new Row(null, "pickupRadiusFlatStep", "flat pickup radius added per flat level-up choice"),
        new Row(null, "projectileSizeFlatStep", "flat projectile size added per flat level-up choice"),
        new Row(null, "pierceFlatStep", "flat pierce added per flat level-up choice"),

        new Row("# --- Level-up shared percent step ---", "levelUpPercentStep", "shared +% step for all percent level-up choices (0.2 = +20%)"),

        new Row("# --- Enemy spawning ---", "baseSpawnInterval", "seconds between spawns at run start"),
        new Row(null, "spawnIntervalCoefficient", "interval reduction per level gained"),
        new Row(null, "minSpawnInterval", "fastest allowed spawn interval (floor)"),
        new Row(null, "spawnIntervalTimeCoefficient", "fractional reduction of spawn interval per elapsed minute"),
        new Row(null, "spawnIntervalTimeFloor", "never shrink spawn interval below this fraction of base"),

        new Row("# --- Time-based boss scaling ---", "bossFireRateTimeCoefficient", "boss fire rate: +15% per elapsed minute"),
        new Row(null, "bossBulletSpeedTimeCoefficient", "boss bullet speed: +10% per elapsed minute"),
        new Row(null, "bossVolleyBonusPerMinute", "extra boss volley bullets per minute (floored)"),
        new Row(null, "bossStatTimeCoefficient", "boss HP and damage: +10% per elapsed minute"),

        new Row("# --- Time-based type progression ---", "shooterStartTime", "seconds before shooter enemies begin spawning"),
        new Row(null, "unlockRampSeconds", "seconds a newly-unlocked type takes to ramp from 0 to full weight"),

        new Row("# --- Boss cadence ---", "bossFirstTime", "seconds into the run when the first boss spawns"),
        new Row(null, "bossInterval", "seconds between subsequent boss spawns"),

        new Row("# --- Enemy HP scaling over time ---", "hpScaleInterval", "seconds per enemy HP tier (newly-spawned enemies only)"),
        new Row(null, "hpScalePerTier", "additive HP fraction per tier: mult = 1 + hpScalePerTier * tier"),

        new Row("# --- Time-based XP doubling ---", "xpDoubleThreshold", "seconds until earned XP is doubled"),
        new Row(null, "xpDoubleFactor", "XP multiplier applied after xpDoubleThreshold"),
        new Row(null, "xpBonusCap", "max total XP bonus per pickup (was const)"),
        new Row(null, "lvlUpCostGrowth", "each level-up XP cost multiplies by this"),
    };

    [MenuItem("Tools/Tuning/Export tuning.csv from defaults")]
    public static void ExportTuningCsv()
    {
        string dir = Application.streamingAssetsPath;
        string path = Path.Combine(dir, "tuning.csv");

        try
        {
            var ws = new worldState();
            Type type = typeof(worldState);
            const BindingFlags Flags = BindingFlags.Public | BindingFlags.Instance;

            var sb = new StringBuilder();
            void Emit(string line) => sb.Append(line).Append('\n');

            Emit("# project-cucumber tuning table. Edit values, save, re-enter Play Mode. No recompile needed.");
            Emit("# Lines starting with '#' are ignored by the loader. Decimal separator is '.' (period).");
            Emit("# Regenerated by Tools/Tuning/Export tuning.csv from defaults — values reflect current worldState fields.");
            Emit("# Columns: key,value,type,comment");
            Emit("key,value,type,comment");

            int count = 0;
            foreach (Row row in Rows)
            {
                if (!string.IsNullOrEmpty(row.Section)) Emit(row.Section);

                FieldInfo fi = type.GetField(row.Key, Flags);
                if (fi == null)
                {
                    Debug.LogWarning($"[TuningCsvExporter] key '{row.Key}' has no matching worldState field — skipped (possible drift).");
                    continue;
                }

                string typeName;
                string valueStr;
                object value = fi.GetValue(ws);
                if (fi.FieldType == typeof(int))
                {
                    typeName = "int";
                    valueStr = ((int)value).ToString(CultureInfo.InvariantCulture);
                }
                else if (fi.FieldType == typeof(float))
                {
                    typeName = "float";
                    valueStr = ((float)value).ToString(CultureInfo.InvariantCulture);
                }
                else
                {
                    Debug.LogWarning($"[TuningCsvExporter] field '{row.Key}' is {fi.FieldType.Name}, not int/float — skipped.");
                    continue;
                }

                Emit($"{row.Key},{valueStr},{typeName},{row.Comment}");
                count++;
            }

            Directory.CreateDirectory(dir);
            File.WriteAllText(path, sb.ToString());
            AssetDatabase.Refresh();
            Debug.Log($"[TuningCsvExporter] Wrote {count} tuning rows to {path}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[TuningCsvExporter] Failed to export tuning.csv to '{path}': {e}");
        }
    }
}
