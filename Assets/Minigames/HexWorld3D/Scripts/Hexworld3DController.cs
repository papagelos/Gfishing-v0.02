// Assets/Minigames/HexWorld3D/Scripts/HexWorld3DController.cs
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using GalacticFishing.Progress;

namespace GalacticFishing.Minigames.HexWorld
{
    public sealed class HexWorld3DController : MonoBehaviour
    {
        private const int CurrentSaveVersion = 3;
        private static HexWorldBalanceConfig _defaultBalanceConfig;
        [Header("Refs")]
        [SerializeField] private Transform cursorGhostParent;
        [SerializeField] private Camera mainCamera;
        [SerializeField] private GameObject ownedPrefab;
        [SerializeField] private GameObject frontierPrefab;
        [SerializeField] private Transform tilesParent;

        [Header("Tile Style")]
        [Tooltip("Optional: style applied to the starting tile at (0,0). If null, it uses the prefab's materials.")]
        [SerializeField] private HexWorldTileStyle startingStyle;

        [Header("Style Application")]
        [Tooltip("If >= 0, forces which material slot is considered the TOP slot (e.g. your prefab shows TOP in Element 1). Set -1 to auto-detect by material name containing 'top'.")]
        [SerializeField] private int topMaterialSlotOverride = -1;

        [Header("Hex spacing")]
        [Tooltip("Set to 0 to auto-detect from ownedPrefab renderer bounds. Otherwise tweak until tiles touch perfectly.")]
        [SerializeField] private float hexSize = 0f;

        [Header("Economy (prototype)")]
        [SerializeField] private int startingCredits = 9999;
        [SerializeField] private int costPerTile = 10;

        // ============================
        // Tile-budget placement (Village)
        // ============================
        [Header("Tile Budget Placement Mode (Village Start)")]
        [Tooltip("If ON: start empty. Player has TilesLeftToPlace budget and places tiles anywhere (snapped). TileBar selects style; cursor shows ghost; LMB places.")]
        [SerializeField] private bool enableTileBudgetPlacement = false;

        [Tooltip("How many tiles the player can place initially (e.g. 37).")]
        [SerializeField] private int startingTilesToPlace = 37;

        [Tooltip("If true, after the first tile, all new placements must be adjacent (distance 1) to existing owned tiles. Allows long strips, prevents scattered islands.")]
        [SerializeField] private bool requireAdjacencyAfterFirst = true;

        [Tooltip("Allow painting (retexturing) an already-placed tile by clicking it while a style is selected.")]
        [SerializeField] private bool allowRepaintOnOwnedClick = true;

        [Tooltip("Right-click removes an owned tile. If refundOnRemove is true, refunds 1 tile back to TilesLeftToPlace.")]
        [SerializeField] private bool enableRightClickRemove = true;

        [SerializeField] private bool refundOnRemove = true;

        [Header("Drag Paint")]
        [SerializeField] private bool enableDragPaint = true;

        private bool _dragHasLast;
        private HexCoord _dragLastCoord;

        [Header("Cursor Ghost")]
        [Tooltip("If null, uses ownedPrefab.")]
        [SerializeField] private GameObject cursorGhostPrefab;

        [Range(0.05f, 1f)]
        [SerializeField] private float ghostAlpha = 0.35f;

        [SerializeField] private float ghostYOffset = 0.01f;

        [Tooltip("If true, we attempt to tint the ghost red when placement is invalid (only works if shader exposes a color property).")]
        [SerializeField] private bool tintGhostWhenInvalid = true;

        [SerializeField] private Color invalidGhostTint = new Color(1f, 0.3f, 0.3f, 1f);

        // ============================
        // Buildings (NEW)
        // ============================
        public enum PaletteMode { Tiles, Roads, Buildings, TileUpgrade, Delete }

        [Header("Buildings")]
        [Tooltip("Catalog used to resolve saved building names back to assets.")]
        [SerializeField] private HexWorldBuildingDefinition[] buildingCatalog;

        [Tooltip("Optional parent for spawned buildings. If empty, creates/uses child named 'Buildings' under this controller.")]
        [SerializeField] private Transform buildingsParent;

        [Header("Building Context Menu (TICKET 1)")]
        [Tooltip("Context menu UI that appears when clicking a building.")]
        [SerializeField] private HexWorldBuildingContextMenu buildingContextMenu;

        [Header("Town Hall / Slots (Design Doc)")]
        [Tooltip("Town Hall level controls Active Slots (Design Doc Section 8). For now this is a simple integer; later it can be driven by a TownHall building upgrade.")]
        [SerializeField, Range(1, 10)] private int townHallLevel = 1;

        [Header("Warehouse (Design Doc)")]
        [Tooltip("Warehouse inventory component. If left empty, the controller will create one on this GameObject.")]
        [SerializeField] private HexWorldWarehouseInventory warehouse;

        [Header("Town Hall Upgrade Gating (TICKET 003)")]
        [Tooltip("World progression provider for gating Town Hall upgrades. If null, uses a mock provider.")]
        [SerializeField] private MonoBehaviour worldProgressionProvider;

        [Header("Starting Village (TICKET 004)")]
        [Tooltip("Tile style for the core tile at (0,0) when starting a new game (no autosave).")]
        [SerializeField] private HexWorldTileStyle townHallStartingStyle;

        [Tooltip("Town Hall building definition placed on (0,0) when starting a new game (no autosave).")]
        [SerializeField] private HexWorldBuildingDefinition townHallBuildingDef;
        [Header("Economy & Balance")]
        [SerializeField] private HexWorldBalanceConfig balanceConfig;

        [Header("Village Save/Load")]
        [Tooltip("If ON, load autosave when entering the village.")]
        [SerializeField] private bool autoLoadOnStart = true;

        [Tooltip("If ON, autosave when exiting/unloading this scene.")]
        [SerializeField] private bool autoSaveOnExit = true;

        [Tooltip("Optional: if set, ExitVillage() will load this scene after saving. Leave empty if you handle scene switching elsewhere.")]
        [SerializeField] private string exitSceneName = "";

        [Tooltip("Style catalog used to resolve saved style names back to assets (drag same list you use in the tile bar).")]
        [SerializeField] private HexWorldTileStyle[] styleCatalog;

        [Header("Town Hall Tiers")]
        [Tooltip("Ordered list of Town Tier definitions (index 0 = Tier 1).")]
        [SerializeField] private TownTierDefinition[] townTierDefinitions;

        // Events for UI
        public event Action<HexWorldTileStyle> SelectedStyleChanged;
        public event Action<HexWorldBuildingDefinition> SelectedBuildingChanged;
        public event Action<PaletteMode> PaletteModeChanged;

        public event Action<string> ToastRequested;
        public event Action<int> TilesLeftChanged;
        public event Action<int> CreditsChanged;
        public event Action<int, int, int> ActiveSlotsChanged; // used, total, townHallLevel
        public event Action<int> TownHallLevelChanged; // townHallLevel
        public event Action<int, int> TilesPlacedChanged; // tilesPlaced, maxCapacity
        public event Action ExitRequested;
        public event Action BlueprintUnlocksChanged;
        public event Action OnProgressionUnlocksChanged;
        public event Action RoadNetworkRecomputed;

        public HexWorldTileStyle SelectedStyle { get; private set; }
        public HexWorldBuildingDefinition SelectedBuilding { get; private set; }
        public PaletteMode CurrentPaletteMode => _paletteMode;

        public int TilesLeftToPlace => _tilesLeftToPlace;
        public int TilesPlaced => _owned.Count;
        public int Credits => _credits;

        public int TownHallLevel => townHallLevel;
        public int ActiveSlotsTotal => HexWorldCapacityService.GetActiveSlots(townHallLevel);
        public int ActiveBuildingsUsed => _activeBuildingsUsed;
        public int TileCapacityMax => HexWorldCapacityService.GetTileCapacity(townHallLevel);
        public int BuildingCapacityMax
        {
            get
            {
                var tierDef = GetTownTierDefinition(townHallLevel);
                return tierDef != null && tierDef.buildingCap > 0 ? tierDef.buildingCap : TileCapacityMax;
            }
        }
        public int BuildingsPlaced => _buildings.Count;

        // Public accessor for district bonus calculations
        public Dictionary<HexCoord, HexWorld3DTile> OwnedTiles => _owned;

        // Public accessor for style catalog (used by TileBar sync)
        public HexWorldTileStyle[] GetStyleCatalog() => styleCatalog;

        // Public accessor for building catalog (used by UI Toolkit HUD)
        public HexWorldBuildingDefinition[] GetBuildingCatalog() => buildingCatalog;

        public bool TryGetCursorPlacementState(out HexCoord coord, out bool isValid)
        {
            if (!TryGetCursorCoord(out coord))
            {
                isValid = false;
                return false;
            }

            isValid = IsPlacementValidForCurrentMode(coord);
            return true;
        }

        public bool IsBuildingPlacementValid(HexCoord coord, HexWorldBuildingDefinition def)
        {
            if (def == null || !def.prefab)
                return false;

            if (!IsUnlocked(def))
                return false;

            if (!_owned.TryGetValue(coord, out var tile) || !tile)
                return false;

            if (_buildings.TryGetValue(coord, out var existing) && existing)
                return false;

            return true;
        }

        // ============================
        // Town Hall Upgrade Gating (TICKET 003)
        // ============================

        /// <summary>
        /// Returns the world progression provider interface.
        /// Falls back to a mock implementation if not set.
        /// </summary>
        private GalacticFishing.IWorldProgression WorldProgression
        {
            get
            {
                if (worldProgressionProvider != null && worldProgressionProvider is GalacticFishing.IWorldProgression wp)
                    return wp;

                // Mock fallback: return level 1
                return new MockWorldProgressionFallback();
            }
        }

        /// <summary>
        /// Placeholder for external Land Deed check.
        /// Currently returns true. Will be wired to external system later.
        /// </summary>
        public struct TownHallMilestoneRequirement
        {
            public HexWorldResourceId id;
            public int quantity;
            public string label;
        }

        private static string GetCanonicalBuildingId(HexWorldBuildingDefinition def)
        {
            if (def == null) return string.Empty;
            string id = def.buildingName;
            return string.IsNullOrWhiteSpace(id) ? def.name : id;
        }

        private static bool IsAlwaysUnlockedBlueprint(HexWorldBuildingDefinition def)
        {
            if (def == null) return false;

            if (def.kind == HexWorldBuildingDefinition.BuildingKind.TownHall ||
                def.kind == HexWorldBuildingDefinition.BuildingKind.Warehouse)
            {
                return true;
            }

            string id = GetCanonicalBuildingId(def);
            return string.Equals(id, "Building_Townhall", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(id, "Building_TownHall", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(id, "Building_Warehouse", StringComparison.OrdinalIgnoreCase);
        }

        public bool IsBlueprintUnlocked(HexWorldBuildingDefinition def)
        {
            if (def == null) return false;
            if (IsAlwaysUnlockedBlueprint(def)) return true;

            var unlocked = PlayerProgressManager.Instance?.Data?.gear?.unlockedBlueprintIds;
            if (unlocked == null || unlocked.Count == 0) return false;

            string buildingId = GetCanonicalBuildingId(def);
            if (string.IsNullOrWhiteSpace(buildingId)) return false;

            for (int i = 0; i < unlocked.Count; i++)
            {
                if (string.Equals(unlocked[i], buildingId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private bool MeetsPlacementPrerequisites(List<string> requiredBuildingPlacementNames)
        {
            if (requiredBuildingPlacementNames == null || requiredBuildingPlacementNames.Count == 0)
                return true;

            for (int i = 0; i < requiredBuildingPlacementNames.Count; i++)
            {
                string requiredName = requiredBuildingPlacementNames[i];
                if (string.IsNullOrWhiteSpace(requiredName))
                    continue;

                if (!_placedBuildingNamesThisSession.Contains(requiredName.Trim()))
                    return false;
            }

            return true;
        }

        public bool IsUnlocked(HexWorldBuildingDefinition def)
        {
            if (def == null)
                return false;

            if (def.unlockTownTier > townHallLevel)
                return false;

            if (!IsBlueprintUnlocked(def))
                return false;

            return MeetsPlacementPrerequisites(def.requiredBuildingPlacementNames);
        }

        public bool IsUnlocked(HexWorldTileStyle style)
        {
            if (style == null)
                return false;

            if (style.unlockTownTier > townHallLevel)
                return false;

            return MeetsPlacementPrerequisites(style.requiredBuildingPlacementNames);
        }

        private bool TryRegisterPlacedBuildingForProgression(string buildingName)
        {
            if (string.IsNullOrWhiteSpace(buildingName))
                return false;

            return _placedBuildingNamesThisSession.Add(buildingName.Trim());
        }

        private void SeedPlacedBuildingNamesFromSessionBuildings()
        {
            bool changed = false;

            foreach (var kv in _buildings)
            {
                var inst = kv.Value;
                if (!inst)
                    continue;

                string buildingId = inst.buildingName;
                if (string.IsNullOrWhiteSpace(buildingId))
                    buildingId = _buildingNameByCoord.TryGetValue(kv.Key, out var cachedId) ? cachedId : string.Empty;

                if (TryRegisterPlacedBuildingForProgression(buildingId))
                    changed = true;
            }

            if (changed)
                OnProgressionUnlocksChanged?.Invoke();
        }

        public void UnlockBlueprint(string buildingId)
        {
            if (string.IsNullOrWhiteSpace(buildingId))
                return;

            var progress = PlayerProgressManager.Instance;
            if (progress?.Data?.gear == null)
                return;

            buildingId = buildingId.Trim();
            var unlocked = progress.Data.gear.unlockedBlueprintIds;
            if (unlocked == null)
                unlocked = progress.Data.gear.unlockedBlueprintIds = new List<string>();

            for (int i = 0; i < unlocked.Count; i++)
            {
                if (string.Equals(unlocked[i], buildingId, StringComparison.OrdinalIgnoreCase))
                    return;
            }

            unlocked.Add(buildingId);
            progress.Save();
            BlueprintUnlocksChanged?.Invoke();
        }

        private bool HasExternalLandDeed()
        {
            // Legacy placeholder no longer used but kept for potential future wiring.
            return true;
        }

        /// <summary>
        /// Fallback implementation when no world progression provider is set.
        /// </summary>
        private class MockWorldProgressionFallback : GalacticFishing.IWorldProgression
        {
            public int HighestUnlockedWorldNumber => 1;
        }

        private PaletteMode _paletteMode = PaletteMode.Tiles;

        private readonly Dictionary<HexCoord, HexWorld3DTile> _owned = new();
        private readonly Dictionary<HexCoord, HexWorld3DTile> _frontier = new();
        private HexWorldProductionTicker _productionTicker;
        private bool _balanceApplied;
        private bool _warnedMissingBalance;
        private bool _warnedMissingTileTierUpgradeCosts;
        private bool _warnedShortTileTierUpgradeCosts;

        // Coord -> style name (since HexWorld3DTile doesn't store style)
        private readonly Dictionary<HexCoord, string> _ownedStyleName = new();

        // Coord -> building instance
        private readonly Dictionary<HexCoord, HexWorldBuildingInstance> _buildings = new();
        private readonly Dictionary<HexCoord, string> _buildingNameByCoord = new();
        private readonly HashSet<string> _placedBuildingNamesThisSession = new(StringComparer.OrdinalIgnoreCase);

        private int _credits;

        private int _activeBuildingsUsed;

        // Road connectivity (TICKET 8)
        private readonly HashSet<HexCoord> _townHallRoadComponent = new();
        private const string RoadTag = "road";

        // Tile budget state
        private int _tilesLeftToPlace;
        private GameObject _cursorGhost;
        private HexCoord _cursorCoord;
        private bool _cursorValid;

        // MaterialPropertyBlock for ghost tint (no allocations, no shared material edits)
        private MaterialPropertyBlock _ghostMpb;
        private float _nextUiBlockLogTime;

        private void Awake()
        {
            Debug.Log($"Awake: HexWorld3DController on {gameObject.name}, TH level = {townHallLevel}");

            _ghostMpb = new MaterialPropertyBlock();

            _credits = startingCredits;
            CreditsChanged?.Invoke(_credits);

            if (!mainCamera) mainCamera = Camera.main;

            if (!tilesParent)
            {
                var go = new GameObject("Tiles");
                go.transform.SetParent(transform, false);
                tilesParent = go.transform;
            }

            if (hexSize <= 0f)
                hexSize = AutoDetectHexSizeOrFallback(0.5f);

            EnsureWarehouse();
        }

        private void Start()
        {
            if (enableTileBudgetPlacement)
            {
                ClearAllTiles();

                Debug.Log($"Start: Initial TH level from Inspector: {townHallLevel}");

                // Initialize tiles based on current Town Hall level (for testing/manual setup)
                // If startingTilesToPlace doesn't match Town Hall capacity, use Town Hall capacity
                int capacityForLevel = HexWorldCapacityService.GetTileCapacity(townHallLevel);
                _tilesLeftToPlace = capacityForLevel;
                TilesLeftChanged?.Invoke(_tilesLeftToPlace);

                Debug.Log($"Start: Initialized with {_tilesLeftToPlace} tiles for TH L{townHallLevel}");

                EnsureCursorGhost();
                SetPaletteModeTiles();
                SetSelectedStyle(null);
                SetSelectedBuilding(null);

                if (autoLoadOnStart)
                {
                    if (TryLoadAutosave())
                    {
                        RequestToast($"Village loaded: {_owned.Count} tiles, {_tilesLeftToPlace} left, TH L{townHallLevel}");
                    }
                    else
                    {
                        // TICKET 004: No autosave found - initialize starting village
                        InitializeStartingVillage(capacityForLevel);
                    }
                }
                else
                {
                    // TICKET 004: Auto-load disabled - initialize starting village
                    InitializeStartingVillage(capacityForLevel);
                }

                SeedPlacedBuildingNamesFromSessionBuildings();
                RecomputeActiveSlotsAndNotify();
                TilesPlacedChanged?.Invoke(_owned.Count, TileCapacityMax);

                // Tutorial triggering is now handled by AIStorySceneMonitor (decoupled)

                return;
            }

            // ---- Original prototype behavior ----
            AddOwned(new HexCoord(0, 0), startingStyle);
            RefreshFrontier();
            SetSelectedStyle(null);

            // Tutorial triggering is now handled by AIStorySceneMonitor (decoupled)
        }

        /// <summary>
        /// TICKET 004: Initializes a new village with core tile at (0,0) and Town Hall building.
        /// Called when no autosave is found.
        /// </summary>
        private void InitializeStartingVillage(int capacityForLevel)
        {
            Debug.Log("InitializeStartingVillage: Creating new village with core tile and Town Hall");

            // 1. Place the starting tile at (0,0)
            HexCoord coreCoord = new HexCoord(0, 0);
            HexWorldTileStyle coreStyle = townHallStartingStyle ? townHallStartingStyle : startingStyle;

            if (coreStyle)
            {
                AddOwned(coreCoord, coreStyle);
                Debug.Log($"InitializeStartingVillage: Placed core tile at (0,0) with style {coreStyle.name}");
            }
            else
            {
                Debug.LogWarning("InitializeStartingVillage: No townHallStartingStyle or startingStyle set. Core tile will use prefab materials.");
                AddOwned(coreCoord, null);
            }

            // 2. Update tiles-left budget (1 tile already placed)
            _tilesLeftToPlace = capacityForLevel - 1;
            TilesLeftChanged?.Invoke(_tilesLeftToPlace);
            Debug.Log($"InitializeStartingVillage: Tiles left to place: {_tilesLeftToPlace} (capacity {capacityForLevel} - 1 core tile)");

            // 3. Place Town Hall building on (0,0)
            if (townHallBuildingDef && townHallBuildingDef.prefab)
            {
                // Temporarily set selected building
                var previousSelection = SelectedBuilding;
                SetSelectedBuilding(townHallBuildingDef);

                // Place the building using existing logic
                TryPlaceBuildingAtCoord(coreCoord);

                // Restore selection (null = no building attached to cursor)
                SetSelectedBuilding(previousSelection);

                Debug.Log($"InitializeStartingVillage: Placed Town Hall on core tile at (0,0)");
            }
            else
            {
                Debug.LogWarning("InitializeStartingVillage: townHallBuildingDef or its prefab is missing. Town Hall not placed.");
            }

            // 4. Select a default tile style so placement can begin immediately.
            if (styleCatalog != null && styleCatalog.Length > 0)
            {
                HexWorldTileStyle defaultStyle = null;

                for (int i = 0; i < styleCatalog.Length; i++)
                {
                    var candidate = styleCatalog[i];
                    if (candidate != null && candidate.category == TileCategory.Cosmetic)
                    {
                        defaultStyle = candidate;
                        break;
                    }
                }

                if (defaultStyle == null)
                {
                    for (int i = 0; i < styleCatalog.Length; i++)
                    {
                        if (styleCatalog[i] != null)
                        {
                            defaultStyle = styleCatalog[i];
                            break;
                        }
                    }
                }

                if (defaultStyle != null)
                    SetSelectedStyle(defaultStyle);
            }

            // 5. Save autosave so this starting village persists
            SaveAutosave();
            Debug.Log("InitializeStartingVillage: Autosave created with starting village");

            // Show toast to player
            RequestToast($"New village: 1 tile placed, {_tilesLeftToPlace} tiles left (TH L{townHallLevel})");
        }

        private void OnDisable()
        {
            if (!Application.isPlaying) return;
            if (!enableTileBudgetPlacement) return;
            if (!autoSaveOnExit) return;

            SaveAutosave();
        }

        private void OnApplicationQuit()
        {
            if (!enableTileBudgetPlacement) return;
            if (!autoSaveOnExit) return;

            SaveAutosave();
        }

        private Transform GetGhostParent()
        {
            if (cursorGhostParent) return cursorGhostParent;

            var t = transform.Find("Ghosts");
            if (!t)
            {
                var go = new GameObject("Ghosts");
                go.transform.SetParent(transform, false);
                t = go.transform;
            }
            cursorGhostParent = t;
            return cursorGhostParent;
        }

        private Transform GetBuildingsParent()
        {
            if (buildingsParent) return buildingsParent;

            var t = transform.Find("Buildings");
            if (!t)
            {
                var go = new GameObject("Buildings");
                go.transform.SetParent(transform, false);
                t = go.transform;
            }
            buildingsParent = t;
            return buildingsParent;
        }

        private void Update()
        {
            if (Mouse.current == null)
                return;

            UpdateBuildingCooldowns();
            PropagateBalanceConfig();

            // TICKET 1: Close context menu on Escape
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (buildingContextMenu && buildingContextMenu.IsVisible)
                {
                    buildingContextMenu.Hide();
                    return;
                }
            }

            if (enableTileBudgetPlacement)
            {
                // UNIFIED BUILDING INTERACTION:
                // If nothing is selected (SelectedStyle == null AND SelectedBuilding == null),
                // left-click always attempts building interaction regardless of PaletteMode.
                bool nothingSelected = (SelectedStyle == null && SelectedBuilding == null);

                // Handle left-click input
                if (Mouse.current.leftButton.wasPressedThisFrame)
                {
                    bool pointerBlockedByUi = ShouldBlockWorldInputByUI();
                    if (pointerBlockedByUi)
                    {
                        if (nothingSelected)
                            LogUiBlockerIfNeeded();
                    }
                    else
                    {
                        // PRIORITY: If nothing selected, always try building interaction (tile-driven)
                        if (nothingSelected && _paletteMode != PaletteMode.Delete)
                        {
                            TryInteractWithBuildingUnderMouse();
                        }
                        else if (_paletteMode == PaletteMode.Delete)
                        {
                            // Delete mode: LMB click deletes building or tile under mouse
                            if (TryGetCursorCoord(out var coord))
                                TryRemoveBuildingOrTileAtCoord(coord);
                        }
                        else if (IsTilePaletteMode(_paletteMode) && SelectedStyle != null)
                        {
                            // Placing/painting tiles - handled by drag paint below
                        }
                        else if (_paletteMode == PaletteMode.Buildings && SelectedBuilding != null)
                        {
                            // Placing a building - use coord-based placement
                            if (TryGetCursorCoord(out var coord))
                            {
                                TryPlaceBuildingAtCoord(coord);
                            }
                        }
                        else if (_paletteMode == PaletteMode.TileUpgrade)
                        {
                            // Tile Upgrade mode: click to upgrade a tile's tier
                            if (TryGetCursorCoord(out var coord))
                            {
                                TryUpgradeTileTier(coord);
                            }
                        }
                    }
                }

                // Ghost visibility and drag paint (Tiles/Roads modes only)
                if (IsTilePaletteMode(_paletteMode))
                {
                    UpdateCursorGhost();

                    if (_cursorGhost)
                    {
                        bool rmbHeld = Mouse.current.rightButton.isPressed;
                        _cursorGhost.SetActive(SelectedStyle != null && !rmbHeld);
                    }

                    // Drag paint / place tiles (only when SelectedStyle is set)
                    if (enableDragPaint && SelectedStyle != null && Mouse.current.leftButton.isPressed)
                    {
                        if (!ShouldBlockWorldInputByUI())
                        {
                            if (TryGetCursorCoord(out var coord))
                            {
                                if (!_dragHasLast || coord.q != _dragLastCoord.q || coord.r != _dragLastCoord.r)
                                {
                                    _dragHasLast = true;
                                    _dragLastCoord = coord;

                                    // MODE GATING: Block painting on tiles with buildings
                                    if (_buildings.ContainsKey(coord))
                                    {
                                        RequestToast("Exit painting mode to interact with buildings.");
                                    }
                                    else
                                    {
                                        TryPlaceOrPaintAtCoord(coord);
                                    }
                                }
                            }
                        }
                    }

                    if (Mouse.current.leftButton.wasReleasedThisFrame)
                        _dragHasLast = false;
                }
                else if (_paletteMode == PaletteMode.Delete)
                {
                    // Delete mode: no ghost, allow drag delete
                    if (_cursorGhost) _cursorGhost.SetActive(false);

                    // Drag delete (LMB held removes buildings/tiles under cursor)
                    if (Mouse.current.leftButton.isPressed)
                    {
                        if (!ShouldBlockWorldInputByUI())
                        {
                            if (TryGetCursorCoord(out var coord))
                            {
                                if (!_dragHasLast || coord.q != _dragLastCoord.q || coord.r != _dragLastCoord.r)
                                {
                                    _dragHasLast = true;
                                    _dragLastCoord = coord;
                                    TryRemoveBuildingOrTileAtCoord(coord);
                                }
                            }
                        }
                    }

                    if (Mouse.current.leftButton.wasReleasedThisFrame)
                        _dragHasLast = false;
                }
                else
                {
                    // Non-tile modes: no ghost, no drag
                    if (_cursorGhost) _cursorGhost.SetActive(false);
                    _dragHasLast = false;
                }

                return;
            }

            // ---- Original prototype input ----
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (ShouldBlockWorldInputByUI())
                    return;

                TryClick();
            }
        }

        private void LogUiBlockerIfNeeded()
        {
            if (Time.unscaledTime < _nextUiBlockLogTime)
                return;

            _nextUiBlockLogTime = Time.unscaledTime + 0.5f;
            Debug.Log($"[HexWorld3DController] UI is blocking world click: {DescribeUiBlocker()}");
        }

        private bool ShouldBlockWorldInputByUI()
        {
            // UI Toolkit: if we are over any non-pass-through UI element, block world input.
            // This must NOT depend on EventSystem, because UI Toolkit clicks may not register there.
            if (!IsPointerOverPassThroughUi())
                return true;

            // uGUI: if you have any uGUI overlay elements, still respect them too.
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        private bool IsPointerOverPassThroughUi()
        {
            if (Mouse.current == null)
                return false;

            var documents = UnityEngine.Object.FindObjectsByType<UnityEngine.UIElements.UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (documents == null || documents.Length == 0)
                return true; // no UI Toolkit docs => nothing to block

            System.Array.Sort(documents, (a, b) =>
            {
                float orderA = a != null ? a.sortingOrder : float.MinValue;
                float orderB = b != null ? b.sortingOrder : float.MinValue;
                return orderB.CompareTo(orderA);
            });

            Vector2 screenPos = Mouse.current.position.ReadValue();

            for (int i = 0; i < documents.Length; i++)
            {
                var doc = documents[i];
                if (!doc || !doc.isActiveAndEnabled)
                    continue;

                var root = doc.rootVisualElement;
                if (root == null || root.panel == null)
                    continue;

                Vector2 panelPos = UnityEngine.UIElements.RuntimePanelUtils.ScreenToPanel(root.panel, screenPos);
                var picked = root.panel.Pick(panelPos);

                if (picked != null)
                {
                    if (!IsPassThroughUiHit(root, picked))
                    {
                        return false;
                    }

                    // Pass-through hit (e.g., ScreenRoot), so keep checking lower-priority documents.
                }
            }

            // No solid blocker found in any document.
            return true;
        }

        private static bool IsPassThroughUiHit(UnityEngine.UIElements.VisualElement root, UnityEngine.UIElements.VisualElement picked)
        {
            if (root == null || picked == null)
                return false;

            if (IsInsideInteractiveElement(picked))
                return false;

            return string.Equals(picked.name, "ScreenRoot", StringComparison.Ordinal);
        }

        private static bool IsInsideInteractiveElement(UnityEngine.UIElements.VisualElement picked)
        {
            for (var current = picked; current != null; current = current.parent)
            {
                if (current is UnityEngine.UIElements.Button ||
                    current is UnityEngine.UIElements.Toggle ||
                    current is UnityEngine.UIElements.TextField ||
                    current is UnityEngine.UIElements.Label ||
                    current is UnityEngine.UIElements.TextElement ||
                    current is UnityEngine.UIElements.ScrollView)
                {
                    return true;
                }
            }

            return false;
        }

        private string DescribeUiBlocker()
        {
            if (Mouse.current == null)
                return "Unknown UI element";

            var documents = UnityEngine.Object.FindObjectsOfType<UnityEngine.UIElements.UIDocument>(true);

            if (documents == null || documents.Length == 0)
                return "Unknown UI element";

            Vector2 screenPos = Mouse.current.position.ReadValue();

            for (int i = 0; i < documents.Length; i++)
            {
                var doc = documents[i];
                if (!doc || !doc.isActiveAndEnabled)
                    continue;

                var root = doc.rootVisualElement;
                if (root == null || root.panel == null)
                    continue;

                Vector2 panelPos = UnityEngine.UIElements.RuntimePanelUtils.ScreenToPanel(root.panel, screenPos);
                var picked = root.panel.Pick(panelPos);
                if (picked == null)
                    continue;

                if (!IsPassThroughUiHit(root, picked))
                {
                    string pickedName = string.IsNullOrWhiteSpace(picked.name) ? "<unnamed>" : picked.name;
                    return $"{pickedName} ({picked.GetType().Name})";
                }
            }

            return "Unknown UI element";
        }

        private void UpdateBuildingCooldowns()
        {
            if (_buildings == null || _buildings.Count == 0) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            foreach (var kv in _buildings)
            {
                var inst = kv.Value;
                if (!inst) continue;

                float current = inst.GetRelocationCooldown();
                if (current <= 0f) continue;

                inst.SetRelocationCooldown(current - dt);
            }
        }

        private void PropagateBalanceConfig()
        {
            if (_balanceApplied) return;

            if (!balanceConfig && !_warnedMissingBalance)
            {
                Debug.LogError("[HexWorld3DController] BalanceConfig is missing. Falling back to internal defaults.");
                _warnedMissingBalance = true;
            }

            if (_productionTicker == null)
                _productionTicker = UnityEngine.Object.FindObjectOfType<HexWorldProductionTicker>(true);

            if (_productionTicker == null) return;

            _productionTicker.ApplyBalanceConfig(GetResolvedBalanceConfig());
            _balanceApplied = true;
        }

        private HexWorldBalanceConfig GetResolvedBalanceConfig()
        {
            if (balanceConfig != null)
                return balanceConfig;

            if (_defaultBalanceConfig != null)
                return _defaultBalanceConfig;

            _defaultBalanceConfig = ScriptableObject.CreateInstance<HexWorldBalanceConfig>();
            _defaultBalanceConfig.hideFlags = HideFlags.HideAndDontSave;
            return _defaultBalanceConfig;
        }

        // ============================
        // Palette mode switching (for your left-side tabs)
        // ============================
        private static bool IsTilePaletteMode(PaletteMode mode)
        {
            return mode == PaletteMode.Tiles || mode == PaletteMode.Roads;
        }

        public void SetPaletteModeTiles()
        {
            if (_paletteMode == PaletteMode.Tiles) return;
            _paletteMode = PaletteMode.Tiles;
            PaletteModeChanged?.Invoke(_paletteMode);

            // Clear stale building selection when switching to Tiles mode
            if (SelectedBuilding != null)
            {
                SelectedBuilding = null;
                SelectedBuildingChanged?.Invoke(null);
            }

            EnsureCursorGhost();
            UpdateCursorGhostVisibility();

            // TICKET 1: Close context menu when switching modes
            if (buildingContextMenu) buildingContextMenu.Hide();
        }

        public void SetPaletteModeRoads()
        {
            if (_paletteMode != PaletteMode.Roads)
            {
                _paletteMode = PaletteMode.Roads;
                PaletteModeChanged?.Invoke(_paletteMode);
            }

            // Clear stale building selection when switching to Roads mode.
            if (SelectedBuilding != null)
            {
                SelectedBuilding = null;
                SelectedBuildingChanged?.Invoke(null);
            }

            HexWorldTileStyle basicRoadStyle = null;
            if (styleCatalog != null)
            {
                for (int i = 0; i < styleCatalog.Length; i++)
                {
                    var candidate = styleCatalog[i];
                    if (candidate != null && candidate.name == "TileStyle_BasicRoad")
                    {
                        basicRoadStyle = candidate;
                        break;
                    }
                }
            }

            if (basicRoadStyle != null)
            {
                if (SelectedStyle != basicRoadStyle)
                {
                    SetSelectedStyle(basicRoadStyle);
                }
                else
                {
                    SelectedStyleChanged?.Invoke(SelectedStyle);
                }
            }
            else
            {
                Debug.LogWarning($"[{nameof(HexWorld3DController)}] Could not find TileStyle_BasicRoad in styleCatalog.");
            }

            EnsureCursorGhost();
            if (SelectedStyle != null)
                ApplyStyleToGhost();
            UpdateCursorGhostVisibility();

            // TICKET 1: Close context menu when switching modes
            if (buildingContextMenu) buildingContextMenu.Hide();
        }

        public void SetPaletteModeBuildings()
        {
            if (_paletteMode == PaletteMode.Buildings) return;
            _paletteMode = PaletteMode.Buildings;
            PaletteModeChanged?.Invoke(_paletteMode);

            // Clear stale tile style selection when switching to Buildings mode
            if (SelectedStyle != null)
            {
                SelectedStyle = null;
                SelectedStyleChanged?.Invoke(null);
            }

            if (_cursorGhost) _cursorGhost.SetActive(false);

            // TICKET 1: Close context menu when switching modes
            if (buildingContextMenu) buildingContextMenu.Hide();
        }

        public void SetPaletteModeTileUpgrade()
        {
            if (_paletteMode == PaletteMode.TileUpgrade) return;
            _paletteMode = PaletteMode.TileUpgrade;
            PaletteModeChanged?.Invoke(_paletteMode);

            SetSelectedStyle(null);
            SetSelectedBuilding(null);

            if (_cursorGhost) _cursorGhost.SetActive(false);

            // TICKET 1: Close context menu when switching modes
            if (buildingContextMenu) buildingContextMenu.Hide();
        }

        public void SetPaletteModeDelete()
        {
            if (_paletteMode == PaletteMode.Delete) return;
            _paletteMode = PaletteMode.Delete;

            SetSelectedStyle(null);
            SetSelectedBuilding(null);

            PaletteModeChanged?.Invoke(_paletteMode);

            if (_cursorGhost) _cursorGhost.SetActive(false);

            // Close context menu when switching modes
            if (buildingContextMenu) buildingContextMenu.Hide();
        }

        public void SetSelectedStyle(HexWorldTileStyle style)
        {
            // click same style again -> deselect
            if (SelectedStyle == style)
                style = null;

            if (style != null && !IsUnlocked(style))
            {
                RequestToast("LOCKED");
                return;
            }

            // MUTUAL EXCLUSIVITY: Clear building selection when selecting a tile style
            if (style != null && SelectedBuilding != null)
            {
                SelectedBuilding = null;
                SelectedBuildingChanged?.Invoke(null);
            }

            SelectedStyle = style;
            SelectedStyleChanged?.Invoke(SelectedStyle);

            // If player selects a tile style (not deselecting), switch to tile mode.
            // Only switch mode if style is not null to avoid overwriting Delete mode.
            if (enableTileBudgetPlacement && style != null)
            {
                bool keepRoadsMode = _paletteMode == PaletteMode.Roads && style.name == "TileStyle_BasicRoad";
                if (!keepRoadsMode)
                    SetPaletteModeTiles();
                EnsureCursorGhost();
                ApplyStyleToGhost();
                UpdateCursorGhostVisibility();
            }
        }

        public void SetSelectedBuilding(HexWorldBuildingDefinition def)
        {
            // click same building again -> deselect
            if (SelectedBuilding == def)
                def = null;

            if (def != null && !IsUnlocked(def))
            {
                RequestToast("LOCKED");
                return;
            }

            // MUTUAL EXCLUSIVITY: Clear tile style selection when selecting a building
            if (def != null && SelectedStyle != null)
            {
                SelectedStyle = null;
                SelectedStyleChanged?.Invoke(null);
            }

            SelectedBuilding = def;
            SelectedBuildingChanged?.Invoke(SelectedBuilding);

            // Only switch mode if def is not null to avoid overwriting Delete mode.
            if (enableTileBudgetPlacement && def != null)
            {
                SetPaletteModeBuildings();
                // Close context menu when entering placement mode (building selected)
                if (buildingContextMenu)
                    buildingContextMenu.Hide();
            }
        }

        // ============================
        // Exit Mode helpers
        // ============================
        public void ExitPaintMode() => SetSelectedStyle(null);
        public void ExitBuildMode() => SetSelectedBuilding(null);

        public void OnExitModeClicked()
        {
            if (_paletteMode == PaletteMode.Delete || _paletteMode == PaletteMode.TileUpgrade)
            {
                // Exit utility modes by switching to tiles mode.
                SetPaletteModeTiles();
            }
            else if (SelectedStyle != null)
                SetSelectedStyle(null);
            else if (SelectedBuilding != null)
                SetSelectedBuilding(null);
        }

        private void RequestToast(string msg)
        {
            ToastRequested?.Invoke(msg);
        }

        // ============================
        // Village: Place / Paint tiles
        // ============================
        private void TryPlaceOrPaintAtCoord(HexCoord coord)
        {
            if (!SelectedStyle)
                return;

            if (!IsUnlocked(SelectedStyle))
            {
                RequestToast("LOCKED");
                return;
            }

            if (_owned.TryGetValue(coord, out var existing) && existing)
            {
                if (allowRepaintOnOwnedClick)
                {
                    // Get existing tile style to determine cost rules
                    _ownedStyleName.TryGetValue(coord, out var oldStyleName);
                    var oldStyle = ResolveStyleByName(oldStyleName);
                    int oldTier = existing.TerrainTier;
                    var newStyle = SelectedStyle;

                    // Determine categories (default to Cosmetic if style is null)
                    var oldCategory = oldStyle != null ? oldStyle.category : TileCategory.Cosmetic;
                    var newCategory = newStyle != null ? newStyle.category : TileCategory.Cosmetic;

                    // Calculate costs based on repaint rules
                    int demolitionFee = 0;
                    var paintCosts = new List<HexWorldResourceStack>();
                    bool refundDowngradedGameplayTile = false;

                    if (oldCategory == TileCategory.Cosmetic && newCategory == TileCategory.Cosmetic)
                    {
                        // Cosmetic → Cosmetic: Free
                    }
                    else if (oldCategory == TileCategory.Cosmetic && newCategory == TileCategory.Gameplay)
                    {
                        // Cosmetic → Gameplay: Pay paint cost
                        if (newStyle != null && newStyle.paintCost != null)
                            paintCosts.AddRange(newStyle.paintCost);
                    }
                    else if (oldCategory == TileCategory.Gameplay && newCategory == TileCategory.Cosmetic)
                    {
                        // Gameplay -> Cosmetic: no demolition fee, apply refund after repaint.
                        refundDowngradedGameplayTile = true;
                    }
                    else if (oldCategory == TileCategory.Gameplay && newCategory == TileCategory.Gameplay)
                    {
                        // Gameplay → Gameplay: Pay demolition fee + paint cost
                        demolitionFee = CalculateDemolitionFee(oldStyle);
                        if (newStyle != null && newStyle.paintCost != null)
                            paintCosts.AddRange(newStyle.paintCost);
                    }

                    // Validate affordability
                    if (!CanAffordRepaint(demolitionFee, paintCosts))
                        return;

                    // Deduct costs
                    DeductRepaintCosts(demolitionFee, paintCosts);

                    // Award IP only for Gameplay paint purchases
                    if (newCategory == TileCategory.Gameplay && oldCategory != newCategory)
                    {
                        int ipAmount = CalculatePaintIP(newStyle);
                        if (ipAmount > 0)
                            PlayerProgressManager.Instance?.AddIP(ipAmount);
                    }

                    // Apply the new style
                    ApplyStyleToTile(existing.gameObject, SelectedStyle);
                    _ownedStyleName[coord] = SelectedStyle ? SelectedStyle.name : "";

                    // Update terrain type when repainting
                    if (SelectedStyle != null)
                        existing.SetTerrainType(SelectedStyle.terrainType);

                    // Clear old decorations and spawn new ones for the new style
                    ClearDecorationsAtTile(coord);
                    SpawnDecorations(coord, SelectedStyle);

                    if (refundDowngradedGameplayTile)
                        TryApplyTileDemolitionRefund(oldStyle, oldTier);

                    // Recompute road network if tile might affect connectivity
                    RecomputeRoadNetwork();
                }
                return;
            }

            // Check if we have tiles left to place
            if (_tilesLeftToPlace <= 0)
            {
                int currentCapacity = TileCapacityMax;
                int tilesPlaced = _owned.Count;

                if (tilesPlaced >= currentCapacity)
                {
                    RequestToast($"Tile capacity reached ({currentCapacity}). Upgrade Town Hall to place more tiles.");
                    return;
                }
                else
                {
                    RequestToast("No tiles left to place. This shouldn't happen - please report this bug.");
                    return;
                }
            }

            if (!IsPlacementValid(coord))
                return;

            bool isRoadPlacement = SelectedStyle != null && SelectedStyle.HasTag(RoadTag);
            if (isRoadPlacement)
            {
                var roadPaintCosts = new List<HexWorldResourceStack>();
                if (SelectedStyle.paintCost != null)
                    roadPaintCosts.AddRange(SelectedStyle.paintCost);

                if (!CanAffordRepaint(0, roadPaintCosts))
                    return;

                DeductRepaintCosts(0, roadPaintCosts);
            }

            AddOwned(coord, SelectedStyle);

            // Decrease tile budget
            _tilesLeftToPlace--;
            TilesLeftChanged?.Invoke(_tilesLeftToPlace);
            TilesPlacedChanged?.Invoke(_owned.Count, TileCapacityMax);
        }

        /// <summary>
        /// Calculates the demolition fee for a gameplay tile (default 30% of original paint cost).
        /// Returns the total credit cost for demolition.
        /// </summary>
        private int CalculateDemolitionFee(HexWorldTileStyle style)
        {
            if (style == null || style.paintCost == null || style.paintCost.Count == 0)
                return 0;

            // Sum all paint costs and apply demolition factor
            int totalOriginalCost = 0;
            foreach (var cost in style.paintCost)
            {
                if (cost.id == HexWorldResourceId.Credits)
                    totalOriginalCost += cost.amount;
                else if (cost.amount > 0)
                    totalOriginalCost += cost.amount * 5; // Convert resources to credit equivalent (5 credits per resource unit)
            }

            return Mathf.RoundToInt(totalOriginalCost * style.demolitionFeeFactor);
        }

        /// <summary>
        /// Checks if the player can afford the demolition fee and paint costs.
        /// </summary>
        private bool CanAffordRepaint(int demolitionFee, List<HexWorldResourceStack> paintCosts)
        {
            EnsureWarehouse();

            // Calculate total credit cost (demolition fee + any credit costs in paintCosts)
            int totalCreditsNeeded = demolitionFee;
            foreach (var cost in paintCosts)
            {
                if (cost.id == HexWorldResourceId.Credits)
                    totalCreditsNeeded += cost.amount;
            }

            // Check credits
            if (totalCreditsNeeded > 0 && _credits < totalCreditsNeeded)
            {
                RequestToast($"Not enough credits. Need {totalCreditsNeeded}, have {_credits}.");
                return false;
            }

            // Check warehouse resources
            foreach (var cost in paintCosts)
            {
                if (cost.id == HexWorldResourceId.None || cost.id == HexWorldResourceId.Credits)
                    continue;
                if (cost.amount <= 0)
                    continue;

                int have = warehouse != null ? warehouse.Get(cost.id) : 0;
                if (have < cost.amount)
                {
                    RequestToast($"Not enough {cost.id}. Need {cost.amount}, have {have}.");
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Deducts the demolition fee and paint costs from player resources.
        /// </summary>
        private void DeductRepaintCosts(int demolitionFee, List<HexWorldResourceStack> paintCosts)
        {
            EnsureWarehouse();

            // Calculate total credit cost
            int totalCreditsToDeduct = demolitionFee;
            foreach (var cost in paintCosts)
            {
                if (cost.id == HexWorldResourceId.Credits)
                    totalCreditsToDeduct += cost.amount;
            }

            // Deduct credits
            if (totalCreditsToDeduct > 0)
            {
                _credits -= totalCreditsToDeduct;
                CreditsChanged?.Invoke(_credits);
            }

            // Deduct warehouse resources
            foreach (var cost in paintCosts)
            {
                if (cost.id == HexWorldResourceId.None || cost.id == HexWorldResourceId.Credits)
                    continue;
                if (cost.amount <= 0)
                    continue;

                warehouse?.TryRemove(cost.id, cost.amount);
            }
        }

        /// <summary>
        /// Calculates Infrastructure Points awarded for placing a gameplay tile.
        /// Returns IP amount (2-5 based on tile cost).
        /// </summary>
        private int CalculatePaintIP(HexWorldTileStyle style)
        {
            if (style == null || style.category != TileCategory.Gameplay)
                return 0;

            // Base IP of 2, plus 1 for each 20 credits worth of cost (max 5)
            int totalCost = 0;
            if (style.paintCost != null)
            {
                foreach (var cost in style.paintCost)
                {
                    if (cost.id == HexWorldResourceId.Credits)
                        totalCost += cost.amount;
                    else if (cost.amount > 0)
                        totalCost += cost.amount * 5;
                }
            }

            return Mathf.Clamp(2 + (totalCost / 20), 2, 5);
        }

        /// <summary>
        /// Attempts to upgrade a tile's terrain tier.
        /// Upgrade costs are data-driven from the current tile style's paintCost.
        /// </summary>
        public bool TryUpgradeTileTier(HexCoord coord)
        {
            if (!_owned.TryGetValue(coord, out var tile) || !tile)
            {
                RequestToast("Tile not found.");
                return false;
            }

            int currentTier = tile.TerrainTier;

            if (currentTier >= 2)
            {
                RequestToast("Tile already at max tier (2).");
                return false;
            }

            // Gating: Tier 1 upgrades require Town Hall Level >= 2
            if (currentTier == 0 && townHallLevel < 2)
            {
                RequestToast("Town Hall Level 2 required to upgrade tiles to Tier 1.");
                return false;
            }

            var currentStyle = ResolveStyleAtCoord(coord);
            if (currentStyle == null)
            {
                RequestToast("Tile style missing.");
                return false;
            }

            var costs = currentStyle.paintCost;
            int creditsToDeduct = 0;
            int totalCreditEquivalentCost = 0;

            EnsureWarehouse();
            if (costs != null)
            {
                foreach (var cost in costs)
                {
                    if (cost.id == HexWorldResourceId.None || cost.amount <= 0)
                        continue;

                    if (cost.id == HexWorldResourceId.Credits)
                    {
                        creditsToDeduct += cost.amount;
                        totalCreditEquivalentCost += cost.amount;
                        continue;
                    }

                    if (!warehouse)
                    {
                        RequestToast("Warehouse missing.");
                        return false;
                    }

                    int available = warehouse.Get(cost.id);
                    if (available < cost.amount)
                    {
                        RequestToast($"Not enough {cost.id}. Need {cost.amount}, have {available}.");
                        return false;
                    }

                    totalCreditEquivalentCost += cost.amount * 5;
                }
            }

            if (_credits < creditsToDeduct)
            {
                RequestToast($"Not enough credits. Need {creditsToDeduct}, have {_credits}.");
                return false;
            }

            if (costs != null)
            {
                foreach (var cost in costs)
                {
                    if (cost.id == HexWorldResourceId.None || cost.id == HexWorldResourceId.Credits || cost.amount <= 0)
                        continue;

                    if (!warehouse.TryRemove(cost.id, cost.amount))
                    {
                        RequestToast($"Failed to deduct {cost.id}.");
                        return false;
                    }
                }
            }

            if (creditsToDeduct > 0)
            {
                _credits -= creditsToDeduct;
                CreditsChanged?.Invoke(_credits);
            }

            // Upgrade tier
            int newTier = currentTier + 1;
            tile.SetTerrainTier(newTier);

            int ipAmount = Mathf.Clamp(2 + (totalCreditEquivalentCost / 20), 2, 5);
            PlayerProgressManager.Instance?.AddIP(ipAmount);

            RequestToast($"Tile upgraded to Tier {tile.TerrainTier}!");
            return true;
        }

        /// <summary>
        /// Purchases a building upgrade by validating and deducting credits + warehouse resources.
        /// TICKET: purchase only (no effect application yet).
        /// </summary>
        public bool TryPurchaseBuildingUpgrade(HexCoord coord, HexWorldUpgradeDefinition upgrade)
        {
            if (upgrade == null)
            {
                RequestToast("Upgrade not found.");
                return false;
            }

            // Optional safety: ensure there's a building at this coord (prevents weird clicks / stale UI)
            if (!_buildings.TryGetValue(coord, out var inst) || !inst)
            {
                RequestToast("Building not found.");
                return false;
            }

            EnsureWarehouse();
            if (!warehouse)
            {
                RequestToast("Warehouse missing.");
                return false;
            }

            int creditCost = Mathf.Max(0, upgrade.creditCost);

            // 1) Validate warehouse resources
            if (upgrade.resourceCosts != null)
            {
                for (int i = 0; i < upgrade.resourceCosts.Count; i++)
                {
                    var cost = upgrade.resourceCosts[i];
                    if (cost.id == HexWorldResourceId.None) continue;
                    if (cost.amount <= 0) continue;

                    int have = warehouse.Get(cost.id);
                    if (have < cost.amount)
                    {
                        RequestToast($"Not enough {cost.id}. Need {cost.amount}, have {have}.");
                        return false;
                    }
                }
            }

            // 2) Validate credits
            if (creditCost > 0 && _credits < creditCost)
            {
                RequestToast($"Not enough credits. Need {creditCost}, have {_credits}.");
                return false;
            }

            // 3) Deduct resources
            if (upgrade.resourceCosts != null)
            {
                for (int i = 0; i < upgrade.resourceCosts.Count; i++)
                {
                    var cost = upgrade.resourceCosts[i];
                    if (cost.id == HexWorldResourceId.None) continue;
                    if (cost.amount <= 0) continue;

                    if (!warehouse.TryRemove(cost.id, cost.amount))
                    {
                        RequestToast($"Failed to deduct {cost.id}.");
                        return false;
                    }
                }
            }

            // 4) Deduct credits
            if (creditCost > 0)
            {
                _credits -= creditCost;
                CreditsChanged?.Invoke(_credits);
            }

            int targetLevel = Mathf.Max(inst.Level + 1, 2);
            int ipAmount = GetBuildingUpgradeIpReward(targetLevel);
            PlayerProgressManager.Instance?.AddIP(ipAmount);

            RequestToast("Upgrade purchased.");

            var upgradedDef = ResolveBuildingByName(inst.buildingName);
            bool shouldApplyRelocationCooldown =
                upgradedDef != null &&
                (upgradedDef.kind == HexWorldBuildingDefinition.BuildingKind.Processor ||
                 upgradedDef.kind == HexWorldBuildingDefinition.BuildingKind.Producer);
            inst.SetRelocationCooldown(shouldApplyRelocationCooldown ? 30f : 0f);
            return true;
        }

        /// <summary>
        /// Deletes the autosave file so you can start fresh.
        /// </summary>
        [ContextMenu("Debug: Delete Autosave")]
        public void DebugDeleteAutosave()
        {
            string path = AutosavePath();
            if (File.Exists(path))
            {
                File.Delete(path);
                RequestToast("Autosave deleted. Restart scene to start fresh.");
                Debug.Log($"Deleted autosave at: {path}");
            }
            else
            {
                RequestToast("No autosave found.");
                Debug.Log($"No autosave at: {path}");
            }
        }

        /// <summary>
        /// Resets to a fresh village state with Town Hall Level 1.
        /// Deletes autosave and resets inspector values.
        /// </summary>
        [ContextMenu("Debug: Reset to Fresh Village (TH Level 1)")]
        public void DebugResetToFreshVillage()
        {
            // Delete autosave
            string path = AutosavePath();
            if (File.Exists(path))
            {
                File.Delete(path);
                Debug.Log($"Deleted autosave at: {path}");
            }

            // Reset to Level 1 in the scene
            townHallLevel = 1;
            int capacity = HexWorldCapacityService.GetTileCapacity(1);
            _tilesLeftToPlace = capacity;
            _credits = startingCredits;

            // Clear tiles if in Play Mode
            if (Application.isPlaying)
            {
                ClearAllTiles();
                TilesLeftChanged?.Invoke(_tilesLeftToPlace);
                TilesPlacedChanged?.Invoke(0, capacity);
                CreditsChanged?.Invoke(_credits);
                TownHallLevelChanged?.Invoke(townHallLevel);
                RecomputeActiveSlotsAndNotify();
            }

            RequestToast($"Reset to fresh village: TH L1, {capacity} tiles available");
            Debug.Log($"Village reset: TH L1, {capacity} tiles, {_credits} credits");
        }

        /// <summary>
        /// Debug/Cheat method to set Town Hall to level 10 without cost.
        /// </summary>
        [ContextMenu("Debug: Set Town Hall to Level 10")]
        public void DebugSetTownHallToMax()
        {
            DebugSetTownHallLevel(10);
        }

        /// <summary>
        /// Debug/Cheat method to set Town Hall to level 5 without cost.
        /// </summary>
        [ContextMenu("Debug: Set Town Hall to Level 5")]
        public void DebugSetTownHallToLevel5()
        {
            DebugSetTownHallLevel(5);
        }

        /// <summary>
        /// Debug/Cheat method to set Town Hall to level 3 without cost.
        /// </summary>
        [ContextMenu("Debug: Set Town Hall to Level 3")]
        public void DebugSetTownHallToLevel3()
        {
            DebugSetTownHallLevel(3);
        }

        /// <summary>
        /// Debug/Cheat method to directly set Town Hall level without cost.
        /// Automatically adjusts tile budget to match the new capacity.
        /// </summary>
        public void DebugSetTownHallLevel(int level)
        {
            int oldLevel = townHallLevel;
            int oldCapacity = HexWorldCapacityService.GetTileCapacity(oldLevel);
            int tilesUsed = _owned.Count;

            townHallLevel = Mathf.Clamp(level, 1, 10);
            int newCapacity = HexWorldCapacityService.GetTileCapacity(townHallLevel);

            Debug.Log($"DebugSetTownHallLevel: Old TH={oldLevel} (cap={oldCapacity}), New TH={townHallLevel} (cap={newCapacity}), TilesUsed={tilesUsed}");
            Debug.Log($"TileCapacityMax property returns: {TileCapacityMax}");

            // Set tiles left to the difference between new capacity and tiles already placed
            _tilesLeftToPlace = Mathf.Max(0, newCapacity - tilesUsed);

            TownHallLevelChanged?.Invoke(townHallLevel);
            TilesLeftChanged?.Invoke(_tilesLeftToPlace);
            TilesPlacedChanged?.Invoke(_owned.Count, TileCapacityMax);
            RecomputeActiveSlotsAndNotify();

            RequestToast($"Debug: TH set to L{townHallLevel}. Tiles: {tilesUsed}/{newCapacity}, {_tilesLeftToPlace} left");
        }

        /// <summary>
        /// Attempts to upgrade the Town Hall to the next level.
        /// TICKET 003: Gated by Land Deed + World progression milestone.
        /// No longer uses warehouse resources or credits.
        /// Upgrading grants additional tile capacity and active building slots.
        /// </summary>
        public bool TryUpgradeTownHall()
        {
            if (townHallLevel >= 10)
            {
                RequestToast("Town Hall already at max level (10).");
                return false;
            }

            int nextLevel = townHallLevel + 1;

            // Check world progression milestone
            var wp = WorldProgression;
            if (wp.HighestUnlockedWorldNumber < nextLevel)
            {
                RequestToast($"Reach World {nextLevel} to unlock Town Hall L{nextLevel}.");
                return false;
            }

            // Check IP threshold from town tier data.
            long requiredIP = GetIPRequirementForTownHallLevel(nextLevel);
            long currentIP = PlayerProgressManager.Instance?.InfrastructurePoints ?? 0;
            if (currentIP < requiredIP)
            {
                RequestToast($"Not enough IP. Need {requiredIP}, have {currentIP}.");
                return false;
            }

            bool hasMilestone = TryGetTownHallMilestoneRequirement(nextLevel, out var milestoneReq);
            if (hasMilestone)
            {
                EnsureWarehouse();
                if (!warehouse)
                {
                    RequestToast("Warehouse unavailable. Cannot verify milestone items.");
                    return false;
                }

                int currentAmount = warehouse.Get(milestoneReq.id);
                if (currentAmount < milestoneReq.quantity)
                {
                    RequestToast($"Missing Milestone Item: {milestoneReq.label} required.");
                    return false;
                }
            }

            // Calculate capacity increases
            int oldCapacity = HexWorldCapacityService.GetTileCapacity(townHallLevel);
            int newCapacity = HexWorldCapacityService.GetTileCapacity(nextLevel);
            int tilesGranted = newCapacity - oldCapacity;

            int oldActiveSlots = HexWorldCapacityService.GetActiveSlots(townHallLevel);
            int newActiveSlots = HexWorldCapacityService.GetActiveSlots(nextLevel);

            if (hasMilestone)
            {
                if (!warehouse.TryRemove(milestoneReq.id, milestoneReq.quantity))
                {
                    RequestToast($"Missing Milestone Item: {milestoneReq.label} required.");
                    return false;
                }
            }

            // Upgrade Town Hall
            townHallLevel = nextLevel;
            TownHallLevelChanged?.Invoke(townHallLevel);

            if (townHallLevel == 2)
            {
                UnlockBlueprint("Building_Sawmill");
            }

            // Grant new tiles (free to place)
            _tilesLeftToPlace += tilesGranted;
            TilesLeftChanged?.Invoke(_tilesLeftToPlace);

            // Update active slots
            RecomputeActiveSlotsAndNotify();

            // Notify tiles placed (capacity changed)
            TilesPlacedChanged?.Invoke(_owned.Count, TileCapacityMax);

            var nextTierDef = GetTownTierDefinition(townHallLevel);
            if (nextTierDef != null && nextTierDef.qpGranted > 0)
            {
                PlayerProgressManager.Instance?.AddQP(nextTierDef.qpGranted);
            }

            RequestToast($"Town Hall upgraded to L{townHallLevel}! +{tilesGranted} tiles, {newActiveSlots} active slots.");
            Debug.Log($"TH upgraded: L{townHallLevel-1}→L{townHallLevel}, Tiles: {oldCapacity}→{newCapacity}, Active Slots: {oldActiveSlots}→{newActiveSlots}");
            return true;
        }

        /// <summary>
        /// Returns the IP requirement for upgrading to the specified Town Hall level.
        /// Values are driven by townTierDefinitions[targetLevel - 1].
        /// </summary>
        private long GetIPRequirementForTownHallLevel(int targetLevel)
        {
            int index = targetLevel - 1;
            if (index < 0 || townTierDefinitions == null || index >= townTierDefinitions.Length)
            {
                Debug.LogWarning($"[{nameof(HexWorld3DController)}] IP requirement lookup out of range for Town Hall target level {targetLevel}.");
                return 0;
            }

            TownTierDefinition tierDef = townTierDefinitions[index];
            if (tierDef == null)
            {
                Debug.LogWarning($"[{nameof(HexWorld3DController)}] Town tier definition missing at index {index} for Town Hall target level {targetLevel}.");
                return 0;
            }

            return tierDef.ipRequired;
        }

        private TownTierDefinition GetTownTierDefinition(int tier)
        {
            if (tier <= 0) return null;
            if (townTierDefinitions == null || townTierDefinitions.Length == 0) return null;

            int index = tier - 1;
            if (index < 0 || index >= townTierDefinitions.Length)
                return null;

            return townTierDefinitions[index];
        }

        public bool TryGetTownHallMilestoneRequirement(int targetLevel, out TownHallMilestoneRequirement requirement)
        {
            requirement = default;

            int index = targetLevel - 1;
            if (index < 0 || townTierDefinitions == null || index >= townTierDefinitions.Length)
            {
                Debug.LogWarning($"[{nameof(HexWorld3DController)}] Milestone lookup out of range for Town Hall target level {targetLevel}.");
                return false;
            }

            TownTierDefinition tierDef = townTierDefinitions[index];
            if (tierDef == null)
            {
                Debug.LogWarning($"[{nameof(HexWorld3DController)}] Town tier definition missing at index {index} for Town Hall target level {targetLevel}.");
                return false;
            }

            if (tierDef.milestoneResourceId == HexWorldResourceId.None || tierDef.milestoneQuantity <= 0)
                return false;

            requirement = new TownHallMilestoneRequirement
            {
                id = tierDef.milestoneResourceId,
                quantity = tierDef.milestoneQuantity,
                label = string.IsNullOrWhiteSpace(tierDef.milestoneLabel)
                    ? tierDef.milestoneResourceId.ToString()
                    : tierDef.milestoneLabel
            };
            return true;
        }

        public int GetWarehouseResourceAmount(HexWorldResourceId id)
        {
            EnsureWarehouse();
            if (!warehouse || id == HexWorldResourceId.None)
                return 0;
            return warehouse.Get(id);
        }

        // ============================
        // Village: Buildings (NEW)
        // ============================
        private void TryPlaceBuildingAtCoord(HexCoord coord)
        {
            Debug.Log($"TryPlaceBuildingAtCoord: coord={coord.q},{coord.r}, SelectedBuilding={(SelectedBuilding ? SelectedBuilding.name : "null")}");

            if (!SelectedBuilding)
            {
                Debug.Log("TryPlaceBuildingAtCoord: No building selected");
                return;
            }

            if (!IsUnlocked(SelectedBuilding))
            {
                RequestToast("LOCKED");
                return;
            }

            if (!_owned.TryGetValue(coord, out var tile) || !tile)
            {
                RequestToast("Place a tile first.");
                Debug.Log($"TryPlaceBuildingAtCoord: No tile at {coord.q},{coord.r}");
                return;
            }

            // Block placement on occupied tiles - must delete old building first
            if (_buildings.TryGetValue(coord, out var existing) && existing)
            {
                RequestToast("Delete the old building first.");
                return;
            }

            if (!SelectedBuilding.prefab)
            {
                RequestToast("Building prefab missing.");
                return;
            }

            // Clear decorations before placing building
            ClearDecorationsAtTile(coord);

            // Spawn as child of the tile (so if tile is destroyed, building goes too)
            var go = Instantiate(SelectedBuilding.prefab, tile.transform);
            go.name = $"B_{(string.IsNullOrWhiteSpace(SelectedBuilding.displayName) ? SelectedBuilding.name : SelectedBuilding.displayName)}";

            go.transform.localPosition = SelectedBuilding.localOffset;
            go.transform.localRotation = Quaternion.Euler(SelectedBuilding.localEuler);
            go.transform.localScale = SelectedBuilding.localScale;

            Debug.Log($"TryPlaceBuildingAtCoord: Applied scale {SelectedBuilding.localScale} from definition {SelectedBuilding.name}");

            var inst = go.GetComponent<HexWorldBuildingInstance>();
            if (!inst) inst = go.AddComponent<HexWorldBuildingInstance>();

            bool consumes = SelectedBuilding.consumesActiveSlot;
            bool wantsActive = SelectedBuilding.defaultActive;
            bool canActivate = !consumes || (_activeBuildingsUsed < ActiveSlotsTotal);
            bool isActive = wantsActive && canActivate;
            string selectedBuildingId = GetCanonicalBuildingId(SelectedBuilding);

            inst.Set(coord, selectedBuildingId, consumesActiveSlot: consumes, isActive: isActive, level: 1);
            bool shouldApplyRelocationCooldown =
                SelectedBuilding.kind == HexWorldBuildingDefinition.BuildingKind.Processor ||
                SelectedBuilding.kind == HexWorldBuildingDefinition.BuildingKind.Producer;
            inst.SetRelocationCooldown(shouldApplyRelocationCooldown ? 30f : 0f);

            _buildings[coord] = inst;
            _buildingNameByCoord[coord] = selectedBuildingId;
            if (TryRegisterPlacedBuildingForProgression(selectedBuildingId))
                OnProgressionUnlocksChanged?.Invoke();

            if (wantsActive && !isActive)
                RequestToast("No Active Slots available. Building placed Dormant.");

            RecomputeActiveSlotsAndNotify();

            RecomputeRoadNetwork();

            // Award IP for building placement using rarity-based table (TICKET 57).
            int ipAmount = GetPlacementIpForRarity(SelectedBuilding);
            if (ipAmount > 0)
                PlayerProgressManager.Instance?.AddIP(ipAmount);
        }

        private static int GetPlacementIpForRarity(HexWorldBuildingDefinition def)
        {
            if (def == null)
                return 0;

            return def.rarity switch
            {
                HexWorldBuildingDefinition.BuildingRarity.Common => 10,
                HexWorldBuildingDefinition.BuildingRarity.Uncommon => 20,
                HexWorldBuildingDefinition.BuildingRarity.Rare => 35,
                HexWorldBuildingDefinition.BuildingRarity.Epic => 60,
                _ => 10
            };
        }

        public void TryToggleBuildingActiveAtCoord(HexCoord coord)
        {
            if (!_buildings.TryGetValue(coord, out var inst) || !inst)
                return;

            if (!inst.ConsumesActiveSlot)
            {
                // Infrastructure / free buildings can always stay active.
                if (!inst.IsActive)
                {
                    inst.SetActive(true);
                    RequestToast("Building activated.");
                }
                return;
            }

            if (inst.IsActive)
            {
                inst.SetActive(false);
                RequestToast("Building set Dormant.");
                RecomputeActiveSlotsAndNotify();
                return;
            }

            // Try activate
            if (_activeBuildingsUsed >= ActiveSlotsTotal)
            {
                RequestToast("No Active Slots available. Upgrade Town Hall!");
                return;
            }

            inst.SetActive(true);
            RequestToast("Building activated.");
            RecomputeActiveSlotsAndNotify();
        }

        // RMB delete helper for non-grid cursor flows.
        private void TryRemoveBuildingOrTileUnderMouse()
        {
            if (!mainCamera) return;

            Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

            if (!Physics.Raycast(ray, out RaycastHit hit, 5000f))
                return;

            // TILE-FIRST: Always resolve via HexWorld3DTile
            var tile = hit.collider.GetComponentInParent<HexWorld3DTile>();
            if (!tile || tile.IsFrontier)
                return;

            TryRemoveBuildingOrTileAtCoord(tile.Coord);
        }

        /// <summary>
        /// Removes building or tile at the specified coordinate (used for drag delete).
        /// </summary>
        private void TryRemoveBuildingOrTileAtCoord(HexCoord coord)
        {
            // ABSOLUTE ORIGIN PROTECTION: Block all deletion at (0,0) immediately
            if (coord.q == 0 && coord.r == 0)
            {
                RequestToast("The Town Hall and its foundation cannot be removed.");
                return;
            }

            if (TryHandleBuildingRemoval(coord))
                return;

            // Otherwise remove tile (only if no building exists)
            if (_owned.TryGetValue(coord, out var ownedTile) && ownedTile)
            {
                // Double-check: block tile removal if building exists (sync safety)
                if (_buildings.TryGetValue(coord, out var b2) && b2)
                {
                    RequestToast("Remove the building first before removing the tile.");
                    return;
                }

                var oldStyle = ResolveStyleAtCoord(coord);
                int oldTier = ownedTile.TerrainTier;
                UnityEngine.Object.Destroy(ownedTile.gameObject);
                _owned.Remove(coord);
                _ownedStyleName.Remove(coord);

                if (refundOnRemove)
                {
                    _tilesLeftToPlace++;
                    TilesLeftChanged?.Invoke(_tilesLeftToPlace);
                }
                TryApplyTileDemolitionRefund(oldStyle, oldTier);

                TilesPlacedChanged?.Invoke(_owned.Count, TileCapacityMax);

                // Recompute road network after tile removal
                RecomputeRoadNetwork();
            }
        }

        private bool TryHandleBuildingRemoval(HexCoord coord)
        {
            if (!_buildings.TryGetValue(coord, out var inst) || !inst)
                return false;

            var buildingDef = ResolveBuildingByName(inst.buildingName);

            if (IsTownHallBuilding(inst, buildingDef))
            {
                RequestToast("This building is a Town Hall and cannot be removed.");
                return true;
            }

            bool canRemoveWarehouse = CanRemoveWarehouseBuilding(buildingDef);
            if (!canRemoveWarehouse)
                return true;

            UnityEngine.Object.Destroy(inst.gameObject);
            _buildings.Remove(coord);
            _buildingNameByCoord.Remove(coord);
            RecomputeActiveSlotsAndNotify();
            RecomputeRoadNetwork();
            return true;
        }

        private HexWorldTileStyle ResolveStyleAtCoord(HexCoord coord)
        {
            if (!_ownedStyleName.TryGetValue(coord, out var styleName))
                return null;
            return ResolveStyleByName(styleName);
        }

        private void TryApplyTileDemolitionRefund(HexWorldTileStyle tileStyle, int tileTier)
        {
            if (tileStyle == null || tileStyle.category != TileCategory.Gameplay)
                return;

            int totalCreditEquivalentCost = GetTileCreditsCost(tileStyle, tileTier);
            if (totalCreditEquivalentCost <= 0)
                return;

            int refundedCredits = Mathf.RoundToInt(totalCreditEquivalentCost * 0.30f);
            if (refundedCredits <= 0)
                return;

            _credits += refundedCredits;
            CreditsChanged?.Invoke(_credits);
        }

        private int GetTileCreditsCost(HexWorldTileStyle style, int terrainTier)
        {
            int baseCredits = style != null ? GetCreditsOnlyFromCosts(style.paintCost) : 0;
            int tierCredits = GetCumulativeTileTierUpgradeCredits(terrainTier);
            return Mathf.Max(0, baseCredits + tierCredits);
        }

        private int GetCumulativeTileTierUpgradeCredits(int terrainTier)
        {
            int clampedTier = Mathf.Max(0, terrainTier);
            var cfg = GetResolvedBalanceConfig();
            var tierCosts = cfg != null ? cfg.tileTierUpgradeCreditCostByTier : null;

            if (tierCosts == null || tierCosts.Length == 0)
            {
                if (!_warnedMissingTileTierUpgradeCosts)
                {
                    Debug.LogWarning("[HexWorld3DController] tileTierUpgradeCreditCostByTier is missing; treating tier investment as 0 credits.");
                    _warnedMissingTileTierUpgradeCosts = true;
                }
                return 0;
            }

            int maxIndex = tierCosts.Length - 1;
            if (clampedTier > maxIndex && !_warnedShortTileTierUpgradeCosts)
            {
                Debug.LogWarning("[HexWorld3DController] tileTierUpgradeCreditCostByTier is shorter than required tier range; clamping to last entry.");
                _warnedShortTileTierUpgradeCosts = true;
            }

            int total = 0;
            for (int t = 1; t <= clampedTier; t++)
            {
                int idx = Mathf.Clamp(t, 0, maxIndex);
                total += Mathf.Max(0, tierCosts[idx]);
            }

            return Mathf.Max(0, total);
        }

        private static int GetProcessorCreditsOnlyCost(HexWorldBuildingDefinition def)
        {
            if (def == null || def.availableUpgrades == null)
                return 0;

            for (int i = 0; i < def.availableUpgrades.Count; i++)
            {
                var up = def.availableUpgrades[i];
                if (up == null) continue;
                if (up.creditCost > 0)
                    return up.creditCost;
            }

            return 0;
        }

        private static int GetCreditsOnlyFromCosts(List<HexWorldResourceStack> costs)
        {
            if (costs == null || costs.Count == 0)
                return 0;

            int credits = 0;
            for (int i = 0; i < costs.Count; i++)
            {
                var c = costs[i];
                if (c.id == HexWorldResourceId.Credits && c.amount > 0)
                    credits += c.amount;
            }

            return Mathf.Max(0, credits);
        }

        private bool IsTownHallBuilding(HexWorldBuildingInstance inst, HexWorldBuildingDefinition def)
        {
            if (def != null && def.kind == HexWorldBuildingDefinition.BuildingKind.TownHall)
                return true;

            if (townHallBuildingDef != null && inst != null &&
                string.Equals(inst.buildingName, GetCanonicalBuildingId(townHallBuildingDef), StringComparison.Ordinal))
                return true;

            return false;
        }

        /// <summary>
        /// TILE-DRIVEN: Tries to interact with a building at the clicked tile.
        /// Opens the context menu if a building exists at the tile's coord.
        /// </summary>
        private void TryInteractWithBuildingUnderMouse()
        {
            if (!mainCamera) return;

            Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

            if (!Physics.Raycast(ray, out RaycastHit hit, 5000f))
            {
                // Clicked on nothing - close context menu
                if (buildingContextMenu) buildingContextMenu.Hide();
                return;
            }

            // TILE-FIRST: Always resolve via HexWorld3DTile
            var tile = hit.collider.GetComponentInParent<HexWorld3DTile>();
            if (!tile || tile.IsFrontier)
            {
                // Clicked on frontier or non-tile object - close context menu
                if (buildingContextMenu) buildingContextMenu.Hide();
                return;
            }

            var coord = tile.Coord;

            // Resolve tile style from _ownedStyleName dictionary
            _ownedStyleName.TryGetValue(coord, out var styleName);
            var tileStyle = ResolveStyleByName(styleName);

            // Resolve building via dictionary lookup (not mesh raycast)
            if (_buildings.TryGetValue(coord, out var buildingInstance) && buildingInstance)
            {
                // Open context menu for this building (with tile style for toggle)
                if (buildingContextMenu)
                {
                    buildingContextMenu.Show(buildingInstance, tileStyle);
                    if (!buildingContextMenu.IsVisible)
                    {
                        buildingContextMenu.ShowTile(tileStyle);
                    }
                }
                else
                {
                    Debug.LogWarning("TryInteractWithBuildingUnderMouse: buildingContextMenu is not assigned.");
                }
                return;
            }

            // Clicked on a tile with no building - show tile stats using resolved style
            if (buildingContextMenu)
            {
                buildingContextMenu.ShowTile(tileStyle);
            }
        }

        // ============================
        // Original Prototype Flow
        // ============================
        private void TryClick()
        {
            if (!mainCamera) return;

            Vector2 mousePos = Mouse.current.position.ReadValue();
            Ray ray = mainCamera.ScreenPointToRay(mousePos);

            if (!Physics.Raycast(ray, out RaycastHit hit, 5000f))
                return;

            var tile = hit.collider.GetComponentInParent<HexWorld3DTile>();
            if (!tile || !tile.IsFrontier)
                return;

            if (!SelectedStyle)
            {
                RequestToast("Pick a tile first");
                return;
            }

            TryPurchase(tile.Coord, SelectedStyle);
        }

        private void TryPurchase(HexCoord coord, HexWorldTileStyle style)
        {
            if (_owned.ContainsKey(coord))
                return;

            int tileCost = costPerTile;

            if (_credits < tileCost)
            {
                RequestToast($"Not enough credits. Have {_credits}, need {tileCost}.");
                return;
            }

            _credits -= tileCost;
            CreditsChanged?.Invoke(_credits);

            if (_frontier.TryGetValue(coord, out var frontierTile) && frontierTile)
            {
                Destroy(frontierTile.gameObject);
                _frontier.Remove(coord);
            }

            AddOwned(coord, style);
            RefreshFrontier();
        }

        private void AddOwned(HexCoord coord, HexWorldTileStyle style, bool recomputeRoadNetwork = true)
        {
            if (_owned.ContainsKey(coord)) return;

            var go = Instantiate(ownedPrefab, AxialToWorld(coord), Quaternion.identity, tilesParent);
            go.name = $"Tile_{coord.q}_{coord.r}";

            var marker = go.GetComponent<HexWorld3DTile>();
            if (!marker) marker = go.AddComponent<HexWorld3DTile>();
            marker.Set(coord, isFrontier: false);

            // Set terrain type from style (defaults to None if style is null)
            if (style != null)
                marker.SetTerrainType(style.terrainType);

            ApplyStyleToTile(go, style);

            _owned.Add(coord, marker);
            _ownedStyleName[coord] = style ? style.name : "";

            Debug.Log($"AddOwned: Tile placed at {coord.q},{coord.r}. Total tiles: {_owned.Count}. Parent: {tilesParent.name}");

            // Spawn decorations for this tile
            SpawnDecorations(coord, style);

            if (recomputeRoadNetwork)
                RecomputeRoadNetwork();
        }

        // ============================
        // Tile Decorations
        // ============================

        private const string DecorRootName = "DecorRoot";

        /// <summary>
        /// Gets or creates the DecorRoot container under a tile for safe decoration management.
        /// </summary>
        private Transform GetOrCreateDecorRoot(Transform tileTransform)
        {
            var existing = tileTransform.Find(DecorRootName);
            if (existing) return existing;

            var go = new GameObject(DecorRootName);
            go.transform.SetParent(tileTransform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go.transform;
        }

        /// <summary>
        /// Spawns random decorations on a tile based on the style's decoration settings.
        /// </summary>
        private void SpawnDecorations(HexCoord coord, HexWorldTileStyle style)
        {
            if (style == null) return;
            if (style.decorations == null || style.decorations.Count == 0) return;

            if (!_owned.TryGetValue(coord, out var tile) || !tile) return;

            int spawnCount = UnityEngine.Random.Range(style.minCount, style.maxCount + 1);
            if (spawnCount <= 0) return;

            // Get or create the decoration container
            Transform decorRoot = GetOrCreateDecorRoot(tile.transform);

            // If not picking randomly per instance, select one entry for all props on this tile
            int fixedIndex = -1;
            if (!style.pickRandomlyPerInstance)
            {
                fixedIndex = UnityEngine.Random.Range(0, style.decorations.Count);
            }

            for (int i = 0; i < spawnCount; i++)
            {
                // Select decoration entry
                int entryIndex = style.pickRandomlyPerInstance
                    ? UnityEngine.Random.Range(0, style.decorations.Count)
                    : fixedIndex;

                var entry = style.decorations[entryIndex];
                if (entry == null || entry.prefab == null) continue;

                // Calculate random position within hex bounds
                Vector2 randomOffset = UnityEngine.Random.insideUnitCircle * 0.4f;
                Vector3 localPos = new Vector3(randomOffset.x, 0f, randomOffset.y);

                // Calculate scale with random variance
                float rolled = UnityEngine.Random.Range(entry.randomScaleRange.x, entry.randomScaleRange.y);
                float finalScale = entry.scale * (1f + rolled / 100f);

                // Spawn decoration under DecorRoot
                var deco = Instantiate(entry.prefab, decorRoot);
                deco.transform.localPosition = localPos;
                deco.transform.localScale = Vector3.one * finalScale;
                deco.name = $"Deco_{entry.prefab.name}_{i}";
            }
        }

        /// <summary>
        /// Clears all decorations from a tile by destroying the DecorRoot container's children.
        /// </summary>
        private void ClearDecorationsAtTile(HexCoord coord)
        {
            if (!_owned.TryGetValue(coord, out var tile) || !tile) return;

            var decorRoot = tile.transform.Find(DecorRootName);
            if (!decorRoot) return;

            // Destroy all children under DecorRoot
            for (int i = decorRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(decorRoot.GetChild(i).gameObject);
            }
        }

        private void AddFrontier(HexCoord coord)
        {
            if (_owned.ContainsKey(coord)) return;
            if (_frontier.ContainsKey(coord)) return;

            var go = Instantiate(frontierPrefab, AxialToWorld(coord), Quaternion.identity, tilesParent);

            var marker = go.GetComponent<HexWorld3DTile>();
            if (!marker) marker = go.AddComponent<HexWorld3DTile>();
            marker.Set(coord, isFrontier: true);

            _frontier.Add(coord, marker);
        }

        private void RefreshFrontier()
        {
            var needed = new HashSet<HexCoord>();

            foreach (var kv in _owned)
            {
                var c = kv.Key;
                for (int i = 0; i < HexCoord.NeighborDirs.Length; i++)
                {
                    var n = new HexCoord(c.q + HexCoord.NeighborDirs[i].q, c.r + HexCoord.NeighborDirs[i].r);
                    if (!_owned.ContainsKey(n))
                        needed.Add(n);
                }
            }

            var toRemove = new List<HexCoord>();
            foreach (var kv in _frontier)
            {
                if (!needed.Contains(kv.Key))
                    toRemove.Add(kv.Key);
            }

            foreach (var c in toRemove)
            {
                if (_frontier.TryGetValue(c, out var t) && t) Destroy(t.gameObject);
                _frontier.Remove(c);
            }

            foreach (var c in needed)
                AddFrontier(c);
        }

        // ============================
        // Tile Budget Placement Flow (Ghost)
        // ============================
        private void EnsureCursorGhost()
        {
            if (_cursorGhost) return;

            var prefab = cursorGhostPrefab ? cursorGhostPrefab : ownedPrefab;
            if (!prefab) return;

            _cursorGhost = Instantiate(prefab, Vector3.zero, Quaternion.identity, GetGhostParent());
            _cursorGhost.name = "CursorGhost";

            // Prevent EdgeBlender from treating the ghost as an owned tile
            var ghostMarkers = _cursorGhost.GetComponentsInChildren<HexWorld3DTile>(true);
            foreach (var gm in ghostMarkers)
            {
                if (!gm) continue;
                gm.IsFrontier = true;
            }

            DisableColliders(_cursorGhost);

            int ignoreLayer = 2;
            int named = LayerMask.NameToLayer("Ignore Raycast");
            if (named >= 0) ignoreLayer = named;
            SetLayerRecursively(_cursorGhost, ignoreLayer);

            SetGhostShadowsOff(_cursorGhost);
            InstanceMaterialsOnce(_cursorGhost);

            _cursorGhost.SetActive(false);
        }

        private void UpdateCursorGhostVisibility()
        {
            if (!_cursorGhost) return;
            _cursorGhost.SetActive(IsTilePaletteMode(_paletteMode) && SelectedStyle != null);
        }

        private void ApplyStyleToGhost()
        {
            if (!_cursorGhost) return;
            if (!SelectedStyle) return;

            ApplyStyleToTile(_cursorGhost, SelectedStyle);
            ForceGhostAlpha(_cursorGhost, ghostAlpha);
        }

        private void UpdateCursorGhost()
        {
            EnsureCursorGhost();

            if (!_cursorGhost)
                return;

            if (!IsTilePaletteMode(_paletteMode) || !SelectedStyle)
            {
                _cursorGhost.SetActive(false);
                return;
            }

            _cursorGhost.SetActive(true);

            if (!TryGetCursorPlacementState(out var coord, out var isValid))
                return;

            _cursorCoord = coord;
            _cursorValid = isValid;

            var pos = AxialToWorld(coord);
            pos.y += ghostYOffset;
            _cursorGhost.transform.position = pos;

            ApplyStyleToGhost();

            if (tintGhostWhenInvalid)
            {
                var tint = _cursorValid ? Color.white : invalidGhostTint;
                ApplyGhostTint(_cursorGhost, tint);
            }
        }

        private bool TryGetCursorCoord(out HexCoord coord)
        {
            coord = default;

            if (!mainCamera) return false;
            if (hexSize <= 0.00001f) return false;

            Vector2 mousePos = Mouse.current.position.ReadValue();
            Ray ray = mainCamera.ScreenPointToRay(mousePos);

            Plane plane = new Plane(Vector3.up, Vector3.zero);
            if (!plane.Raycast(ray, out float enter))
                return false;

            Vector3 world = ray.GetPoint(enter);
            coord = WorldToAxial(world);
            return true;
        }

        private bool IsPlacementValid(HexCoord coord)
        {
            if (_owned.ContainsKey(coord))
                return false;

            if (!requireAdjacencyAfterFirst)
                return true;

            if (_owned.Count == 0)
                return true;

            for (int i = 0; i < HexCoord.NeighborDirs.Length; i++)
            {
                var n = new HexCoord(coord.q + HexCoord.NeighborDirs[i].q, coord.r + HexCoord.NeighborDirs[i].r);
                if (_owned.ContainsKey(n))
                    return true;
            }
            return false;
        }

        private bool IsPlacementValidForCurrentMode(HexCoord coord)
        {
            if (_paletteMode == PaletteMode.Buildings && SelectedBuilding != null)
                return IsBuildingPlacementValid(coord, SelectedBuilding);

            return IsPlacementValid(coord);
        }

        private void ClearAllTiles()
        {
            foreach (var kv in _owned)
                if (kv.Value) Destroy(kv.Value.gameObject);
            _owned.Clear();

            foreach (var kv in _frontier)
                if (kv.Value) Destroy(kv.Value.gameObject);
            _frontier.Clear();

            // buildings (may already be destroyed as children, but clear our dicts)
            foreach (var kv in _buildings)
                if (kv.Value) Destroy(kv.Value.gameObject);
            _buildings.Clear();
            _buildingNameByCoord.Clear();

            _ownedStyleName.Clear();
        }

        // ============================
        // Save / Load (Autosave + Presets)
        // ============================
        [Serializable]
        private struct TileSave
        {
            public int q;
            public int r;
            public string styleName;
            public int tier;
        }

        [Serializable]
        private struct BuildingSave
        {
            public int q;
            public int r;
            public string buildingName;

            // Save v2+
            public bool isActive;
            public int level;

            // Save v3+: minigame state persistence
            public string state;
        }

        [Serializable]
        private class VillageSave
        {
            // Increment this if you change the save schema.
            public int saveVersion;

            public int tilesLeft;
            public int credits;

            public int townHallLevel;
            public int warehouseLevel;
            public List<HexWorldResourceStack> warehouse = new List<HexWorldResourceStack>();
            public List<TileSave> tiles = new List<TileSave>();
            public List<BuildingSave> buildings = new List<BuildingSave>();
        }

        private string SaveDir() => Path.Combine(Application.persistentDataPath, "GalacticFishing", "HexWorldVillage");
        private string AutosavePath() => Path.Combine(SaveDir(), "autosave.json");
        private string PresetDir() => Path.Combine(SaveDir(), "presets");

        private string PresetPath(string presetName)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                presetName = presetName.Replace(c, '_');

            presetName = presetName.Trim();
            if (string.IsNullOrEmpty(presetName))
                presetName = "preset";

            return Path.Combine(PresetDir(), presetName + ".json");
        }

        public void SaveAutosave() => TrySaveToPath(AutosavePath());
        public bool TryLoadAutosave() => TryLoadFromPath(AutosavePath());

        public void SavePreset(string presetName)
        {
            TrySaveToPath(PresetPath(presetName));
            RequestToast($"Preset saved: {presetName}");
        }

        public bool LoadPreset(string presetName)
        {
            bool ok = TryLoadFromPath(PresetPath(presetName));
            RequestToast(ok ? $"Preset loaded: {presetName}" : $"Preset not found: {presetName}");
            return ok;
        }

        private void TrySaveToPath(string path)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));

                var save = new VillageSave
                {
                    saveVersion = CurrentSaveVersion,
                    tilesLeft = _tilesLeftToPlace,
                    credits = _credits,
                    townHallLevel = townHallLevel,
                    warehouseLevel = warehouse ? warehouse.WarehouseLevel : 1,
                    warehouse = warehouse ? warehouse.ToStacks() : new List<HexWorldResourceStack>()
                };

                foreach (var kv in _owned)
                {
                    var c = kv.Key;
                    var tile = kv.Value;
                    save.tiles.Add(new TileSave
                    {
                        q = c.q,
                        r = c.r,
                        styleName = _ownedStyleName.TryGetValue(c, out var s) ? s : "",
                        tier = tile ? tile.TerrainTier : 0
                    });
                }

                foreach (var kv in _buildings)
                {
                    var c = kv.Key;
                    var inst = kv.Value;
                    if (!inst) continue;

                    // Gather minigame state from building (if any)
                    string buildingState = inst.GatherSerializedState();

                    save.buildings.Add(new BuildingSave
                    {
                        q = c.q,
                        r = c.r,
                        buildingName = inst.buildingName,
                        isActive = inst.IsActive,
                        level = inst.Level,
                        state = buildingState
                    });
                }

                string json = JsonUtility.ToJson(save, true);
                File.WriteAllText(path, json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"HexWorldVillage save failed: {e.Message}");
            }
        }

        private bool TryLoadFromPath(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return false;

                string json = File.ReadAllText(path);
                var save = JsonUtility.FromJson<VillageSave>(json);
                if (save == null)
                    return false;

                Debug.Log($"Loading save: TH L{save.townHallLevel}, {save.tiles.Count} tiles, {save.tilesLeft} tiles left");

                ClearAllTiles();

                _tilesLeftToPlace = Mathf.Max(0, save.tilesLeft);
                TilesLeftChanged?.Invoke(_tilesLeftToPlace);

                _credits = save.credits;
                CreditsChanged?.Invoke(_credits);

                townHallLevel = Mathf.Clamp(save.townHallLevel <= 0 ? 1 : save.townHallLevel, 1, 10);
                Debug.Log($"Town Hall level set to: {townHallLevel}, Capacity: {HexWorldCapacityService.GetTileCapacity(townHallLevel)}");

                EnsureWarehouse();
                if (warehouse)
                    warehouse.LoadFromStacks(level: (save.warehouseLevel <= 0 ? 1 : save.warehouseLevel), stacks: save.warehouse);

                // 1) Tiles
                for (int i = 0; i < save.tiles.Count; i++)
                {
                    var t = save.tiles[i];
                    var coord = new HexCoord(t.q, t.r);
                    var style = ResolveStyleByName(t.styleName);
                    AddOwned(coord, style, false);

                    // Load order: apply restored tier after style assignment.
                    if (_owned.TryGetValue(coord, out var loadedTile) && loadedTile)
                        loadedTile.SetTerrainTier(t.tier);
                }

                // 2) Buildings (only place if tile exists)
                for (int i = 0; i < save.buildings.Count; i++)
                {
                    var b = save.buildings[i];
                    var coord = new HexCoord(b.q, b.r);

                    if (!_owned.ContainsKey(coord))
                        continue;

                    var def = ResolveBuildingByName(b.buildingName);
                    if (def)
                    {
                        // place without UI selection side-effects
                        PlaceBuildingFromLoad(coord, def, b, save.saveVersion);
                    }
                }

                SeedPlacedBuildingNamesFromSessionBuildings();
                RecomputeActiveSlotsAndNotify(enforceLimit: true);

                // Reset UI selection state
                SetSelectedStyle(null);
                SetSelectedBuilding(null);
                SetPaletteModeTiles();

                // Notify tiles placed
                TilesPlacedChanged?.Invoke(_owned.Count, TileCapacityMax);

                // Recompute road network after loading
                RecomputeRoadNetwork();

                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"HexWorldVillage load failed: {e.Message}");
                return false;
            }
        }

        private void PlaceBuildingFromLoad(HexCoord coord, HexWorldBuildingDefinition def, BuildingSave save, int saveVersion)
        {
            if (!def || !def.prefab) return;
            if (!_owned.TryGetValue(coord, out var tile) || !tile) return;

            // Clear decorations before placing building
            ClearDecorationsAtTile(coord);

            var go = Instantiate(def.prefab, tile.transform);
            go.name = $"B_{(string.IsNullOrWhiteSpace(def.displayName) ? def.name : def.displayName)}";

            go.transform.localPosition = def.localOffset;
            go.transform.localRotation = Quaternion.Euler(def.localEuler);
            go.transform.localScale = def.localScale;

            var inst = go.GetComponent<HexWorldBuildingInstance>();
            if (!inst) inst = go.AddComponent<HexWorldBuildingInstance>();

            // Back-compat:
            // - saveVersion <= 1 (or missing) means buildings did not store active/level; default to Active and Level 1.
            // - saveVersion <= 2 means buildings did not store minigame state.
            bool consumes = def.consumesActiveSlot;
            bool isActive = (saveVersion <= 1) ? def.defaultActive : save.isActive;
            int lvl = (saveVersion <= 1 || save.level <= 0) ? 1 : save.level;
            string buildingId = GetCanonicalBuildingId(def);
            inst.Set(coord, buildingId, consumesActiveSlot: consumes, isActive: isActive, level: lvl);

            // Load path should never apply default relocation cooldown intended for new placements.
            inst.SetRelocationCooldown(0f);

            // Restore minigame state (v3+)
            if (saveVersion >= 3 && !string.IsNullOrEmpty(save.state))
            {
                inst.RestoreSerializedState(save.state);
            }

            _buildings[coord] = inst;
            _buildingNameByCoord[coord] = buildingId;
        }

        private void EnsureWarehouse()
        {
            if (warehouse) return;
            warehouse = GetComponent<HexWorldWarehouseInventory>();
            if (!warehouse)
                warehouse = gameObject.AddComponent<HexWorldWarehouseInventory>();
        }

        private bool CanRemoveWarehouseBuilding(HexWorldBuildingDefinition buildingDef)
        {
            if (buildingDef == null || buildingDef.kind != HexWorldBuildingDefinition.BuildingKind.Warehouse)
                return true;

            EnsureWarehouse();
            if (!warehouse) return true;

            int newLevel = Mathf.Max(1, warehouse.WarehouseLevel - 1);
            int newCapacity = HexWorldWarehouseInventory.GetCapacityForLevel(newLevel);

            if (warehouse.TotalStored > newCapacity)
            {
                RequestToast("Cannot remove Warehouse: Storage would overflow.");
                return false;
            }

            return true;
        }

        private void RecomputeActiveSlotsAndNotify(bool enforceLimit = false)
        {
            int used = 0;
            foreach (var kv in _buildings)
            {
                var b = kv.Value;
                if (!b) continue;
                if (!b.IsActive) continue;
                if (!b.ConsumesActiveSlot) continue;
                used++;
            }

            _activeBuildingsUsed = used;

            if (enforceLimit)
                EnforceActiveSlotsLimit();

            ActiveSlotsChanged?.Invoke(_activeBuildingsUsed, ActiveSlotsTotal, townHallLevel);
        }

        private void EnforceActiveSlotsLimit()
        {
            int total = ActiveSlotsTotal;
            if (_activeBuildingsUsed <= total) return;

            // Deterministic order: sort coords, deactivate overflow.
            var coords = new List<HexCoord>(_buildings.Keys);
            coords.Sort((a, b) =>
            {
                int cq = a.q.CompareTo(b.q);
                return cq != 0 ? cq : a.r.CompareTo(b.r);
            });

            int used = 0;
            for (int i = 0; i < coords.Count; i++)
            {
                if (!_buildings.TryGetValue(coords[i], out var b) || !b) continue;
                if (!b.ConsumesActiveSlot) continue;

                if (b.IsActive)
                {
                    used++;
                    if (used > total)
                        b.SetActive(false);
                }
            }

            // Recompute used after enforcement
            used = 0;
            foreach (var kv in _buildings)
                if (kv.Value && kv.Value.IsActive && kv.Value.ConsumesActiveSlot)
                    used++;
            _activeBuildingsUsed = used;
        }

        // ============================
        // Road Connectivity (TICKET 8)
        // ============================

        /// <summary>
        /// Recomputes the road network using BFS from Town Hall at (0,0).
        /// Only tiles with the "road" gameplay tag are traversable.
        /// Call this when tiles are painted or removed.
        /// </summary>
        public void RecomputeRoadNetwork()
        {
            _townHallRoadComponent.Clear();

            var townHallCoord = new HexCoord(0, 0);

            // Check if Town Hall tile exists and is a road (or always include it as the source)
            if (!_owned.ContainsKey(townHallCoord))
            {
                Debug.Log("[RoadNetwork] Town Hall tile not found at (0,0). Road network empty.");
                RoadNetworkRecomputed?.Invoke();
                return;
            }

            // BFS from Town Hall
            var visited = new HashSet<HexCoord>();
            var queue = new Queue<HexCoord>();

            // Always start from Town Hall regardless of its tag
            queue.Enqueue(townHallCoord);
            visited.Add(townHallCoord);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                // Check if current tile is a road (or is the Town Hall)
                bool isRoad = IsRoadTile(current);
                bool isTownHall = current == townHallCoord;

                if (isRoad || isTownHall)
                {
                    _townHallRoadComponent.Add(current);

                    // Explore neighbors
                    for (int i = 0; i < HexCoord.NeighborDirs.Length; i++)
                    {
                        var neighbor = current.Neighbor(i);

                        if (visited.Contains(neighbor))
                            continue;

                        if (!_owned.ContainsKey(neighbor))
                            continue;

                        visited.Add(neighbor);

                        // Only queue if it's a road tile (to continue the path)
                        if (IsRoadTile(neighbor))
                            queue.Enqueue(neighbor);
                    }
                }
            }

            Debug.Log($"[RoadNetwork] Recomputed: {_townHallRoadComponent.Count} tiles connected to Town Hall via roads.");
            RoadNetworkRecomputed?.Invoke();
        }

        /// <summary>
        /// Checks if a tile at the given coordinate has the "road" gameplay tag.
        /// </summary>
        private bool IsRoadTile(HexCoord coord)
        {
            if (!_ownedStyleName.TryGetValue(coord, out var styleName))
                return false;

            var style = ResolveStyleByName(styleName);
            if (style == null)
                return false;

            return style.HasTag(RoadTag);
        }

        /// <summary>
        /// Returns true if the given coordinate is connected to Town Hall via roads.
        /// Buildings can use this to apply the +10% connectivity bonus.
        /// </summary>
        public bool IsConnectedToTownHall(HexCoord coord)
        {
            // Town Hall itself is always "connected"
            if (coord.q == 0 && coord.r == 0)
                return true;

            // Check if coord is adjacent to any tile in the road component
            for (int i = 0; i < HexCoord.NeighborDirs.Length; i++)
            {
                var neighbor = coord.Neighbor(i);
                if (_townHallRoadComponent.Contains(neighbor))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Returns the set of all road tiles connected to the Town Hall.
        /// Useful for debug visualization or UI.
        /// </summary>
        public IReadOnlyCollection<HexCoord> GetTownHallRoadComponent() => _townHallRoadComponent;

        /// <summary>
        /// Returns true if the given coordinate is adjacent to any road tile.
        /// Used for the RoadAdjacent synergy bonus.
        /// </summary>
        public bool IsAdjacentToRoad(HexCoord coord)
        {
            for (int i = 0; i < HexCoord.NeighborDirs.Length; i++)
            {
                var neighbor = coord.Neighbor(i);
                if (IsRoadTile(neighbor))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Counts how many adjacent tiles have a specific gameplay tag.
        /// Used for AdjacentTileTag synergy stacking.
        /// </summary>
        public int CountAdjacentTilesWithTag(HexCoord coord, string tag)
        {
            if (string.IsNullOrEmpty(tag)) return 0;

            int count = 0;
            for (int i = 0; i < HexCoord.NeighborDirs.Length; i++)
            {
                var neighbor = coord.Neighbor(i);
                if (!_ownedStyleName.TryGetValue(neighbor, out var styleName))
                    continue;

                var style = ResolveStyleByName(styleName);
                if (style != null && style.HasTag(tag))
                    count++;
            }
            return count;
        }

        /// <summary>
        /// Counts buildings of a specific type within a radius of the given coordinate.
        /// Used for WithinRadiusBuildingType synergy.
        /// </summary>
        public int CountBuildingsWithinRadius(HexCoord center, string buildingId, int radius)
        {
            if (string.IsNullOrEmpty(buildingId) || radius <= 0) return 0;

            int count = 0;
            foreach (var kv in _buildings)
            {
                if (kv.Value == null) continue;
                if (center.DistanceTo(kv.Key) > radius) continue;
                if (center == kv.Key) continue; // Don't count self

                if (kv.Value.buildingName == buildingId)
                    count++;
            }
            return count;
        }

#if UNITY_EDITOR
        [Header("Debug (Editor Only)")]
        [SerializeField] private bool debugDrawRoadNetwork = true;
        [SerializeField] private Color debugRoadColor = new Color(0.2f, 0.8f, 0.2f, 0.5f);

        private void OnDrawGizmos()
        {
            if (!debugDrawRoadNetwork) return;
            if (_townHallRoadComponent == null || _townHallRoadComponent.Count == 0) return;

            Gizmos.color = debugRoadColor;

            foreach (var coord in _townHallRoadComponent)
            {
                var worldPos = AxialToWorld(coord);
                worldPos.y += 0.5f; // Lift above tile
                Gizmos.DrawWireSphere(worldPos, 0.3f);
            }

            // Draw Town Hall marker in different color
            Gizmos.color = Color.yellow;
            var thPos = AxialToWorld(new HexCoord(0, 0));
            thPos.y += 0.5f;
            Gizmos.DrawSphere(thPos, 0.4f);
        }
#endif

        private HexWorldTileStyle ResolveStyleByName(string styleName)
        {
            if (string.IsNullOrEmpty(styleName))
                return startingStyle;

            if (styleCatalog != null)
            {
                for (int i = 0; i < styleCatalog.Length; i++)
                {
                    var s = styleCatalog[i];
                    if (!s) continue;
                    if (s.name == styleName) return s;
                }
            }

            return startingStyle;
        }

        public HexWorldBuildingDefinition ResolveBuildingByName(string buildingName)
        {
            if (string.IsNullOrEmpty(buildingName))
                return null;

            if (buildingCatalog != null)
            {
                for (int i = 0; i < buildingCatalog.Length; i++)
                {
                    var b = buildingCatalog[i];
                    if (!b) continue;
                    if (string.Equals(GetCanonicalBuildingId(b), buildingName, StringComparison.Ordinal))
                        return b;
                    if (string.Equals(b.name, buildingName, StringComparison.Ordinal))
                        return b;
                }
            }

            return null;
        }

        // Exit button can call this
        public void ExitVillage()
        {
            if (enableTileBudgetPlacement && autoSaveOnExit)
                SaveAutosave();

            ExitRequested?.Invoke();

            if (!string.IsNullOrEmpty(exitSceneName))
                UnityEngine.SceneManagement.SceneManager.LoadScene(exitSceneName);
        }

        // ============================
        // Hex math (world <-> axial)
        // ============================
        private Vector3 AxialToWorld(HexCoord c)
        {
            float x = hexSize * (1.5f * c.q);
            float z = hexSize * (Mathf.Sqrt(3f) * (c.r + c.q * 0.5f));
            return new Vector3(x, 0f, z);
        }

        private HexCoord WorldToAxial(Vector3 world)
        {
            float qf = (2f / 3f) * (world.x / hexSize);
            float rf = (-1f / 3f) * (world.x / hexSize) + (1f / Mathf.Sqrt(3f)) * (world.z / hexSize);
            return HexRound(qf, rf);
        }

        private static HexCoord HexRound(float qf, float rf)
        {
            float xf = qf;
            float zf = rf;
            float yf = -xf - zf;

            int rx = Mathf.RoundToInt(xf);
            int ry = Mathf.RoundToInt(yf);
            int rz = Mathf.RoundToInt(zf);

            float xDiff = Mathf.Abs(rx - xf);
            float yDiff = Mathf.Abs(ry - yf);
            float zDiff = Mathf.Abs(rz - zf);

            if (xDiff > yDiff && xDiff > zDiff)
                rx = -ry - rz;
            else if (yDiff > zDiff)
                ry = -rx - rz;
            else
                rz = -rx - ry;

            return new HexCoord(rx, rz);
        }

        private float AutoDetectHexSizeOrFallback(float fallback)
        {
            if (!ownedPrefab) return fallback;

            var r = ownedPrefab.GetComponentInChildren<Renderer>();
            if (!r) return fallback;

            float approx = Mathf.Max(r.bounds.extents.x, r.bounds.extents.z);
            return approx > 0.0001f ? approx : fallback;
        }

        // ============================
        // Style application (robust TOP slot logic)
        // ============================
        private void ApplyStyleToTile(GameObject tileRoot, HexWorldTileStyle style)
        {
            if (!tileRoot) return;
            if (!style) return;
            if (style.materials == null || style.materials.Length == 0) return;

            Material topMat = style.materials[0];
            Material sideMat = (style.materials.Length > 1 && style.materials[1] != null)
                ? style.materials[1]
                : style.materials[0];

            var renderers = tileRoot.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var mr in renderers)
            {
                if (!mr) continue;

                var mats = mr.sharedMaterials;
                if (mats == null || mats.Length == 0) continue;

                if (mats.Length == 1)
                {
                    mats[0] = topMat;
                    mr.sharedMaterials = mats;
                    continue;
                }

                int topIndex = ResolveTopMaterialIndex(mats);

                for (int i = 0; i < mats.Length; i++)
                    mats[i] = (i == topIndex) ? topMat : sideMat;

                mr.sharedMaterials = mats;
            }
        }

        private int ResolveTopMaterialIndex(Material[] current)
        {
            if (current == null || current.Length == 0) return 0;

            if (topMaterialSlotOverride >= 0 && topMaterialSlotOverride < current.Length)
                return topMaterialSlotOverride;

            for (int i = 0; i < current.Length; i++)
            {
                var m = current[i];
                if (!m) continue;

                string n = m.name;
                if (string.IsNullOrEmpty(n)) continue;

                n = n.ToLowerInvariant();
                if (n.Contains("tiletop") || n.Contains("_top") || n.Contains(" top") || n.Contains("top_") || n.Contains("top"))
                    return i;
            }

            return (current.Length > 1) ? 1 : 0;
        }

        // ============================
        // Ghost helpers
        // ============================
        private static void DisableColliders(GameObject root)
        {
            var cols = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
                cols[i].enabled = false;
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            if (!root) return;
            root.layer = layer;
            foreach (Transform t in root.transform)
                SetLayerRecursively(t.gameObject, layer);
        }

        private static void SetGhostShadowsOff(GameObject root)
        {
            var r = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < r.Length; i++)
            {
                r[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r[i].receiveShadows = false;
            }
        }

        private static void InstanceMaterialsOnce(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (!r) continue;
                var mats = r.materials;
                r.materials = mats;
            }
        }

        private static readonly string[] _ghostColorProps = { "_BaseColor", "_Color", "_TintColor", "_Tint" };
        private static readonly string[] _ghostAlphaProps = { "_Alpha", "_Opacity", "_Transparency" };

        private static bool TrySetMaterialGhostAlpha(Material mat, float a)
        {
            if (!mat) return false;

            for (int i = 0; i < _ghostAlphaProps.Length; i++)
            {
                var p = _ghostAlphaProps[i];
                if (mat.HasProperty(p))
                {
                    mat.SetFloat(p, a);
                    return true;
                }
            }

            for (int i = 0; i < _ghostColorProps.Length; i++)
            {
                var p = _ghostColorProps[i];
                if (mat.HasProperty(p))
                {
                    Color c = mat.GetColor(p);
                    c.a = a;
                    mat.SetColor(p, c);
                    return true;
                }
            }

            return false;
        }

        private void ForceGhostAlpha(GameObject root, float alpha)
        {
            if (!root) return;
            alpha = Mathf.Clamp01(alpha);

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (!r) continue;

                var mats = r.materials;
                if (mats == null || mats.Length == 0) continue;

                for (int i = 0; i < mats.Length; i++)
                    TrySetMaterialGhostAlpha(mats[i], alpha);

                r.materials = mats;
            }
        }

        private void ApplyGhostTint(GameObject root, Color tint)
        {
            if (!root) return;

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (!r) continue;

                _ghostMpb.Clear();

                bool any = false;
                if (r.sharedMaterial && r.sharedMaterial.HasProperty("_BaseColor"))
                {
                    _ghostMpb.SetColor("_BaseColor", tint);
                    any = true;
                }
                if (r.sharedMaterial && r.sharedMaterial.HasProperty("_Color"))
                {
                    _ghostMpb.SetColor("_Color", tint);
                    any = true;
                }

                if (any)
                    r.SetPropertyBlock(_ghostMpb);
                else
                    r.SetPropertyBlock(null);
            }
        }


        private int GetBuildingUpgradeIpReward(int targetLevel)
        {
            return targetLevel switch
            {
                2 => 10,
                3 => 15,
                4 => 25,
                5 => 40,
                _ => 10
            };
        }



    }
}
        
