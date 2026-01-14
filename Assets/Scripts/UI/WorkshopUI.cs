using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using GalacticFishing.Progress;

/// <summary>
/// Data for one upgrade row. Shown in the inspector.
/// </summary>
[Serializable]
public class WorkshopUpgrade
{
    public string id;
    public string title;
    [TextArea] public string description;
    public Sprite icon;

    public int cost = 100;
    public int level;
    public int maxLevel = 10;

    public bool IsMaxed       => level >= maxLevel;
    public float FillFraction => maxLevel > 0 ? (float)level / maxLevel : 0f;
}

/// <summary>
/// Controls the whole Workshop panel and drives the row views.
/// </summary>
public class WorkshopUI : MonoBehaviour
{
    [Header("Scene references")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text currencyText;
    [SerializeField] private RectTransform upgradesListRoot;   // content of the scroll
    [SerializeField] private WorkshopUpgradeRowView rowPrefab;

    [Header("Scroll (optional but recommended)")]
    [Tooltip("ScrollRect on 'UpgradesScroll'. Used to snap list to the top on first open.")]
    [SerializeField] private ScrollRect upgradesScroll;

    [Header("Upgrades data")]
    [SerializeField] private WorkshopUpgrade[] upgrades;

    [Header("Persistence / Effects")]
    [Tooltip("If true, upgrade levels are loaded/saved to PlayerProgressManager save data.")]
    [SerializeField] private bool persistUpgradeLevels = true;

    [Tooltip("The WorkshopUpgrade.id that should upgrade the CURRENTLY EQUIPPED ROD (affects Rod Power). Example: 'rod_power'")]
    [SerializeField] private string rodPowerUpgradeId = "rod_power";

    private readonly List<WorkshopUpgradeRowView> _rows = new();
    private PlayerProgressManager _progress;

    // We only force-scroll to top once per game session.
    private bool _didInitialScrollToTop = false;

    private void Awake()
    {
        _progress = PlayerProgressManager.Instance;
        if (_progress == null)
        {
            Debug.LogWarning("[WorkshopUI] No PlayerProgressManager instance found in scene.");
        }
    }

    private void OnEnable()
    {
        // Pull persisted levels BEFORE drawing
        LoadLevelsFromSave();

        RedrawAll();

        // First time this panel becomes enabled after game start:
        if (!_didInitialScrollToTop)
        {
            StartCoroutine(CoScrollToTopInitial());
            _didInitialScrollToTop = true;
        }
        // After that we leave the scroll position alone so it "remembers".
    }

    private void Start()
    {
        // In case the panel is visible on scene start.
        LoadLevelsFromSave();
        RedrawAll();
    }

    /// <summary>Get current credits from PlayerProgressManager (0 if missing).</summary>
    private float GetCurrentCredits()
    {
        return _progress != null ? _progress.GetCredits() : 0f;
    }

    private void LoadLevelsFromSave()
    {
        if (!persistUpgradeLevels) return;
        if (_progress == null || _progress.Data == null || _progress.Data.gear == null) return;
        if (upgrades == null) return;

        for (int i = 0; i < upgrades.Length; i++)
        {
            var u = upgrades[i];
            if (u == null) continue;

            if (!string.IsNullOrWhiteSpace(u.id))
            {
                int saved = _progress.GetWorkshopUpgradeLevel(u.id);
                u.level = Mathf.Clamp(saved, 0, Mathf.Max(0, u.maxLevel));
            }
        }
    }

    private void SaveUpgradeLevel(WorkshopUpgrade u)
    {
        if (!persistUpgradeLevels) return;
        if (_progress == null) return;
        if (u == null) return;
        if (string.IsNullOrWhiteSpace(u.id)) return;

        _progress.SetWorkshopUpgradeLevel(u.id, u.level);
    }

    private void ApplyUpgradeEffects(WorkshopUpgrade u)
    {
        if (_progress == null || u == null) return;

        // Config-based mapping (not hardcoded to a specific button):
        // If this upgrade's id matches the configured rodPowerUpgradeId,
        // we treat it as "upgrade currently equipped rod level".
        if (!string.IsNullOrWhiteSpace(rodPowerUpgradeId) &&
            !string.IsNullOrWhiteSpace(u.id) &&
            string.Equals(u.id, rodPowerUpgradeId, StringComparison.OrdinalIgnoreCase))
        {
            // Keep rod upgrade level in sync with this workshop upgrade level.
            _progress.SetEquippedRodUpgradeLevel(u.level);
        }
    }

    /// <summary>
    /// Completely redraws the list and currency text.
    /// </summary>
    private void RedrawAll()
    {
        if (titleText != null)
            titleText.text = "Workshop";

        UpdateCurrencyText();

        if (upgradesListRoot == null || rowPrefab == null || upgrades == null)
            return;

        // Clear children and rebuild.
        foreach (Transform child in upgradesListRoot)
        {
            Destroy(child.gameObject);
        }
        _rows.Clear();

        float credits = GetCurrentCredits();

        foreach (var upgrade in upgrades)
        {
            var row = Instantiate(rowPrefab, upgradesListRoot);
            bool canAfford = !upgrade.IsMaxed && credits >= upgrade.cost;
            row.Bind(upgrade, canAfford, OnUpgradeClicked);
            _rows.Add(row);
        }
    }

    /// <summary>
    /// Just re-binds existing rows (no Instantiate/Destroy).
    /// Used after level changes or currency changes.
    /// </summary>
    private void RebindExistingRows()
    {
        if (upgrades == null)
            return;

        float credits = GetCurrentCredits();

        for (int i = 0; i < upgrades.Length && i < _rows.Count; i++)
        {
            WorkshopUpgrade upgrade = upgrades[i];
            WorkshopUpgradeRowView row = _rows[i];

            bool canAfford = !upgrade.IsMaxed && credits >= upgrade.cost;
            row.Bind(upgrade, canAfford, OnUpgradeClicked);
        }

        UpdateCurrencyText();
    }

    private void UpdateCurrencyText()
    {
        if (currencyText == null)
            return;

        float credits = GetCurrentCredits();
        currencyText.text = $"{credits:N0} Credits";
    }

    private void OnUpgradeClicked(WorkshopUpgrade upgrade)
    {
        if (upgrade == null)
            return;

        // Credits are already checked & spent in WorkshopUpgradeRowView.OnClickPrice.
        // Here we bump the level, persist, apply effects, then refresh UI.
        if (!upgrade.IsMaxed)
        {
            upgrade.level = Mathf.Clamp(upgrade.level + 1, 0, upgrade.maxLevel);
        }

        SaveUpgradeLevel(upgrade);
        ApplyUpgradeEffects(upgrade);

        // IMPORTANT: RowView spent credits directly in save data; save now so progress isn't lost on crash.
        if (_progress != null)
        {
            _progress.Save();
        }

        RebindExistingRows();
    }

    /// <summary>
    /// Coroutine: wait one frame so layout/ScrollRect can finish,
    /// then snap the ScrollRect to the top once.
    /// </summary>
    private IEnumerator CoScrollToTopInitial()
    {
        if (upgradesScroll == null)
            yield break;

        // Wait one frame so VerticalLayoutGroup / ContentSizeFitter have finished.
        yield return null;

        Canvas.ForceUpdateCanvases();

        // For a normal vertical ScrollRect, 1 = top, 0 = bottom.
        upgradesScroll.verticalNormalizedPosition = 1f;
    }
}
