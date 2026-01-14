// Assets/Scripts/UI/Taskbar/RodPowerValueOnlyText.cs
using System;
using TMPro;
using UnityEngine;
using GalacticFishing.Progress;

[DisallowMultipleComponent]
public sealed class RodPowerValueOnlyText : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text label;

    [Header("Source")]
    [Tooltip("If empty, uses PlayerProgressManager.Instance.")]
    [SerializeField] private PlayerProgressManager progressManager;

    [Header("Output")]
    [SerializeField] private string missingValueText = "—";

    [Tooltip("If true, formats as 100K / 3.4M / 1.1B etc, but ONLY starting at 100,000.")]
    [SerializeField] private bool compact = true;

    [Min(0)]
    [SerializeField] private double compactStartAt = 100000d;

    [Range(0, 2)]
    [SerializeField] private int compactDecimals = 1;

    [Tooltip("If compact=false (or value < 100,000), use this numeric format.")]
    [SerializeField] private string normalNumberFormat = "0";

    [Header("Value styling (optional)")]
    [SerializeField] private bool styleValue = false;
    [SerializeField] private bool boldValue = true;
    [SerializeField] private string valueColorHex = "";
    [Range(80, 140)]
    [SerializeField] private int valueSizePercent = 100;

    [Header("Value font tag (optional)")]
    [SerializeField] private bool useValueFontTag = false;
    [SerializeField] private string valueFontAssetName = "";

    [Header("Refresh")]
    [SerializeField] private float refreshSeconds = 0.25f;

    private float _nextRefresh;
    private string _last;

    private void Reset()
    {
        label = GetComponent<TMP_Text>();
    }

    private void Awake()
    {
        if (!label) label = GetComponent<TMP_Text>();
        if (label) label.richText = true;

        if (!progressManager)
            progressManager = PlayerProgressManager.Instance;

        Apply(true);
    }

    private void OnEnable()
    {
        if (!label) label = GetComponent<TMP_Text>();
        if (label) label.richText = true;

        if (!progressManager)
            progressManager = PlayerProgressManager.Instance;

        Apply(true);
    }

    private void Update()
    {
        if (!label) return;

        if (Time.unscaledTime < _nextRefresh) return;
        _nextRefresh = Time.unscaledTime + Mathf.Max(0.05f, refreshSeconds);

        Apply(false);
    }

    private void Apply(bool force)
    {
        if (!progressManager)
            progressManager = PlayerProgressManager.Instance;

        string msg;

        if (!progressManager)
        {
            msg = missingValueText;
        }
        else
        {
            float power = progressManager.CurrentRodPower;

            string raw = compact
                ? CompactFormat(power, compactDecimals, compactStartAt)
                : power.ToString(normalNumberFormat);

            msg = styleValue ? StyleValue(raw) : raw;
        }

        if (force || !string.Equals(_last, msg, StringComparison.Ordinal))
        {
            _last = msg;
            label.text = msg;
        }
    }

    private string StyleValue(string s)
    {
        string outStr = s;

        if (useValueFontTag && !string.IsNullOrWhiteSpace(valueFontAssetName))
            outStr = $"<font=\"{valueFontAssetName}\">{outStr}</font>";

        if (!string.IsNullOrWhiteSpace(valueColorHex))
            outStr = $"<color={valueColorHex}>{outStr}</color>";

        if (valueSizePercent != 100)
            outStr = $"<size={valueSizePercent}%>{outStr}</size>";

        if (boldValue)
            outStr = $"<b>{outStr}</b>";

        return outStr;
    }

    public static string CompactFormat(double value, int decimals, double startAt)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) return "?";

        double abs = Math.Abs(value);
        string sign = value < 0 ? "-" : "";

        if (abs < Math.Max(0d, startAt))
        {
            // if basically integer, show integer
            if (abs < 1e16 && Math.Abs(abs - Math.Round(abs)) < 1e-6)
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
