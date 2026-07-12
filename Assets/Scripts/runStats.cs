using UnityEngine;

// Run-scoped combat telemetry. Read by pauseStatsView for the pause/stats page.
// Implemented as a static class so any damage/death code path can record without a
// wired reference. Because static fields survive Unity Editor domain reloads, they
// are cleared via a RuntimeInitializeOnLoadMethod hook before each Play session so a
// new run never inherits the previous run's totals.
public static class runStats
{
    public static int  EnemiesKilled;
    public static long TotalDamage;

    // Average damage-per-second over the current run. Divisor is floored at 1s so an
    // opening-frame read can never divide by zero.
    public static float AvgDps()
    {
        return TotalDamage / Mathf.Max(1f, Time.timeSinceLevelLoad);
    }

    // Clears all run-scoped totals. Safe to call whenever a fresh run begins.
    public static void Reset()
    {
        EnemiesKilled = 0;
        TotalDamage   = 0;
    }

    // Runs before the first scene loads each Play session (post domain reload),
    // guaranteeing a zeroed baseline at run start without touching gameController/worldState.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLoad()
    {
        Reset();
    }
}
