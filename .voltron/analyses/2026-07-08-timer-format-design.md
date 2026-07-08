# Timer Format Design — `Assets/Scripts/timerUI.cs`

**Date:** 2026-07-08
**Agent:** project-planner (Tier 2, DESIGN ONLY — no implementation)
**Task (verbatim):** "fix the timer script I added to show time in the regular 00:00:00 format where it goes mins:seconds:milliseconds"

---

## 0. Decision up front — the "milliseconds" segment

The requested shape is **three two-digit segments** → `00:00:00`.

True milliseconds are **3 digits** (000–999) and would not fit the two-digit box — you'd get `00:00:000` (a nine-char, uneven string). To keep the exact `00:00:00` shape the user asked for, the third segment must be **HUNDREDTHS of a second = centiseconds (cc), range 00–99, two digits.**

- **Chosen:** `mm:ss:cc` (minutes : seconds : centiseconds).
- Example: 2 min 7 sec + 0.34 s fractional → **`02:07:34`** (matches the brief's example exactly).
- This is what stopwatches colloquially call "milliseconds" but is really the fractional 1/100 s field.

> If the user genuinely wants 3-digit milliseconds, the shape becomes `mm:ss:fff` (`02:07:340`) — flagged in Open Questions. Default deliverable below is centiseconds to honor the literal `00:00:00` request.

---

## 1. Anchors (grounded in the real file)

| Concern | Line | Current code |
|---|---|---|
| **Time SOURCE** | 16–17 | `Time.timeSinceLevelLoad` (seconds since level load, `float`) |
| **Minutes calc** | 16 | `int mins = Mathf.RoundToInt(Time.timeSinceLevelLoad/60);` |
| **Seconds calc** | 17 | `int seconds = Mathf.RoundToInt(Time.timeSinceLevelLoad - (mins*60));` |
| **Current FORMAT / assignment** | 19 | `timerText.text = "" + mins + ":" + seconds;` |
| **Text FIELD** | 6 | `[SerializeField] private TextMeshProUGUI timerText;` — **TMP**, not legacy `UI.Text` |
| **Init** | 10 | `timerText.text = "00:00:00";` (already correct placeholder) |

### Bugs in the current code (why it looks wrong at runtime)
1. **`RoundToInt` on minutes** rounds to nearest minute — at t=30 s it shows `1:` (one minute) instead of `0:`. Must be **floor**, not round.
2. **Seconds derived from rounded minutes** — because `mins` is rounded, `Time - mins*60` can go **negative** (e.g. t=31 s → mins=1 → seconds = 31−60 = −29). Broken.
3. **No zero-padding** — shows `2:7` instead of `02:07`.
4. **Only two segments** — the centiseconds field is entirely missing.

---

## 2. Drop-in replacement (produces `mm:ss:cc`)

Replace the body of `Update()` (lines 16–19). Keep `Start()` and the `timerText` field as-is.

```csharp
void Update()
{
    float t = Time.timeSinceLevelLoad;

    int minutes    = Mathf.FloorToInt(t / 60f);        // whole minutes
    int seconds    = Mathf.FloorToInt(t) % 60;         // 0–59
    int hundredths = Mathf.FloorToInt(t * 100f) % 100; // centiseconds, 0–99

    timerText.text = $"{minutes:00}:{seconds:00}:{hundredths:00}";
}
```

**Equivalent without interpolation** (identical output):

```csharp
timerText.text = string.Format("{0:00}:{1:00}:{2:00}", minutes, seconds, hundredths);
```

### Why each term
- `Mathf.FloorToInt(t / 60f)` — floor, not round → minutes only tick over at true 60 s boundaries.
- `Mathf.FloorToInt(t) % 60` — floor the whole seconds first, then wrap 0–59. Independent of `minutes`, so it can never go negative.
- `Mathf.FloorToInt(t * 100f) % 100` — scales to centiseconds then wraps 0–99 → the two-digit fractional field.
- `:00` format specifier — zero-pads each `int` to two digits (`7` → `07`).

Verified against the brief's example: t = 127.34 s → minutes = 2, seconds = 127%60 = 7, hundredths = 12734%100 = 34 → **`02:07:34`** ✓

---

## 3. Grep acceptance checklist

Run after implementation (`csharp-dev`) against `Assets/Scripts/timerUI.cs`:

```bash
# 1. Floor (not Round) used for minutes and seconds
grep -q 'FloorToInt' Assets/Scripts/timerUI.cs && echo "OK floor"
# 2. No RoundToInt left behind
! grep -q 'RoundToInt' Assets/Scripts/timerUI.cs && echo "OK no round"
# 3. Three padded segments in one format expression
grep -Eq '\{minutes:00\}:\{seconds:00\}:\{hundredths:00\}|\{0:00\}:\{1:00\}:\{2:00\}' Assets/Scripts/timerUI.cs && echo "OK format"
# 4. Centiseconds term present
grep -q 't \* 100f' Assets/Scripts/timerUI.cs && echo "OK centiseconds"
# 5. Still assigns to the TMP field
grep -q 'timerText.text' Assets/Scripts/timerUI.cs && echo "OK assignment"
# 6. Source unchanged (still timeSinceLevelLoad)
grep -q 'Time.timeSinceLevelLoad' Assets/Scripts/timerUI.cs && echo "OK source"
```

Manual/Play-Mode check (build-validator): timer reads `00:00:00` at start and increments as `mm:ss:cc`; at ~1 min rollover it reads `01:00:xx` (not `00:60:xx`).

---

## 4. Update-vs-cached concern

- **Per-frame churn:** `Update()` rebuilds the string every frame (~60/s). The centiseconds field changes almost every frame anyway, so a "only update when the value changed" guard buys little here — but string allocation each frame is minor GC pressure. **Acceptable for this small game; no change required.** If GC ever matters, cache the last `hundredths` value and early-out, or use a `StringBuilder`/TMP `SetText` with numeric args to avoid boxing.
- **Time source semantics:** `Time.timeSinceLevelLoad` **respects `Time.timeScale`** — it freezes when the game is paused (timeScale=0) and slows in slow-mo. For a *run timer* that is usually the desired behavior. If the timer must keep counting during pause menus, switch the source to `Time.unscaledTime` (flagged in Open Questions). Source is left as `timeSinceLevelLoad` per the brief.
- **Placement:** logic stays in `Update()` as the file already does; no coroutine or caching architecture change is warranted for a one-line display.

---

## 5. Open Questions (need human input before / not blocking implement)
1. **3-digit ms vs 2-digit centiseconds?** Deliverable uses **centiseconds** to fit the literal `00:00:00` shape. Confirm if true milliseconds (`mm:ss:fff`) are wanted instead.
2. **Pause behavior:** keep `timeSinceLevelLoad` (freezes on pause) or move to `unscaledTime` (counts through pause)?

---

## Handoff

Design complete. Implementation belongs to **`@agent-csharp-dev`**: apply the §2 replacement to lines 16–19 of `Assets/Scripts/timerUI.cs`, then **`@agent-build-validator`** confirms compile + Play-Mode display. No files were modified by this planning pass.
