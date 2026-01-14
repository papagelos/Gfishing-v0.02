using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using GalacticFishing.Progress;

/// <summary>
/// Generic "Workshop-style" scroll list controller that reuses WorkshopUpgradeRowView visuals.
/// - Builds runtime WorkshopUpgrade list from a ShopCatalog
/// - Uses PlayerProgressManager.Get/SetWorkshopUpgradeLevel for persistence
///   with unique keys: shop:<catalogId>:<itemId>
/// - IMPORTANT: Does NOT spend credits. WorkshopUpgradeRowView already does that in OnClickPrice().
/// </summary>
public class ShopListUI : MonoBehaviour
{
    [Header("Catalog")]
    [SerializeField] private ShopCatalog catalog;

    [Header("UI")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text currencyText;

    [Tooltip("ScrollRect content root (the VerticalLayoutGroup / ContentSizeFitter container).")]
    [SerializeField] private RectTransform listRoot;

    [SerializeField] private WorkshopUpgradeRowView rowPrefab;

    [Header("Scroll (optional)")]
    [SerializeField] private ScrollRect scrollRect;

    [Tooltip("Snap list to top only the first time this panel is shown per session.")]
    [SerializeField] private bool snapToTopOnFirstOpen = true;

    [Header("Currency formatting for the bottom bar")]
    [SerializeField] private double abbreviateStart = 100000d;
    [Range(0, 6)]
    [SerializeField] private int compactMaxDecimals = 1;
    [SerializeField] private bool trimTrailingZeros = true;
    [SerializeField] private string currencySuffix = " Credits";

    [Header("Optional behavior hook")]
    [Tooltip("Assign any MonoBehaviour that implements IShopActionHandler.")]
    [SerializeField] private MonoBehaviour actionHandler;

    // Runtime
    private readonly List<WorkshopUpgradeRowView> _rows = new();
    private readonly List<WorkshopUpgrade> _upgrades = new();
    private readonly Dictionary<WorkshopUpgrade, ShopCatalogItem> _upgradeToItem = new();

    private PlayerProgressManager _progress;
    private bool _didInitialScrollToTop;

    // -------- Reflection (icon assignment without modifying WorkshopUpgradeRowView) --------
    private static FieldInfo _fiIconImage;

    private void Awake()
    {
        _progress = PlayerProgressManager.Instance;
        CacheReflection();
    }

    private void OnEnable()
    {
        if (_progress == null)
            _progress = PlayerProgressManager.Instance;

        BuildOrRebuildList();
        RefreshAllRows();

        if (snapToTopOnFirstOpen && !_didInitialScrollToTop)
        {
            StartCoroutine(CoScrollToTopInitial());
            _didInitialScrollToTop = true;
        }
    }

    private void CacheReflection()
    {
        if (_fiIconImage != null) return;

        // private Image iconImage;
        _fiIconImage = typeof(WorkshopUpgradeRowView).GetField("iconImage",
            BindingFlags.Instance | BindingFlags.NonPublic);
    }

    private IShopActionHandler Handler => actionHandler as IShopActionHandler;

    private void BuildOrRebuildList()
    {
        if (catalog == null || listRoot == null || rowPrefab == null)
        {
            Debug.LogWarning("[ShopListUI] Missing references (catalog/listRoot/rowPrefab).");
            return;
        }

        if (titleText != null)
            titleText.text = string.IsNullOrWhiteSpace(catalog.title) ? "Shop" : catalog.title;

        // Clear any previous
        foreach (Transform child in listRoot)
            Destroy(child.gameObject);

        _rows.Clear();
        _upgrades.Clear();
        _upgradeToItem.Clear();

        // Validate ids
        if (string.IsNullOrWhiteSpace(catalog.catalogId))
            Debug.LogWarning($"[ShopListUI] Catalog '{catalog.name}' has empty catalogId. Save keys may collide.");

        var seenItemIds = new HashSet<string>(StringComparer.Ordinal);

        // Build runtime upgrades and rows
        foreach (var item in catalog.items)
        {
            if (item == null) continue;

            if (string.IsNullOrWhiteSpace(item.id))
            {
                Debug.LogWarning($"[ShopListUI] Catalog '{catalog.name}' contains an item with empty id. Skipping.");
                continue;
            }

            if (!seenItemIds.Add(item.id))
            {
                Debug.LogWarning($"[ShopListUI] Duplicate item id '{item.id}' in catalog '{catalog.name}'. Skipping duplicate.");
                continue;
            }

            string saveKey = BuildSaveKey(catalog.catalogId, item.id);

            // Runtime upgrade object used by WorkshopUpgradeRowView
            var u = new WorkshopUpgrade
            {
                id = saveKey, // IMPORTANT: unique key stored in save
                title = item.title,
                description = item.description,
                icon = item.icon,
                cost = Mathf.Max(0, item.cost),
                maxLevel = Mathf.Max(1, item.maxLevel),
                level = 0
            };

            // Load saved level (persisted in PlayerProgressManager workshopUpgradeLevels)
            if (_progress != null)
            {
                int saved = _progress.GetWorkshopUpgradeLevel(saveKey);
                u.level = Mathf.Clamp(saved, 0, u.maxLevel);
            }

            _upgrades.Add(u);
            _upgradeToItem[u] = item;

            var row = Instantiate(rowPrefab, listRoot);
            _rows.Add(row);

            // Bind now (canAfford computed in RefreshAllRows / Rebind)
            // We still bind once so the button is wired, then we'll refresh immediately after.
            row.Bind(u, canAfford: false, OnRowClicked);

            // Set icon via reflection because row view deliberately doesn't touch iconImage.sprite
            TrySetRowIcon(row, item.icon);
        }

        UpdateCurrencyText();
    }

    private void RefreshAllRows()
    {
        if (_progress == null || catalog == null) { UpdateCurrencyText(); return; }

        float credits = _progress.GetCredits();

        for (int i = 0; i < _upgrades.Count && i < _rows.Count; i++)
        {
            var u = _upgrades[i];
            var row = _rows[i];

            bool canAfford = !u.IsMaxed && credits >= u.cost;
            row.Bind(u, canAfford, OnRowClicked);

            // In case prefab got duplicated / icon reset:
            if (_upgradeToItem.TryGetValue(u, out var item))
                TrySetRowIcon(row, item.icon);
        }

        UpdateCurrencyText();
    }

    private void UpdateCurrencyText()
    {
        if (currencyText == null)
            return;

        if (_progress == null)
        {
            currencyText.text = "0" + currencySuffix;
            return;
        }

        double credits = _progress.GetCredits();

        string formatted =
            Math.Abs(credits) < abbreviateStart
                ? credits.ToString("N0") // current culture
                : GFNumberFormatter.Abbreviate(credits, compactMaxDecimals, trimTrailingZeros);

        currencyText.text = formatted + currencySuffix;
    }

    private void OnRowClicked(WorkshopUpgrade upgrade)
    {
        if (upgrade == null || _progress == null)
            return;

        // IMPORTANT: credits were already validated and spent in WorkshopUpgradeRowView.OnClickPrice().
        // Here we only bump level + persist + optional hook + refresh.
        if (!upgrade.IsMaxed)
        {
            upgrade.level = Mathf.Clamp(upgrade.level + 1, 0, upgrade.maxLevel);
        }

        // Persist using upgrade.id (which is the unique save key)
        _progress.SetWorkshopUpgradeLevel(upgrade.id, upgrade.level);
        _progress.Save();

        // Call hook with catalogId and original itemId (not the save key)
        if (_upgradeToItem.TryGetValue(upgrade, out var item))
        {
            Handler?.OnPurchased(
                catalogId: catalog != null ? catalog.catalogId : "",
                itemId: item != null ? item.id : "",
                newLevel: upgrade.level,
                maxLevel: upgrade.maxLevel
            );
        }

        RefreshAllRows();
    }

    private static string BuildSaveKey(string catalogId, string itemId)
    {
        catalogId = string.IsNullOrWhiteSpace(catalogId) ? "catalog" : catalogId.Trim();
        itemId = string.IsNullOrWhiteSpace(itemId) ? "item" : itemId.Trim();
        return $"shop:{catalogId}:{itemId}";
    }

    private void TrySetRowIcon(WorkshopUpgradeRowView row, Sprite icon)
    {
        if (row == null || icon == null) return;
        if (_fiIconImage == null) return;

        try
        {
            var img = _fiIconImage.GetValue(row) as Image;
            if (img != null)
            {
                img.sprite = icon;
                img.enabled = true;
            }
        }
        catch
        {
            // Silent: icon is optional; we don't want to spam logs.
        }
    }

    private IEnumerator CoScrollToTopInitial()
    {
        if (scrollRect == null)
            yield break;

        yield return null;
        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 1f;
    }
}

/// <summary>
/// Optional hook for menu-specific behavior (VFX, unlocks, quest triggers, etc).
/// Assign any MonoBehaviour implementing this interface into ShopListUI.actionHandler.
/// </summary>
public interface IShopActionHandler
{
    void OnPurchased(string catalogId, string itemId, int newLevel, int maxLevel);
}
