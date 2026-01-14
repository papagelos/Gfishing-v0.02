using System;
using TMPro;
using UnityEngine;
using GalacticFishing;                 // WorldManager, Lake
using GalacticFishing.Progress;         // PlayerProgressManager

/// <summary>
/// Taskbar text: "<prefix>: <value>"
/// Prefix is taken from the TMP_Text's initial text (or override).
/// Styles ONLY the value part via TMP rich-text; leaves prefix untouched.
/// Value is fetched from current Lake definition (lake.maxAlive) + per-lake upgrade bonus.
/// </summary>
[DisallowMultipleComponent]
public class TaskbarMaxAliveText : MonoBehaviour
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

    [Header("Prefix")]
    [Tooltip("If true, uses the label's initial text as prefix (recommended).")]
    [SerializeField] private bool useInitialLabelAsPrefix = true;

    [Tooltip("Used only if useInitialLabelAsPrefix is false or label text is empty.")]
    [SerializeField] private string prefixOverride = "Lake Max Capacity";

    [Tooltip("Shown if we can't resolve the current lake/max alive.")]
    [SerializeField] private string missingValueText = "—";

    [Header("Value styling (ONLY affects the value part)")]
    [SerializeField] private bool styleValue = true;

    [SerializeField] private bool boldValue = true;

    [Tooltip("Optional color for the value (hex). Leave empty for no color. Example: #FFD54A")]
    [SerializeField] private string valueColorHex = "";

    [Tooltip("Optional size for the value (100 = unchanged). Example: 115")]
    [Range(80, 140)]
    [SerializeField] private int valueSizePercent = 100;

    [Header("Value font (optional)")]
    [Tooltip("If enabled, wraps the value in a TMP <font> tag so only the value uses a different font asset.")]
    [SerializeField] private bool useValueFontTag = false;

    [Tooltip("Font asset name to use inside <font=\"...\">. This must match the TMP Font Asset name.")]
    [SerializeField] private string valueFontAssetName = "";

    [Header("Refresh")]
    [SerializeField] private float refreshSeconds = 0.25f;

    private float _nextRefresh;
    private string _prefixCached;
    private string _last;

    private void Reset()
    {
        label = GetComponent<TMP_Text>();
    }

    private void Awake()
    {
        if (!label) label = GetComponent<TMP_Text>();
        if (label) label.richText = true;

        // Optional auto-find
        if (!spawner) spawner = FindFirstObjectByType<GFishSpawner>();
        if (!worldManager) worldManager = FindFirstObjectByType<WorldManager>();

        CachePrefix();
        Apply(true);
    }

    private void OnEnable()
    {
        if (!label) label = GetComponent<TMP_Text>();
        if (label) label.richText = true;

        if (!spawner) spawner = FindFirstObjectByType<GFishSpawner>();
        if (!worldManager) worldManager = FindFirstObjectByType<WorldManager>();

        CachePrefix();
        Apply(true);
    }

    private void Update()
    {
        if (Time.unscaledTime < _nextRefresh) return;
        _nextRefresh = Time.unscaledTime + Mathf.Max(0.05f, refreshSeconds);
        Apply(false);
    }

    private void CachePrefix()
    {
        if (useInitialLabelAsPrefix && label != null)
        {
            var t = label.text?.Trim();
            _prefixCached = string.IsNullOrWhiteSpace(t) ? prefixOverride : t;
        }
        else
        {
            _prefixCached = prefixOverride;
        }
    }

    private void Apply(bool force)
    {
        if (!label) return;

        // Best-effort to keep references alive
        if (!spawner) spawner = FindFirstObjectByType<GFishSpawner>();
        if (!worldManager) worldManager = FindFirstObjectByType<WorldManager>();

        // Prefer worldManager from spawner if available (keeps it in sync with lake switching)
        var wm = ResolveWorldManager();
        string rawValue = TryGetCurrentLakeMaxAlive(wm, out int finalMaxAlive)
            ? finalMaxAlive.ToString()
            : missingValueText;

        string valueOut = styleValue ? StyleValue(rawValue) : rawValue;

        // Prefix untouched
        string msg = $"{_prefixCached}: {valueOut}";

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

        // Per-lake upgrade: "max_alive_inc_" + lakeId
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
        catch
        {
            // ignore; keep bonus=0
        }

        finalMaxAlive = baseMax + bonus;
        return true;
    }

    private string StyleValue(string s)
    {
        string outStr = s;

        // Font tag (optional) — only affects the wrapped text
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
}
