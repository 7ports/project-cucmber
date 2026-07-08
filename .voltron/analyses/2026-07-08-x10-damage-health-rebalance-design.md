# ×10 Damage & Health Rebalance — Audit & Edit Design

**Project:** project-cucumber (Unity 6.x, 2D top-down survivors)
**Date:** 2026-07-08
**Author:** project-planner (Tier 2 — DESIGN/AUDIT ONLY, no implementation)
**Task (verbatim):** "multiply all damage related values in the game by 10. this includes the health of all units including the player, all damage values, and all related upgrade and base values."

**Ruling summary:** 20 absolute values scale ×10 — **9 C# edits** + **11 prefab-YAML edits**. Defense **does** scale (its flat step 1→10) because defense is flat subtractive mitigation in damage units; defenseBase stays 0.

---

## 0. Key structural finding — the enemy prefab VARIANT chain (read before editing)

Enemy HP/damage live in a **prefab-variant inheritance chain**, not flat per-prefab values. This governs where each value is scaled **exactly once**:

```
slime.prefab            (BASE, direct values: maxHp 40, enemyDamage 5)
  └─ chaser.prefab      (VARIANT of slime; overrides  maxHp 30, enemyDamage 8)
       ├─ shooter.prefab (VARIANT of chaser; NO maxHp/enemyDamage override → INHERITS chaser's 30 / 8)
       ├─ boss.prefab    (VARIANT of chaser; overrides maxHp 250, enemyDamage 15)
       ├─ diggy.prefab   (VARIANT of chaser; overrides maxHp 250, enemyDamage 15)
       └─ ziggy.prefab   (VARIANT of chaser; overrides maxHp 250, enemyDamage 15)
```

Verified via meta-guid resolution:
- `slime.prefab.meta` guid `a3f0c52240fc14f4fbcb9e1d3c7451b1` → chaser's modification `target` → chaser is a variant of slime.
- `chaser.prefab.meta` guid `ac858a605179fdc44bd51edb0da4b967` → the `target`/`m_SourcePrefab` for shooter, boss, diggy, ziggy → all four are variants of chaser.

**Consequence:** `shooter.prefab` has **no** `maxHp`/`enemyDamage` override. Scaling chaser's override (30→300, 8→80) automatically flows to shooter. **Do NOT add a maxHp/enemyDamage override to shooter** — that would double-count and desync it. Each override is scaled once at the level where the `propertyPath` line exists.

---

## (a) Complete TABLE of every value to scale ×10

### C# code defaults (edit type: **C#**)

| # | Location (file:line) | Field | Current | ×10 | Notes |
|---|----------------------|-------|---------|-----|-------|
| 1 | `Assets/Scripts/worldState.cs:9` | `attackDamageBase` | `10f` | `100f` | Player base attack damage (absolute). Feeds `AttackDamage()` → player bullet dmg. |
| 2 | `Assets/Scripts/worldState.cs:17` | `maxHPBase` | `100f` | `1000f` | Player base max HP (absolute). Feeds `MaxHP()`. |
| 3 | `Assets/Scripts/worldState.cs:67` | `currentHP` | `100` | `1000` | Player starting current HP — must track maxHPBase or player starts at 10% HP. |
| 4 | `Assets/Scripts/worldState.cs:37` | `attackDamageFlatStep` | `5f` | `50f` | Flat per-level damage upgrade ("+X Damage"). "related upgrade value". |
| 5 | `Assets/Scripts/worldState.cs:41` | `maxHPFlatStep` | `50f` | `500f` | Flat per-level Max HP upgrade ("+X Max HP"). |
| 6 | `Assets/Scripts/worldState.cs:42` | `defenseFlatStep` | `1f` | `10f` | Flat Defense upgrade — subtractive mitigation in **damage units** (see defense ruling). |
| 7 | `Assets/Scripts/enemyHealth.cs:5` | `maxHp` (default) | `3` | `30` | Code default/placeholder (prefabs override), scaled for consistency. Low risk. |
| 8 | `Assets/Scripts/enemyHealth.cs:10` | `enemyDamage` (default) | `5` | `50` | Code default/placeholder (prefabs override), scaled for consistency. |
| 9 | `Assets/Scripts/enemyProjectile.cs:5` | `damage` (default) | `8` | `80` | Enemy bullet code default (prefab overrides), scaled for consistency. |

> Items 7–9 are placeholders overridden by prefab YAML at runtime; scaling them keeps code and serialized data consistent and prevents surprises if a future prefab omits the override. Zero double-scaling risk because a serialized override always wins over the code default for existing prefabs.

### Prefab-serialized values (edit type: **prefab-YAML**)

| # | Location (prefab : line) | Serialized field | Current | ×10 | Notes |
|---|--------------------------|------------------|---------|-----|-------|
| 10 | `Assets/Prefabs/enemies/slime.prefab:184` | `maxHp` (direct) | `40` | `400` | Base slime HP. |
| 11 | `Assets/Prefabs/enemies/slime.prefab:187` | `enemyDamage` (direct) | `5` | `50` | Slime contact damage. |
| 12 | `Assets/Prefabs/enemies/enemyProjectile.prefab:172` | `damage` (direct) | `8` | `80` | Enemy bullet damage (fired by shooter). |
| 13 | `Assets/Prefabs/enemies/chaser.prefab:21` | `maxHp` (override `propertyPath: maxHp`, fileID `3976880873792520215`) | `30` | `300` | Chaser HP; **also inherited by shooter**. |
| 14 | `Assets/Prefabs/enemies/chaser.prefab:25` | `enemyDamage` (override, fileID `3976880873792520215`) | `8` | `80` | Chaser contact damage; **also inherited by shooter**. |
| 15 | `Assets/Prefabs/enemies/bosses/boss.prefab:304` | `maxHp` (override, fileID `8993517367165848743`) | `250` | `2500` | Boss HP. |
| 16 | `Assets/Prefabs/enemies/bosses/boss.prefab:308` | `enemyDamage` (override, fileID `8993517367165848743`) | `15` | `150` | Boss contact damage. |
| 17 | `Assets/Prefabs/enemies/bosses/diggy.prefab:304` | `maxHp` (override, fileID `8993517367165848743`) | `250` | `2500` | Diggy boss HP. |
| 18 | `Assets/Prefabs/enemies/bosses/diggy.prefab:308` | `enemyDamage` (override, fileID `8993517367165848743`) | `15` | `150` | Diggy contact damage. |
| 19 | `Assets/Prefabs/enemies/bosses/ziggy.prefab:304` | `maxHp` (override, fileID `8993517367165848743`) | `250` | `2500` | Ziggy boss HP. |
| 20 | `Assets/Prefabs/enemies/bosses/ziggy.prefab:308` | `enemyDamage` (override, fileID `8993517367165848743`) | `15` | `150` | Ziggy contact damage. |

**Totals: 20 values → 9 C# edits, 11 prefab-YAML edits.**
Line numbers are current-snapshot anchors; the editing agent must match on the **field name + nearest `propertyPath`/`target` fileID** (given in the table), not the raw line number, in case the file shifts.

---

## (b) EXCLUSIONS — do NOT scale (with reason)

| Location | Value | Reason excluded |
|----------|-------|-----------------|
| `worldState.cs:10,12,14,16,18,20,22,24` `*Mult = 1f` | attackDamageMult, maxHPMult, defenseMult, etc. | **Multipliers** — effective = base × mult. Scaling would ×10 again on top of the base. |
| `worldState.cs:50` `levelUpPercentStep = 0.1f` | +10% step | **Percent** upgrade step — ratio, not absolute. |
| `worldState.cs:86` `hpScalePerTier = 0.5f` | +50%/tier | **Percent** in the additive `mult = 1 + perTier*tier` formula. |
| `worldState.cs:85` `hpScaleInterval = 420f` | 7-min tier | **Timing** (seconds), not damage/HP. |
| `worldState.cs:91-98` `EnemyHpTimeMultiplier()` | ratio | Returns a **runtime multiplier** applied to maxHp. Scaling it double-scales enemy HP. |
| `worldState.cs:11,13,15,21,23` bases | moveSpeedBase, fireRateBase, rangeBase, regenBase, pickupRadiusBase | Not damage/HP (speed, rate, range, regen, radius). |
| `worldState.cs:19` `defenseBase = 0f` | 0 | Absolute defense base, but 0×10 = 0 — no change (documented, not edited). |
| `worldState.cs:26,45` `pierceBase`, `pierceFlatStep` | 1, 1 | **Pierce counts**, not damage. |
| `worldState.cs:30,46,47` xpBonus fields, `xpBonusCap` | 0,1,3 | **XP** values — excluded per scope. |
| `worldState.cs:38,39,40,43,44` flat steps | moveSpeed/fireRate/range/regen/pickupRadius steps | Non-damage/HP stat steps. |
| `worldState.cs:64,110` `lvlUpXP`, ×1.5 | 4, 1.5 | **XP curve** — excluded. |
| `worldState.cs:69-72,75-79` spawn/timing | intervals, coeff, bossFirstTime/Interval, shooterStartTime | **Spawn-timing / level-gate** numbers. |
| `enemyHealth.cs:9` `xpDropCount = 1` | 1 | **XP** drop count. |
| `enemyProjectile.cs:6` `lifetime = 4f` | 4 | **Timer**, not damage. |
| `enemyProjectile.prefab` / boss `xpDropCount: 20` | 20 | **XP** drop count (bosses). |
| `projectileBehaviour.cs:5,7` `lifeSeconds=10`, `fallbackRange=8` | 10, 8 | Timer / range. |
| `projectileBehaviour.cs:51` `: 1` fallback | 1 | Null-guard fallback for `AttackDamage()` when worldState is null — never a live gameplay value. Leave (optional: 1→10 cosmetic; no behavioral effect). |
| `playerHealth.cs:6` `damageInterval = 2f` | 2 | Contact-damage **tick timer**. |
| `playerHealth.cs:63` `Mathf.Max(1, …)` floor | 1 | Minimum-damage **floor**, not a tunable amount. Leave as 1. |
| `shooterBehaviour` (shooter.prefab): `aimRange 4`, `fireCooldown 2`, `projectileSpeed 6`, etc. | — | Range / cooldown / speed — not damage/HP. |
| `shooter.prefab` maxHp/enemyDamage | (none present) | **Inherited from chaser** — no override exists; must not add one. |

---

## (c) Explicit DEFENSE ruling — **DOES scale (the flat step)**

How defense works (`playerHealth.cs:63`):
```csharp
int Reduce(int raw) => Mathf.Max(1, raw - Mathf.RoundToInt(worldState.instance.Defense()));
```
Defense is **flat subtractive mitigation** — it is subtracted directly from incoming damage, i.e. it lives in the **same units as damage**. `Defense() = defenseBase * defenseMult` (`worldState.cs:58`).

Therefore, to keep the damage↔defense relationship invariant after ×10:
- `defenseFlatStep` **MUST scale** `1f → 10f` (item #6). A "+1 Defense" upgrade currently blocks 1 dmg out of a ~5–15 hit; without scaling it would block 1 out of a ~50–150 hit and become worthless.
- `defenseBase = 0f` — scales in principle but 0×10 = 0, so **no edit** (documented).
- `defenseMult = 1f` — **excluded** (multiplier).

This is the one non-obvious "damage-related" value outside the raw damage/HP fields, and it is included by design.

---

## (d) Exact edits

### C# (drop-in replacements — match the full line)

```
worldState.cs:9    public float attackDamageBase = 10f;   →   public float attackDamageBase = 100f;
worldState.cs:17   public float maxHPBase = 100f;         →   public float maxHPBase = 1000f;
worldState.cs:67   public int currentHP = 100;            →   public int currentHP = 1000;
worldState.cs:37   public float attackDamageFlatStep = 5f;→   public float attackDamageFlatStep = 50f;
worldState.cs:41   public float maxHPFlatStep        = 50f;→  public float maxHPFlatStep        = 500f;
worldState.cs:42   public float defenseFlatStep      = 1f; →   public float defenseFlatStep      = 10f;
enemyHealth.cs:5   [SerializeField] private int maxHp = 3;      →  = 30;
enemyHealth.cs:10  [SerializeField] private int enemyDamage = 5;→  = 50;
enemyProjectile.cs:5 [SerializeField] private int   damage   = 8;→ = 80;
```

### Prefab-YAML (change only the `value:`/inline scalar of the named field)

```
slime.prefab                 maxHp: 40         → maxHp: 400            (line ~184, direct)
slime.prefab                 enemyDamage: 5    → enemyDamage: 50       (line ~187, direct)
enemyProjectile.prefab       damage: 8         → damage: 80            (line ~172, direct)
chaser.prefab   (propertyPath: maxHp,       fileID 3976880873792520215)  value: 30  → value: 300   (~line 21)
chaser.prefab   (propertyPath: enemyDamage, fileID 3976880873792520215)  value: 8   → value: 80    (~line 25)
boss.prefab     (propertyPath: maxHp,       fileID 8993517367165848743)  value: 250 → value: 2500  (~line 304)
boss.prefab     (propertyPath: enemyDamage, fileID 8993517367165848743)  value: 15  → value: 150   (~line 308)
diggy.prefab    (propertyPath: maxHp,       fileID 8993517367165848743)  value: 250 → value: 2500  (~line 304)
diggy.prefab    (propertyPath: enemyDamage, fileID 8993517367165848743)  value: 15  → value: 150   (~line 308)
ziggy.prefab    (propertyPath: maxHp,       fileID 8993517367165848743)  value: 250 → value: 2500  (~line 304)
ziggy.prefab    (propertyPath: enemyDamage, fileID 8993517367165848743)  value: 15  → value: 150   (~line 308)
```
For overrides, the editing agent must confirm the `value:` line immediately follows the matching `propertyPath:` line under the correct `target` fileID before editing — several `propertyPath` blocks share the file.

---

## (e) Grep / inspection acceptance checklist

Run after edits; every assertion must hold:

```bash
# C# — new values present, old absent
grep -n 'attackDamageBase = 100f'      Assets/Scripts/worldState.cs
grep -n 'maxHPBase = 1000f'            Assets/Scripts/worldState.cs
grep -n 'currentHP = 1000'             Assets/Scripts/worldState.cs
grep -n 'attackDamageFlatStep = 50f'   Assets/Scripts/worldState.cs
grep -n 'maxHPFlatStep        = 500f'  Assets/Scripts/worldState.cs
grep -n 'defenseFlatStep      = 10f'   Assets/Scripts/worldState.cs
grep -n 'private int maxHp = 30;'      Assets/Scripts/enemyHealth.cs
grep -n 'private int enemyDamage = 50;' Assets/Scripts/enemyHealth.cs
grep -n 'private int   damage   = 80;' Assets/Scripts/enemyProjectile.cs

# Prefabs — new values present
grep -n 'maxHp: 400'   Assets/Prefabs/enemies/slime.prefab
grep -n 'enemyDamage: 50' Assets/Prefabs/enemies/slime.prefab
grep -n 'damage: 80'   Assets/Prefabs/enemies/enemyProjectile.prefab
grep -n 'value: 300'   Assets/Prefabs/enemies/chaser.prefab      # maxHp override
grep -n 'value: 80'    Assets/Prefabs/enemies/chaser.prefab      # enemyDamage override
for b in boss diggy ziggy; do
  grep -c 'value: 2500' Assets/Prefabs/enemies/bosses/$b.prefab   # expect 1 (maxHp)
  grep -c 'value: 150'  Assets/Prefabs/enemies/bosses/$b.prefab   # expect 1 (enemyDamage)
done

# NEGATIVE — old damage/HP values must be gone from the touched fields
! grep -n 'attackDamageBase = 10f' Assets/Scripts/worldState.cs
! grep -n 'maxHPBase = 100f'       Assets/Scripts/worldState.cs

# Untouched-by-design sanity: multipliers & percent steps unchanged
grep -n 'levelUpPercentStep = 0.1f' Assets/Scripts/worldState.cs
grep -n 'hpScalePerTier  = 0.5f'    Assets/Scripts/worldState.cs
grep -n 'attackDamageMult = 1f'     Assets/Scripts/worldState.cs
```

Editor-side checks (build-validator, host): no compile errors; Play Mode — player spawns at 1000/1000 HP; slime dies in the same number of hits as before (damage and HP both ×10 → time-to-kill unchanged); boss HP bar fills correctly (bars read `scaledMaxHp`, proportion preserved).

---

## (f) Risk notes — double-scaling & ratio hazards

1. **Variant inheritance (highest risk):** shooter inherits chaser's HP/damage. Scale chaser **once**; do **not** add a shooter override. Boss/diggy/ziggy each carry their own override — scale those independently. Editing the base and the override for the same effective value would ×100 it.
2. **Multipliers must not change.** `EnemyHpTimeMultiplier()`, all `*Mult` fields, `levelUpPercentStep`, `hpScalePerTier` are ratios applied to the (already-scaled) bases at runtime. `scaledMaxHp = maxHp * mult` — because `maxHp` is now ×10 and `mult` is untouched, enemy HP scales exactly ×10 at every tier. Touching the multiplier would compound.
3. **currentHP must scale with maxHPBase.** If item #3 is skipped, the player spawns at 100/1000 (10%). They are coupled — scale both or neither.
4. **Balance invariant preserved:** player damage ×10 vs enemy HP ×10 ⇒ hits-to-kill unchanged; enemy damage ×10 vs player HP ×10 ⇒ hits-to-die unchanged. The rebalance is numerically cosmetic *by design* (bigger numbers, same TTK) **provided every listed value is scaled and no multiplier is**. A partial application breaks balance.
5. **Defense floor:** `Mathf.Max(1, …)` guarantees ≥1 damage; unaffected and correct after scaling.
6. **Code defaults vs prefab overrides (items 7–9):** harmless if both change; the serialized override always wins at load, so these are consistency edits, not behavioral ones — no double-scale risk.

---

## Handoff

This is a **design/audit only** deliverable (project-planner never implements). Implementation of the 9 C# edits and 11 prefab-YAML edits is a Docker file-editing job.

```json
{
  "handoff": true,
  "from_agent": "project-planner",
  "to_agent": "scrum-master",
  "reason": "Design complete; project-planner does not implement. 20 file edits (9 C#, 11 prefab-YAML) are ready for csharp-dev / prefab file-editing agents, then build-validator for Play Mode verification.",
  "next_task": "Dispatch csharp-dev (Docker) to apply the 9 C# edits in worldState.cs/enemyHealth.cs/enemyProjectile.cs and the 11 prefab-YAML edits in slime/chaser/enemyProjectile/boss/diggy/ziggy prefabs exactly per section (d); then run the section (e) grep checklist; then build-validator (host) for compile + Play Mode balance check.",
  "artifacts": [".voltron/analyses/2026-07-08-x10-damage-health-rebalance-design.md"]
}
```
