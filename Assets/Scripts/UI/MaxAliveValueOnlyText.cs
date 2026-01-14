// Assets/Scripts/UI/Taskbar/MaxAliveValueOnlyText.cs
using System;
using TMPro;
using UnityEngine;
using GalacticFishing;                  // WorldManager, Lake, GFishSpawner
using GalacticFishing.Progress;          // PlayerProgressManager

/// <summary>
/// VALUE-ONLY text for Lake Max Capacity (no prefix).
/// Fetches from current Lake definition (lake.maxAlive) + per-lake upgrade bonus.
/// Intended to sit next to a separate static label TMP (e.g. "Lake Max Capacity").
/// </summary>
[DisallowMultipleComponent]
public sealed class MaxAliveValueOnlyText : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text label;

    [Header("Source")]
    [Tooltip("Optional. If set, we will read worldManager from this spawner (spawner.worldManager).")]
    [SerializeField] private GFishSpawner spawner;

    [Tooltip("Optional. If empty, we auto-find the first WorldManager in the scene.")]
    [SerializeField] private WorldManager worldManager;

    [Header("Upgrades")]
    [Tooltip("Upgrade id prefix. Final id becomes: prefix + lakeId (e.g. max_alive_inc_lake_01).")]
    [SerializeField] private string upgradeIdPrefix = "max_alive_inc_";

    [Header("Output")]
    [Tooltip("Shown if we can't resolve the current lake/max alive.")]
    [SerializeField] private string missingValueText = "—";

    [Tooltip("If true, formats as 100K / 3.4M / 1.1B etc, but ONLY starting at 100,000.")]
    [SerializeField] private bool compact = true;

    [Tooltip("Compacting starts at this value (default 100,000).")]
    [Min(0)]
    [SerializeField] private double compactStartAt = 100000d;

    [Range(0, 2)]
    [SerializeField] private int compactDecimals = 1;

    [Tooltip("If compact=false, use this numeric format (ignored for ints, but kept for consistency).")]
    [SerializeField] private string normalNumberFormat = "0";

    [Header("Value styling (optional)")]
    [SerializeField] private bool styleValue = false;
    [SerializeField] private bool boldValue = true;

    [Tooltip("Optional color for the value (hex). Leave empty for no color. Example: #FFD54A")]
    [SerializeField] private string valueColorHex = "";

    [Tooltip("Optional size for the value (100 = unchanged). Example: 115")]
    [Range(80, 140)]
    [SerializeField] private int valueSizePercent = 100;

    [Header("Value font tag (optional)")]
    [Tooltip("If enabled, wraps the value in a TMP <font> tag so only the value uses a different font asset by NAME.")]
    [SerializeField] private bool useValueFontTag = false;

    [Tooltip("Font asset name to use inside <font=\"...\">. Must match the TMP Font Asset name exactly.")]
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

        if (!spawner) spawner = FindFirstObjectByType<GFishSpawner>();
        if (!worldManager) worldManager = FindFirstObjectByType<WorldManager>();

        Apply(true);
    }

    private void OnEnable()
    {
        if (!label) label = GetComponent<TMP_Text>();
        if (label) label.richText = true;

        if (!spawner) spawner = FindFirstObjectByType<GFishSpawner>();
        if (!worldManager) worldManager = FindFirstObjectByType<WorldManager>();

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
        if (!spawner) spawner = FindFirstObjectByType<GFishSpawner>();
        if (!worldManager) worldManager = FindFirstObjectByType<WorldManager>();

        var wm = ResolveWorldManager();
        string rawValue = TryGetCurrentLakeMaxAlive(wm, out int finalMaxAlive)
            ? FormatNumber(finalMaxAlive)
            : missingValueText;

        string msg = styleValue ? StyleValue(rawValue) : rawValue;

        if (force || !string.Equals(_last, msg, StringComparison.Ordinal))
        {
            _last = msg;
            label.text = msg;
        }
    }

    private WorldManager ResolveWorldManager()
    {
        if (spawner != null && spawner.worldManager != null)
            return spawner.worldManager;

        return worldManager;
    }

    private bool TryGetCurrentLakeMaxAlive(WorldManager wm, out int finalMaxAlive)
    {
        finalMaxAlive = 0;

        if (wm == null || wm.world == null)
            return false;

        int lakeIndex = wm.lakeIndex;
        var lake = wm.GetLake(lakeIndex);
        if (lake == null)
            return false;

        int baseMax = Mathf.Max(0, lake.maxAlive);

        int bonus = 0;
        try
        {
            var ppm = PlayerProgressManager.Instance;
            if (ppm != null && !string.IsNullOrWhiteSpace(lake.lakeId))
            {
                string upgradeId = upgradeIdPrefix + lake.lakeId;
                bonus = Mathf.Max(0, ppm.GetWorkshopUpgradeLevel(upgradeId));
            }
        }
        catch { /* ignore */ }

        finalMaxAlive = baseMax + bonus;
        return true;
    }

    private string FormatNumber(double value)
    {
        if (compact)
            return CompactFormat(value, compactDecimals, compactStartAt);

        // For ints, this is fine (but works for any double too)
        return value.ToString(normalNumberFormat);
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

    // 99999 -> "99999"
    // 100000 -> "100K"
    public static string CompactFormat(double value, int decimals, double startAt)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) return "?";

        double abs = Math.Abs(value);
        string sign = value < 0 ? "-" : "";

        if (abs < Math.Max(0d, startAt))
        {
            // Prefer integer look if it's basically an int
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
