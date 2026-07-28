# Implementation Plan: CSV-Driven Tuning for `worldState`

**Author:** project-planner (design only — no code written)
**Date:** 2026-07-15
**Source file analysed:** `Assets/Scripts/worldState.cs` (252 lines) + load site `Assets/Scripts/gameController.cs`
**Goal (verbatim user request):** move all adjustable base tuning values out of code into an easily edited CSV file so they can be changed *without diving into the code every time*, ideally *without a full recompile*.

---

## Overview

`worldState` is a plain C# class (NOT a MonoBehaviour), instantiated once per run in `gameController.Start()` (`worldState.instance = new worldState()`), and reset to `null` by `gameOverManager.Restart()` so a fresh instance is built on the next run. This gives us a single, clean, once-per-run load point at which to overlay CSV values onto the in-code defaults.

The recommendation is a **`StreamingAssets/tuning.csv`** file read via `System.IO.File.ReadAllText` inside the `worldState` constructor. StreamingAssets is copied verbatim into builds and is editable as a plain file on disk with **no reimport and no recompile** — you edit the CSV, press Play (or restart the run), and the new numbers take effect. The in-code field initializers stay exactly as they are today and serve as the **fallback default** for any key that is missing, blank, or malformed, so the game always runs even if the CSV is deleted or contains a typo.

---

## Field Inventory — True Inputs vs Derived

The critical structural nuance in `worldState`: **some values are independently editable numbers (TRUE INPUTS)**, while **others are computed off `attackDamageBase` via formula methods (DERIVED)**. The CSV must expose the *inputs*, not the computed outputs — otherwise editing e.g. `AuraDps()` directly would fight the formula. For the four derived weapon formulas, we expose the **factor constant** embedded in the formula (currently a hard-coded literal), so the user can still tune them independently. Exposing those factors requires refactoring four literals into named fields (small, mechanical `csharp-dev` change).

### A. TRUE INPUTS — belong in the CSV (recommended keys)

| # | CSV key | Field in code | Type | Default | Notes |
|---|---------|---------------|------|---------|-------|
| 1 | `attackDamageBase` | `attackDamageBase` | float | 100 | drives all 4 derived weapon formulas |
| 2 | `moveSpeedBase` | `moveSpeedBase` | float | 1 | |
| 3 | `fireRateBase` | `fireRateBase` | float | 1.2 | |
| 4 | `rangeBase` | `rangeBase` | float | 3 | also used by `grenadeRadiusBase` |
| 5 | `maxHPBase` | `maxHPBase` | float | 1000 | |
| 6 | `defenseBase` | `defenseBase` | float | 0 | |
| 7 | `regenBase` | `regenBase` | float | 1 | |
| 8 | `pickupRadiusBase` | `pickupRadiusBase` | float | 1.5 | |
| 9 | `projectileSizeBase` | `projectileSizeBase` | float | 1 | |
| 10 | `pierceBase` | `pierceBase` | **int** | 2 | |
| 11 | `critChanceBase` | `critChanceBase` | float | 0.1 | |
| 12 | `critDamageBase` | `critDamageBase` | float | 2.0 | |
| 13 | `fireStackCap` | `fireStackCap` | **int** | 5 | |
| 14 | `fireTickInterval` | `fireTickInterval` | float | 0.1 | |
| 15 | `fireBurnDuration` | `fireBurnDuration` | float | 3 | |
| 16 | `freezeDefaultDuration` | `freezeDefaultDuration` | float | 2 | |
| 17 | `coneHalfAngleDeg` | `coneHalfAngleDeg` | float | 15 | |
| 18 | `bounceSearchRadius` | `bounceSearchRadius` | float | 6 | |
| 19 | `explosionRadiusFactor` | `explosionRadiusFactor` | float | 0.5 | |
| 20 | `freezeChance` | `freezeChance` | float | 0.2 | |
| 21 | `freezeItemDuration` | `freezeItemDuration` | float | 2 | |
| 22 | `auraRadiusBase` | `auraRadiusBase` | float | 1.5 | |
| 23 | `auraTickInterval` | `auraTickInterval` | float | 0.1 | |
| 24 | `robotDamageFactor` | `robotDamageFactor` | float | 0.5 | |
| 25 | `robotSpeedFactor` | `robotSpeedFactor` | float | 1 | |
| 26 | `robotHitInterval` | `robotHitInterval` | float | 0.5 | |
| 27 | `trailSegmentLifetime` | `trailSegmentLifetime` | float | 2 | |
| 28 | `trailEmitDistance` | `trailEmitDistance` | float | 0.5 | |
| 29 | `trailTickInterval` | `trailTickInterval` | float | 0.1 | |
| 30 | `grenadeInterval` | `grenadeInterval` | float | 2 | |
| 31 | `xpBonusPerPickup` | `xpBonusPerPickup` | **int** | 0 | starting bonus (usually 0) |
| 32 | `xpBonusStep` | `xpBonusStep` | **int** | 1 | |
| 33 | `attackDamageFlatStep` | `attackDamageFlatStep` | float | 50 | level-up +X |
| 34 | `moveSpeedFlatStep` | `moveSpeedFlatStep` | float | 0.2 | |
| 35 | `fireRateFlatStep` | `fireRateFlatStep` | float | 0.2 | |
| 36 | `rangeFlatStep` | `rangeFlatStep` | float | 0.5 | |
| 37 | `maxHPFlatStep` | `maxHPFlatStep` | float | 200 | |
| 38 | `defenseFlatStep` | `defenseFlatStep` | float | 5 | |
| 39 | `regenFlatStep` | `regenFlatStep` | float | 2 | |
| 40 | `pickupRadiusFlatStep` | `pickupRadiusFlatStep` | float | 1 | |
| 41 | `projectileSizeFlatStep` | `projectileSizeFlatStep` | float | 0.2 | |
| 42 | `pierceFlatStep` | `pierceFlatStep` | **int** | 1 | |
| 43 | `levelUpPercentStep` | `levelUpPercentStep` | float | 0.2 | shared +% step |
| 44 | `baseSpawnInterval` | `baseSpawnInterval` | float | 1.5 | |
| 45 | `spawnIntervalCoefficient` | `spawnIntervalCoefficient` | float | 0.3 | |
| 46 | `minSpawnInterval` | `minSpawnInterval` | float | 0.3 | |
| 47 | `spawnIntervalTimeCoefficient` | `spawnIntervalTimeCoefficient` | float | 0.1 | |
| 48 | `spawnIntervalTimeFloor` | `spawnIntervalTimeFloor` | float | 0.1 | |
| 49 | `bossFireRateTimeCoefficient` | `bossFireRateTimeCoefficient` | float | 0.15 | |
| 50 | `bossBulletSpeedTimeCoefficient` | `bossBulletSpeedTimeCoefficient` | float | 0.10 | |
| 51 | `bossVolleyBonusPerMinute` | `bossVolleyBonusPerMinute` | float | 0.5 | |
| 52 | `bossStatTimeCoefficient` | `bossStatTimeCoefficient` | float | 0.10 | |
| 53 | `shooterStartTime` | `shooterStartTime` | float | 120 | |
| 54 | `unlockRampSeconds` | `unlockRampSeconds` | float | 30 | |
| 55 | `bossFirstTime` | `bossFirstTime` | float | 200 | |
| 56 | `bossInterval` | `bossInterval` | float | 200 | |
| 57 | `hpScaleInterval` | `hpScaleInterval` | float | 300 | |
| 58 | `hpScalePerTier` | `hpScalePerTier` | float | 1 | |
| 59 | `xpDoubleThreshold` | `xpDoubleThreshold` | float | 300 | |
| 60 | `xpDoubleFactor` | `xpDoubleFactor` | float | 2 | |

### B. DERIVED FORMULA FACTORS — expose the *factor*, not the output (requires refactor to fields)

These four are currently `=> attackDamageBase * <literal>` methods. Recommendation: refactor each literal into a named `float` field (default unchanged) so the method reads `=> attackDamageBase * <field>`, then add the field to the CSV. Behaviour is identical at default values; the user gains the ability to retune the weapon's share of attack damage.

| # | CSV key (new field) | Current formula | Default factor |
|---|---------------------|-----------------|----------------|
| 61 | `fireDpsFactor` | `fireDpsPerStack() => attackDamageBase * 0.05f` | 0.05 |
| 62 | `auraDpsFactor` | `auraDpsBase() => attackDamageBase * 0.1f` | 0.10 |
| 63 | `trailDpsFactor` | `trailDpsBase() => attackDamageBase * 0.05f` | 0.05 |
| 64 | `grenadeDamageFactor` | `grenadeDamageBase() => attackDamageBase * 2f` | 2.0 |

**Total tuning inputs exposed: 64** (60 plain input fields + 4 newly-extracted derived factors).

### C. EXCLUDED — do NOT put in the CSV (and why)

| Item | Reason to exclude |
|------|-------------------|
| All `*Mult` fields (`attackDamageMult`, `moveSpeedMult`, … `grenadeRadiusMult`) | Run-state, not tuning. Default to `1f` and are mutated at runtime by items/level-ups. Overriding from CSV would corrupt the base+mult model. |
| `AttackDamage()`, `MoveSpeed()`, `Range()`, … all getter methods | DERIVED outputs (`base * mult`). Expose the base, never the getter. |
| `fireDpsPerStack()`, `auraDpsBase()`, `trailDpsBase()`, `grenadeDamageBase()` | DERIVED — we expose their *factor* (section B), not the computed value. |
| `grenadeRadiusBase => rangeBase` | Read-only expression-bodied property aliasing `rangeBase`; not independently assignable. Tune via `rangeBase`. |
| `RollDamage()`, `EnemyHpTimeMultiplier()`, `XpTimeMultiplier()`, `Boss*TimeMultiplier()`, `SpawnIntervalTimeMultiplier()` | Behaviour methods; they consume the input fields already in the CSV. |
| `lvlUpXP`, `currentXP`, `level`, `currentHP`, `currentSpawnInterval`, `slotPityPending` | Per-run mutable game state, not tuning. |
| `player` (Transform) | Object reference, wired in code. |
| `const int xpBonusCap = 3` | A compile-time `const` — cannot be reassigned at runtime. See Risks: to make it CSV-editable it must first be changed from `const` to a regular field (optional follow-up). Documented in the CSV as a comment, not a live row, unless de-const'd. |
| `1.3f` growth in `addXP` (`lvlUpXP * 1.3f`) | Hard-coded inline literal, not a field. Optional: extract to `lvlUpXpGrowth` field and add to CSV in a later pass. Out of scope for v1. |

---

## Recommended Approach (+ Alternatives)

### ✅ Recommendation: `StreamingAssets/tuning.csv`, read with `System.IO` in the `worldState` constructor

**Why StreamingAssets:**
- **No reimport, no recompile.** Files in `Assets/StreamingAssets/` are *not* imported as Unity assets — they are copied byte-for-byte into the build and read at runtime with `System.IO.File`. Editing the CSV does not dirty the AssetDatabase, does not trigger a script recompile, and does not require Unity to reimport anything. This directly satisfies "edit without diving into code / without a full recompile."
- **Editable in a build too.** In a shipped PC build the file lives under `<Build>_Data/StreamingAssets/tuning.csv` and can still be edited on disk — great for balance passes without rebuilding.
- **Plain path access.** `Application.streamingAssetsPath` gives a real filesystem path on Standalone (PC — the only target), so `File.ReadAllText` works synchronously and simply. (Note: on Android the path is inside the APK and needs `UnityWebRequest`; irrelevant for this PC-only project, but noted so nobody copies the pattern to mobile blindly.)

**Load timing:** `worldState`'s constructor runs once per run from `gameController.Start()`, and `gameOverManager.Restart()` nulls the instance so the next run rebuilds it. Reading the CSV in the constructor therefore means **every run re-reads the file** → editing the CSV and restarting the run (or re-entering Play Mode) picks up changes with zero code interaction.

### Alternatives considered

| Option | Recompile/Reimport on edit? | Verdict |
|--------|------------------------------|---------|
| **StreamingAssets + `System.IO`** (recommended) | **None.** Not an imported asset. | ✅ Best fit for the "no recompile" requirement. |
| `Resources/tuning.csv` as `TextAsset` (`Resources.Load<TextAsset>`) | **Reimport required.** The file is an imported asset; Unity must reimport it (and the editor may stall briefly) before the new bytes are available. Also gets baked into the build — not editable post-ship. | ❌ Violates the "no reimport" goal; only upside is `TextAsset` convenience. |
| Project-root / `Assets/`-adjacent Editor-only file read via `Application.dataPath` | Editor-only; won't exist in a build; brittle relative paths. | ❌ Doesn't ship; inconsistent editor-vs-build behaviour. |
| ScriptableObject tuning asset (inspector-edited) | Edits happen in the Inspector, no CSV. Values change without recompile, but the user explicitly asked for a **CSV file**, and SO editing still means "diving into Unity." | ❌ Doesn't match the stated request (CSV). Worth mentioning as the "Unity-native" road not taken. |

---

## CSV Schema + Example

### Schema

A minimal, human-friendly **`key,value,type,comment`** layout:

- **`key`** — must exactly match a field name from the inventory (the loader maps key → field). This is the single source of truth for names.
- **`value`** — the number. Parsed with `CultureInfo.InvariantCulture` so the decimal separator is always `.` regardless of OS locale (see Risks).
- **`type`** — `float` or `int`. Lets the loader route to `float.TryParse` / `int.TryParse` and lets the export tool regenerate correct rows. (Optional but recommended — it makes malformed-value handling and the template exporter unambiguous.)
- **`comment`** — free text describing the value (copied from the code comments). Ignored by the parser; purely author-facing.

**Rules the loader enforces:**
- Lines beginning with `#` are **section headers / comments** → skipped.
- Blank lines → skipped.
- The first non-comment line MAY be a header row (`key,value,type,comment`) → detected and skipped.
- A missing key, blank value, or a value that fails `TryParse` → **the field keeps its in-code default** and a `Debug.LogWarning` is emitted naming the offending key. A malformed CSV therefore **never breaks a run**.
- Unknown keys (in CSV but not in code) → warn and ignore (helps catch drift/typos).

### Example excerpt (`Assets/StreamingAssets/tuning.csv`)

```csv
# project-cucumber tuning table. Edit values, save, re-enter Play Mode. No recompile needed.
# Lines starting with '#' are ignored. Columns: key,value,type,comment
key,value,type,comment
# --- Core player stats (base values; effective = base * runtime mult) ---
attackDamageBase,100,float,player base attack damage (drives fire/aura/trail/grenade formulas)
moveSpeedBase,1,float,player base move speed
fireRateBase,1.2,float,shots per second baseline
rangeBase,3,float,projectile/weapon range (also grenade radius)
maxHPBase,1000,float,player base max HP
defenseBase,0,float,flat damage reduction
regenBase,1,float,HP regen per second baseline
pickupRadiusBase,1.5,float,XP pickup radius baseline
projectileSizeBase,1,float,bullet visual+collider scale
pierceBase,2,int,enemies a bullet passes through
critChanceBase,0.1,float,crit probability 0..1
critDamageBase,2.0,float,crit damage multiplier
# --- Derived-weapon factors (weapon DPS = attackDamageBase * factor) ---
fireDpsFactor,0.05,float,burn DPS per stack = attackDamageBase * this
auraDpsFactor,0.1,float,aura DPS = attackDamageBase * this
trailDpsFactor,0.05,float,trail DPS = attackDamageBase * this
grenadeDamageFactor,2,float,grenade damage = attackDamageBase * this
# --- Spawn / time ramps ---
baseSpawnInterval,1.5,float,seconds between spawns at run start
minSpawnInterval,0.3,float,fastest spawn interval
hpScaleInterval,300,float,seconds per enemy-HP tier
hpScalePerTier,1,float,+fraction of base HP per tier
xpDoubleThreshold,300,float,seconds until earned XP doubles
xpDoubleFactor,2,float,the XP multiplier after threshold
# ... (all 64 keys from the inventory follow, grouped by section) ...
```

> `pierceBase`, `fireStackCap`, `xpBonusPerPickup`, `xpBonusStep`, `pierceFlatStep` are the **int** rows; everything else is `float`. `int`-typed rows should hold whole numbers; a decimal value there → warn + keep default.

---

## Load Mechanism

### New class: `tuningTable` (static loader)  — `Assets/Scripts/tuningTable.cs` (global namespace, matches project conventions)

Responsibilities:
1. `public static Dictionary<string,string> Load()` — read `Path.Combine(Application.streamingAssetsPath, "tuning.csv")`. If the file does not exist, return an empty dictionary and `Debug.Log` "tuning.csv not found — using in-code defaults." (Not an error; defaults are valid.)
2. Parse line-by-line: strip `#` comment lines and blanks, skip an optional header row, split each remaining line on the first two commas into `key,value` (ignore the `type`/`comment` remainder for parsing), trim whitespace, and populate the dictionary. Hand-rolled `string.Split` — **no new package**. (Rationale below.)
3. Provide typed helpers: `float GetFloat(dict, key, float fallback)` and `int GetInt(dict, key, int fallback)`, each using `TryParse` with `NumberStyles.Float | NumberStyles.AllowLeadingSign` and `CultureInfo.InvariantCulture`; on failure they `LogWarning(key)` and return `fallback`.

**Why hand-rolled, not a CSV package:** the schema is flat `key,value` with no quoted fields, embedded commas, or multi-line cells (comments are the only free text and live in a trailing column the parser ignores). A `string.Split(',')` with a 2-comma limit is sufficient and adds zero dependency to `Packages/manifest.json`. Pulling in a CSV library would be unjustified weight for `key,value` rows. (If the schema ever needs quoted commas in comments, revisit — but v1 doesn't.)

### `worldState` changes

In the `worldState` constructor (add one if none exists — currently the class has only field initializers, so add an explicit `public worldState()`):

```csharp
public worldState()
{
    var t = tuningTable.Load();               // empty dict if file absent
    attackDamageBase = tuningTable.GetFloat(t, "attackDamageBase", attackDamageBase);
    pierceBase       = tuningTable.GetInt  (t, "pierceBase",       pierceBase);
    // ... one line per inventory key; fallback arg is the existing field value (the in-code default) ...
}
```

Key property of this pattern: **the fallback passed to each getter is the field's own current value**, which is exactly the in-code default. So:
- File missing → dict empty → every getter returns its fallback → **behaviour identical to today**.
- Key present & valid → field overwritten with CSV value.
- Key present but malformed → warn + keep default.

The four derived factors (`fireDpsFactor` etc.) are loaded the same way, and the formula methods change from `attackDamageBase * 0.05f` to `attackDamageBase * fireDpsFactor`.

**Const caveat:** `xpBonusCap` is `const` and cannot be assigned in the constructor. Leave it as-is for v1 (documented in the CSV as a comment). If the user wants it tunable, a follow-up task changes `const int xpBonusCap = 3;` → `public int xpBonusCap = 3;` and adds a CSV row — flagged in Open Questions.

---

## Editor-time vs Runtime Workflow (the whole point)

| Action | Result |
|--------|--------|
| Open `Assets/StreamingAssets/tuning.csv` in any text editor, change a number, save | **No recompile, no reimport.** Unity does not treat StreamingAssets as an imported asset. |
| Re-enter Play Mode (or die → Restart, which nulls `worldState.instance`) | `gameController.Start()` builds a fresh `worldState`, whose constructor re-reads the CSV → **new values live this run.** |
| Delete or rename the CSV | Loader logs "not found", all fields keep in-code defaults → **game still runs.** |
| Typo a value (e.g. `abc`) | That one key warns and keeps its default; every other key still applies. |

There is **no** need to touch C#, recompile, or reimport for a normal tuning edit — a save + Play-Mode restart is the entire loop. (Values are read at run construction, so an edit mid-run takes effect on the *next* run, not instantly during the current one — acceptable and predictable for balance work. A hot-reload-during-play feature is possible but out of scope; see Open Questions.)

---

## Round-trip / Authoring Aid — **Recommended: YES**

Add a small Editor menu item (`Assets/Editor/TuningCsvExporter.cs`, `[MenuItem("Tools/Tuning/Export tuning.csv template")]`) that constructs a temporary `new worldState()` (or reads the field defaults) and writes a fully-populated `tuning.csv` with every key, its current default value, the type, and the code comment — then `AssetDatabase.Refresh()`.

**Why yes:** the single biggest failure mode of a hand-maintained CSV is **key drift** — a field is renamed or added in code, and the CSV silently goes stale. A generator that emits the CSV *from the code fields* makes the code the source of truth for names and guarantees the seed file is always complete and correctly spelled. The user runs it once to (re)generate the template whenever fields change, then hand-edits values. This is Editor-only (`asset-manager`/`csharp-dev` via the Editor), does not ship in a build, and is cheap. Pair it with a loader warning on unknown keys and drift is caught from both sides.

---

## Derived-Formula Handling — decision

**Expose the factor constants as CSV rows** (section B), by refactoring the four literals (`0.05f`, `0.1f`, `0.05f`, `2f`) into named fields. Rationale:
- It keeps the *formula* (weapon DPS scales with attack damage) in code where it belongs, while making the *tuning knob* (each weapon's share of attack damage) editable — which is precisely the kind of value the user tweaks during balance passes.
- Leaving the formulas fully in code and exposing only `attackDamageBase` would mean the user *cannot* rebalance one weapon relative to another without a code edit — reintroducing the exact pain the request is about.
- Behaviour is identical at the default factors, so this is a safe, behaviour-neutral refactor.

We do **not** expose the derived *outputs* (`AuraDps()`, etc.) — those remain computed.

---

## Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| **Key drift** between CSV keys and code field names | A renamed/removed field leaves a dead CSV row (silently ignored) or a new field has no CSV row (uses default, user confused why edits "don't work") | (a) Editor export tool regenerates the CSV from code (single source of truth for names); (b) loader `LogWarning` on unknown CSV keys; (c) document that adding a field means re-exporting the template. |
| **Culture/locale decimal parsing** (`,` vs `.`) | On a machine with a comma-decimal locale, `float.Parse("1.5")` could misread or throw | **Always** parse with `CultureInfo.InvariantCulture` and `NumberStyles.Float`. Document in the CSV header that the decimal separator is `.`. This is the single most common StreamingAssets-CSV bug — call it out explicitly to `csharp-dev`. |
| **CSV not present in a build** | Reading a missing file → exception → run breaks | Loader checks `File.Exists` first; missing file → empty dict → in-code defaults. StreamingAssets *is* copied into Standalone builds, but the not-found path must still be safe. Never let a parse/IO error escape the loader — wrap in try/catch, fall back to defaults, log once. |
| **Malformed row / wrong type** (`pierceBase,2.5` or `attackDamageBase,abc`) | One bad row shouldn't nuke the whole run | Per-key `TryParse` with fallback to the in-code default + a warning naming the key. Int rows reject non-integers → keep default. |
| **Editing mid-run has no effect until restart** | User changes a value expecting live update, sees nothing | Documented behaviour: values load at run construction. Restart the run (or re-enter Play) to apply. (Optional hot-reload is a future enhancement — Open Questions.) |
| **`const xpBonusCap` can't be loaded** | User adds it to CSV, nothing happens | Excluded from v1 CSV and documented as a comment. Follow-up task can de-`const` it if desired. |
| **StreamingAssets `.meta` churn / folder missing** | Folder must exist and be committed with its `.meta` | `asset-manager` creates `Assets/StreamingAssets/` + the seed `tuning.csv` + lets Unity generate `.meta` files, then commits them. |

---

## Task Decomposition (for scrum-master — NOT implemented here)

Dependency-ordered. Agent mapping per CLAUDE.md: `csharp-dev` = script work (Docker); `asset-manager` = folder/seed-file/meta (folder tasks in Docker, but StreamingAssets creation is a plain file op); `build-validator` = Play-Mode verification (host Editor + Coplay MCP).

1. **[asset-manager] Create `Assets/StreamingAssets/` folder + seed `tuning.csv` + let Unity generate `.meta` files.**
   Seed CSV contains all 64 keys from the inventory at their default values, grouped by section with `#` comments and the `key,value,type,comment` header. Commit folder + CSV + metas.
   *Depends on: none.*

2. **[csharp-dev] Add `tuningTable.cs` static loader** (`Assets/Scripts/`, global namespace).
   `Load()` → `Dictionary<string,string>` from `Application.streamingAssetsPath/tuning.csv`; `#`/blank/header skipping; `GetFloat`/`GetInt` helpers using `InvariantCulture` + `TryParse` with fallback + warning; `File.Exists` guard + try/catch → empty dict on any failure.
   *Depends on: none (can run parallel to #1). Verifies against #1's schema.*

3. **[csharp-dev] Refactor the 4 derived-formula literals into named fields** in `worldState.cs`
   (`fireDpsFactor 0.05`, `auraDpsFactor 0.1`, `trailDpsFactor 0.05`, `grenadeDamageFactor 2`; update the four formula methods to multiply by the field). Behaviour-neutral at defaults.
   *Depends on: none. Should land before #4 so the constructor can load them.*

4. **[csharp-dev] Add `worldState()` constructor that overlays CSV onto defaults.**
   One `GetFloat`/`GetInt` line per inventory key (fallback = existing field value). Route int keys through `GetInt`. Do NOT touch `*Mult` fields, run-state, or `const xpBonusCap`.
   *Depends on: #2 (loader), #3 (factor fields).*

5. **[build-validator] Play-Mode verification (host Editor + Coplay MCP).**
   Confirm: (a) no compile errors; (b) with CSV present, an edited value (e.g. `attackDamageBase,500`) takes effect on a fresh run; (c) with CSV deleted/renamed, the game still runs on in-code defaults + logs "not found"; (d) a malformed row warns and keeps its default without breaking the run; (e) no new NullRefs.
   *Depends on: #1, #2, #3, #4.*

6. **[csharp-dev, Editor] (Recommended, optional) `Assets/Editor/TuningCsvExporter.cs`** — `[MenuItem]` that regenerates `tuning.csv` from current field defaults to prevent key drift.
   *Depends on: #3, #4 (needs the final field set). Can ship after #5.*

7. **[csharp-dev] (Optional follow-up) De-`const` `xpBonusCap`** and add `lvlUpXpGrowth` field for the `1.3f` literal, then add both to the CSV + exporter.
   *Depends on: #4, #6. Explicitly out of v1 scope — needs user sign-off.*

---

## Open Questions (need human input before build)

1. **Derived factors — confirm exposing them.** Recommendation is to extract `fireDpsFactor/auraDpsFactor/trailDpsFactor/grenadeDamageFactor` into CSV. Agree, or leave those formulas code-only and expose just `attackDamageBase`?
2. **`xpBonusCap` (const) and the `1.3f` level-cost growth** — want these made CSV-tunable now (task #7), or leave them in code for v1?
3. **Hot-reload during Play** — is "changes apply on next run/restart" acceptable, or do you want a keypress/menu to reload the CSV live mid-run? (Adds complexity; not recommended for v1.)
4. **Authoring aid** — confirm you want the Editor export menu item (task #6). Recommended to prevent key drift.
5. **CSV column set** — is `key,value,type,comment` right, or do you prefer the leaner `key,value` (with comments as `#` lines only)? The `type` column helps the exporter and int-validation but is optional.
