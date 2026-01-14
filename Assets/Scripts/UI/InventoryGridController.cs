using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using GalacticFishing; // Fish, FishRegistry, FishMetaIndex

// Uses FishMetaIndex to sort by FishMeta rarity at runtime
public class InventoryGridController : MonoBehaviour
{
    [Header("References")]
    public RectTransform Content;            // ScrollView/Viewport/Content
    public GridLayoutGroup Grid;             // on Content
    public InventorySlot SlotPrefab;         // Prefabs/UI/Slot_Prefab

    [Header("Fish Data")]
    public FishRegistry FishRegistry;        // Assign: Assets/Data/FishRegistry.asset
    public bool HideZeroItems = false;       // show zeros so ?-mark logic can run
    public bool DimZeroInsteadOfHide = true; // if zeros are shown, dim them

    [Header("Layout")]
    public int Columns = 13;                 // how many columns of slots
    public bool AutoSizeContent = true;
    public bool SnapScrollToTop = true;

    [Header("Sorting")]
    public InventorySortMode SortMode = InventorySortMode.OwnedFirst;

    [Header("Data")]
    public FishMetaIndex FishMetaIndex;      // assign: Assets/Data/FishMetaIndex.asset

    [Header("Paging")]
    public bool UsePaging = true;
    [Tooltip("Visible rows per page (eg. 6 if you want a 6×13=78 slot page).")]
    public int RowsPerPage = 6;

    [Header("Debug")]
    public bool GenerateDemoIfEmpty = false;
    public int DemoItemCount = 40;

    // backing store for ALL items (not a page slice)
    public List<ItemData> Items = new();

    // paging state
    public int Page { get; private set; } = 0;        // 0-based
    public int TotalPages { get; private set; } = 1;

    int PageSize => Mathf.Max(1, Columns * Mathf.Max(1, RowsPerPage));

    bool _subscribed;

    // Detail panel (built on first use)
    InventoryDetailView _detailView;

    void Awake() => TryCacheRefs();

    void OnEnable()
    {
        TryCacheRefs();

        if (FishRegistry != null && !InventoryService.IsInitialized)
        {
            Debug.Log($"[InventoryGrid] OnEnable – initializing InventoryService with registry '{FishRegistry.name}'", this);
            InventoryService.Initialize(FishRegistry);
        }

        if (!_subscribed)
        {
            InventoryService.OnChanged += HandleInventoryChanged;
            _subscribed = true;
        }

        Debug.Log(
            $"[InventoryGrid] OnEnable – IsInitialized={InventoryService.IsInitialized}, " +
            $"Content={(Content ? Content.name : "null")}, " +
            $"SlotPrefab={(SlotPrefab ? SlotPrefab.name : "null")}",
            this
        );

        // First build
        RebuildFromInventory();
    }

    void OnDisable()
    {
        if (_subscribed)
        {
            InventoryService.OnChanged -= HandleInventoryChanged;
            _subscribed = false;
        }
    }

    void OnValidate()
    {
        // keep numbers sane while editing
        Columns     = Mathf.Max(1, Columns);
        RowsPerPage = Mathf.Max(1, RowsPerPage);
        if (UsePaging) ClampPage();
    }

    void HandleInventoryChanged() => RebuildFromInventory();

    public void SetSortMode(InventorySortMode mode)
    {
        if (SortMode == mode) return;
        SortMode = mode;
        RebuildFromInventory();
    }

    // ------- external paging API (used by InventoryPager) -------
    public void SetPage(int page)
    {
        if (!UsePaging) return;
        Page = Mathf.Clamp(page, 0, Mathf.Max(0, TotalPages - 1));
        Populate(); // draw the current page slice
    }

    public void PrevPage() => SetPage(Page - 1);
    public void NextPage() => SetPage(Page + 1);

    void ClampPage() => Page = Mathf.Clamp(Page, 0, Mathf.Max(0, TotalPages - 1));

    // ------- Public entry kept for existing callers -------
    public void Populate()
    {
        if (!Content)
        {
            Debug.LogWarning("[InventoryGrid] Populate aborted – Content is null.", this);
            return;
        }

        if (!SlotPrefab)
        {
            Debug.LogWarning("[InventoryGrid] Populate aborted – SlotPrefab is null.", this);
            return;
        }

        // Clear
        for (int i = Content.childCount - 1; i >= 0; i--)
            Destroy(Content.GetChild(i).gameObject);

        if (Items == null || Items.Count == 0)
        {
            Debug.Log("[InventoryGrid] Populate – Items list is empty, no slots created.", this);
            if (AutoSizeContent) UpdateContentSize();
            // still snap scroll to top so behaviour is deterministic
            if (SnapScrollToTop)
            {
                var srEmpty = Content.GetComponentInParent<ScrollRect>();
                if (srEmpty) srEmpty.verticalNormalizedPosition = 1f;
            }
            return;
        }

        if (UsePaging)
        {
            // slice the visible window
            int start = Page * PageSize;
            int end   = Mathf.Min(start + PageSize, Items.Count);

            Debug.Log($"[InventoryGrid] Populate – drawing page {Page + 1}/{TotalPages}, slots {start}..{end - 1}", this);

            for (int i = start; i < end; i++)
            {
                var slot = Instantiate(SlotPrefab, Content);
                slot.name = $"Slot_{i:000}";
                slot.Bind(Items[i]);
                slot.Clicked -= HandleSlotClicked; // avoid double subscription
                slot.Clicked += HandleSlotClicked;
            }
        }
        else
        {
            Debug.Log($"[InventoryGrid] Populate – drawing all {Items.Count} items (no paging).", this);

            for (int i = 0; i < Items.Count; i++)
            {
                var slot = Instantiate(SlotPrefab, Content);
                slot.name = $"Slot_{i:000}";
                slot.Bind(Items[i]);
                slot.Clicked -= HandleSlotClicked;
                slot.Clicked += HandleSlotClicked;
            }
        }

        if (AutoSizeContent) UpdateContentSize();

        // Make sure first show starts at top
        if (SnapScrollToTop)
        {
            var sr = Content.GetComponentInParent<ScrollRect>();
            if (sr) sr.verticalNormalizedPosition = 1f;
        }
    }

    public void UpdateContentSize()
    {
        if (!Grid || !Content) return;

        int cols = Mathf.Max(1, Columns);
        int rows;
        if (UsePaging)
        {
            rows = Mathf.Max(1, RowsPerPage);
        }
        else
        {
            rows = Mathf.CeilToInt(Mathf.Max(1, Items.Count) / (float)cols);
        }

        float w = Grid.padding.left + Grid.padding.right
                + cols * Grid.cellSize.x + (cols - 1) * Grid.spacing.x;

        float h = Grid.padding.top + Grid.padding.bottom
                + rows * Grid.cellSize.y + (rows - 1) * Grid.spacing.y;

        // Respect how the Content is anchored:
        // if it is fully stretched in an axis (0..1), do not override sizeDelta on that axis.
        var rt = Content;
        bool stretchX = Mathf.Approximately(rt.anchorMin.x, 0f) &&
                        Mathf.Approximately(rt.anchorMax.x, 1f);
        bool stretchY = Mathf.Approximately(rt.anchorMin.y, 0f) &&
                        Mathf.Approximately(rt.anchorMax.y, 1f);

        // Start from current sizeDelta and only override non-stretched axes
        Vector2 size = rt.sizeDelta;

        // For non-stretched axes we’re allowed to drive size
        if (!stretchX) size.x = w;
        if (!stretchY) size.y = h;

        rt.sizeDelta = size;
    }

    void TryCacheRefs()
    {
        if (!Content) Content = GetComponent<RectTransform>();
        if (!Grid && Content) Grid = Content.GetComponent<GridLayoutGroup>();
    }

    // ------- Build Items list from InventoryService + registry, then Populate() -------
    void RebuildFromInventory()
    {
        Items ??= new List<ItemData>();
        Items.Clear();

        Debug.Log(
            $"[InventoryGrid] RebuildFromInventory – IsInitialized={InventoryService.IsInitialized}, " +
            $"FishRegistry={(FishRegistry ? FishRegistry.name : "null")}",
            this
        );

        // If not initialized yet, try to initialize once more here.
        if (!InventoryService.IsInitialized && FishRegistry != null)
        {
            Debug.Log("[InventoryGrid] RebuildFromInventory – trying to initialize InventoryService.", this);
            InventoryService.Initialize(FishRegistry);
        }

        if (!InventoryService.IsInitialized)
        {
            Debug.LogWarning("[InventoryGrid] InventoryService is not initialized. Using demo/empty data.", this);

            if (GenerateDemoIfEmpty)
            {
                // fallback demo
                for (int i = 0; i < DemoItemCount; i++)
                {
                    Items.Add(new ItemData
                    {
                        Icon     = null,
                        Count    = Random.Range(0, 128),
                        Rarity   = Rarity.Common,
                        Disabled = false,
                        Tooltip  = "Demo"
                    });
                }
            }

            ComputeTotalPages();
            Populate();
            return;
        }

        // Owned-first ordering: bucket by count
        var owned = new List<ItemData>(128);
        var zeros = new List<ItemData>(128);

        int rowsFromService = 0;

        foreach (var (id, fish, count) in InventoryService.All())
        {
            rowsFromService++;

            bool isZero = count <= 0;
            if (HideZeroItems && isZero) continue;

            FishMeta meta = null;
            if (FishMetaIndex && fish) meta = FishMetaIndex.FindByFish(fish);

            string label = GetLabelFromMetaOrFish(meta, fish);
            var rarityMapped = meta != null
                ? MapRarity(meta.rarity)
                : MapRarity(fish ? fish.rarity : FishRarity.Common);

            var data = new ItemData
            {
                // identity so click can open correct records
                RegistryId = id,
                FishDef    = fish,

                Icon       = fish ? fish.sprite : null,
                Count      = Mathf.Max(0, count),
                Disabled   = (!HideZeroItems && isZero) && DimZeroInsteadOfHide,
                Rarity     = rarityMapped,
                Tooltip    = label
            };

            if (isZero) zeros.Add(data);
            else        owned.Add(data);
        }

        Debug.Log($"[InventoryGrid] RebuildFromInventory – InventoryService.All() rows={rowsFromService}, owned={owned.Count}, zeros={zeros.Count}", this);

        ApplySorting(owned, zeros);

        Debug.Log($"[InventoryGrid] RebuildFromInventory – Items.Count after sorting={Items.Count}", this);

        ComputeTotalPages();
        Populate();
    }

    void ComputeTotalPages()
    {
        if (UsePaging)
        {
            int ps = Mathf.Max(1, PageSize);
            TotalPages = Mathf.Max(1, Mathf.CeilToInt(Items.Count / (float)ps));
            ClampPage();
        }
        else
        {
            TotalPages = 1;
            Page = 0;
        }
    }

    static Rarity MapRarity(FishRarity r) => r switch
    {
        FishRarity.Uncommon      => Rarity.Uncommon,
        FishRarity.Rare          => Rarity.Rare,
        FishRarity.Epic          => Rarity.Epic,
        FishRarity.Legendary     => Rarity.Legendary,
        FishRarity.UberLegendary => Rarity.Mythic,
        FishRarity.OneOfAKind    => Rarity.Mythic,
        _                        => Rarity.Common
    };

    // ---------- Records page ----------
    void HandleSlotClicked(InventorySlot slot)
    {
        var d = slot != null ? slot.Data : null;
        if (d == null || d.Count <= 0) return;

        // Build/find detail panel under the same frame that hosts the grid and pager.
        var contentFrame = Content ? Content.transform.parent as RectTransform : null; // Inventory-background/ContentFrame
        if (!_detailView && contentFrame) _detailView = InventoryDetailView.EnsureUnder(contentFrame);

        // Hide grid + pager while detail is shown
        ToggleGridAndPager(false, contentFrame);
        _detailView.Show(d.Tooltip, d.RegistryId, onBack: () =>
        {
            _detailView.Hide();
            ToggleGridAndPager(true, contentFrame);
        });
    }

    void ToggleGridAndPager(bool show, RectTransform contentFrame)
    {
        if (Content) Content.gameObject.SetActive(show);
        var pager = contentFrame ? contentFrame.Find("Pager") : null;
        if (pager) pager.gameObject.SetActive(show);
    }

    void ApplySorting(List<ItemData> owned, List<ItemData> zeros)
    {
        Items.Clear();
        switch (SortMode)
        {
            case InventorySortMode.OwnedFirst:
            case InventorySortMode.Quantity:
            {
                var all = owned.Concat(zeros)
                               .OrderByDescending(d => d.Count)
                               .ThenBy(NameKey);
                Items.AddRange(all);
                break;
            }
            case InventorySortMode.Name:
            {
                var all = owned.Concat(zeros).OrderBy(NameKey);
                Items.AddRange(all);
                break;
            }
            case InventorySortMode.Rarity:
            {
                var all = owned.Concat(zeros)
                               .OrderByDescending(GetRarityRank)
                               .ThenBy(NameKey);
                Items.AddRange(all);

                if (DebugSortLogs)
                {
                    try
                    {
                        var preview = Items.Take(10)
                                           .Select(d => $"{d.Tooltip} — rank {GetRarityRank(d)} (src:{GetSourceRarityName(d)})");
                        Debug.Log("[InventoryGridController] Rarity order (top 10):\n" + string.Join("\n", preview));
                    }
                    catch
                    {
                        // ignore errors when logging preview
                    }
                }
                break;
            }
        }
    }

    static string NameKey(ItemData data)
    {
        return (data?.Tooltip ?? string.Empty).Trim().ToLowerInvariant();
    }

    static string GetLabelFromMetaOrFish(FishMeta meta, Fish fish)
    {
        string dn = TryGetDisplayName(meta);
        if (!string.IsNullOrWhiteSpace(dn)) return dn;
        dn = TryGetDisplayName(fish);
        if (!string.IsNullOrWhiteSpace(dn)) return dn;
        return fish ? fish.name : "Unknown Fish";
    }

    static string TryGetDisplayName(object obj)
    {
        if (obj == null) return null;
        const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase;
        var type = obj.GetType();
        var prop = type.GetProperty("DisplayName", BF) ?? type.GetProperty("displayName", BF);
        if (prop != null && prop.CanRead)
        {
            try
            {
                var value = prop.GetValue(obj, null) as string;
                if (!string.IsNullOrWhiteSpace(value)) return value;
            }
            catch { }
        }
        var field = type.GetField("DisplayName", BF) ?? type.GetField("displayName", BF);
        if (field != null)
        {
            try
            {
                var value = field.GetValue(obj) as string;
                if (!string.IsNullOrWhiteSpace(value)) return value;
            }
            catch { }
        }
        return null;
    }

    [Header("Debug (Sorting)")]
    public bool DebugSortLogs = false;

    int GetRarityRank(ItemData data)
    {
        // 1) Prefer FishMeta via index (source of truth).
        var fish = data != null ? data.FishDef : null;
        if (FishMetaIndex != null && fish != null)
        {
            var meta = FishMetaIndex.FindByFish(fish);
            if (meta != null)
            {
                return meta.rarity switch
                {
                    FishRarity.OneOfAKind    => 6,
                    FishRarity.UberLegendary => 5,
                    FishRarity.Legendary     => 4,
                    FishRarity.Epic          => 3,
                    FishRarity.Rare          => 2,
                    FishRarity.Uncommon      => 1,
                    _                        => 0,
                };
            }
        }

        // 2) Fallback to reflection for legacy defs.
        var rarityValue = TryGetMemberValueCI(fish, "rarity");
        if (rarityValue == null && fish != null)
        {
            foreach (var containerName in new[] { "meta", "fishMeta", "def", "definition", "data", "info" })
            {
                var container = TryGetMemberValueCI(fish, containerName);
                if (container == null) continue;
                rarityValue = TryGetMemberValueCI(container, "rarity");
                if (rarityValue != null) break;
            }
        }

        if (rarityValue != null)
        {
            return RankFromName(rarityValue.ToString());
        }

        return data != null ? CollapsedRarityRank(data.Rarity) : 0;
    }

    static int CollapsedRarityRank(Rarity rarity)
    {
        return rarity switch
        {
            Rarity.Mythic    => 5,
            Rarity.Legendary => 4,
            Rarity.Epic      => 3,
            Rarity.Rare      => 2,
            Rarity.Uncommon  => 1,
            _                => 0
        };
    }

    string GetSourceRarityName(ItemData data)
    {
        var fish = data != null ? data.FishDef : null;
        if (FishMetaIndex != null && fish != null)
        {
            var meta = FishMetaIndex.FindByFish(fish);
            if (meta != null) return meta.rarity.ToString();
        }
        if (fish == null) return "null";

        var value = TryGetMemberValueCI(fish, "rarity");
        if (value == null)
        {
            foreach (var containerName in new[] { "meta", "fishMeta", "def", "definition", "data", "info" })
            {
                var container = TryGetMemberValueCI(fish, containerName);
                if (container == null) continue;
                value = TryGetMemberValueCI(container, "rarity");
                if (value != null) break;
            }
        }
        return value != null ? value.ToString() : "n/a";
    }

    static object TryGetMemberValueCI(object target, string memberName)
    {
        if (target == null || string.IsNullOrEmpty(memberName)) return null;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase;

        var type = target.GetType();
        var prop = type.GetProperty(memberName, flags);
        if (prop != null)
        {
            try { return prop.GetValue(target); } catch { }
        }

        var field = type.GetField(memberName, flags);
        if (field != null)
        {
            try { return field.GetValue(target); } catch { }
        }

        return null;
    }

    static int RankFromName(string name)
    {
        if (string.IsNullOrEmpty(name)) return 0;
        var key = name.Replace(" ", string.Empty).ToLowerInvariant();
        return key switch
        {
            "oneofakind"    => 6,
            "uberlegendary" => 5,
            "legendary"     => 4,
            "epic"          => 3,
            "rare"          => 2,
            "uncommon"      => 1,
            _               => 0,
        };
    }
}

public enum InventorySortMode
{
    OwnedFirst = 0,
    Quantity   = 1,
    Name       = 2,
    Rarity     = 3
}
