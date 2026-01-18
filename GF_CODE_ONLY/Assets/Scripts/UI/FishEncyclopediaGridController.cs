using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GalacticFishing;
using GalacticFishing.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fish Encyclopedia (per-world + per-lake) grid.
/// Uses WorldManager's active world + lakeIndex to decide which fish to show.
///
/// RULE:
/// - The lake selection drives which fish list is shown.
/// - World is mainly the context/title, and determines which lakes exist.
/// - Fish list comes from WorldManager.GetActivePool() (matches gameplay spawning logic).
///
/// Display:
/// - Owned (inventory count > 0)     -> show fish sprite + count
/// - Discovered (caught before)     -> show fish sprite (even if auto-sold)
/// - Unknown (never caught before)  -> show unknown placeholder (Icon = null, Count = 0)
///
/// Paging *inside* the current lake list can be disabled with UsePaging = false.
/// </summary>
public sealed class FishEncyclopediaGridController : MonoBehaviour
{
    [Header("World Source")]
    [Tooltip("If assigned, encyclopedia will follow this manager's active world and lake.")]
    public WorldManager worldManager;

    [Header("Worlds (optional list for world pager)")]
    [Tooltip("All worlds that should appear in the encyclopedia world pager.")]
    public List<WorldDefinition> Worlds = new();

    [Header("References")]
    public RectTransform Content;            // Grid container
    public GridLayoutGroup Grid;             // GridLayoutGroup on Content
    public InventorySlot SlotPrefab;         // Slot_Prefab (Inventory Slot)

    [Header("Core Data")]
    [Tooltip("Same registry that InventoryService uses.")]
    public FishRegistry FishRegistry;
    [Tooltip("Meta index for rarity & display names (also used to map FishMeta -> Fish).")]
    public FishMetaIndex FishMetaIndex;

    [Header("Layout")]
    public int Columns = 13;
    public bool AutoSizeContent = true;
    public bool SnapScrollToTop = true;

    [Header("Paging (within current lake list)")]
    public bool UsePaging = true;
    [Tooltip("Visible rows per page when UsePaging is true.")]
    public int RowsPerPage = 6;

    [Header("Sorting")]
    public InventorySortMode SortMode = InventorySortMode.OwnedFirst;

    [Header("Discovery (auto-sell friendly)")]
    [Tooltip("If true, a fish is considered discovered if lifetime stats indicate it has ever been caught (InventoryStatsService), even if auto-sold so inventory count is 0).")]
    public bool UseLifetimeDiscovery = true;

    [Tooltip("Workaround for InventorySlot implementations that only show the real icon when Count > 0. " +
             "If enabled, discovered-but-not-owned fish get a fake Count=1 in ItemData (does NOT affect InventoryService).")]
    public bool FakeCountForDiscoveredFish = true;

    // Backing store for ALL items for the current lake
    public List<ItemData> Items { get; private set; } = new List<ItemData>();

    // Paging state (within lake)
    public int Page { get; private set; } = 0;        // 0-based
    public int TotalPages { get; private set; } = 1;

    int PageSize => Mathf.Max(1, Columns * Mathf.Max(1, RowsPerPage));

    // Local indices (used if no worldManager)
    [SerializeField] private int worldIndex = 0; // 0-based
    [SerializeField] private int lakeIndex = 0; // 0-based

    public int CurrentWorldIndex => worldIndex;
    public int CurrentLakeIndex => worldManager ? Mathf.Max(0, worldManager.lakeIndex) : Mathf.Max(0, lakeIndex);
    public int WorldCount => Worlds != null ? Worlds.Count : 0;

    bool _inventorySubscribed;
    bool _worldSubscribed;

    [Header("Optional Labels (TMP)")]
    [SerializeField] private TMP_Text worldLabel;
    [SerializeField] private TMP_Text lakeLabel;

    InventoryDetailView _detailView;

    // Cache: Fish -> registryId (index in FishRegistry)
    readonly Dictionary<Fish, int> _registryIdCache = new Dictionary<Fish, int>();

    void Awake()
    {
        TryCacheRefs();
    }

    void OnEnable()
    {
        TryCacheRefs();

        if (FishRegistry != null && !InventoryService.IsInitialized)
            InventoryService.Initialize(FishRegistry);

        if (!_inventorySubscribed)
        {
            InventoryService.OnChanged += HandleInventoryChanged;
            _inventorySubscribed = true;
        }

        if (worldManager && !_worldSubscribed)
        {
            worldManager.WorldChanged += HandleWorldChanged;
            _worldSubscribed = true;
        }

        RebuildForCurrentSelection();
    }

    void OnDisable()
    {
        if (_inventorySubscribed)
        {
            InventoryService.OnChanged -= HandleInventoryChanged;
            _inventorySubscribed = false;
        }

        if (_worldSubscribed && worldManager)
        {
            worldManager.WorldChanged -= HandleWorldChanged;
            _worldSubscribed = false;
        }
    }

    void OnValidate()
    {
        Columns = Mathf.Max(1, Columns);
        RowsPerPage = Mathf.Max(1, RowsPerPage);

        ClampWorldIndex();
        ClampLakeIndex();

        if (UsePaging) ClampPage();
    }

    void HandleInventoryChanged() => RebuildForCurrentSelection();

    void HandleWorldChanged(WorldDefinition w, int lake)
    {
        // Keep local indices in sync (helps fallback labeling when displayName is empty)
        worldIndex = GetCurrentWorldIndexInList();
        if (!worldManager) lakeIndex = lake;

        Page = 0;
        RebuildForCurrentSelection();
    }

    // --------------------------------------------------------------------
    // WORLD API (for buttons)
    // --------------------------------------------------------------------

    int GetCurrentWorldIndexInList()
    {
        if (Worlds == null || Worlds.Count == 0)
            return 0;

        if (worldManager && worldManager.world)
        {
            int idx = Worlds.IndexOf(worldManager.world);
            if (idx >= 0) return idx;
        }

        return Mathf.Clamp(worldIndex, 0, Worlds.Count - 1);
    }

    public void NextWorld()
    {
        if (Worlds == null || Worlds.Count == 0) return;

        int idx = GetCurrentWorldIndexInList();
        idx = (idx + 1) % Worlds.Count;

        worldIndex = idx;
        Page = 0;

        // Reset lake to 0 when switching world
        if (worldManager)
        {
            worldManager.SetWorld(Worlds[idx], 0);
            return; // WorldChanged will rebuild + update labels
        }

        lakeIndex = 0;
        RebuildForCurrentSelection();
    }

    public void PrevWorld()
    {
        if (Worlds == null || Worlds.Count == 0) return;

        int idx = GetCurrentWorldIndexInList();
        idx = (idx - 1 + Worlds.Count) % Worlds.Count;

        worldIndex = idx;
        Page = 0;

        if (worldManager)
        {
            worldManager.SetWorld(Worlds[idx], 0);
            return;
        }

        lakeIndex = 0;
        RebuildForCurrentSelection();
    }

    public void SetWorldIndex(int index)
    {
        if (Worlds == null || Worlds.Count == 0) return;

        int clamped = Mathf.Clamp(index, 0, Worlds.Count - 1);
        worldIndex = clamped;
        Page = 0;

        if (worldManager)
        {
            worldManager.SetWorld(Worlds[clamped], 0);
            return;
        }

        lakeIndex = 0;
        RebuildForCurrentSelection();
    }

    void ClampWorldIndex()
    {
        if (Worlds == null || Worlds.Count == 0)
            worldIndex = 0;
        else
            worldIndex = Mathf.Clamp(worldIndex, 0, Worlds.Count - 1);
    }

    // --------------------------------------------------------------------
    // LAKE API (for buttons)
    // --------------------------------------------------------------------

    int GetLakeCountForCurrentWorld()
    {
        var w = GetCurrentWorld();
        if (!w || w.lakes == null) return 0;
        return w.lakes.Count;
    }

    void ClampLakeIndex()
    {
        int count = GetLakeCountForCurrentWorld();
        if (count <= 0)
        {
            lakeIndex = 0;
            return;
        }

        lakeIndex = Mathf.Clamp(lakeIndex, 0, count - 1);
    }

    public void NextLake()
    {
        int count = GetLakeCountForCurrentWorld();
        if (count <= 0) return;

        Page = 0;

        int next = (CurrentLakeIndex + 1) % count;

        if (worldManager)
        {
            worldManager.SetLake(next);
            return;
        }

        lakeIndex = next;
        RebuildForCurrentSelection();
    }

    public void PrevLake()
    {
        int count = GetLakeCountForCurrentWorld();
        if (count <= 0) return;

        Page = 0;

        int prev = (CurrentLakeIndex - 1 + count) % count;

        if (worldManager)
        {
            worldManager.SetLake(prev);
            return;
        }

        lakeIndex = prev;
        RebuildForCurrentSelection();
    }

    public void SetLakeIndex(int index)
    {
        int count = GetLakeCountForCurrentWorld();
        if (count <= 0) return;

        Page = 0;

        int clamped = Mathf.Clamp(index, 0, count - 1);

        if (worldManager)
        {
            worldManager.SetLake(clamped);
            return;
        }

        lakeIndex = clamped;
        RebuildForCurrentSelection();
    }

    // --------------------------------------------------------------------
    // LABELS
    // --------------------------------------------------------------------

    public string GetCurrentWorldLabel()
    {
        var w = GetCurrentWorld();
        if (w != null && !string.IsNullOrWhiteSpace(w.displayName))
            return w.displayName.ToUpperInvariant();

        if (Worlds != null && Worlds.Count > 0)
        {
            int idx = GetCurrentWorldIndexInList();
            return $"WORLD {idx + 1}";
        }

        return "WORLD";
    }

    public string GetCurrentLakeLabel()
    {
        var w = GetCurrentWorld();
        if (!w) return "LAKE";

        int idx = CurrentLakeIndex;
        Lake lake = null;

        if (worldManager) lake = worldManager.GetLake(idx);
        else if (w.lakes != null && idx >= 0 && idx < w.lakes.Count) lake = w.lakes[idx];

        // Prefer LakeId (you asked for this)
        if (lake != null)
        {
            var id = TryGetLakeId(lake);
            if (!string.IsNullOrWhiteSpace(id))
                return id.ToUpperInvariant();
        }

        return $"LAKE {idx + 1}";
    }

    static string TryGetLakeId(Lake lake)
    {
        var t = lake.GetType();
        const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        var f = t.GetField("lakeId", BF);
        if (f != null)
        {
            try { return f.GetValue(lake) as string; } catch { }
        }

        var p = t.GetProperty("lakeId", BF);
        if (p != null && p.CanRead)
        {
            try { return p.GetValue(lake, null) as string; } catch { }
        }

        return null;
    }

    void UpdateLabels()
    {
        if (worldLabel) worldLabel.text = GetCurrentWorldLabel();
        if (lakeLabel) lakeLabel.text = GetCurrentLakeLabel();
    }

    // --------------------------------------------------------------------
    // PAGE API (within lake)
    // --------------------------------------------------------------------

    public void SetPage(int page)
    {
        if (!UsePaging) return;
        Page = Mathf.Clamp(page, 0, Mathf.Max(0, TotalPages - 1));
        Populate();
    }

    public void PrevPage() => SetPage(Page - 1);
    public void NextPage() => SetPage(Page + 1);

    void ClampPage() => Page = Mathf.Clamp(Page, 0, Mathf.Max(0, TotalPages - 1));

    // --------------------------------------------------------------------
    // VISUAL POPULATION
    // --------------------------------------------------------------------

    public void Populate()
    {
        if (!Content || !SlotPrefab) return;

        for (int i = Content.childCount - 1; i >= 0; i--)
            Destroy(Content.GetChild(i).gameObject);

        if (Items == null || Items.Count == 0)
        {
            UpdateContentSize();
            return;
        }

        if (UsePaging)
        {
            int start = Page * PageSize;
            int end = Mathf.Min(start + PageSize, Items.Count);

            for (int i = start; i < end; i++)
            {
                var slot = Instantiate(SlotPrefab, Content);
                slot.name = $"Slot_{i:000}";
                slot.Bind(Items[i]);
                slot.Clicked -= HandleSlotClicked;
                slot.Clicked += HandleSlotClicked;
            }
        }
        else
        {
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
        int rows = UsePaging ? Mathf.Max(1, RowsPerPage)
                             : Mathf.CeilToInt(Mathf.Max(1, Items.Count) / (float)cols);

        float w = Grid.padding.left + Grid.padding.right
                + cols * Grid.cellSize.x + (cols - 1) * Grid.spacing.x;

        float h = Grid.padding.top + Grid.padding.bottom
                + rows * Grid.cellSize.y + (rows - 1) * Grid.spacing.y;

        var rt = Content;
        bool stretchX = Mathf.Approximately(rt.anchorMin.x, 0f) &&
                        Mathf.Approximately(rt.anchorMax.x, 1f);
        bool stretchY = Mathf.Approximately(rt.anchorMin.y, 0f) &&
                        Mathf.Approximately(rt.anchorMax.y, 1f);

        Vector2 size = rt.sizeDelta;
        if (!stretchX) size.x = w;
        if (!stretchY) size.y = h;
        rt.sizeDelta = size;
    }

    void TryCacheRefs()
    {
        if (!Content) Content = GetComponent<RectTransform>();
        if (!Grid && Content) Grid = Content.GetComponent<GridLayoutGroup>();
    }

    WorldDefinition GetCurrentWorld()
    {
        if (worldManager && worldManager.world)
            return worldManager.world;

        if (Worlds == null || Worlds.Count == 0) return null;
        ClampWorldIndex();
        return Worlds[worldIndex];
    }

    // --------------------------------------------------------------------
    // BUILD ITEMS FOR CURRENT WORLD + LAKE
    // --------------------------------------------------------------------

    void RebuildForCurrentSelection()
    {
        Items ??= new List<ItemData>();
        Items.Clear();

        var world = GetCurrentWorld();
        if (!world)
        {
            Debug.LogWarning("[FishEncyclopedia] No current world set.", this);
            ComputeTotalPages();
            Populate();
            UpdateLabels();
            return;
        }

        var lakeFish = CollectActiveLakeFish(world);

        // Fish -> (registryIndex, ownedCount)
        var inventoryByFish = new Dictionary<Fish, (int registryIndex, int count)>();

        if (InventoryService.IsInitialized)
        {
            foreach (var (registryIndex, fish, count) in InventoryService.All())
            {
                if (!fish) continue;
                inventoryByFish[fish] = (registryIndex, Mathf.Max(0, count));
            }
        }

        var ownedList = new List<ItemData>(lakeFish.Count);
        var unknownList = new List<ItemData>(lakeFish.Count);

        int ownedCountTotal = 0;
        int discoveredTotal = 0;
        int unknownTotal = 0;

        foreach (var fish in lakeFish)
        {
            if (!fish) continue;

            int registryIndex = -1;
            int ownedCount = 0;

            if (inventoryByFish.TryGetValue(fish, out var entry))
            {
                registryIndex = entry.registryIndex;
                ownedCount = Mathf.Max(0, entry.count);
            }

            // If not in inventory (auto-sold), resolve registry id anyway so we can query lifetime stats.
            if (registryIndex < 0)
                registryIndex = ResolveRegistryId(fish);

            bool owned = ownedCount > 0;

            bool discovered = owned;
            if (!discovered && UseLifetimeDiscovery)
                discovered = HasLifetimeDiscovery(registryIndex);

            bool unknown = !discovered;

            if (owned) ownedCountTotal++;
            if (discovered) discoveredTotal++;
            if (unknown) unknownTotal++;

            FishMeta meta = null;
            if (FishMetaIndex && fish)
                meta = FishMetaIndex.FindByFish(fish);

            string label = GetLabelFromMetaOrFish(meta, fish);

            var rarityMapped = meta != null
                ? MapRarity(meta.rarity)
                : MapRarity(fish ? fish.rarity : FishRarity.Common);

            // IMPORTANT:
            // - Unknown fish => Icon = null, Count = 0 (your slot shows "Fish More" text)
            // - Discovered fish => Icon = fish.sprite even if ownedCount==0 (auto-sell)
            // - Some InventorySlot implementations only show icon when Count > 0.
            //   FakeCountForDiscoveredFish makes discovered icons show without touching InventoryService.
            int displayCount = 0;
            if (owned) displayCount = ownedCount;
            else if (discovered && FakeCountForDiscoveredFish) displayCount = 1;

            var data = new ItemData
            {
                RegistryId = registryIndex,
                FishDef = fish,
                Icon = discovered ? fish.sprite : null,
                Count = unknown ? 0 : displayCount,
                Disabled = false,
                Rarity = rarityMapped,
                Tooltip = label
            };

            if (unknown) unknownList.Add(data);
            else ownedList.Add(data);
        }

        ApplySorting(ownedList, unknownList);
        ComputeTotalPages();
        Populate();
        UpdateLabels();

        Debug.Log($"[FishEncyclopedia] World '{world.displayName}' LakeIndex {CurrentLakeIndex} – fish: {lakeFish.Count}, items: {Items.Count}, owned: {ownedCountTotal}, discovered: {discoveredTotal}, unknown: {unknownTotal}", this);
    }

    List<Fish> CollectActiveLakeFish(WorldDefinition world)
    {
        var result = new List<Fish>();
        var set = new HashSet<Fish>();

        void AddFromPool(IEnumerable<FishWeight> pool)
        {
            if (pool == null) return;

            foreach (var fw in pool)
            {
                if (!fw.fish) continue;

                var fishDef = ResolveFishFromObject(fw.fish);
                if (!fishDef) continue;

                if (set.Add(fishDef))
                    result.Add(fishDef);
            }
        }

        if (worldManager)
        {
            AddFromPool(worldManager.GetActivePool());

            var lake = worldManager.GetLake(CurrentLakeIndex);
            if (lake != null && lake.isBossLake && lake.bossFish)
            {
                var boss = ResolveFishFromObject(lake.bossFish);
                if (boss && set.Add(boss))
                    result.Add(boss);
            }
        }
        else
        {
            Lake lake = null;
            if (world.lakes != null && CurrentLakeIndex >= 0 && CurrentLakeIndex < world.lakes.Count)
                lake = world.lakes[CurrentLakeIndex];

            if (lake != null && lake.usePoolOverride && lake.poolOverride != null && lake.poolOverride.Count > 0)
                AddFromPool(lake.poolOverride);
            else
                AddFromPool(world.defaultPool);

            if (lake != null && lake.isBossLake && lake.bossFish)
            {
                var boss = ResolveFishFromObject(lake.bossFish);
                if (boss && set.Add(boss))
                    result.Add(boss);
            }
        }

        return result;
    }

    Fish ResolveFishFromObject(UnityEngine.Object obj)
    {
        if (!obj) return null;

        if (obj is Fish directFish)
            return directFish;

        if (obj is FishMeta meta)
        {
            if (FishMetaIndex)
            {
                var fromIndex = FishMetaIndex.FindFishByMeta(meta);
                if (fromIndex) return fromIndex;
            }

            var tMeta = meta.GetType();
            const BindingFlags BFMeta = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            foreach (var field in tMeta.GetFields(BFMeta))
            {
                if (!typeof(Fish).IsAssignableFrom(field.FieldType)) continue;
                try
                {
                    var val = field.GetValue(meta) as Fish;
                    if (val) return val;
                }
                catch { }
            }

            foreach (var prop in tMeta.GetProperties(BFMeta))
            {
                if (!prop.CanRead || !typeof(Fish).IsAssignableFrom(prop.PropertyType)) continue;
                try
                {
                    var val = prop.GetValue(meta, null) as Fish;
                    if (val) return val;
                }
                catch { }
            }
        }

        var type = obj.GetType();
        const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        foreach (var prop in type.GetProperties(BF))
        {
            if (!prop.CanRead || !typeof(Fish).IsAssignableFrom(prop.PropertyType)) continue;
            try
            {
                var value = prop.GetValue(obj, null) as Fish;
                if (value) return value;
            }
            catch { }
        }

        foreach (var field in type.GetFields(BF))
        {
            if (!typeof(Fish).IsAssignableFrom(field.FieldType)) continue;
            try
            {
                var value = field.GetValue(obj) as Fish;
                if (value) return value;
            }
            catch { }
        }

        Debug.LogWarning($"[FishEncyclopedia] Could not resolve Fish from '{obj.name}' (type {type.Name}).", obj);
        return null;
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

    void HandleSlotClicked(InventorySlot slot)
    {
        var d = slot != null ? slot.Data : null;
        if (d == null || d.Icon == null) return;

        var contentFrame = Content ? Content.transform.parent as RectTransform : null;
        if (!_detailView && contentFrame) _detailView = InventoryDetailView.EnsureUnder(contentFrame);

        ToggleGridAndPager(false, contentFrame);

        _detailView.Show(d.Tooltip, d.RegistryId, d.Icon, onBack: () =>
        {
            _detailView.Hide();
            ToggleGridAndPager(true, contentFrame);
        });
    }

    void ToggleGridAndPager(bool show, RectTransform contentFrame)
    {
        if (Content) Content.gameObject.SetActive(show);

        // NOTE: Your scene has PagerWorld + PagerLake; older code searched for "Pager".
        // We keep the old behavior but also try to toggle both if they exist.
        if (contentFrame)
        {
            var parent = contentFrame.parent;
            if (parent)
            {
                var pager = parent.Find("Pager");
                if (pager) pager.gameObject.SetActive(show);

                var pagerWorld = parent.Find("PagerWorld");
                if (pagerWorld) pagerWorld.gameObject.SetActive(show);

                var pagerLake = parent.Find("PagerLake");
                if (pagerLake) pagerLake.gameObject.SetActive(show);
            }
        }
    }

    public void SetSortMode(InventorySortMode mode)
    {
        if (SortMode == mode) return;
        SortMode = mode;
        RebuildForCurrentSelection();
    }

    void ApplySorting(List<ItemData> discovered, List<ItemData> unknown)
    {
        Items.Clear();

        switch (SortMode)
        {
            case InventorySortMode.OwnedFirst:
            case InventorySortMode.Quantity:
            {
                var all = discovered.Concat(unknown)
                                    .OrderByDescending(d => d.Icon != null)
                                    .ThenBy(NameKey);
                Items.AddRange(all);
                break;
            }
            case InventorySortMode.Name:
            {
                var all = discovered.Concat(unknown).OrderBy(NameKey);
                Items.AddRange(all);
                break;
            }
            case InventorySortMode.Rarity:
            {
                var all = discovered.Concat(unknown)
                                    .OrderByDescending(GetRarityRank)
                                    .ThenBy(NameKey);
                Items.AddRange(all);
                break;
            }
        }
    }

    static string NameKey(ItemData data) => (data?.Tooltip ?? string.Empty).Trim().ToLowerInvariant();

    static Rarity MapRarity(FishRarity r) => r switch
    {
        FishRarity.Uncommon => Rarity.Uncommon,
        FishRarity.Rare => Rarity.Rare,
        FishRarity.Epic => Rarity.Epic,
        FishRarity.Legendary => Rarity.Legendary,
        FishRarity.UberLegendary => Rarity.Mythic,
        FishRarity.OneOfAKind => Rarity.Mythic,
        _ => Rarity.Common
    };

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

        const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public |
                                BindingFlags.NonPublic | BindingFlags.IgnoreCase;

        var type = obj.GetType();

        var prop = type.GetProperty("DisplayName", BF) ??
                   type.GetProperty("displayName", BF);

        if (prop != null && prop.CanRead)
        {
            try
            {
                var value = prop.GetValue(obj, null) as string;
                if (!string.IsNullOrWhiteSpace(value)) return value;
            }
            catch { }
        }

        var field = type.GetField("DisplayName", BF) ??
                    type.GetField("displayName", BF);

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

    int GetRarityRank(ItemData data)
    {
        var fish = data != null ? data.FishDef : null;
        if (FishMetaIndex != null && fish != null)
        {
            var meta = FishMetaIndex.FindByFish(fish);
            if (meta != null)
            {
                return meta.rarity switch
                {
                    FishRarity.OneOfAKind => 6,
                    FishRarity.UberLegendary => 5,
                    FishRarity.Legendary => 4,
                    FishRarity.Epic => 3,
                    FishRarity.Rare => 2,
                    FishRarity.Uncommon => 1,
                    _ => 0,
                };
            }
        }

        return data != null ? CollapsedRarityRank(data.Rarity) : 0;
    }

    static int CollapsedRarityRank(Rarity rarity) => rarity switch
    {
        Rarity.Mythic => 5,
        Rarity.Legendary => 4,
        Rarity.Epic => 3,
        Rarity.Rare => 2,
        Rarity.Uncommon => 1,
        _ => 0
    };

    // --------------------------------------------------------------------
    // RegistryId resolving (for auto-sell: fish never appears in InventoryService.All())
    // --------------------------------------------------------------------

    int ResolveRegistryId(Fish fish)
    {
        if (!fish) return -1;
        if (_registryIdCache.TryGetValue(fish, out var cached)) return cached;

        int id = -1;

        // Try InventoryService helper methods (if any exist) via reflection.
        id = TryResolveIdViaInventoryService(fish);
        if (id < 0)
            id = TryResolveIdViaFishRegistry(fish);

        _registryIdCache[fish] = id;
        return id;
    }

    static int TryResolveIdViaInventoryService(Fish fish)
    {
        try
        {
            var t = typeof(InventoryService);
            const BindingFlags BF = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

            // Look for something like: int GetId(Fish fish)
            var mi = t.GetMethods(BF)
                      .FirstOrDefault(m =>
                          m.ReturnType == typeof(int) &&
                          m.GetParameters().Length == 1 &&
                          m.GetParameters()[0].ParameterType == typeof(Fish));

            if (mi != null)
            {
                var val = mi.Invoke(null, new object[] { fish });
                if (val is int i) return i;
            }
        }
        catch { }

        return -1;
    }

    int TryResolveIdViaFishRegistry(Fish fish)
    {
        if (!FishRegistry || !fish) return -1;

        var t = FishRegistry.GetType();
        const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // Try common methods: IndexOf/GetId/GetIndex(Fish)
        try
        {
            var method = t.GetMethods(BF)
                .FirstOrDefault(m =>
                {
                    if (m.ReturnType != typeof(int)) return false;
                    var p = m.GetParameters();
                    if (p.Length != 1) return false;
                    return p[0].ParameterType == typeof(Fish);
                });

            if (method != null)
            {
                var val = method.Invoke(FishRegistry, new object[] { fish });
                if (val is int i) return i;
            }
        }
        catch { }

        // Try scanning a list/array field/property that contains Fish
        try
        {
            foreach (var f in t.GetFields(BF))
            {
                var ft = f.FieldType;

                if (ft.IsArray && ft.GetElementType() == typeof(Fish))
                {
                    var arr = f.GetValue(FishRegistry) as Fish[];
                    if (arr != null)
                    {
                        int idx = Array.IndexOf(arr, fish);
                        if (idx >= 0) return idx;
                    }
                }

                if (typeof(IList<Fish>).IsAssignableFrom(ft))
                {
                    var list = f.GetValue(FishRegistry) as IList<Fish>;
                    if (list != null)
                    {
                        int idx = list.IndexOf(fish);
                        if (idx >= 0) return idx;
                    }
                }
            }

            foreach (var p in t.GetProperties(BF))
            {
                if (!p.CanRead) continue;

                var pt = p.PropertyType;

                if (pt.IsArray && pt.GetElementType() == typeof(Fish))
                {
                    var arr = p.GetValue(FishRegistry, null) as Fish[];
                    if (arr != null)
                    {
                        int idx = Array.IndexOf(arr, fish);
                        if (idx >= 0) return idx;
                    }
                }

                if (typeof(IList<Fish>).IsAssignableFrom(pt))
                {
                    var list = p.GetValue(FishRegistry, null) as IList<Fish>;
                    if (list != null)
                    {
                        int idx = list.IndexOf(fish);
                        if (idx >= 0) return idx;
                    }
                }
            }
        }
        catch { }

        return -1;
    }

    // --------------------------------------------------------------------
    // Lifetime discovery (InventoryStatsService) via reflection to avoid hard coupling.
    // --------------------------------------------------------------------

    static Type _statsType;

    static bool HasLifetimeDiscovery(int registryId)
    {
        if (registryId < 0) return false;

        try
        {
            _statsType ??= FindStatsServiceType();
            if (_statsType == null) return false;

            // Best-effort: if any of the "TryGet*Record" methods says true, we consider it discovered.
            if (TryInvokeBoolOut(_statsType, "TryGetWeightRecord", registryId)) return true;
            if (TryInvokeBoolOut(_statsType, "TryGetLengthRecord", registryId)) return true;
            if (TryInvokeBoolOut(_statsType, "TryGetQualityRecord", registryId)) return true;

            // Some projects name them slightly differently:
            if (TryInvokeBoolOutLoose(_statsType, registryId, "Weight", "Record")) return true;
            if (TryInvokeBoolOutLoose(_statsType, registryId, "Length", "Record")) return true;
            if (TryInvokeBoolOutLoose(_statsType, registryId, "Quality", "Record")) return true;

            // If there is a total caught API, use it
            if (TryInvokeIntGetter(_statsType, registryId, out var totalCaught) && totalCaught > 0)
                return true;

            // If there is a TryGetStats(id, out stats) and stats has totalFishCaught
            if (TryInvokeTryGetStatsTotalCaught(_statsType, registryId, out var caught2) && caught2 > 0)
                return true;
        }
        catch { }

        return false;
    }

    static Type FindStatsServiceType()
    {
        // Common full names first
        var candidates = new[]
        {
            "GalacticFishing.InventoryStatsService",
            "InventoryStatsService"
        };

        foreach (var full in candidates)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var t = asm.GetType(full);
                    if (t != null) return t;
                }
                catch { }
            }
        }

        // Fallback: scan by short name
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type t = null;
            try
            {
                t = asm.GetTypes().FirstOrDefault(x => x.Name == "InventoryStatsService");
            }
            catch { }
            if (t != null) return t;
        }

        return null;
    }

    static bool TryInvokeBoolOut(Type t, string methodName, int id)
    {
        const BindingFlags BF = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        var mi = t.GetMethods(BF).FirstOrDefault(m =>
        {
            if (!string.Equals(m.Name, methodName, StringComparison.Ordinal)) return false;
            if (m.ReturnType != typeof(bool)) return false;

            var p = m.GetParameters();
            if (p.Length != 2) return false;
            if (p[0].ParameterType != typeof(int)) return false;
            return p[1].IsOut;
        });

        if (mi == null) return false;

        var outType = mi.GetParameters()[1].ParameterType.GetElementType();
        object outVal = outType != null && outType.IsValueType ? Activator.CreateInstance(outType) : null;

        var args = new object[] { id, outVal };
        var result = mi.Invoke(null, args);
        return result is bool b && b;
    }

    static bool TryInvokeBoolOutLoose(Type t, int id, string mustContainA, string mustContainB)
    {
        const BindingFlags BF = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        var mi = t.GetMethods(BF).FirstOrDefault(m =>
        {
            if (m.ReturnType != typeof(bool)) return false;
            if (m.Name.IndexOf(mustContainA, StringComparison.OrdinalIgnoreCase) < 0) return false;
            if (m.Name.IndexOf(mustContainB, StringComparison.OrdinalIgnoreCase) < 0) return false;

            var p = m.GetParameters();
            if (p.Length != 2) return false;
            if (p[0].ParameterType != typeof(int)) return false;
            return p[1].IsOut;
        });

        if (mi == null) return false;

        var outType = mi.GetParameters()[1].ParameterType.GetElementType();
        object outVal = outType != null && outType.IsValueType ? Activator.CreateInstance(outType) : null;

        var args = new object[] { id, outVal };
        var result = mi.Invoke(null, args);
        return result is bool b && b;
    }

    static bool TryInvokeIntGetter(Type t, int id, out int value)
    {
        value = 0;
        const BindingFlags BF = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        // Try common method names
        var names = new[]
        {
            "GetTotalFishCaught",
            "GetTotalCaught",
            "TotalFishCaught",
            "TotalCaught"
        };

        foreach (var name in names)
        {
            var mi = t.GetMethods(BF).FirstOrDefault(m =>
            {
                if (!string.Equals(m.Name, name, StringComparison.Ordinal)) return false;
                if (m.ReturnType != typeof(int)) return false;
                var p = m.GetParameters();
                return p.Length == 1 && p[0].ParameterType == typeof(int);
            });

            if (mi != null)
            {
                var res = mi.Invoke(null, new object[] { id });
                if (res is int i)
                {
                    value = i;
                    return true;
                }
            }
        }

        // Try TryGetTotalFishCaught(int, out int)
        var tryNames = new[]
        {
            "TryGetTotalFishCaught",
            "TryGetTotalCaught"
        };

        foreach (var name in tryNames)
        {
            var mi = t.GetMethods(BF).FirstOrDefault(m =>
            {
                if (!string.Equals(m.Name, name, StringComparison.Ordinal)) return false;
                if (m.ReturnType != typeof(bool)) return false;
                var p = m.GetParameters();
                return p.Length == 2 && p[0].ParameterType == typeof(int) && p[1].IsOut && p[1].ParameterType.GetElementType() == typeof(int);
            });

            if (mi != null)
            {
                object outVal = 0;
                var args = new object[] { id, outVal };
                var ok = mi.Invoke(null, args);
                if (ok is bool b && b && args[1] is int i)
                {
                    value = i;
                    return true;
                }
            }
        }

        return false;
    }

    static bool TryInvokeTryGetStatsTotalCaught(Type t, int id, out int totalCaught)
    {
        totalCaught = 0;
        const BindingFlags BF = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        // Try: TryGetStats(int id, out <something>)
        var mi = t.GetMethods(BF).FirstOrDefault(m =>
        {
            if (m.ReturnType != typeof(bool)) return false;
            if (m.Name.IndexOf("TryGet", StringComparison.OrdinalIgnoreCase) < 0) return false;
            if (m.Name.IndexOf("Stat", StringComparison.OrdinalIgnoreCase) < 0) return false;

            var p = m.GetParameters();
            if (p.Length != 2) return false;
            if (p[0].ParameterType != typeof(int)) return false;
            return p[1].IsOut;
        });

        if (mi == null) return false;

        var outType = mi.GetParameters()[1].ParameterType.GetElementType();
        object outVal = outType != null && outType.IsValueType ? Activator.CreateInstance(outType) : null;

        var args = new object[] { id, outVal };
        var okObj = mi.Invoke(null, args);
        if (!(okObj is bool ok) || !ok) return false;

        var statsObj = args[1];
        if (statsObj == null) return false;

        // Look for totalFishCaught anywhere on that returned object (field or property)
        const BindingFlags BF2 = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase;
        var st = statsObj.GetType();

        try
        {
            var prop = st.GetProperty("totalFishCaught", BF2) ?? st.GetProperty("TotalFishCaught", BF2);
            if (prop != null && prop.CanRead && prop.PropertyType == typeof(int))
            {
                totalCaught = (int)prop.GetValue(statsObj, null);
                return true;
            }

            var field = st.GetField("totalFishCaught", BF2) ?? st.GetField("TotalFishCaught", BF2);
            if (field != null && field.FieldType == typeof(int))
            {
                totalCaught = (int)field.GetValue(statsObj);
                return true;
            }
        }
        catch { }

        return false;
    }
}
