# XP-Gain Level-Up Upgrade — Design (2026-07-07)

**Feature (verbatim):** "add xp gain as an upgrade option making xp drops worth +1 xp, a maximum of 3 times (so max would be 4 xp per pickup) I've already made the xp pickups for you in the prefabs folder under pickups."

**Type:** DESIGN ONLY. No code was modified. This document specifies exact drop-in code for a follow-up implementer (`@agent-csharp-dev`).

---

## (a) Anchors — the XP-grant path + how base pickup XP is defined

**XP-grant path (found):**

- `Assets/Scripts/pickupBehaviour.cs:26` — `worldState.instance.addXP(xpValue);` inside `OnTriggerEnter2D`. **This is the single XP-grant site for pickups.**
- `Assets/Scripts/worldState.cs:69-80` — `public void addXP(int amount)` accumulates `currentXP` and handles level-up. This is the shared sink; leaving it untouched keeps the +bonus additive and local to the pickup.

**Base pickup XP is a per-prefab serialized field, NOT a constant:**

- `Assets/Scripts/pickupBehaviour.cs:7` — `[SerializeField] private int xpValue = 1;`
- Confirmed distinct values baked into the four prefabs the user created:

  | Prefab | `xpValue` |
  |---|---|
  | `Assets/Prefabs/pickups/XP1.prefab` | 1 |
  | `Assets/Prefabs/pickups/XP2.prefab` | 2 |
  | `Assets/Prefabs/pickups/XP3.prefab` | 3 |
  | `Assets/Prefabs/pickups/XP4.prefab` | 4 |

So each drop type already carries its own base worth. The upgrade must add on top of `xpValue`, not replace it.

**Other `currentXP` readers (no change needed):** `Assets/Scripts/xpBarUI.cs:13-14` (display only).

---

## Interpretation of "+1 xp, max 4 per pickup" (clarification required by task)

Two readings are possible:

1. **Flat +N added to whatever a pickup grants** (bonus applies to every pickup type).
2. **Specific to the basic (XP1) pickup only.**

**Chosen: interpretation (1) — a flat +N added to whatever a pickup grants.** Justification:

- The feature says "making xp **drops** worth +1 xp" — "drops" (plural, generic), not "the basic drop". The natural reading is *every* pickup is worth +1 more.
- "so max would be 4 xp per pickup" is the worked example for the **basic** pickup (`XP1`, base `xpValue = 1`): `1 + 3 = 4`. It is an illustration of the cap on the smallest drop, not a global clamp to 4. Under interpretation (1) an `XP2` drop at full upgrade grants `2 + 3 = 5`, `XP3` → 6, `XP4` → 7. This is consistent and desirable (the upgrade scales all drops uniformly).
- Interpretation (1) needs **zero per-prefab logic** — one global bonus in `worldState`, added at the single grant site. Interpretation (2) would require the grant code to distinguish prefab types, which is fragile and contradicts "prefer no Editor change / drive everything from worldState + grant code."

**Composition rule:** `granted = xpValue + worldState.instance.XpBonus()`, where `XpBonus()` ∈ [0, 3].

---

## (b) worldState bonus field design (base 0, +1/step, cap 3) + getter

Add to `Assets/Scripts/worldState.cs`. Place the field next to `pierceBase` (line 26 area) and the step next to `pierceFlatStep` (line 41), mirroring the Pierce pattern.

```csharp
// --- add near pierceBase (line 26) ---
// Flat bonus XP added to EVERY pickup's xpValue at collection time.
// Base 0 (behavior unchanged); each XP-Gain upgrade adds +xpBonusStep; hard cap 3.
public int xpBonusPerPickup = 0;

// --- add near pierceFlatStep (line 41) ---
public int xpBonusStep = 1;              // flat-only XP-Gain upgrade step
public const int xpBonusCap = 3;         // max total bonus (base 1 pickup -> 4 XP)

// --- add near the getters (line 55, after Pierce()) ---
public int XpBonus() => xpBonusPerPickup;
```

- Base `0` → default grant unchanged (regression-safe).
- Cap `3` enforced at mutation time (see Choose) AND used as the pool-exclusion threshold (see BuildPool). `const` so both the menu and any future reader share one source of truth.

---

## (c) How the grant code adds the bonus

Single-line change in `Assets/Scripts/pickupBehaviour.cs`. Replace line 26:

```csharp
// before:
worldState.instance.addXP(xpValue);
// after:
worldState.instance.addXP(xpValue + worldState.instance.XpBonus());
```

`worldState.instance` is already null-guarded at `pickupBehaviour.cs:23`, so `XpBonus()` is safe here. No new fields on the prefab; `xpValue` stays per-prefab.

---

## (d) levelUpMenuController design

New flat-only `StatKind.XpGain`. Follows the exact Pierce pattern, plus a cap gate that removes it from the pool once `xpBonusPerPickup >= xpBonusCap`.

**1. Enum (line 7)** — add `XpGain`:

```csharp
private enum StatKind { MaxHP, FireRate, AttackDamage, MoveSpeed, Range, Defense, Regen, PickupRadius, Pierce, XpGain }
```

**2. `BuildPool()` (lines 50-85)** — add to `stats[]`, cap-gate the whole entry, and exclude from percent like Pierce. Drop-in replacement:

```csharp
private List<Upgrade> BuildPool()
{
    StatKind[] stats =
    {
        StatKind.MaxHP,
        StatKind.FireRate,
        StatKind.AttackDamage,
        StatKind.MoveSpeed,
        StatKind.Range,
        StatKind.Defense,
        StatKind.Regen,
        StatKind.PickupRadius,
        StatKind.Pierce,
        StatKind.XpGain
    };

    bool defenseHasBase = worldState.instance != null && worldState.instance.defenseBase > 0f;
    bool regenHasBase = worldState.instance != null && worldState.instance.regenBase > 0f;
    bool xpGainAtCap = worldState.instance != null && worldState.instance.xpBonusPerPickup >= worldState.xpBonusCap;

    List<Upgrade> pool = new List<Upgrade>();
    foreach (StatKind k in stats)
    {
        // XP-Gain is capped: once the bonus hits the cap, stop offering it entirely.
        if (k == StatKind.XpGain && xpGainAtCap) continue;

        // Flat is always offered.
        pool.Add(new Upgrade { kind = k, mode = Mode.Flat });

        // Pierce and XpGain are flat-only stats — never offer them as a percent.
        if (k == StatKind.Pierce) continue;
        if (k == StatKind.XpGain) continue;

        // Percent is inert on a 0 base for Defense/Regen — only offer once seeded.
        if (k == StatKind.Defense && !defenseHasBase) continue;
        if (k == StatKind.Regen && !regenHasBase) continue;

        pool.Add(new Upgrade { kind = k, mode = Mode.Percent });
    }

    return pool;
}
```

> Pool-size note: `OfferCount = 5`. Even with XpGain excluded at cap, the pool has 8 other flats + several percents (≥13 entries), so `pool[0..4]` is always safe.

**3. `LabelFor()` flat switch (after line 106)** — add case:

```csharp
case StatKind.XpGain:       return "+" + ws.xpBonusStep.ToString(ci) + " XP per Pickup";
```

(No percent case — XpGain never reaches the percent switch.)

**4. `Choose()` flat switch (after line 166, the Pierce case)** — add case with cap enforcement (defensive; BuildPool already prevents offering at cap):

```csharp
case StatKind.XpGain:
    ws.xpBonusPerPickup = Mathf.Min(worldState.xpBonusCap, ws.xpBonusPerPickup + ws.xpBonusStep);
    break;
```

`Mathf.Min` guarantees the bonus can never exceed 3 even if selected via a stale button reference — belt-and-suspenders alongside the BuildPool gate.

---

## (e) EXACT drop-in code — summary of every touched file

| File | Change | Anchor |
|---|---|---|
| `Assets/Scripts/worldState.cs` | add `xpBonusPerPickup=0`; `xpBonusStep=1`; `const xpBonusCap=3`; `XpBonus()` getter | near lines 26 / 41 / 55 |
| `Assets/Scripts/pickupBehaviour.cs` | `addXP(xpValue + worldState.instance.XpBonus())` | line 26 |
| `Assets/Scripts/levelUpMenuController.cs` | enum `+XpGain`; BuildPool cap-gate + percent-exclude; LabelFor flat case; Choose flat case | lines 7, 50-85, 106, 166 |

Full snippets are in sections (b), (c), (d) above — copy verbatim.

---

## (f) Existing pickup prefabs + Editor change needed?

**No Editor change is required.** The four prefabs (`XP1`–`XP4`) already carry correct per-prefab `xpValue` (1/2/3/4). The upgrade adds a **global** `worldState.xpBonusPerPickup`, applied at the single grant site in `pickupBehaviour.cs`. Nothing on the prefabs, the level-up menu prefab, or any inspector reference changes.

One caveat that is *not* an Editor change but must be verified by the implementer: `levelUpMenuController` requires `buttons`/`labels` arrays of length ≥ `OfferCount` (5). This is a pre-existing requirement, unaffected by adding `XpGain`. No new serialized fields are introduced on the menu component.

---

## (g) Grep acceptance (run after implementation)

```bash
# worldState: bonus field, step, cap, getter
grep -n "xpBonusPerPickup\|xpBonusStep\|xpBonusCap\|XpBonus" Assets/Scripts/worldState.cs
# grant site adds the bonus
grep -n "XpBonus" Assets/Scripts/pickupBehaviour.cs
# menu: new StatKind + cap gate + flat-only exclusion
grep -n "XpGain\|xpBonusPerPickup\|xpBonusCap" Assets/Scripts/levelUpMenuController.cs
```

Expected: `worldState.cs` shows 4+ hits (field, step, cap, getter); `pickupBehaviour.cs` shows 1 hit; `levelUpMenuController.cs` shows XpGain in enum/BuildPool/LabelFor/Choose plus the cap check.

Post-change compile check: Unity console clean (no `[Error]`/`[Exception]`), then Play-Mode smoke — collect an `XP1` drop before any upgrade (grants 1), take the "XP per Pickup" upgrade ×3, confirm the option disappears on the 4th level-up and an `XP1` drop now grants 4.

---

## (h) Risk note

- **Cap correctness:** enforced in TWO places — BuildPool stops *offering* at cap, and Choose clamps with `Mathf.Min`. Even a stale/double-clicked button cannot push the bonus past 3. Low risk.
- **`const` in enum context:** `worldState.xpBonusCap` is a `public const int` referenced from the menu; static-const access is fine on the plain (non-Mono) `worldState` class. No instance needed for the cap value itself, but the *current bonus* (`xpBonusPerPickup`) is read via `worldState.instance` — guarded in BuildPool (`worldState.instance != null`).
- **Interpretation risk:** if the user actually wanted the bonus to apply ONLY to the basic pickup, this design over-applies (it boosts XP2–XP4 too). Flagged in the interpretation section; chosen reading matches the plural wording and the "no Editor change" constraint. Cheap to revert if wrong (single grant-site line).
- **Balance:** at full upgrade every drop is +3 XP; combined with the existing `XP4` (base 4 → 7) this accelerates leveling noticeably. Purely a tuning concern — `xpBonusStep`/`xpBonusCap` live in `worldState` for easy adjustment; no code change to retune.
- **No new allocation / no per-frame cost:** bonus is read once per pickup collection.

---

**Handoff:** implementer = `@agent-csharp-dev` (file edits only, no Editor access needed — all three files are plain scripts). No `scene-architect` involvement; no prefab or scene edits.
