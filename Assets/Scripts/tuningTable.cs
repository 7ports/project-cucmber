using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

/// <summary>
/// Static runtime loader for StreamingAssets/tuning.csv. Parses key,value rows
/// (extra columns such as type/comment are ignored) into a lookup, and exposes
/// typed getters that report failure so callers keep their in-code defaults.
/// Loads lazily and once; never throws. Call <see cref="Reload"/> to re-read the
/// file on disk (e.g. from a future Editor exporter or a manual refresh).
/// </summary>
public static class tuningTable
{
    private static readonly Dictionary<string, string> _values = new Dictionary<string, string>();
    private static bool _loaded;

    private static void EnsureLoaded()
    {
        if (_loaded) return;
        Load();
    }

    /// <summary>
    /// Forces a fresh read of tuning.csv from StreamingAssets, replacing the
    /// in-memory table. Safe to call at any time; never throws.
    /// </summary>
    public static void Reload()
    {
        _loaded = false;
        Load();
    }

    private static void Load()
    {
        _values.Clear();
        _loaded = true;

        string path = Path.Combine(Application.streamingAssetsPath, "tuning.csv");

        string text;
        try
        {
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[tuningTable] tuning.csv not found at '{path}' — using in-code defaults.");
                return;
            }
            text = File.ReadAllText(path);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[tuningTable] Failed to read tuning.csv ('{path}'): {e.Message} — using in-code defaults.");
            return;
        }

        string[] lines = text.Split('\n');
        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim();
            if (line.Length == 0) continue;   // skip blank lines
            if (line[0] == '#') continue;      // skip comment / section-header lines

            int firstComma = line.IndexOf(',');
            if (firstComma < 0) continue;      // no separator — not a data row

            string key = line.Substring(0, firstComma).Trim();
            if (key.Length == 0) continue;
            if (key == "key") continue;        // skip the header row (key,value,...)

            string rest = line.Substring(firstComma + 1);
            int secondComma = rest.IndexOf(',');
            string value = (secondComma < 0 ? rest : rest.Substring(0, secondComma)).Trim();

            _values[key] = value;              // last row wins on duplicate keys
        }
    }

    /// <summary>
    /// Outputs the CSV float for <paramref name="key"/> and returns true; returns
    /// false when the key is absent or its value is malformed, in which case the
    /// caller should keep its own default. A malformed value logs one warning.
    /// </summary>
    public static bool TryGetFloat(string key, out float value)
    {
        value = 0f;
        EnsureLoaded();

        if (!_values.TryGetValue(key, out string raw)) return false;

        if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            return true;

        Debug.LogWarning($"[tuningTable] Malformed float for key '{key}': '{raw}' — keeping default.");
        value = 0f;
        return false;
    }

    /// <summary>
    /// Outputs the CSV int for <paramref name="key"/> and returns true; returns
    /// false when the key is absent or its value is malformed, in which case the
    /// caller should keep its own default. Whole-number floats (e.g. "5.0") are
    /// accepted and rounded; genuine decimals are rejected. Logs one warning on
    /// a malformed value.
    /// </summary>
    public static bool TryGetInt(string key, out int value)
    {
        value = 0;
        EnsureLoaded();

        if (!_values.TryGetValue(key, out string raw)) return false;

        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            return true;

        // Accept whole-number floats ("5.0" -> 5); reject true decimals.
        if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float f)
            && Mathf.Approximately(f, Mathf.Round(f)))
        {
            value = Mathf.RoundToInt(f);
            return true;
        }

        Debug.LogWarning($"[tuningTable] Malformed int for key '{key}': '{raw}' — keeping default.");
        value = 0;
        return false;
    }
}
