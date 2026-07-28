# Analysis: timescale-selector-gap

**Date:** 2026-07-15 · **Analyst:** code-analyst · **Branch:** slot-machine-levlup
**Request:** Pause-menu radio selection for 1x/2x/3x gameplay timescale; must reset to 1x on game reset / fresh startup.

> Note: `mcp__project-voltron__submit_analysis` / `append_journal` were unavailable in this run; report persisted directly to this file.

## Summary

The C# side of this feature is **already complete**. `Assets/Scripts/timescaleController.cs` (untracked, new file) implements the full multiplier model: a {1,2,3} clamp, UI-ready public setters (`SetSpeedMultiplier(int)`, `SetSpeed1x/2x/3x()`, `ResetToDefault()`), startup application of the 1x default, and natural reset-to-1x on scene reload because the selection lives in a non-static instance field. All three resume paths (pause, level-up, item-choice) already read `timescaleController.RunningTimeScale`. **No script anywhere references the setters** — the entire remaining gap is scene/UI work in the Unity Editor: place the controller GameObject, build the 1x/2x/3x toggle group in the pause menu, and wire persistent listeners.

## Q&A with evidence

### 1. Value clamp — YES
`timescaleController.cs:73-76`:
```csharp
private static int Sanitize(int multiplier)
{
    return (multiplier == 2 || multiplier == 3) ? multiplier : 1;
}
```
Applied in `Awake()` (:32) and `SetSpeedMultiplier` (:47). Fields and defaults:
```csharp
[SerializeField] private int _defaultMultiplier = 1;   // :16
private int _selectedMultiplier = 1;                   // :18
```

### 2. UI setter API — YES, complete
- `public void SetSpeedMultiplier(int multiplier)` — `timescaleController.cs:45`
- `public void SetSpeed1x()` / `SetSpeed2x()` / `SetSpeed3x()` — `:52-54` (parameterless, made for persistent listeners)
- `public void ResetToDefault()` — `:57`

Caution for scene-architect: the parameterless setters do not check `Toggle.isOn`, so with a ToggleGroup the deselected toggle's listener also fires. Unity's event order (deselected fires before selected) makes the end state correct, but if flakiness appears, a small C# follow-up adding `SetSpeed1x(bool isOn)`-style guarded overloads is the fix.

### 3. Reset-to-1x on game reset — HOLDS
- `_selectedMultiplier` is a **non-static, non-serialized instance field** initialized to 1 (`:18`); the class has no `DontDestroyOnLoad`.
- `gameOverManager.cs:22` `Restart()` reloads the active scene → old controller destroyed → new instance's `Awake()` (`:29-33`) re-derives the value from `_defaultMultiplier` (1).
- Belt-and-braces: `gameOverManager.cs:20` also sets `Time.timeScale = 1f` before the reload.
- The static `instance` reference (`:14`) is reassigned in the new `Awake()`, so no stale-instance leak.

Conditional: this holds **only if a timescaleController component actually exists in SampleScene** — needs Editor verification (see Q5/Q6). If absent, `RunningTimeScale` safely falls back to 1f (`:24`) but the selector feature is inert.

### 4. Startup default — YES
`timescaleController.cs:35-39` `Start()` calls `ApplyIfRunning()`, which sets `Time.timeScale = _selectedMultiplier` (= 1) unless a menu has frozen time (`:66-70`).

### 5. Pause-menu UI wiring — ABSENT in code
`pauseMenuController.cs` has no serialized `Toggle`/`ToggleGroup` fields and never references `timescaleController` setters (its only touchpoint is the resume read at `:43`). A project-wide grep for `SetSpeed|Toggle|radio` finds **zero** callers of the setter API in any script. Wiring is designed to be scene-side persistent listeners (per the controller's own doc comment, `:41-44`), so no C# is missing — but whether the scene contains the controller GameObject and any toggle UI **cannot be confirmed from code; needs Editor verification**.

### 6. Remaining work

**C# gaps (Docker):** NONE required. Optional only: bool-guarded toggle overloads in `timescaleController.cs` if ToggleGroup event-order issues surface (see Q2 caution).

**Scene/UI gaps (Editor — scene-architect):**
- Verify/add a `timescaleController` GameObject in `Assets/Scenes/SampleScene.unity` with the component attached.
- Build the radio UI under `pauseRoot`: three `Toggle`s (1x/2x/3x) sharing one `ToggleGroup`, 1x toggle `isOn = true` by default (scene default = auto-reset of visual state on reload).
- Wire persistent listeners: 1x → `timescaleController.SetSpeed1x`, 2x → `SetSpeed2x`, 3x → `SetSpeed3x`.
- Save scene; validate via build-validator (compile clean, pause → select 2x → resume runs at 2x, restart returns to 1x).

## Findings

| Severity | Description | File |
|---|---|---|
| info | Multiplier clamp to {1,2,3} implemented and applied on Awake + set | Assets/Scripts/timescaleController.cs:73 |
| info | Full UI setter API exists (SetSpeedMultiplier, SetSpeed1x/2x/3x, ResetToDefault) | Assets/Scripts/timescaleController.cs:45 |
| info | Reset-to-1x on restart holds via instance-field + scene reload; Restart also forces timeScale=1 | Assets/Scripts/gameOverManager.cs:20 |
| medium | No code-side toggle wiring exists; scene must contain controller GO + ToggleGroup UI — Editor work outstanding | Assets/Scripts/pauseMenuController.cs:5 |
| low | Parameterless toggle setters don't check isOn; ToggleGroup deselect events also fire them (order-dependent but currently correct) | Assets/Scripts/timescaleController.cs:52 |
