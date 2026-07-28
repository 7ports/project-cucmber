# Combat Scaling Baseline Audit

- **Topic:** combat-scaling-baseline
- **Date:** 2026-07-22
- **Author:** code-analyst
- **Branch:** csv-tuning
- **Scope:** Player damage pipeline, cone-shot reduction, stat system seams, enemy HP scaling, boss spawn/HP + spawner batching seam.

## Summary

All player stats live in the plain-C# singleton `worldState` (not a MonoBehaviour), using a `base * mult` pattern with getters. Outgoing damage flows: `attackDamageBase * attackDamageMult` → optional per-weapon factor → `RollDamage()` (crit) → `enemyHealth.takeDamage()` (frozen ×1.5 amp). The cone item's 1/3 reduction is a **global, permanent** `attackDamageMult *= 2/3` applied once at acquisition — but four weapon items (fire DoT, aura, trail, grenade) compute damage from `attackDamageBase` only, so the cone penalty (and ALL percent damage upgrades) silently skip them. Enemy HP scaling is additive `1 + 1.0 * floor(t/300s)` applied at spawn in `enemyHealth.OnEnable`. Boss HP (2500) is a serialized prefab-variant override in each of the three boss prefabs (all variants of chaser.prefab); no boss-number counter exists. The enemy batch-spawn seam is the single `objectPool.instance.get(...)` call at `enemySpawner.cs:147`.

---

## 1. Player damage pipeline

**Stat holder:** `Assets/Scripts/worldState.cs` — a plain C# class singleton (`public static worldState instance`, worldState.cs:5), re-created each run. Damage stat:

```csharp
// worldState.cs:119-120
public float attackDamageBase = 100f;
public float attackDamageMult = 1f;
// worldState.cs:217
public float AttackDamage() => attackDamageBase * attackDamageMult;
```

**Shared final roll** — every hit site converts float→int and rolls crit here:

```csharp
// worldState.cs:242-247
public int RollDamage(float baseDamage, out bool isCrit)
{
    isCrit = Random.value < CritChance();
    float d = isCrit ? baseDamage * CritMultiplier() : baseDamage;
    return Mathf.RoundToInt(d);
}
```

Crit inputs: `CritChance() => Mathf.Clamp01(critChanceBase * critChanceMult)` (worldState.cs:231, `critChanceBase = 0.1f` at :144 — the comment at :143 claiming "base 0" is stale) and `CritMultiplier() => critDamageBase * critDamageMult` (worldState.cs:232, base 2.0).

**Application point** — ALL player→enemy damage lands in one method:

```csharp
// enemyHealth.cs:137, 141-142, 144
public void takeDamage(int amount, bool isCrit = false)
    if (IsFrozen)
        amount = Mathf.Max(1, Mathf.RoundToInt(amount * 1.5f));   // frozen amp, all sources
    currentHp -= amount;
```

**Every damage source and its multiplication chain:**

| Source | File:line | Formula | Uses `attackDamageMult`? | Through `RollDamage`? |
|---|---|---|---|---|
| Main projectile | projectileBehaviour.cs:83-86 | `RollDamage(AttackDamage())` | ✅ | ✅ |
| Explode splash | projectileBehaviour.cs:106-113 → explosionUtil.cs:33-36 | `floor(AttackDamage()/3)` then `RollDamage(splash)` | ✅ | ✅ |
| Attack robot | attackRobot.cs:166-168 | `floor(robotDamageFactor * AttackDamage())` then `RollDamage` | ✅ | ✅ |
| Fire DoT (burn) | enemyHealth.cs:66,76 | `fireDpsPerStack() * stacks` = `attackDamageBase * fireDpsFactor * stacks` | ❌ (base only) | ❌ (no crit) |
| Aura | auraWeapon.cs:73,86-87 | `AuraDps() * fireScale` = `attackDamageBase * auraDpsFactor * auraDpsMult * fireScale`, then `RollDamage` | ❌ (base only) | ✅ |
| Trail | trailSegment.cs:79-82 | `RollDamage(TrailDps() * interval)` = `attackDamageBase * trailDpsFactor * trailDpsMult * interval` | ❌ (base only) | ✅ |
| Grenade | grenade.cs:75-77 → explosionUtil.cs:33-36 | `floor(GrenadeDamage())` = `attackDamageBase * grenadeDamageFactor * grenadeDamageMult`, then `RollDamage` | ❌ (base only) | ✅ |

Key base-only factor definitions:
```csharp
// worldState.cs:151
public float fireDpsPerStack() => attackDamageBase * fireDpsFactor;
// worldState.cs:168
public float auraDpsBase()        => attackDamageBase * auraDpsFactor;
// worldState.cs:179
public float trailDpsBase()        => attackDamageBase * trailDpsFactor;
// worldState.cs:187
public float grenadeDamageBase()   => attackDamageBase * grenadeDamageFactor;
```

Full multiplication/addition points in order: (1) `attackDamageBase` additive flat upgrades (upgradePool.cs:130) and CSV overlay (worldState.cs:26); (2) `attackDamageMult` percent upgrades (upgradePool.cs:195) and cone penalty (itemManager.cs:56); (3) per-weapon factor (table above); (4) crit ×`CritMultiplier()` inside `RollDamage` (worldState.cs:245); (5) frozen ×1.5 in `takeDamage` (enemyHealth.cs:141-142). Telemetry tap: `runStats.TotalDamage += amount` (enemyHealth.cs:145).

## 2. Cone-shot damage reduction

**Mechanism** — applied ONCE, at item acquisition, as a permanent global multiplier:

```csharp
// itemManager.cs:51-58
// Cone tradeoff: firing in a spread cuts overall attack damage to 2/3 (a one-third
// reduction). Applied exactly once here at acquisition — GrantRandomItem grants
// random-without-duplicates, so Cone can only be granted a single time.
if (chosen == ItemId.Cone && worldState.instance != null)
{
    worldState.instance.attackDamageMult *= (2f / 3f);
```

**Global vs cone-only:** it is **global** — it mutates the shared `attackDamageMult`, not a cone-specific value. The cone's triple-shot itself is separate, in the shooter:

```csharp
// playerProjectileShooter.cs:34-40
bool cone = playerInventory.instance != null && playerInventory.instance.Has(ItemId.Cone);
if (cone)
{
    float half = worldState.instance != null ? worldState.instance.coneHalfAngleDeg : 15f;
    FireOne(RotateVec(dir, -half)); FireOne(dir); FireOne(RotateVec(dir, +half));
```

**Which weapons the reduction actually hits:** because it lives in `attackDamageMult`, it applies to the main projectile, the Explode splash, and the attack robot — but **NOT** to Fire DoT, Aura, Trail, or Grenade, which key off `attackDamageBase` alone (see table in §1). Bounce and Freeze have no damage of their own (Bounce redirects the same projectile, projectileBehaviour.cs:122-135; Freeze applies status + a defender-side ×1.5 amp, projectileBehaviour.cs:96-100 / enemyHealth.cs:141-142), so they inherit the pipeline of whatever hit carries them. So the "1/3 tradeoff" is inconsistently scoped: it's global across percent-upgraded weapons but invisible to the four base-keyed weapons.

## 3. Player stat system — base+multiplier pattern and the global-damage-modifier seam

**Declarations:** paired fields at worldState.cs:118-147 (`attackDamageBase/Mult` :119-120 through `critDamageBase/Mult` :146-147), plus weapon-factor fields :150-190. Effective values via getters at worldState.cs:217-239 (`AttackDamage()` :217 ... `GrenadeDamage()` :238). CSV tuning overlay overwrites `*Base` defaults in the constructor (`ApplyTuningOverrides`, worldState.cs:23-116).

**How flat/percent upgrades combine** — single apply site shared by both level-up menus:

```csharp
// upgradePool.cs:130 (Flat branch)
ws.attackDamageBase += ws.attackDamageFlatStep;
// upgradePool.cs:192-196 (Percent branch)
float p = 1f + ws.levelUpPercentStep;
case StatKind.AttackDamage:
    ws.attackDamageMult *= p;
```

Step magnitudes: `attackDamageFlatStep = 50f` (worldState.cs:201), `levelUpPercentStep = 0.2f` (worldState.cs:215).

**Seam for a NEW global damage-modifier multiplier** — two candidate insertion points, differing in coverage:

1. **`worldState.RollDamage()` (worldState.cs:242-247)** — multiply `baseDamage` (or the result) by the new modifier. Covers main projectile, explode, robot, aura, trail, grenade — everything EXCEPT the Fire burn tick, which calls `takeDamage` directly without `RollDamage` (enemyHealth.cs:76).
2. **`enemyHealth.takeDamage()` (enemyHealth.cs:137)** — covers literally ALL player→enemy damage including burn (this method is never used for enemy→player damage; that goes through `playerHealth`). Downside: modifier applies post-crit-rounding and sits in the enemy script rather than the stat hub.

Recommended shape: add `public float globalDamageMult = 1f;` next to worldState.cs:120 and apply it inside `RollDamage` (worldState.cs:245), plus one explicit multiply at the burn tick site (enemyHealth.cs:66 or :76) if burn must be covered. Accessor used at damage time today is `worldState.instance.AttackDamage()` / `RollDamage(...)` (call sites in §1 table).

## 4. Enemy health scaling

**Formula** (additive tiers, spawn-time only — living enemies are unaffected):

```csharp
// worldState.cs:287-288  (NOTE: comments say "7 minutes"/"+50%" but values are 300s/+100% — comments stale vs csv-tuning values)
public float hpScaleInterval = 300f;   // 7 minutes per tier
public float hpScalePerTier  = 1f;   // +50% of base per tier
// worldState.cs:293-299
public float EnemyHpTimeMultiplier()
{
    if (hpScaleInterval <= 0f) return 1f;
    int tier = Mathf.FloorToInt(Time.timeSinceLevelLoad / hpScaleInterval);
    if (tier < 0) tier = 0;
    return 1f + hpScalePerTier * tier;
```

Both fields are CSV-overridable (`hpScaleInterval`/`hpScalePerTier`, worldState.cs:110-111 via `Assets/StreamingAssets/tuning.csv`). Current in-code defaults mean **+100% of base HP every 5 minutes**; the "+50%/7min" description in CLAUDE.md matches the stale comments, not the live values — an implementer must check tuning.csv for the effective numbers.

**Spawn-time application** — in `enemyHealth.OnEnable` (fires on every pool respawn):

```csharp
// enemyHealth.cs:44-49
float mult = (worldState.instance != null) ? worldState.instance.EnemyHpTimeMultiplier() : 1f;
float bossMult = (_isBoss && worldState.instance != null) ? worldState.instance.BossStatTimeMultiplier() : 1f;
scaledMaxHp = Mathf.Max(1, Mathf.RoundToInt(maxHp * mult * bossMult));   // never mutate serialized maxHp
currentHp = scaledMaxHp;
scaledEnemyDamage = Mathf.Max(1, Mathf.RoundToInt(enemyDamage * bossMult));
```

**Boss inclusion:** bosses get `EnemyHpTimeMultiplier()` like everyone (the `mult` term) **times** an extra `BossStatTimeMultiplier() = 1 + 0.10 * minutes` (worldState.cs:274, :338-342), gated by `_isBoss = GetComponent<bossBehaviour>() != null` (enemyHealth.cs:39). Boss damage also scales via the same `bossMult` (enemyHealth.cs:49).

## 5. Boss spawn, boss HP, spawner batching seam

### (a) Boss spawn trigger — `Assets/Scripts/bossSpawner.cs`

Time-cadence driven, lazily initialized:

```csharp
// bossSpawner.cs:18-24
if (nextBossTime < 0f) nextBossTime = worldState.instance.bossFirstTime;   // first boss at bossFirstTime
if (Time.timeSinceLevelLoad < nextBossTime) return;
if (SpawnBoss())
    nextBossTime += worldState.instance.bossInterval;
```

Cadence values: `bossFirstTime = 200f`, `bossInterval = 200f` (worldState.cs:281-282; CSV-overridable at :106-107; comments again stale — say "5:00" but value is 200s = 3:20). One-boss-at-a-time guard at bossSpawner.cs:34 (`if (activeBoss != null && activeBoss.activeInHierarchy) return false;`). Boss picked **randomly** from a serialized array: `bossPrefabs[Random.Range(0, bossPrefabs.Length)]` (bossSpawner.cs:8, :44). Bosses use `Instantiate` (bossSpawner.cs:57), NOT the object pool — unlike regular enemies.

**Boss-number counter: none exists.** `bossSpawner` tracks only `nextBossTime` (bossSpawner.cs:11) and `activeBoss` (:10). "Which boss number is this" cannot currently be queried; the natural place to add `private int _bossesSpawned;` and increment it is inside the `if (SpawnBoss())` success branch at bossSpawner.cs:23-24 (cadence only advances on a real spawn, so a counter there is exact). Deriving it from elapsed time would be wrong when a boss survives past the next interval.

### (b) Boss max-HP — serialized in prefab variants, NOT in script

`enemyHealth.maxHp` is `[SerializeField] private int maxHp = 30;` (enemyHealth.cs:5). All three boss prefabs are **prefab variants of `Assets/Prefabs/enemies/chaser.prefab`** (`m_SourcePrefab: {... guid: ac858a605179fdc44bd51edb0da4b967 ...}` at boss.prefab:335, same in diggy/ziggy) and override HP via `m_Modifications`:

| Prefab | maxHp override | enemyDamage override |
|---|---|---|
| `Assets/Prefabs/enemies/bosses/boss.prefab` | `2500` (lines 302-303) | `150` (lines 306-307) |
| `Assets/Prefabs/enemies/bosses/diggy.prefab` | `2500` (lines 302-303) | `150` (lines 306-307) |
| `Assets/Prefabs/enemies/bosses/ziggy.prefab` | `2500` (lines 302-303) | `150` (lines 306-307) |

Variant chain for context: `slime.prefab` (base; flat `maxHp: 400` at slime.prefab:190, `enemyDamage: 50` at :198) ← `chaser.prefab` (variant; overrides `maxHp` → 300, `enemyDamage` → 80 at chaser.prefab:20-25) ← the three boss variants above. `crossShooter.prefab` and `grenadierChaser.prefab` also override to 300/80 (each at lines 20-25). `shooter.prefab` has **no** maxHp override — its file contains a root GameObject with only `shooterBehaviour` (shooter.prefab:256) plus a nested chaser `PrefabInstance` with transform-only overrides (shooter.prefab:155-183), so its effective HP inherits chaser's 300 (Editor verification recommended; see open questions).

**Implication:** changing boss HP means editing `.prefab` YAML overrides (doable as a text edit in Docker, but prefab-variant `m_Modifications` edits are safer via Editor) — or scaling at runtime in `enemyHealth.OnEnable` (pure C#, Docker-safe).

### (c) Regular enemy spawner batching seam — `Assets/Scripts/enemySpawner.cs`

Single-spawn flow per interval tick (interval consumed at enemySpawner.cs:78-83: `currentSpawnInterval * SpawnIntervalTimeMultiplier()`): weighted pick over time-gated `spawnTable` entries (struct at :5-11, ramp/weight loop at :93-106, pick at :110-117), position chosen at :119-144, then the **single instantiation call**:

```csharp
// enemySpawner.cs:147
objectPool.instance.get(prefab, point, Quaternion.identity);
```

**Natural batch seam:** after `GameObject prefab = eligible[pick];` (enemySpawner.cs:117), wrap position-selection + the `objectPool.instance.get` (lines 119-147) in a `for (int n = 0; n < batchCount; n++)` loop — position code is self-contained per iteration (the bias branch at :122 rolls `Random.value` per call), so batch members naturally scatter. Alternatively hoist lines 119-147 into a `SpawnOne(GameObject prefab)` helper and call it N times. Enemies are pooled (`objectPool.instance.get`), so batching has no allocation concern; HP scaling applies per-spawn automatically via `enemyHealth.OnEnable`.

---

## Findings (severity-tagged)

1. **[high] Cone/percent-upgrade damage bypass** — Fire DoT, Aura, Trail, Grenade compute from `attackDamageBase` only (worldState.cs:151,168,179,187), so `attackDamageMult` (cone penalty itemManager.cs:56, +20% percent upgrades upgradePool.cs:195) never affects them. Any new "global damage modifier" placed on `AttackDamage()` would inherit this same hole.
2. **[high] Burn DoT bypasses `RollDamage`** — enemyHealth.cs:76 calls `takeDamage` directly; no crit, and it would miss a modifier inserted only in `RollDamage` (worldState.cs:242).
3. **[medium] No boss counter** — bossSpawner tracks only `nextBossTime`/`activeBoss` (bossSpawner.cs:10-11); "Nth boss" logic needs a new counter at the :23-24 success branch.
4. **[medium] Boss HP is prefab-variant data** — 2500/150 overrides in the three boss .prefab files (each lines 302-307), not script defaults; decides Docker-vs-Editor for HP changes.
5. **[low] Stale tuning comments** — worldState.cs:287-288 say "7 min / +50%" but code says 300s / +100%; :281-282 say "5:00" but value is 200s; :143 says crit base 0 but it's 0.1. Effective values may further differ via `Assets/StreamingAssets/tuning.csv` overlay (worldState.cs:23-116).
6. **[low] Bosses skip the object pool** — `Instantiate` at bossSpawner.cs:57 vs pooled `objectPool.instance.get` for regular enemies (enemySpawner.cs:147); `enemyHealth.OnEnable` scaling still fires either way.

## Open questions for project-planner

1. **Boss HP location decides Docker vs Editor:** boss max-HP is confirmed to be serialized prefab-variant overrides (`bosses/{boss,diggy,ziggy}.prefab` lines 302-303, value 2500). Change it via (a) text-edit of prefab YAML in Docker, (b) scene-architect via Editor, or (c) a runtime multiplier in `enemyHealth.OnEnable` (pure C#, Docker-safe)? Option (c) also composes with the existing `BossStatTimeMultiplier()`.
2. **Scope of the new global damage modifier:** should it cover the four base-keyed weapons (fire/aura/trail/grenade) and the burn DoT, or intentionally mirror the current `attackDamageMult` scope? This decides insertion at `RollDamage` (worldState.cs:242) + burn-site vs `enemyHealth.takeDamage` (enemyHealth.cs:137) vs `AttackDamage()` (worldState.cs:217).
3. **Should the cone penalty stay global?** If damage routing is unified, cone's `attackDamageMult *= 2/3` (itemManager.cs:56) will suddenly also nerf fire/aura/trail/grenade — a balance change that needs sign-off.
4. **Effective tuning values:** `Assets/StreamingAssets/tuning.csv` can override nearly every number cited here (worldState.cs:23-116). Planner should read the CSV (not audited here) before fixing balance targets.
5. **shooter.prefab HP path:** shooter.prefab has no `maxHp` override and an unusual structure (root + unparented nested chaser instance, shooter.prefab:155-183); confirm in the Editor which object carries `enemyHealth` and its effective maxHp before batch-tuning enemy HP.
6. **Boss-number semantics:** if "which boss number" should drive scaling, confirm the counter increments on spawn (bossSpawner.cs:23) rather than on kill, given the one-alive-at-a-time guard can delay the cadence.
