# Design: Configurable Level-Up Increment Magnitudes (via worldState)

**Date:** 2026-07-07
**Feature (verbatim):** "please make the level up increment values abstracted and configurable, preferrably via worldstate as that is where all the base values are configured as well"
**Author:** project-planner (Tier 2 — DESIGN ONLY, no implementation)

## Goal

Move EVERY hardcoded level-up increment magnitude — both the flat additive steps and
the percent multiplier factors — out of `levelUpMenuController.cs` and into
`worldState.cs` as configurable fields, mirroring how base/mult stats already live
there. After the refactor both `LabelFor()` (displayed `+X` / `+Y%` strings) and
`Choose()` (the actual mutations) read magnitudes from `worldState`, so one edit in
`worldState` changes both label and effect. Behavior must remain byte-for-byte
identical at the supplied defaults.

Files studied (each read once):
- `Assets/Scripts/levelUpMenuController.cs` (193 lines)
- `Assets/Scripts/worldState.cs` (60 lines)

---

## (a) Current increment magnitudes — every StatKind (from real code)

`StatKind` enum: `MaxHP, FireRate, AttackDamage, MoveSpeed, Range, Defense, Regen, PickupRadius`
(`levelUpMenuController.cs:7`).

Percent factor is uniform `*= 1.1f` for all 8 stats (`levelUpMenuController.cs:158–183`),
displayed as `+10%` for all 8 (`levelUpMenuController.cs:103–110`).

| StatKind | Flat label (line) | Flat mutation (line) | Flat magnitude | Base field mutated | Percent label (line) | Percent mutation (line) | Percent factor |
|---|---|---|---|---|---|---|---|
| AttackDamage | `+2 Damage` (89) | `attackDamageBase += 2f` (124) | **2** | `attackDamageBase` | `+10% Damage` (103) | `attackDamageMult *= 1.1f` (158) | **1.1** |
| MoveSpeed | `+0.2 Move Speed` (90) | `moveSpeedBase += 0.2f` (127) | **0.2** | `moveSpeedBase` | `+10% Move Speed` (104) | `moveSpeedMult *= 1.1f` (161) | **1.1** |
| FireRate | `+0.25 Fire Rate` (91) | `fireRateBase += 0.25f` (130) | **0.25** | `fireRateBase` | `+10% Fire Rate` (105) | `fireRateMult *= 1.1f` (164) | **1.1** |
| Range | `+0.5 Range` (92) | `rangeBase += 0.5f` (133) | **0.5** | `rangeBase` | `+10% Range` (106) | `rangeMult *= 1.1f` (167) | **1.1** |
| MaxHP | `+15 Max HP` (93) | `maxHPBase += 15f` (138) | **15** | `maxHPBase` | `+10% Max HP` (107) | `maxHPMult *= 1.1f` (172) | **1.1** |
| Defense | `+2 Defense` (94) | `defenseBase += 2f` (143) | **2** | `defenseBase` | `+10% Defense` (108) | `defenseMult *= 1.1f` (177) | **1.1** |
| Regen | `+0.1 HP/s Regen` (95) | `regenBase += 0.1f` (146) | **0.1** | `regenBase` | `+10% Regen` (109) | `regenMult *= 1.1f` (180) | **1.1** |
| PickupRadius | `+0.5 Pickup Radius` (96) | `pickupRadiusBase += 0.5f` (149) | **0.5** | `pickupRadiusBase` | `+10% Pickup Radius` (110) | `pickupRadiusMult *= 1.1f` (183) | **1.1** |

**MaxHP special case:** both MaxHP mutations wrap the change in a heal-preservation block
that snapshots `MaxHP()` before, applies the increment, then bumps `currentHP` by the
delta (`levelUpMenuController.cs:135–141` flat, `169–175` percent). This structure must be
preserved verbatim; only the magnitude literals move to `worldState`.

Note the label **text** (`Damage`, `Move Speed`, `HP/s Regen`, etc.) is descriptive
prose, not a magnitude — it stays hardcoded in `LabelFor()`. Only the numeric part is
sourced from `worldState`.

---

## (b) Designed worldState config fields

### Naming scheme

`worldState.cs` uses public plain fields with in-code defaults and the suffix pattern
`xxxBase` / `xxxMult` (lines 9–24), plus effective getters like
`AttackDamage() => attackDamageBase * attackDamageMult` (lines 26–34). To mirror this
exactly, increments use the suffix **`xxxFlatStep`** for the per-stat additive step.

For the percent factors: **all 8 stats currently share the identical factor `1.1f`.**
Rather than store the raw multiplier (`1.1f`) eight times, store a single **shared
fractional step** `levelUpPercentStep = 0.1f` (0.1 = +10%). This is the cleanest scheme
because:
- One value drives all percent upgrades, matching current uniform behavior.
- The label (`+10%`) is derived as `step * 100`, and the mutation factor is derived as
  `1f + step` — so label and effect are guaranteed consistent from one field (the exact
  consistency the feature requests).
- Storing the fraction (0.1) rather than the multiplier (1.1) makes the `+X%` label
  derivation trivial and avoids the `(mult - 1) * 100` round-trip.

**Alternative considered (per-stat percent step):** eight fields
`attackDamagePercentStep`, `moveSpeedPercentStep`, … Rejected as the default because it
adds 8 fields with identical values and no current requirement for per-stat percent
tuning. If per-stat percent tuning is later needed, the shared field can be replaced by
per-stat fields with a mechanical switch change (see §e risk note). The shared-step
choice is fully forward-compatible: the `Choose()`/`LabelFor()` refactor below reads the
percent step through a single accessor, so swapping to per-stat is a localized edit.

### Fields to add to `worldState.cs`

Insert after the existing stat block (after line 24, before the getters at line 26),
so increment config sits directly beneath the base/mult config it parallels:

```csharp
    // Level-up increment magnitudes. Single source of truth for BOTH the
    // "+X" / "+Y%" labels and the actual mutations in levelUpMenuController.
    // Defaults equal the previously hardcoded values -> behavior unchanged.

    // Flat additive steps (per stat).
    public float attackDamageFlatStep = 2f;
    public float moveSpeedFlatStep    = 0.2f;
    public float fireRateFlatStep     = 0.25f;
    public float rangeFlatStep        = 0.5f;
    public float maxHPFlatStep        = 15f;
    public float defenseFlatStep      = 2f;
    public float regenFlatStep        = 0.1f;
    public float pickupRadiusFlatStep = 0.5f;

    // Percent step, shared across all stats. 0.1 = +10% (mult factor = 1 + step).
    public float levelUpPercentStep = 0.1f;
```

Defaults are exactly the current hardcoded magnitudes (§a table), so at these defaults
output is unchanged.

---

## (c) Exact drop-in refactor for LabelFor() and Choose()

Both methods currently either don't touch `instance` (`LabelFor`) or null-guard it
(`Choose`, line 117). The refactor makes `LabelFor` depend on `worldState.instance`, so
it gains a null guard returning `""` (parity with existing empty-string fallbacks at
lines 97/111).

### Number formatting note (byte-for-byte labels)

Current flat labels print floats as `2`, `0.2`, `0.25`, `0.5`, `15`, `0.1`. To reproduce
these exactly and avoid locale decimal-comma drift, format with
`System.Globalization.CultureInfo.InvariantCulture`. `2f`→`"2"`, `0.2f`→`"0.2"`,
`0.25f`→`"0.25"`, `0.5f`→`"0.5"`, `15f`→`"15"`, `0.1f`→`"0.1"` under invariant culture —
matching the current literals character-for-character. The percent label is an integer
(`10`), formatted via `Mathf.RoundToInt(step * 100f)`.

### Replace `LabelFor()` (currently lines 83–113)

```csharp
    private string LabelFor(Upgrade u)
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
            default: return "";
        }
    }
```

### Replace `Choose()` (currently lines 115–192)

Only the magnitude literals change; control flow, the null guard (117), the MaxHP
heal-preservation blocks, and the trailing `levelUpManager` advance (188–191) are
preserved exactly. Introduce a local `ws` and a percent factor `p = 1f + levelUpPercentStep`.

```csharp
    private void Choose(Upgrade u)
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
            }
        }

        if (levelUpManager.instance != null)
        {
            levelUpManager.instance.ApplyChoiceAndAdvance();
        }
    }
```

**Consistency guarantee:** the percent label uses `levelUpPercentStep * 100` and the
percent mutation uses `1f + levelUpPercentStep`. Both come from the one field, so editing
`levelUpPercentStep` (e.g. to `0.15f`) makes the label read `+15%` AND the mult multiply
by `1.15f` in lockstep. Likewise each `xxxFlatStep` drives both its label and its
`+=` mutation.

---

## (d) Grep-based acceptance checklist

Run from repo root. All must pass after implementation.

```bash
# 1. No hardcoded flat magnitudes remain in the controller mutations/labels.
#    Expect: 0
grep -nE '\+= (2f|0\.2f|0\.25f|0\.5f|15f|0\.1f)\b' Assets/Scripts/levelUpMenuController.cs | wc -l

# 2. No hardcoded percent factor remains in the controller.
#    Expect: 0
grep -nE '\*= 1\.1f' Assets/Scripts/levelUpMenuController.cs | wc -l

# 3. No literal "+10%" or "+<number> <word>" magnitude strings remain baked in labels.
#    Expect: 0  (labels now built from ws fields; only "% " suffix text remains)
grep -nE '"\+(10%|2 |0\.2 |0\.25 |0\.5 |15 |0\.1 )' Assets/Scripts/levelUpMenuController.cs | wc -l

# 4. New config fields exist in worldState.
#    Expect: 8 FlatStep fields + 1 percent step = 9
grep -cE 'FlatStep|levelUpPercentStep' Assets/Scripts/worldState.cs
grep -n 'levelUpPercentStep' Assets/Scripts/worldState.cs   # expect exactly 1 field decl

# 5. Controller now reads increments from worldState.
#    Expect: >= 16 (8 flat + 8 percent reads) FlatStep/percent references
grep -cE 'FlatStep|levelUpPercentStep' Assets/Scripts/levelUpMenuController.cs

# 6. MaxHP heal-preservation blocks intact (snapshot before / delta after).
#    Expect: 2  (one flat, one percent)
grep -c 'int before = ws.MaxHP();' Assets/Scripts/levelUpMenuController.cs

# 7. Defaults preserved — spot check magnitudes live in worldState.
grep -nE 'attackDamageFlatStep = 2f|maxHPFlatStep = 15f|levelUpPercentStep = 0.1f' Assets/Scripts/worldState.cs
```

Manual/Play-Mode confirmation (out of scope for this design; hand to build-validator):
open the level-up menu and confirm the 5 offered labels read identically to pre-refactor
(`+2 Damage`, `+10% Damage`, `+0.25 Fire Rate`, `+0.1 HP/s Regen`, etc.) and that picking
each applies the same stat change.

---

## (e) Risk note

**Must remain byte-for-byte behavior-preserving at the supplied defaults.**

1. **Label float formatting.** The single real risk. Current labels are literal strings
   (`+0.25 Fire Rate`). The refactor formats floats at runtime. Using
   `CultureInfo.InvariantCulture` (§c) reproduces `2`, `0.2`, `0.25`, `0.5`, `15`, `0.1`
   exactly. Without invariant culture, a machine set to a comma-decimal locale would
   render `+0,25 Fire Rate` — a regression. The invariant-culture `ToString(ci)` is
   mandatory, not optional. Acceptance check verifies the strings render correctly in
   Play Mode.

2. **`LabelFor` now dereferences `worldState.instance`.** Previously it returned strings
   with no instance access; now it returns `""` when `instance == null`. In practice
   `OnEnable` only builds labels when the menu opens, by which point `instance` exists
   (the existing `Choose` and `BuildPool` already assume it), so parity holds. The `""`
   guard matches the method's existing empty-string default branches.

3. **Percent derivation exactness.** `1f + 0.1f` in float is `1.1f` bit-identical, and
   `Mathf.RoundToInt(0.1f * 100f)` = `10`. So both label and mutation match the old
   `1.1f` / `"+10%"` exactly at default. If a future non-round step is chosen (e.g.
   `0.075f` → label `+8%` after rounding while the mult uses `1.075f`), label and effect
   can visually diverge due to integer rounding — acceptable and by design, but worth
   noting for tuning.

4. **Shared-vs-per-stat percent.** Choosing a shared `levelUpPercentStep` means a future
   requirement for per-stat percent tuning needs a follow-up change (replace the one field
   with 8, and read `ws.<stat>PercentStep` in the percent switch). This is localized to
   the two files and does not affect the current byte-for-byte guarantee.

5. **No new allocations/perf concern.** `ToString(ci)` runs only when the menu opens
   (`OnEnable`, ≤5 labels), not per-frame. Negligible.

---

## Handoff

This is a design only. Implementation belongs to `csharp-dev` (both files are plain C#,
no Editor access needed → `run_agent_in_docker`). Suggested task: apply §b field
additions to `worldState.cs` and §c drop-in replacements to `levelUpMenuController.cs`,
then run the §d grep checklist; dispatch `build-validator` for the Play-Mode label
confirmation in risk note #1.
