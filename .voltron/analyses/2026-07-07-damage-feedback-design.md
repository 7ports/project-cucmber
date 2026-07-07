# Damage Feedback Batch — Design (2026-07-07)

**Scope:** 4 items that add visual/perf feedback to the existing damage paths. Design-only; no source edited.

**Existing patterns to mirror**
- Pooling: `objectPool.instance.get(prefab,pos,rot)` / `ret(go)`; auto-adds `pooledObject` marker; `OnEnable()` re-inits pooled state (see `enemyHealth.OnEnable`, `projectileBehaviour.OnEnable`, `floatingText.OnEnable`). **Any pooled component MUST reset all mutable state in `OnEnable`.**
- Floating world text: `floatingText.cs` drives a world-space `CanvasGroup` prefab — rises via `travelOffset`, fades in/hold/out on `Time.unscaledDeltaTime`, self-returns to pool at end of life. `damageNumber` (Item 4) mirrors this exactly but sets a `-N` string + dark-red color.
- Damage entry points: `enemyHealth.takeDamage(int)` (from `projectileBehaviour.OnTriggerEnter2D`) → hooks for Items 2 & 4. `playerHealth.ApplyContactDamage()` (subtracts from `worldState.instance.currentHP`) → hook for Items 2 (player flash) & 3 (shake + screen flash).
- `worldState` is a plain C# class (not a MonoBehaviour), `worldState.instance.currentHP` / `.player`. No singleton MonoBehaviour for camera/UI feedback exists yet.

---

## Item 1 — Animator float threshold (perf/quality)

**Edit:** `Assets/Scripts/playerMovement.cs` only. No new files. No Editor work.

**Problem:** `Update()` calls `SetFloat("x",x)`/`SetFloat("y",y)` every frame even when values are unchanged (they're `GetAxisRaw`, so effectively only -1/0/1, but the pattern should be guarded).

**Design**
- Add serialized field `[SerializeField] private float _animThreshold = 0.1f;` (small enough to still cross blend-tree branch boundaries — raw axis jumps 0↔1, so any value in `(0,0.5)` is safe; 0.1 default).
- Reuse existing cache fields `oldX`/`oldY` (already declared, currently unused after Start) as the "last-set" values; initialize to a sentinel (e.g. `float.NaN`) in `Start()` so the first frame always writes.
- In `Update()`: read `x`,`y`; then
  - `if (Mathf.Abs(x - oldX) > _animThreshold) { playerAnimator.SetFloat("x", x); oldX = x; }`
  - `if (Mathf.Abs(y - oldY) > _animThreshold) { playerAnimator.SetFloat("y", y); oldY = y; }`
- Movement (`FixedUpdate`) is unchanged — it still uses the live `x`/`y`, not the cached values.

**Hooks:** none external. **Editor prereqs:** none (field has a default; existing `x`/`y` params already drive the 2D blend tree).

---

## Item 2 — Entity damage flash (enemies + player)

**Create:** `Assets/Scripts/damageFlash.cs` (MonoBehaviour, pool-safe).
**Edit:** `enemyHealth.cs`, `playerHealth.cs`.

**`damageFlash` design**
- Fields: `[SerializeField] private SpriteRenderer _sr;` `[SerializeField] private Color _flashColor = Color.red;` `[SerializeField] private float _flashDuration = 0.12f;`
- Cache `private Color _baseColor;` captured in `Awake()` (not `OnEnable` — see reset note).
- `public void Flash()` — (re)starts the tint: sets `_sr.color = _flashColor`, then lerps back to `_baseColor` over `_flashDuration`. Implement via a single coroutine handle (`StopCoroutine` any running one first) OR a timer in `Update()`; **coroutine must be stoppable** per CLAUDE.md. Prefer a timer-in-`Update` approach so it's pool-safe without coroutine bookkeeping: `Flash()` sets `_timer = _flashDuration`; `Update()` lerps `_sr.color` from `_flashColor`→`_baseColor` as `_timer` decays; when `_timer<=0` force `_sr.color = _baseColor`.
- **Pooled reset:** `OnEnable()` → `_timer = 0; if (_sr) _sr.color = _baseColor;` so a reused enemy never spawns mid-flash. Auto-resolve `_sr = GetComponent<SpriteRenderer>()` in `Awake` if unassigned.

**Hooks**
- `enemyHealth.takeDamage(int)`: after `currentHp -= amount;`, before the die check, call cached `damageFlash` → add `private damageFlash _flash;` resolved in `OnEnable`/`Awake` via `GetComponent`, call `_flash?.Flash();`. (Only flash if it survives; if it dies the enemy is pooled away anyway — flashing before die() is harmless since die() returns it.)
- `playerHealth.ApplyContactDamage()`: after applying damage (`worldState.instance.currentHP -= dmg`), call a cached `damageFlash` on the player (`GetComponent<damageFlash>()` in `Awake`) → `_flash?.Flash();`.

**Editor prereqs**
- Add `damageFlash` component to: `slime.prefab`, `chaser.prefab` (each has a SpriteRenderer), and the **Player** GameObject in `SampleScene` (has a SpriteRenderer). Assign the SpriteRenderer ref (or rely on `Awake` auto-resolve). Prefab edits = scene-architect; verify no missing refs via build-validator.

---

## Item 3 — Screen shake + red flash on PLAYER damage (configurable)

**Create:** `Assets/Scripts/cameraShake.cs` (on Main Camera), `Assets/Scripts/screenFlash.cs` (on a full-screen UI Image).
**Edit:** `playerHealth.cs` (trigger both from `ApplyContactDamage`).

**`cameraShake` design** (must respect the follow-cam, which sets camera position each frame)
- Singleton-ish: `public static cameraShake instance;` set in `Awake` (mirrors `levelUpManager`/`objectPool` pattern), so `playerHealth` can call it without a serialized ref.
- Serialized defaults: `[SerializeField] float _defaultMagnitude = 0.25f;` `[SerializeField] float _defaultDuration = 0.2f;`
- `public void Shake()` / `public void Shake(float magnitude, float duration)` — starts a decaying shake. Store `_magnitude`, `_duration`, `_elapsed=0`.
- Apply as a **local offset in `LateUpdate`** (runs after the follow script moves the camera): keep `_shakeOffset`; each `LateUpdate` compute `_shakeOffset = Random.insideUnitCircle * currentMag` where `currentMag = _magnitude * (1 - _elapsed/_duration)` (linear decay), then `transform.position += (Vector3)_shakeOffset` (z untouched). Because the follow cam re-sets position next frame, offset naturally does not accumulate — but to be safe, subtract the previous frame's offset first (`transform.position -= _prevOffset; transform.position += _shakeOffset; _prevOffset = _shakeOffset`). Use `Time.deltaTime` (normal time — damage occurs while unpaused; menu pause stops damage anyway). When `_elapsed>=_duration`, zero the offset.

**`screenFlash` design**
- On a full-screen `Image` (stretched RectTransform) under a screen-space overlay Canvas, high sort order, `raycastTarget=false`.
- Singleton `public static screenFlash instance;` in `Awake`.
- Serialized: `[SerializeField] Image _img;` `[SerializeField] Color _flashColor = new Color(1,0,0,1);` `[SerializeField] float _maxAlpha = 0.4f;` `[SerializeField] float _flashDuration = 0.35f;`
- `public void Flash()` / `Flash(float intensity, float duration)` — set `_timer = duration`, `_intensity = intensity`. `Update()` sets `_img.color = _flashColor with alpha = _maxAlpha * _intensity * (_timer/_duration)` (fade out). Start with alpha 0; force 0 on finish. Use normal `Time.deltaTime`.

**Hook**
- `playerHealth.ApplyContactDamage()` after damage applied: `cameraShake.instance?.Shake(); screenFlash.instance?.Flash();`. (Same call site as Item 2 player flash.)

**Editor prereqs**
- Add `cameraShake` to the Main Camera / `playerCam` in `SampleScene`. Confirm the follow script writes position in `Update`/`LateUpdate` earlier than `cameraShake.LateUpdate` (if follow is also `LateUpdate`, set script execution order or move follow to `Update`).
- Create a screen-space-overlay Canvas (or reuse HUD Canvas) with a stretched red `Image` (alpha 0, raycast off), attach `screenFlash`, assign `_img`. scene-architect builds the UI; build-validator confirms no NRE and the flash renders above HUD.

**Timescale note:** both use normal `Time.deltaTime`. Player damage cannot occur while paused (level-up menu sets `timeScale=0`, contact ticks freeze), so no need for unscaled time. If a shake is somehow in progress when a pause hits it simply freezes and resumes — acceptable.

---

## Item 4 — Pooled damage numbers

**Create:** `Assets/Scripts/damageNumber.cs` + a new pooled prefab `Assets/damageNumber.prefab` (mirror of the floating-text prefab).
**Edit:** `enemyHealth.cs` (spawn on hit).

**`damageNumber` design** (clone of `floatingText` + a settable label)
- Same rise/fade lifecycle as `floatingText` (copy fields: `group` CanvasGroup, `fadeIn/travel/hold/fadeOut`, `travelOffset`, `Time.unscaledDeltaTime`, self-`ret` at end of life). Keep it pool-safe: reset `timer`, `startPos`, `alpha` in `OnEnable` (already the floatingText pattern).
- Add `[SerializeField] private TMP_Text _label;` (or UGUI `Text` if project isn't on TMP — see open questions) and `[SerializeField] private Color _color = new Color(0.55f, 0f, 0f, 1f);` (dark red).
- `public void Set(int amount)` — `_label.text = "-" + amount; _label.color = _color;`. Called by the spawner **immediately after** `get()` (which activates the object and fires `OnEnable`); because `OnEnable` doesn't touch the text, calling `Set` right after is safe.
- Because color is on the label (not CanvasGroup), fade uses the existing `CanvasGroup.alpha` — color stays dark red, alpha animates. Good.

**Hook**
- `enemyHealth.takeDamage(int amount)`: after `currentHp -= amount;`, spawn:
  ```
  if (objectPool.instance != null && _damageNumberPrefab != null) {
      var go = objectPool.instance.get(_damageNumberPrefab, transform.position + _dmgTextOffset, Quaternion.identity);
      go.GetComponent<damageNumber>()?.Set(amount);
  }
  ```
  Add `[SerializeField] private GameObject _damageNumberPrefab;` and `[SerializeField] private Vector3 _dmgTextOffset = new Vector3(0,0.5f,0);`. Spawn on every hit (including the killing blow) so the last hit still shows its number.

**Editor prereqs**
- Build `damageNumber.prefab`: world-space Canvas (or same setup as the existing floating-text/LEVEL-UP prefab), a `TMP_Text`/`Text` child, a `CanvasGroup`, and the `damageNumber` component with `group`+`_label` wired. Register nothing special — pooling is automatic via `pooledObject`.
- Assign `_damageNumberPrefab` on `slime.prefab` and `chaser.prefab` `enemyHealth`. scene-architect creates prefab + wires refs; build-validator confirms numbers appear on hit and return to pool.

---

## Task list (dependency-ordered)

**C# — Docker / `csharp-dev` (file edits, no Editor):**
1. Item 1 — edit `playerMovement.cs` (threshold + `oldX/oldY` cache). *(independent)*
2. Item 2 — create `damageFlash.cs`; edit `enemyHealth.cs` + `playerHealth.cs` to call `Flash()`. *(create script before edits)*
3. Item 4 — create `damageNumber.cs`; edit `enemyHealth.cs` to spawn+`Set()`. *(share `enemyHealth` edit window with Item 2 to avoid two passes)*
4. Item 3 — create `cameraShake.cs` + `screenFlash.cs`; edit `playerHealth.cs` to call `Shake()`+`Flash()`. *(share `playerHealth` edit with Item 2)*

> Ordering note: `enemyHealth.cs` is touched by Items 2 & 4; `playerHealth.cs` by Items 2 & 3. Batch each file's edits into one csharp-dev pass. All new scripts must exist before the prefab/scene wiring below.

**Editor — host / `scene-architect` (prefab + scene wiring), then `build-validator`:**
5. Item 2 wiring — add `damageFlash` to `slime.prefab`, `chaser.prefab`, and Player object (`SampleScene`). *(prefab + scene)*
6. Item 4 wiring — create `damageNumber.prefab` (world-space canvas + TMP/Text + CanvasGroup + `damageNumber`); assign it + offset on both enemy prefabs' `enemyHealth`. *(new prefab + prefab wiring)*
7. Item 3 wiring — add `cameraShake` to Main Camera; create/reuse overlay Canvas + full-screen red Image + `screenFlash`, wire `_img`; verify script execution order vs follow cam. *(scene wiring)*
8. `build-validator` — Play Mode smoke: 0 compile errors, enemy hit shows dark-red `-N` + flash, player contact shakes + red-pulses, pooled reuse resets color/text cleanly, no NRE.

*(Item 1 needs no Editor step.)*

---

## Open questions / decisions for the user (with defaults)

1. **Animator threshold value** — default `0.1f`. (Raw axis is -1/0/1 so anything in (0,0.5) works; low value keeps responsiveness.)
2. **Enemy/player flash color & duration** — default `Color.red`, `0.12s`. OK, or a lighter tint / longer hold?
3. **Damage-number color / size / rise** — default dark red `(0.55,0,0)`, reuse floatingText's rise (`+1.0y` over ~0.6s travel, ~1.2s total life). Font size TBD by prefab; suggest ~4–5 (world units) matching existing floating text.
4. **Screen-shake magnitude / duration** — defaults `0.25` units / `0.2s`, linear decay. Bigger/juicier?
5. **Screen-flash intensity / duration** — default red at `maxAlpha 0.4`, `0.35s` fade. Too strong/weak?
6. **Text component: TMP vs UGUI `Text`** — existing `floatingText` uses a `CanvasGroup` but its label type isn't in the files read. `damageNumber` should match whatever the LEVEL-UP floating-text prefab uses (assume **TMP_Text** unless the existing prefab uses UGUI `Text`). scene-architect to confirm when cloning.
7. **Is the damage number a brand-new pooled prefab?** — **Yes** (default): a new `damageNumber.prefab`, mirroring the floating-text prefab, pooled the same way. (Alternative: parameterize the existing floating-text prefab — rejected: it has a fixed "LEVEL UP!" string and different lifetime.)
8. **Shake vs follow-cam ordering** — default: apply shake as an offset in `LateUpdate` with prev-offset subtraction. If the follow script also runs in `LateUpdate`, set Script Execution Order so shake runs after follow (flag for scene-architect).

---

## Self-validation
- Doc exists at `.voltron/analyses/2026-07-07-damage-feedback-design.md`.
- 4 `## Item` sections present.
- Docker-vs-Editor task list present (steps 1–4 C#, 5–8 Editor).
- Open-questions section with 8 defaulted decisions.
- No source files modified (design-only).
