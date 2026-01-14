using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using GalacticFishing.Progress;

/// <summary>
/// Displays the player's current credits using PlayerProgressManager.
/// Supports compact number formats to avoid UI overflow at endgame.
/// </summary>
[DisallowMultipleComponent]
public class CreditDisplay : MonoBehaviour
{
    public enum NumberDisplayMode
    {
        Standard,     // uses standardFormat + numeric value (CurrentCulture)
        Abbreviated,  // 100k, 1M, 1B...
        Scientific,   // 1.23E+05
        Engineering,  // 123.4E+03 (exponent multiple of 3)

        // Added at end so existing serialized values don't shift.
        Commas        // Standard, but forced en-US thousands separators (10,000)
    }

    [Header("Target")]
    [SerializeField] private TMP_Text label;

    [Header("Formatting")]
    [SerializeField] private NumberDisplayMode displayMode = NumberDisplayMode.Abbreviated;

    [Tooltip("Used for Standard/Commas mode, and also for Abbreviated mode when value < Abbrev Start.\nExample: \"{0:N0}\" or \"{0:N0} Credits\"")]
    [SerializeField] private string standardFormat = "{0:N0}";

    [Tooltip("Used for Abbreviated/Scientific/Engineering modes.\nExample: \"{0}\" or \"{0} Credits\"")]
    [SerializeField] private string compactFormat = "{0}";

    [Tooltip("Abbreviate once credits reach this value (default 100000 => 100k).")]
    [SerializeField] private double abbreviateStart = 100000d;

    [Tooltip("Max decimals to show in compact modes (e.g., 1 => 1.2M).")]
    [Range(0, 6)]
    [SerializeField] private int compactMaxDecimals = 1;

    [Tooltip("If true, trims trailing zeros in compact modes (e.g., 1.0M -> 1M).")]
    [SerializeField] private bool trimTrailingZeros = true;

    [Header("Optional Icon Prefix (TextMeshPro sprite tag)")]
    [SerializeField] private bool showIcon = false;

    [Tooltip("Example: <sprite name=\"coin\">  (sprite name must exist in TMP sprite asset on the TMP component)")]
    [SerializeField] private string iconTag = "<sprite name=\"coin\">";

    [SerializeField] private bool spaceAfterIcon = true;

    [Header("Refresh")]
    [Tooltip("How often to refresh the label (seconds).")]
    [SerializeField] private float refreshSeconds = 0.5f;

    private float _nextRefreshTime;

    private static readonly CultureInfo CommaCulture = CultureInfo.GetCultureInfo("en-US");

    private void Awake()
    {
        if (!label)
            label = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        _nextRefreshTime = 0f;
        RefreshLabel();
    }

    private void Update()
    {
        if (Time.unscaledTime < _nextRefreshTime)
            return;

        _nextRefreshTime = Time.unscaledTime + Mathf.Max(0.05f, refreshSeconds);
        RefreshLabel();
    }

    private void RefreshLabel()
    {
        if (!label)
            return;

        var mgr = PlayerProgressManager.Instance;
        if (mgr == null)
            return;

        // Credits are stored as float; convert to double for formatting stability.
        double credits = mgr.GetCredits();

        string prefix = "";
        if (showIcon && !string.IsNullOrWhiteSpace(iconTag))
            prefix = iconTag + (spaceAfterIcon ? " " : "");

        string body = FormatCredits(credits);
        label.text = prefix + body;
    }

    private string FormatCredits(double value)
    {
        // Standard mode uses CurrentCulture (whatever OS/Unity is set to).
        if (displayMode == NumberDisplayMode.Standard)
            return string.Format(CultureInfo.CurrentCulture, standardFormat, value);

        // Commas forces 10,000 style.
        if (displayMode == NumberDisplayMode.Commas)
            return string.Format(CommaCulture, standardFormat, value);

        // In Abbreviated mode, below threshold stays readable with standard formatting.
        if (displayMode == NumberDisplayMode.Abbreviated && Math.Abs(value) < abbreviateStart)
            return string.Format(CultureInfo.CurrentCulture, standardFormat, value);

        string compact = displayMode switch
        {
            NumberDisplayMode.Abbreviated  => GFNumberFormatter.Abbreviate(value, compactMaxDecimals, trimTrailingZeros),
            NumberDisplayMode.Scientific   => GFNumberFormatter.Scientific(value, compactMaxDecimals, trimTrailingZeros),
            NumberDisplayMode.Engineering  => GFNumberFormatter.Engineering(value, compactMaxDecimals, trimTrailingZeros),
            _                              => value.ToString(CultureInfo.CurrentCulture),
        };

        return string.Format(CultureInfo.CurrentCulture, compactFormat, compact);
    }
}

/// <summary>
/// Reusable compact number formatter for UI (credits, damage, fish weight/length later, etc).
/// Keep this centralized so you can add “settings” later (player preference).
/// </summary>
public static class GFNumberFormatter
{
    // Common incremental suffixes (you can extend later).
    // Uses "k" lowercase because you asked for 100k.
    private static readonly string[] Suffixes =
    {
        "", "k", "M", "B", "T", "Qa", "Qi", "Sx", "Sp", "Oc", "No", "Dc"
    };

    public static string Abbreviate(double value, int maxDecimals, bool trimZeros)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            return value.ToString(CultureInfo.InvariantCulture);

        double abs = Math.Abs(value);
        int sign = value < 0 ? -1 : 1;

        if (abs < 1000d)
            return (sign * abs).ToString("0", CultureInfo.CurrentCulture);

        int suffixIndex = 0;
        double scaled = abs;

        while (scaled >= 1000d && suffixIndex < Suffixes.Length - 1)
        {
            scaled /= 1000d;
            suffixIndex++;
        }

        string fmt = maxDecimals <= 0 ? "0" : "0." + new string('#', maxDecimals);
        string num = (sign * scaled).ToString(fmt, CultureInfo.CurrentCulture);

        if (trimZeros)
            num = TrimZeros(num);

        // Rounding edge-case: 999.9k -> 1000k -> 1M
        if (suffixIndex < Suffixes.Length - 1)
        {
            if (TryParseCurrentCulture(num, out double parsedAbs))
            {
                double parsed = Math.Abs(parsedAbs);
                if (parsed >= 1000d)
                {
                    parsed /= 1000d;
                    suffixIndex++;

                    string num2 = (Math.Sign(value) * parsed).ToString(fmt, CultureInfo.CurrentCulture);
                    if (trimZeros)
                        num2 = TrimZeros(num2);

                    return num2 + Suffixes[suffixIndex];
                }
            }
        }

        return num + Suffixes[suffixIndex];
    }

    public static string Scientific(double value, int maxDecimals, bool trimZeros)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            return value.ToString(CultureInfo.InvariantCulture);

        string fmt = maxDecimals <= 0 ? "0E+0" : "0." + new string('#', maxDecimals) + "E+0";
        string s = value.ToString(fmt, CultureInfo.InvariantCulture);

        if (!trimZeros || maxDecimals <= 0)
            return s;

        int e = s.IndexOf('E');
        if (e <= 0) return s;

        string mantissa = TrimZerosInvariant(s.Substring(0, e));
        return mantissa + s.Substring(e);
    }

    public static string Engineering(double value, int maxDecimals, bool trimZeros)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            return value.ToString(CultureInfo.InvariantCulture);

        if (Math.Abs(value) < double.Epsilon)
            return "0";

        double abs = Math.Abs(value);
        int exp = (int)Math.Floor(Math.Log10(abs));

        int engExp = exp - (exp % 3);
        double mantissa = value / Math.Pow(10, engExp);

        string fmt = maxDecimals <= 0 ? "0" : "0." + new string('#', maxDecimals);
        string mant = mantissa.ToString(fmt, CultureInfo.InvariantCulture);

        if (trimZeros && maxDecimals > 0)
            mant = TrimZerosInvariant(mant);

        string expStr = engExp >= 0 ? $"+{engExp:00}" : $"-{Math.Abs(engExp):00}";
        return $"{mant}E{expStr}";
    }

    private static string TrimZeros(string s)
    {
        char dec = Convert.ToChar(CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator);
        int i = s.IndexOf(dec);
        if (i < 0) return s;

        int end = s.Length - 1;
        while (end > i && s[end] == '0') end--;
        if (end == i) end--;

        return s.Substring(0, end + 1);
    }

    private static string TrimZerosInvariant(string s)
    {
        int i = s.IndexOf('.');
        if (i < 0) return s;

        int end = s.Length - 1;
        while (end > i && s[end] == '0') end--;
        if (end == i) end--;

        return s.Substring(0, end + 1);
    }

    private static bool TryParseCurrentCulture(string s, out double value)
    {
        return double.TryParse(s, NumberStyles.Float, CultureInfo.CurrentCulture, out value)
            || double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
