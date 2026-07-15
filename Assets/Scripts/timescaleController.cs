using UnityEngine;

/// <summary>
/// Gameplay timescale selector (1x / 2x / 3x) for testing / QOL.
/// Holds the player-selected running-game speed multiplier. Menu-freeze systems
/// (pause, level-up, item-choice) remain the sole owners of the *paused*
/// Time.timeScale = 0 state; on resume they read <see cref="RunningTimeScale"/>
/// so the game resumes at the selected speed instead of a hardcoded 1x.
/// The multiplier always defaults to 1x on a fresh scene load, so game reset /
/// restart (which reloads the scene) naturally returns to 1x.
/// </summary>
public class timescaleController : MonoBehaviour
{
    public static timescaleController instance;

    [SerializeField] private int _defaultMultiplier = 1;   // fresh-startup default (1x)

    private int _selectedMultiplier = 1;

    /// <summary>
    /// The timescale a resume/un-pause path should restore. Falls back to 1x
    /// if no controller is present, so callers are always safe.
    /// </summary>
    public static float RunningTimeScale => instance != null ? instance._selectedMultiplier : 1f;

    /// <summary>Currently selected multiplier (1, 2, or 3).</summary>
    public int SelectedMultiplier => _selectedMultiplier;

    private void Awake()
    {
        instance = this;
        _selectedMultiplier = Sanitize(_defaultMultiplier);
    }

    private void Start()
    {
        // Fresh startup: make the running game reflect the default (1x).
        ApplyIfRunning();
    }

    /// <summary>
    /// UI setter — bind a Toggle/Button persistent listener to this with a fixed
    /// int argument (1, 2, or 3). Out-of-range values clamp to 1x.
    /// </summary>
    public void SetSpeedMultiplier(int multiplier)
    {
        _selectedMultiplier = Sanitize(multiplier);
        ApplyIfRunning();
    }

    // Parameterless convenience setters for Button.onClick / Toggle persistent listeners.
    public void SetSpeed1x() => SetSpeedMultiplier(1);
    public void SetSpeed2x() => SetSpeedMultiplier(2);
    public void SetSpeed3x() => SetSpeedMultiplier(3);

    /// <summary>Force the multiplier back to 1x (game reset). Also applies if running.</summary>
    public void ResetToDefault()
    {
        _selectedMultiplier = 1;
        ApplyIfRunning();
    }

    // Only drive Time.timeScale while the game is actually running. If a menu has
    // frozen time (timeScale == 0) we leave it alone; that menu's resume path will
    // read RunningTimeScale when it un-freezes.
    private void ApplyIfRunning()
    {
        if (!Mathf.Approximately(Time.timeScale, 0f))
            Time.timeScale = _selectedMultiplier;
    }

    // Clamp to the allowed set {1, 2, 3}; anything else becomes 1x.
    private static int Sanitize(int multiplier)
    {
        return (multiplier == 2 || multiplier == 3) ? multiplier : 1;
    }
}
