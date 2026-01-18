using System;
using TMPro;
using UnityEngine;
using GalacticFishing; // GFishSpawner

[DisallowMultipleComponent]
public sealed class RodPowerUnlockTracker : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text label;

    [Header("Source")]
    [Tooltip("If not assigned, we auto-find the first GFishSpawner in the scene.")]
    [SerializeField] private GFishSpawner spawner;

    [Header("Output mode")]
    [Tooltip("If true, outputs ONLY the number (e.g. '30000' or '100K'). If false, outputs prefix + number.")]
    [SerializeField] private bool valueOnly = false;

    [Header("Formatting")]
    [SerializeField] private string prefix = "Next Fish Unlocked At Rodpower: ";
    [SerializeField] private string unknownText = "?";
    [SerializeField] private string allUnlockedText = "Next Unlock at: Lake Complete!";

    [Tooltip("If true, formats as 100K / 3.4M / 1.1B etc, but ONLY starting at 100,000.")]
    [SerializeField] private bool compact = true;

    [Tooltip("Compacting starts at this value (default 100,000).")]
    [Min(0)]
    [SerializeField] private double compactStartAt = 100000d;

    [Range(0, 2)]
    [SerializeField] private int compactDecimals = 1;

    [Tooltip("If compact=false, use this numeric format (e.g. 0.#).")]
    [SerializeField] private string normalNumberFormat = "0.#";

    [Header("Refresh")]
    [SerializeField] private float refreshIntervalSeconds = 0.5f;

    private float _nextRefresh;
    private string _last;

    private void Reset()
    {
        label = GetComponent<TMP_Text>();
    }

    private void Awake()
    {
        if (!label) label = GetComponent<TMP_Text>();
        if (!spawner) spawner = FindFirstObjectByType<GFishSpawner>();
        Apply(true);
    }

    private void OnEnable()
    {
        if (!label) label = GetComponent<TMP_Text>();
        if (!spawner) spawner = FindFirstObjectByType<GFishSpawner>();
        Apply(true);
    }

    private void Update()
    {
        if (!label) return;

        if (Time.unscaledTime < _nextRefresh) return;
        _nextRefresh = Time.unscaledTime + Mathf.Max(0.05f, refreshIntervalSeconds);

        Apply(false);
    }

    private void Apply(bool force)
    {
        if (!spawner)
            spawner = FindFirstObjectByType<GFishSpawner>();

        string msg;

        if (!spawner)
        {
            msg = valueOnly ? unknownText : (prefix + unknownText);
        }
        else
        {
            if (!spawner.TryGetNextUnlockPower(out float nextPower))
            {
                msg = allUnlockedText;
            }
            else
            {
                string valueStr = compact
                    ? CompactFormat(nextPower, compactDecimals, compactStartAt)
                    : nextPower.ToString(normalNumberFormat);

                msg = valueOnly ? valueStr : (prefix + valueStr);
            }
        }

        if (force || !string.Equals(_last, msg, StringComparison.Ordinal))
        {
            _last = msg;
            label.text = msg;
        }
    }

    // 99999 -> "99999"
    // 100000 -> "100K"
    public static string CompactFormat(double value, int decimals, double startAt)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) return "?";

        double abs = Math.Abs(value);
        string sign = value < 0 ? "-" : "";

        if (abs < Math.Max(0d, startAt))
        {
            if (abs >= 0 && abs < 1e16 && Math.Abs(abs - Math.Round(abs)) < 1e-6)
                return sign + ((long)Math.Round(abs)).ToString();
            return sign + abs.ToString("0.#");
        }

        string[] suffix = { "K", "M", "B", "T" };
        double scaled = abs;
        int idx = -1;

        while (scaled >= 1000d && idx < suffix.Length - 1)
        {
            scaled /= 1000d;
            idx++;
        }

        if (scaled >= 999.5d && idx < suffix.Length - 1)
        {
            scaled /= 1000d;
            idx++;
        }

        decimals = Mathf.Clamp(decimals, 0, 2);
        string fmt = decimals == 0 ? "0" : (decimals == 1 ? "0.#" : "0.##");

        return sign + scaled.ToString(fmt) + suffix[idx];
    }
}
