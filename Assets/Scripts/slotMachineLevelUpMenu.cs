using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Slot-machine level-up menu — BLIND PICK-BEFORE-SPIN with 5 reels.
//
// Flow:
//   1. OnEnable draws up to 5 distinct upgrades from upgradePool.BuildPool() and shows
//      the three symbol buttons (Circle / Triangle / Square). Reels sit idle.
//   2. The player picks ONE symbol (blind — they don't yet know which reel holds which
//      upgrade). All reels start spinning.
//   3. Any movement input or Space stops the reels and rolls final symbols. Reels whose
//      symbol matches the pick apply their upgrade.
//   4. Pity: a roll that lands <=1 match arms pity for the NEXT roll (gives a SLIGHT odds
//      boost toward more matching reels — never a guarantee); a roll with >=2 matches clears pity.
//
// The game is paused (Time.timeScale = 0) while this menu is open, so every wait uses
// WaitForSecondsRealtime and input is polled in Update.
public class slotMachineLevelUpMenu : MonoBehaviour
{
    public enum SlotSymbol { Circle, Triangle, Square }

    private enum Phase { AwaitPick, Spinning, Revealing, AwaitConfirm, Stopped }

    [Header("Reels")]
    [SerializeField] private Image[] _reels;               // 5 reel images
    [SerializeField] private Sprite[] _symbolSprites;      // 3 sprites, indexed by (int)SlotSymbol

    [Header("Symbol pick buttons")]
    [SerializeField] private Button[] _symbolButtons;      // 3 buttons: Circle / Triangle / Square

    [Header("Optional labels")]
    [SerializeField] private Text[] _reelLabels;           // optional per-reel upgrade labels
    [SerializeField] private Text _subtitle;               // "pick a symbol" subtitle, shown while the menu is open
    [SerializeField] private Text _spinHint;               // "press space to stop" hint, shown only while spinning

    [Header("Tuning")]
    [SerializeField] private float _spinFrameInterval = 0.07f;
    [SerializeField] private float _reelStopInterval = 0.18f;   // stagger between reel stops (left->right)
    [SerializeField] private float _revealDelay = 0.5f;         // pause before the winning-reel highlight
    [SerializeField] private Color _highlightColor = Color.yellow;
    [SerializeField] private float _pityMatchChance = 0.45f;    // per-reel pick odds under pity (baseline 1/3); slight boost, never a guarantee

    private Phase _phase;
    private SlotSymbol _picked;
    private Upgrade[] _reelUpgrades;
    private SlotSymbol[] _reelSymbols;
    private Coroutine[] _spinCoroutines;
    private Coroutine _revealCoroutine;
    private Coroutine _closeCoroutine;
    private bool _advanced;
    private int _activeReelCount;

    private void OnEnable()
    {
        // Reset all state for a fresh presentation.
        StopAllReels();
        StopReveal();
        StopClose();
        _advanced = false;
        ResetReelColors();
        _phase = Phase.AwaitPick;

        // Draw the pool and shuffle (Fisher-Yates) so reel upgrades are random each time.
        List<Upgrade> pool = upgradePool.BuildPool();
        if (pool == null) pool = new List<Upgrade>();
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            Upgrade tmp = pool[i];
            pool[i] = pool[j];
            pool[j] = tmp;
        }

        int reelCount = _reels != null ? _reels.Length : 0;
        // Use as many reels as we have upgrades for (guards pool < 5).
        _activeReelCount = Mathf.Min(reelCount, pool.Count);

        _reelUpgrades = new Upgrade[reelCount];
        _reelSymbols = new SlotSymbol[reelCount];
        _spinCoroutines = new Coroutine[reelCount];

        for (int i = 0; i < reelCount; i++)
        {
            if (i < _activeReelCount)
            {
                _reelUpgrades[i] = pool[i];
                SetReelLabel(i, upgradePool.LabelFor(pool[i]));
            }
            else
            {
                SetReelLabel(i, "");
            }
        }

        if (_spinHint != null) _spinHint.gameObject.SetActive(false);
        if (_subtitle != null)
        {
            _subtitle.text = "Pick a symbol!";
            _subtitle.gameObject.SetActive(true);
        }
        ShowSymbolButtons(true);
    }

    private void OnDisable()
    {
        // Coroutine-stop discipline: never let a reel spin (or a reveal run) on a disabled object.
        StopAllReels();
        StopReveal();
        StopClose();

        // Menu is now fully closed — RELEASE the held splash queue so the yellow "stat up"
        // texts begin animating only after this menu disappears. Safe even if nothing was
        // held (Release just clears the flag and drains any pending labels).
        if (upgradeSplashController.instance != null)
            upgradeSplashController.instance.Release();
    }

    private void Update()
    {
        if (_phase == Phase.Spinning)
        {
            // First press: stop the reels and begin the staged reveal. Do NOT resolve here.
            if (Input.GetKeyDown(KeyCode.Space))
            {
                _phase = Phase.Revealing;
                if (_spinHint != null) _spinHint.gameObject.SetActive(false);
                StopReveal();
                _revealCoroutine = StartCoroutine(RevealSequence());
            }
            return;
        }

    }

    // Wired to each symbol button's onClick in ShowSymbolButtons().
    public void Pick(SlotSymbol s)
    {
        if (_phase != Phase.AwaitPick) return;

        _picked = s;
        ShowSymbolButtons(false);
        _phase = Phase.Spinning;

        if (_subtitle != null) _subtitle.gameObject.SetActive(false);
        if (_spinHint != null)
        {
            _spinHint.text = "press space to stop";
            _spinHint.gameObject.SetActive(true);
        }

        if (_reels == null) return;
        for (int i = 0; i < _reels.Length; i++)
        {
            if (i < _activeReelCount && _reels[i] != null)
                _spinCoroutines[i] = StartCoroutine(SpinReel(_reels[i]));
        }
    }

    private IEnumerator SpinReel(Image reel)
    {
        WaitForSecondsRealtime wait = new WaitForSecondsRealtime(_spinFrameInterval);
        int frame = 0;
        while (true)
        {
            if (_symbolSprites != null && _symbolSprites.Length > 0)
            {
                Sprite s = _symbolSprites[frame % _symbolSprites.Length];
                if (s != null) reel.sprite = s;
            }
            frame++;
            yield return wait;
        }
    }

    // Phase 1 of the resolve: roll the final symbols (with pity/bias), then stop the reels
    // one-by-one left->right revealing each reel's upgrade label, pause, and highlight the
    // winning reels. Applies NOTHING — that waits for a separate Confirm press.
    private IEnumerator RevealSequence()
    {
        // Freeze the roll now so what the player sees is exactly what Confirm applies.
        bool pity = worldState.instance != null && worldState.instance.slotPityPending;

        // Under pity, each reel gets a SLIGHT probability bump toward the pick — no reel is
        // ever locked to the pick, so extra matches are only nudged, never guaranteed.
        for (int i = 0; i < _activeReelCount; i++)
        {
            SlotSymbol rolled;
            if (pity)
            {
                // Slightly raised probability of matching the pick during pity.
                rolled = Random.value < _pityMatchChance ? _picked : (SlotSymbol)Random.Range(0, 3);
            }
            else
            {
                rolled = (SlotSymbol)Random.Range(0, 3);
            }

            _reelSymbols[i] = rolled;
        }

        // Stop reels left->right, snapping each to its final symbol and showing its upgrade.
        WaitForSecondsRealtime stopWait = new WaitForSecondsRealtime(_reelStopInterval);
        for (int i = 0; i < _activeReelCount; i++)
        {
            if (_spinCoroutines != null && i < _spinCoroutines.Length && _spinCoroutines[i] != null)
            {
                StopCoroutine(_spinCoroutines[i]);
                _spinCoroutines[i] = null;
            }

            SnapReelSprite(i, _reelSymbols[i]);
            SetReelLabel(i, upgradePool.LabelFor(_reelUpgrades[i]));

            yield return stopWait;
        }

        // Beat before the payout highlight lands.
        yield return new WaitForSecondsRealtime(_revealDelay);

        // Highlight every reel (and its label) that matched the pick.
        for (int i = 0; i < _activeReelCount; i++)
        {
            if (_reelSymbols[i] != _picked) continue;

            if (_reels != null && i < _reels.Length && _reels[i] != null)
                _reels[i].color = _highlightColor;
            if (_reelLabels != null && i < _reelLabels.Length && _reelLabels[i] != null)
                _reelLabels[i].color = _highlightColor;
        }

        _revealCoroutine = null;
        _phase = Phase.AwaitConfirm;
        Confirm();
    }

    // Phase 2 of the resolve: commit the revealed result exactly once.
    private void Confirm()
    {
        if (_phase != Phase.AwaitConfirm) return;
        _phase = Phase.Stopped;   // terminal — guards against a double-fire

        // HOLD the splash queue so the yellow "stat up" texts enqueue silently while
        // this menu is still on screen; they're RELEASEd in OnDisable once the menu is
        // fully closed. Only the slot path holds — the regular level-up menu never does.
        if (upgradeSplashController.instance != null)
            upgradeSplashController.instance.Hold();

        // Apply the upgrade behind every reel that landed on the pick.
        int matches = 0;
        for (int i = 0; i < _activeReelCount; i++)
        {
            if (_reelSymbols[i] == _picked)
            {
                matches++;
                upgradePool.ApplyUpgrade(_reelUpgrades[i]);
            }
        }

        // Pity update: arm on a lone/no match, clear once the player lands a real payout.
        if (worldState.instance != null)
        {
            if (matches <= 1) worldState.instance.slotPityPending = true;
            else if (matches >= 2) worldState.instance.slotPityPending = false;
        }

        // Winners applied and splashes fired; close the menu after a short realtime beat.
        StopClose();
        _closeCoroutine = StartCoroutine(AutoCloseSequence());
    }

    // Auto-close: upgrades/splashes already fired in Confirm(); after a short realtime beat,
    // advance the level-up flow EXACTLY ONCE to close the menu. Guarded so it can't fire twice.
    private IEnumerator AutoCloseSequence()
    {
        yield return new WaitForSecondsRealtime(1.75f);
        _closeCoroutine = null;
        if (_advanced) yield break;
        _advanced = true;
        if (levelUpManager.instance != null)
            levelUpManager.instance.ApplyChoiceAndAdvance();
    }

    private void StopClose()
    {
        if (_closeCoroutine != null)
        {
            StopCoroutine(_closeCoroutine);
            _closeCoroutine = null;
        }
    }

    private void SnapReelSprite(int index, SlotSymbol symbol)
    {
        if (_reels == null || index < 0 || index >= _reels.Length) return;
        if (_reels[index] == null) return;
        if (_symbolSprites == null) return;

        int si = (int)symbol;
        if (si >= 0 && si < _symbolSprites.Length && _symbolSprites[si] != null)
            _reels[index].sprite = _symbolSprites[si];
    }

    private void SetReelLabel(int index, string text)
    {
        if (_reelLabels == null || index < 0 || index >= _reelLabels.Length) return;
        if (_reelLabels[index] != null) _reelLabels[index].text = text;
    }

    private void ShowSymbolButtons(bool show)
    {
        if (_symbolButtons == null) return;
        for (int i = 0; i < _symbolButtons.Length; i++)
        {
            Button b = _symbolButtons[i];
            if (b == null) continue;

            b.gameObject.SetActive(show);
            b.interactable = show;
            b.onClick.RemoveAllListeners();

            if (show)
            {
                // Capture the symbol for this button index (0=Circle,1=Triangle,2=Square).
                SlotSymbol symbol = (SlotSymbol)i;
                b.onClick.AddListener(() => Pick(symbol));
            }
        }
    }

    private void StopAllReels()
    {
        if (_spinCoroutines == null) return;
        for (int i = 0; i < _spinCoroutines.Length; i++)
        {
            if (_spinCoroutines[i] != null)
            {
                StopCoroutine(_spinCoroutines[i]);
                _spinCoroutines[i] = null;
            }
        }
    }

    private void StopReveal()
    {
        if (_revealCoroutine != null)
        {
            StopCoroutine(_revealCoroutine);
            _revealCoroutine = null;
        }
    }

    // Clear any leftover winning-reel highlight so a re-opened menu starts neutral.
    private void ResetReelColors()
    {
        if (_reels != null)
        {
            for (int i = 0; i < _reels.Length; i++)
                if (_reels[i] != null) _reels[i].color = Color.white;
        }
        if (_reelLabels != null)
        {
            for (int i = 0; i < _reelLabels.Length; i++)
                if (_reelLabels[i] != null) _reelLabels[i].color = Color.white;
        }
    }
}
