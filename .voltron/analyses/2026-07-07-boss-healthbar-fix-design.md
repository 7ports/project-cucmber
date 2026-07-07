# Boss Health Bar — Root-Cause Analysis & Fix Design

**Date:** 2026-07-07
**Feature:** World-space floating health bar on `boss.prefab`
**Reported bug (verbatim):** "the boss's health bar doesn't currently work, please look into it"
**Analyst:** project-planner (Tier 2, design only)

---

## FIX = PREFAB-WIRING (Editor scene-architect)

> The C# is correct and every serialized reference in the prefab is wired correctly.
> The defect is a **missing Source Image sprite** on the `BarFill` (and `BarBG`) UI Image
> in `Assets/Prefabs/enemies/boss.prefab`. No code change is required.

---

## ROOT CAUSE

The `BarFill` Image is configured as **Image Type = Filled** but has **no sprite assigned**
(`m_Sprite: {fileID: 0}`). In `UnityEngine.UI.Image.OnPopulateMesh`, when
`activeSprite == null` Unity **early-returns to `Graphic.OnPopulateMesh` and draws a full quad,
completely ignoring `type` and `fillAmount`.** Filled rendering only runs when a sprite is present.

Result: `bossHealthBar` correctly computes and assigns `fill.fillAmount`, but the assignment
is a visual no-op. The red fill always renders as a full rectangle covering the dark background,
so the bar looks permanently 100% full and never depletes as the boss takes damage — i.e. "doesn't work."

### Evidence (grounded in the actual files)

- **`Assets/Prefabs/enemies/boss.prefab:223`** — `BarFill` Image `m_Sprite: {fileID: 0}` (NO sprite).
  - Same object confirms `m_Type: 3` (Filled, line 224) and `m_FillMethod: 0` (Horizontal, line 227) — the fill *config* is right; only the sprite is missing.
  - `BarFill` GameObject = fileID `4092787085342095040` (line 158); its Image component = fileID `2970737090952490346` (line 203).
- **`Assets/Prefabs/enemies/boss.prefab:68`** — `BarBG` Image also has `m_Sprite: {fileID: 0}` (renders as a plain quad; cosmetically OK today but should also get a sprite for consistency). `BarBG` Image = fileID `5552970641702208333`.
- **`Assets/Scripts/bossHealthBar.cs:20-21`** — fill logic is correct:
  ```csharp
  if (health != null && fill != null && health.MaxHp > 0)
      fill.fillAmount = Mathf.Clamp01((float)health.CurrentHp / health.MaxHp);
  ```
- **`Assets/Scripts/enemyHealth.cs:18-19,33,41`** — `MaxHp`/`CurrentHp` getters exist; `currentHp` decreases in `takeDamage`. HP source is valid.
- **Prefab wiring is all correct** — `boss.prefab:153-155`, the `bossHealthBar` MonoBehaviour (fileID `8268825732704047954`, on `HealthBarCanvas`):
  - `health: {fileID: 4855371742518237053}` → the boss's stripped `enemyHealth` (line 382) ✓
  - `fill: {fileID: 2970737090952490346}` → the `BarFill` Image ✓
  - `followTarget: {fileID: 439472164530480579}` → the boss root Transform (line 336) ✓
  - `HealthBarCanvas` Canvas `m_RenderMode: 2` (World Space, line 126), scale `0.01` (line 105), parented to the boss root (line 110) ✓
  - `maxHp` overridden to `250` on the boss instance (`boss.prefab:301-303`) ✓

**Conclusion:** Every reference and the Canvas/RectTransform setup are correct. The *only* thing
preventing the bar from working is the absent Source Image on the Filled `BarFill` Image.

---

## THE FIX (Editor steps — on `Assets/Prefabs/enemies/boss.prefab`)

Open the prefab (Prefab Mode) and set the Source Image on the fill (and background) Image:

1. Select **`HealthBarCanvas → BarFill`** (GameObject fileID `4092787085342095040`).
2. In the **Image** component:
   - Set **Source Image** to a sprite. Use the Unity built-in **`UISprite`** (the default rounded UI sprite) — or any solid white sprite in the project. (In YAML this becomes `m_Sprite: {fileID: 10907, guid: 0000000000000000f000000000000000, type: 0}`.)
   - Confirm **Image Type = Filled** (already set), **Fill Method = Horizontal** (already set), **Fill Origin = Left**, **Fill Amount = 1**.
3. Select **`HealthBarCanvas → BarBG`** (GameObject fileID `7308923123412508`) and set its Image **Source Image** to the same `UISprite` (Type stays Simple). Cosmetic/consistency — not strictly required for function, but recommended so the empty portion reads as a proper track.
4. **Save the prefab** (Ctrl/Cmd-S in Prefab Mode).

> Do NOT change `bossHealthBar.cs` or any serialized reference — they are correct.
> Assigning the sprite is the whole fix: with a sprite present, `Image.type=Filled` is honored and `fill.fillAmount` visibly drives the bar.

### Alternative (only if a "no-sprite" look is desired) — NOT recommended
Rewrite `bossHealthBar` to drive the fill via `RectTransform` width/`localScale` instead of `Image.fillAmount`. This makes it FIX = CODE, but it's more code, changes the prefab layout assumptions, and the sprite assignment above is simpler and idiomatic. Prefer the wiring fix.

---

## ACCEPTANCE CHECKLIST

Wiring verification (post-fix, inspect the prefab YAML):
- [ ] `grep -n "m_Sprite: {fileID: 0}" Assets/Prefabs/enemies/boss.prefab` returns **no** hit for the `BarFill` Image block (lines ~203-232). A non-zero `m_Sprite` fileID is present for `BarFill`.
- [ ] `BarFill` Image still shows `m_Type: 3` and `m_FillMethod: 0` (Filled / Horizontal) — sprite assignment must not have reset these.
- [ ] `bossHealthBar` references unchanged: `grep -n "fill: {fileID: 2970737090952490346}" Assets/Prefabs/enemies/boss.prefab` still matches.

Behavior verification (Play Mode, requires Editor):
- [ ] Spawn the boss; a dark track with a red fill appears ~1.2 units above it, billboarded to camera.
- [ ] Damage the boss; the red fill visibly shrinks left-to-right proportional to `CurrentHp/250`.
- [ ] At/near death the fill approaches empty (0), then the boss returns to pool via `enemyHealth.die()`.
- [ ] Bar follows the boss as it moves/leashes (position tracks each `LateUpdate`).

No-regression:
- [ ] No new compile errors (no code touched).
- [ ] `boss.prefab` still opens without missing-reference warnings.

---

## RISK NOTE

- **Low risk.** Change is a single serialized field (Source Image) on a prefab; no code, no reference rewiring, no API surface change.
- **Watch:** assigning a sprite via the Inspector can silently reset **Image Type** back to *Simple* on some Unity versions. After assigning, re-confirm `BarFill` Type = **Filled**, Method = **Horizontal**, Origin = **Left**, or the fill will render full again.
- If a project-owned white/rounded sprite is preferred over the built-in `UISprite`, ensure it's a `Sprite` (Texture Type = Sprite (2D and UI)) with full opacity; a texture imported as `Default` won't be assignable.
- Editor-only fix ⇒ must be applied on the host with a live Unity Editor (scene-architect via the `Agent` tool). Docker cannot assign the sprite through the Inspector, though the YAML edit itself is theoretically possible; prefer the Editor path so Type/Method are validated visually.

---

## HANDOFF

```json
{
  "handoff": true,
  "from_agent": "project-planner",
  "to_agent": "scene-architect",
  "reason": "Fix is Editor prefab-wiring (assign Source Image sprite to BarFill/BarBG on boss.prefab); requires a live Unity Editor Inspector, outside project-planner's design-only scope.",
  "next_task": "In Assets/Prefabs/enemies/boss.prefab Prefab Mode, set BarFill (GameObject 4092787085342095040) Image Source Image to the built-in UISprite; confirm Type=Filled, Fill Method=Horizontal, Origin=Left, Amount=1. Also set BarBG (7308923123412508) Source Image to UISprite. Do not modify bossHealthBar.cs or any serialized references. Save prefab; verify red fill depletes with boss HP in Play Mode.",
  "artifacts": [".voltron/analyses/2026-07-07-boss-healthbar-fix-design.md"]
}
```
