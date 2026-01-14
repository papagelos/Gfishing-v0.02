using System;
using TMPro;
using UnityEngine;
using GalacticFishing; // WorldManager, WorldDefinition, Lake

/// <summary>
/// Taskbar VALUE text only:
///   "<LakeName> (WXLX)"
/// Example:
///   "Lonely Lake (W1L2)"
///
/// Use this on your VALUE TMP (ex: LakeValue).
/// Keep your label TMP (ex: LakeName) as static text like "Lake:".
/// </summary>
[DisallowMultipleComponent]
public class TaskbarLakeNameText : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text label; // This should be the VALUE TMP (ex: LakeValue)

    [Header("Source")]
    [Tooltip("If empty, we auto-find the first WorldManager in the scene.")]
    [SerializeField] private WorldManager worldManager;

    [Header("World number rules")]
    [Tooltip("If worldId ends with digits (e.g. 'world_3' or 'world_01'), display that as X. Otherwise use worldId.")]
    [SerializeField] private bool worldXFromWorldIdDigits = true;

    [Header("Lake number rules")]
    [Tooltip("Prefer using digits from lake.lakeId if possible (e.g. 'lake_2' or 'Lake 2' => 2).")]
    [SerializeField] private bool useLakeIdDigitsIfPossible = true;

    [Tooltip("If true, Lake Y is (lakeIndex + 1). If false, Lake Y is lakeIndex.")]
    [SerializeField] private bool displayLakeNumberOneBased = true;

    [Header("Output")]
    [Tooltip("If true: outputs only '<LakeName> (WXLX)'.")]
    [SerializeField] private bool valueOnly = true;

    [Tooltip("If true: appends ' (WXLX)'. If false: just lake name.")]
    [SerializeField] private bool includeWxLx = true;

    [Header("Value styling (ONLY affects the value parts)")]
    [SerializeField] private bool styleValues = true;

    [Tooltip("Applies to LakeName value.")]
    [SerializeField] private bool styleLakeNameValue = true;

    [Tooltip("Applies to World X and Lake Y values.")]
    [SerializeField] private bool styleWorldAndLakeNumbers = true;

    [Tooltip("Bold the styled values.")]
    [SerializeField] private bool boldValues = true;

    [Tooltip("Optional color for values (hex). Leave empty for no color. Example: #FFFFFF or #FFD54A")]
    [SerializeField] private string valueColorHex = "";

    [Tooltip("Optional size for values (100 = unchanged). Example: 110 makes values slightly larger.")]
    [Range(80, 140)]
    [SerializeField] private int valueSizePercent = 100;

    [Header("Fallbacks")]
    [SerializeField] private string unknownLakeName = "?";
    [SerializeField] private string unknownNumber = "?";

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

        if (!worldManager) worldManager = FindFirstObjectByType<WorldManager>();
        Apply(true);
    }

    private void OnEnable()
    {
        if (!label) label = GetComponent<TMP_Text>();
        if (label) label.richText = true;

        if (!worldManager) worldManager = FindFirstObjectByType<WorldManager>();
        if (worldManager != null)
            worldManager.WorldChanged += OnWorldChanged;

        Apply(true);
    }

    private void OnDisable()
    {
        if (worldManager != null)
            worldManager.WorldChanged -= OnWorldChanged;
    }

    private void Update()
    {
        if (Time.unscaledTime < _nextRefresh) return;
        _nextRefresh = Time.unscaledTime + Mathf.Max(0.05f, refreshSeconds);
        Apply(false);
    }

    private void OnWorldChanged(WorldDefinition newWorld, int newLakeIndex)
    {
        Apply(true);
    }

    private void Apply(bool force)
    {
        if (!label) return;

        if (!worldManager)
        {
            worldManager = FindFirstObjectByType<WorldManager>();
            if (worldManager != null)
            {
                worldManager.WorldChanged -= OnWorldChanged;
                worldManager.WorldChanged += OnWorldChanged;
            }
        }

        string msg = BuildText();

        if (force || !string.Equals(_last, msg, StringComparison.Ordinal))
        {
            _last = msg;
            label.text = msg;
        }
    }

    private string BuildText()
    {
        if (worldManager == null || worldManager.world == null)
        {
            // value-only fallback
            return valueOnly
                ? (includeWxLx ? $"{unknownLakeName} (W{unknownNumber}L{unknownNumber})" : unknownLakeName)
                : $"Lake: {unknownLakeName} (W{unknownNumber}L{unknownNumber})";
        }

        var world = worldManager.world;
        int lakeIndex = worldManager.lakeIndex;
        var lake = worldManager.GetLake(lakeIndex);

        // ---- World X ----
        string worldX = unknownNumber;
        if (!string.IsNullOrWhiteSpace(world.worldId))
        {
            if (worldXFromWorldIdDigits && TryGetTrailingDigits(world.worldId, out int wNum) && wNum > 0)
                worldX = wNum.ToString();
            else
                worldX = world.worldId;
        }
        else if (!string.IsNullOrWhiteSpace(world.displayName))
        {
            worldX = world.displayName;
        }

        // ---- Lake Y ----
        string lakeNumberStr = unknownNumber;

        if (useLakeIdDigitsIfPossible && lake != null && !string.IsNullOrWhiteSpace(lake.lakeId))
        {
            if (TryGetTrailingDigits(lake.lakeId, out int lNum) && lNum > 0)
                lakeNumberStr = lNum.ToString();
        }

        if (lakeNumberStr == unknownNumber)
        {
            int lakeNumber = lakeIndex;
            if (displayLakeNumberOneBased && lakeNumber >= 0) lakeNumber += 1;
            lakeNumberStr = lakeNumber >= 0 ? lakeNumber.ToString() : unknownNumber;
        }

        // ---- Lake name ----
        string lakeName = (lake != null && !string.IsNullOrWhiteSpace(lake.displayName)) ? lake.displayName : unknownLakeName;

        // ---- Style ONLY values ----
        string lakeNameOut = (styleValues && styleLakeNameValue) ? StyleValue(lakeName) : lakeName;
        string worldXOut = (styleValues && styleWorldAndLakeNumbers) ? StyleValue(worldX) : worldX;
        string lakeNumOut = (styleValues && styleWorldAndLakeNumbers) ? StyleValue(lakeNumberStr) : lakeNumberStr;

        // ---- Output ----
        string wxlx = includeWxLx ? $" (W{worldXOut}L{lakeNumOut})" : "";

        if (valueOnly)
            return $"{lakeNameOut}{wxlx}";

        // If you ever want the old combined string back:
        return $"Lake: {lakeNameOut}{wxlx}";
    }

    private string StyleValue(string s)
    {
        if (!styleValues) return s;

        string outStr = s;

        if (!string.IsNullOrWhiteSpace(valueColorHex))
            outStr = $"<color={valueColorHex}>{outStr}</color>";

        if (valueSizePercent != 100)
            outStr = $"<size={valueSizePercent}%>{outStr}</size>";

        if (boldValues)
            outStr = $"<b>{outStr}</b>";

        return outStr;
    }

    private static bool TryGetTrailingDigits(string s, out int value)
    {
        value = 0;
        if (string.IsNullOrEmpty(s)) return false;

        int i = s.Length - 1;
        while (i >= 0 && char.IsDigit(s[i])) i--;
        int start = i + 1;

        if (start >= s.Length) return false;
        return int.TryParse(s.Substring(start), out value);
    }
}
