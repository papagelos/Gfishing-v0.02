// Assets/Minigames/HexWorld3D/Scripts/HexWorld3DController.cs
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace GalacticFishing.Minigames.HexWorld
{
    public sealed class HexWorld3DController : MonoBehaviour
    {
        private const int CurrentSaveVersion = 2;
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
        public enum PaletteMode { Tiles, Buildings, TileUpgrade }

        [Header("Buildings")]
        [Tooltip("Catalog used to resolve saved building names back to assets.")]
        [SerializeField] private HexWorldBuildingDefinition[] buildingCatalog;

        [Tooltip("Optional parent for spawned buildings. If empty, creates/uses child named 'Buildings' under this controller.")]
        [SerializeField] private Transform buildingsParent;

        [Tooltip("Allow placing a building to replace an existing building on the same tile.")]
        [SerializeField] private bool allowReplaceBuilding = true;

        [Header("Town Hall / Slots (Design Doc)")]
        [Tooltip("Town Hall level controls Active Slots (Design Doc Section 8). For now this is a simple integer; later it can be driven by a TownHall building upgrade.")]
        [SerializeField, Range(1, 10)] private int townHallLevel = 1;

        [Header("Warehouse (Design Doc)")]
        [Tooltip("Warehouse inventory component. If left empty, the controller will create one on this GameObject.")]
        [SerializeField] private HexWorldWarehouseInventory warehouse;

        [Header("Village Save/Load")]
        [Tooltip("If ON, load autosave when entering the village.")]
        [SerializeField] private bool autoLoadOnStart = true;

        [Tooltip("If ON, autosave when exiting/unloading this scene.")]
        [SerializeField] private bool autoSaveOnExit = true;

        [Tooltip("Optional: if set, ExitVillage() will load this scene after saving. Leave empty if you handle scene switching elsewhere.")]
        [SerializeField] private string exitSceneName = "";

        [Tooltip("Style catalog used to resolve saved style names back to assets (drag same list you use in the tile bar).")]
        [SerializeField] private HexWorldTileStyle[] styleCatalog;

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

        // Public accessor for district bonus calculations
        public Dictionary<HexCoord, HexWorld3DTile> OwnedTiles => _owned;

        private PaletteMode _paletteMode = PaletteMode.Tiles;

        private readonly Dictionary<HexCoord, HexWorld3DTile> _owned = new();
        private readonly Dictionary<HexCoord, HexWorld3DTile> _frontier = new();

        // Coord -> style name (since HexWorld3DTile doesn't store style)
        private readonly Dictionary<HexCoord, string> _ownedStyleName = new();

        // Coord -> building instance
        private readonly Dictionary<HexCoord, HexWorldBuildingInstance> _buildings = new();
        private readonly Dictionary<HexCoord, string> _buildingNameByCoord = new();

        private int _credits;

        private int _activeBuildingsUsed;

        // Tile budget state
        private int _tilesLeftToPlace;
        private GameObject _cursorGhost;
        private HexCoord _cursorCoord;
        private bool _cursorValid;

        // MaterialPropertyBlock for ghost tint (no allocations, no shared material edits)
        private MaterialPropertyBlock _ghostMpb;

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
                        RequestToast($"New village: {_tilesLeftToPlace} tiles available (TH L{townHallLevel})");
                    }
                }
                else
                {
                    RequestToast($"New village: {_tilesLeftToPlace} tiles available (TH L{townHallLevel})");
                }

                RecomputeActiveSlotsAndNotify();
                TilesPlacedChanged?.Invoke(_owned.Count, TileCapacityMax);

                return;
            }

            // ---- Original prototype behavior ----
            AddOwned(new HexCoord(0, 0), startingStyle);
            RefreshFrontier();
            SetSelectedStyle(null);
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

            if (enableTileBudgetPlacement)
            {
                // Ghost only in tile mode.
                if (_paletteMode == PaletteMode.Tiles)
                {
                    UpdateCursorGhost();

                    if (_cursorGhost)
                    {
                        bool rmbHeld = Mouse.current.rightButton.isPressed;
                        _cursorGhost.SetActive(SelectedStyle != null && !rmbHeld);
                    }

                    // Drag paint / place tiles
                    if (enableDragPaint && Mouse.current.leftButton.isPressed)
                    {
                        if (!(EventSystem.current && EventSystem.current.IsPointerOverGameObject()))
                        {
                            if (SelectedStyle && TryGetCursorCoord(out var coord))
                            {
                                if (!_dragHasLast || coord.q != _dragLastCoord.q || coord.r != _dragLastCoord.r)
                                {
                                    _dragHasLast = true;
                                    _dragLastCoord = coord;

                                    TryPlaceOrPaintAtCoord(coord);
                                }
                            }
                        }
                    }

                    if (Mouse.current.leftButton.wasReleasedThisFrame)
                        _dragHasLast = false;
                }
                else if (_paletteMode == PaletteMode.Buildings)
                {
                    // Buildings mode: no tile ghost
                    if (_cursorGhost) _cursorGhost.SetActive(false);
                    _dragHasLast = false;

                    // Place building on click
                    if (Mouse.current.leftButton.wasPressedThisFrame)
                    {
                        if (!(EventSystem.current && EventSystem.current.IsPointerOverGameObject()))
                        {
                            if (TryGetCursorCoord(out var coord))
                            {
                                if (SelectedBuilding)
                                    TryPlaceBuildingAtCoord(coord);
                                else
                                    TryToggleBuildingActiveAtCoord(coord);
                            }
                        }
                    }
                }
                else if (_paletteMode == PaletteMode.TileUpgrade)
                {
                    // Tile Upgrade mode: click to upgrade a tile's tier
                    if (_cursorGhost) _cursorGhost.SetActive(false);
                    _dragHasLast = false;

                    if (Mouse.current.leftButton.wasPressedThisFrame)
                    {
                        if (!(EventSystem.current && EventSystem.current.IsPointerOverGameObject()))
                        {
                            if (TryGetCursorCoord(out var coord))
                            {
                                TryUpgradeTileTier(coord);
                            }
                        }
                    }
                }

                // RMB delete (priority: building first, else tile)
                if (enableRightClickRemove && Mouse.current.rightButton.wasPressedThisFrame)
                {
                    if (EventSystem.current && EventSystem.current.IsPointerOverGameObject())
                        return;

                    TryRemoveBuildingOrTileUnderMouse();
                }

                return;
            }

            // ---- Original prototype input ----
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (EventSystem.current && EventSystem.current.IsPointerOverGameObject())
                    return;

                TryClick();
            }
        }

        // ============================
        // Palette mode switching (for your left-side tabs)
        // ============================
        public void SetPaletteModeTiles()
        {
            if (_paletteMode == PaletteMode.Tiles) return;
            _paletteMode = PaletteMode.Tiles;
            PaletteModeChanged?.Invoke(_paletteMode);

            EnsureCursorGhost();
            UpdateCursorGhostVisibility();
        }

        public void SetPaletteModeBuildings()
        {
            if (_paletteMode == PaletteMode.Buildings) return;
            _paletteMode = PaletteMode.Buildings;
            PaletteModeChanged?.Invoke(_paletteMode);

            if (_cursorGhost) _cursorGhost.SetActive(false);
        }

        public void SetPaletteModeTileUpgrade()
        {
            if (_paletteMode == PaletteMode.TileUpgrade) return;
            _paletteMode = PaletteMode.TileUpgrade;
            PaletteModeChanged?.Invoke(_paletteMode);

            if (_cursorGhost) _cursorGhost.SetActive(false);
        }

        public void SetSelectedStyle(HexWorldTileStyle style)
        {
            // click same style again -> deselect
            if (SelectedStyle == style)
                style = null;

            SelectedStyle = style;
            SelectedStyleChanged?.Invoke(SelectedStyle);

            // If player selects a tile style, it's natural to be in tile mode.
            if (enableTileBudgetPlacement)
            {
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

            SelectedBuilding = def;
            SelectedBuildingChanged?.Invoke(SelectedBuilding);

            if (enableTileBudgetPlacement)
                SetPaletteModeBuildings();
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

            if (_owned.TryGetValue(coord, out var existing) && existing)
            {
                if (allowRepaintOnOwnedClick)
                {
                    ApplyStyleToTile(existing.gameObject, SelectedStyle);
                    _ownedStyleName[coord] = SelectedStyle ? SelectedStyle.name : "";

                    // Update terrain type when repainting
                    if (SelectedStyle != null)
                        existing.SetTerrainType(SelectedStyle.terrainType);
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

            AddOwned(coord, SelectedStyle);

            // Decrease tile budget
            _tilesLeftToPlace--;
            TilesLeftChanged?.Invoke(_tilesLeftToPlace);
            TilesPlacedChanged?.Invoke(_owned.Count, TileCapacityMax);
        }

        /// <summary>
        /// Attempts to upgrade a tile's terrain tier.
        /// Tier 0 -> 1: 10 wood, 6 stone, 4 fiber, 50 credits
        /// Tier 1 -> 2: (gated by Town Hall Level >= 2) - costs TBD, using same for now
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

            // Define costs (Tier 0->1 for now, can extend for Tier 1->2)
            var costs = new[]
            {
                new HexWorldResourceStack(HexWorldResourceId.Wood, 10),
                new HexWorldResourceStack(HexWorldResourceId.Stone, 6),
                new HexWorldResourceStack(HexWorldResourceId.Fiber, 4)
            };

            int creditCost = 50;

            // Check warehouse for resources
            EnsureWarehouse();
            foreach (var cost in costs)
            {
                if (warehouse.Get(cost.id) < cost.amount)
                {
                    RequestToast($"Not enough {cost.id}. Need {cost.amount}, have {warehouse.Get(cost.id)}.");
                    return false;
                }
            }

            // Check credits
            if (_credits < creditCost)
            {
                RequestToast($"Not enough credits. Need {creditCost}, have {_credits}.");
                return false;
            }

            // Deduct resources
            foreach (var cost in costs)
            {
                if (!warehouse.TryRemove(cost.id, cost.amount))
                {
                    RequestToast($"Failed to deduct {cost.id}.");
                    return false;
                }
            }

            // Deduct credits
            _credits -= creditCost;
            CreditsChanged?.Invoke(_credits);

            // Upgrade tier
            tile.SetTerrainTier(currentTier + 1);

            RequestToast($"Tile upgraded to Tier {tile.TerrainTier}!");
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
        /// Upgrading grants additional tile capacity and active building slots.
        /// L1->L2: 20 wood, 15 stone, 10 fiber, 100 credits
        /// L2->L3: 40 wood, 30 stone, 20 fiber, 200 credits
        /// L3->L4: 60 wood, 45 stone, 30 fiber, 300 credits
        /// L4->L5: 80 wood, 60 stone, 40 fiber, 400 credits
        /// L5->L6: 100 wood, 75 stone, 50 fiber, 500 credits
        /// L6->L7: 120 wood, 90 stone, 60 fiber, 600 credits
        /// L7->L8: 140 wood, 105 stone, 70 fiber, 700 credits
        /// L8->L9: 160 wood, 120 stone, 80 fiber, 800 credits
        /// L9->L10: 200 wood, 150 stone, 100 fiber, 1000 credits
        /// </summary>
        public bool TryUpgradeTownHall()
        {
            if (townHallLevel >= 10)
            {
                RequestToast("Town Hall already at max level (10).");
                return false;
            }

            int nextLevel = townHallLevel + 1;

            // Define upgrade costs based on next level
            HexWorldResourceStack[] costs;
            int creditCost;

            switch (nextLevel)
            {
                case 2:
                    costs = new[]
                    {
                        new HexWorldResourceStack(HexWorldResourceId.Wood, 20),
                        new HexWorldResourceStack(HexWorldResourceId.Stone, 15),
                        new HexWorldResourceStack(HexWorldResourceId.Fiber, 10)
                    };
                    creditCost = 100;
                    break;
                case 3:
                    costs = new[]
                    {
                        new HexWorldResourceStack(HexWorldResourceId.Wood, 40),
                        new HexWorldResourceStack(HexWorldResourceId.Stone, 30),
                        new HexWorldResourceStack(HexWorldResourceId.Fiber, 20)
                    };
                    creditCost = 200;
                    break;
                case 4:
                    costs = new[]
                    {
                        new HexWorldResourceStack(HexWorldResourceId.Wood, 60),
                        new HexWorldResourceStack(HexWorldResourceId.Stone, 45),
                        new HexWorldResourceStack(HexWorldResourceId.Fiber, 30)
                    };
                    creditCost = 300;
                    break;
                case 5:
                    costs = new[]
                    {
                        new HexWorldResourceStack(HexWorldResourceId.Wood, 80),
                        new HexWorldResourceStack(HexWorldResourceId.Stone, 60),
                        new HexWorldResourceStack(HexWorldResourceId.Fiber, 40)
                    };
                    creditCost = 400;
                    break;
                case 6:
                    costs = new[]
                    {
                        new HexWorldResourceStack(HexWorldResourceId.Wood, 100),
                        new HexWorldResourceStack(HexWorldResourceId.Stone, 75),
                        new HexWorldResourceStack(HexWorldResourceId.Fiber, 50)
                    };
                    creditCost = 500;
                    break;
                case 7:
                    costs = new[]
                    {
                        new HexWorldResourceStack(HexWorldResourceId.Wood, 120),
                        new HexWorldResourceStack(HexWorldResourceId.Stone, 90),
                        new HexWorldResourceStack(HexWorldResourceId.Fiber, 60)
                    };
                    creditCost = 600;
                    break;
                case 8:
                    costs = new[]
                    {
                        new HexWorldResourceStack(HexWorldResourceId.Wood, 140),
                        new HexWorldResourceStack(HexWorldResourceId.Stone, 105),
                        new HexWorldResourceStack(HexWorldResourceId.Fiber, 70)
                    };
                    creditCost = 700;
                    break;
                case 9:
                    costs = new[]
                    {
                        new HexWorldResourceStack(HexWorldResourceId.Wood, 160),
                        new HexWorldResourceStack(HexWorldResourceId.Stone, 120),
                        new HexWorldResourceStack(HexWorldResourceId.Fiber, 80)
                    };
                    creditCost = 800;
                    break;
                case 10:
                    costs = new[]
                    {
                        new HexWorldResourceStack(HexWorldResourceId.Wood, 200),
                        new HexWorldResourceStack(HexWorldResourceId.Stone, 150),
                        new HexWorldResourceStack(HexWorldResourceId.Fiber, 100)
                    };
                    creditCost = 1000;
                    break;
                default:
                    RequestToast("Invalid Town Hall upgrade level.");
                    return false;
            }

            // Check warehouse for resources
            EnsureWarehouse();
            foreach (var cost in costs)
            {
                if (warehouse.Get(cost.id) < cost.amount)
                {
                    RequestToast($"Not enough {cost.id}. Need {cost.amount}, have {warehouse.Get(cost.id)}.");
                    return false;
                }
            }

            // Check credits
            if (_credits < creditCost)
            {
                RequestToast($"Not enough credits. Need {creditCost}, have {_credits}.");
                return false;
            }

            // Deduct resources
            foreach (var cost in costs)
            {
                if (!warehouse.TryRemove(cost.id, cost.amount))
                {
                    RequestToast($"Failed to deduct {cost.id}.");
                    return false;
                }
            }

            // Deduct credits
            _credits -= creditCost;
            CreditsChanged?.Invoke(_credits);

            // Calculate tile capacity increase
            int oldCapacity = HexWorldCapacityService.GetTileCapacity(townHallLevel);
            int newCapacity = HexWorldCapacityService.GetTileCapacity(nextLevel);
            int tilesGranted = newCapacity - oldCapacity;

            // Upgrade Town Hall
            townHallLevel = nextLevel;
            TownHallLevelChanged?.Invoke(townHallLevel);

            // Grant new tiles (free to place)
            _tilesLeftToPlace += tilesGranted;
            TilesLeftChanged?.Invoke(_tilesLeftToPlace);

            // Update active slots
            RecomputeActiveSlotsAndNotify();

            // Notify tiles placed (capacity changed)
            TilesPlacedChanged?.Invoke(_owned.Count, TileCapacityMax);

            RequestToast($"Town Hall upgraded to Level {townHallLevel}! Granted {tilesGranted} new tiles.");
            return true;
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

            if (!_owned.TryGetValue(coord, out var tile) || !tile)
            {
                RequestToast("Place a tile first.");
                Debug.Log($"TryPlaceBuildingAtCoord: No tile at {coord.q},{coord.r}");
                return;
            }

            if (_buildings.TryGetValue(coord, out var existing) && existing)
            {
                if (!allowReplaceBuilding)
                {
                    RequestToast("Tile already has a building.");
                    return;
                }

                Destroy(existing.gameObject);
                _buildings.Remove(coord);
                _buildingNameByCoord.Remove(coord);
            }

            if (!SelectedBuilding.prefab)
            {
                RequestToast("Building prefab missing.");
                return;
            }

            // Spawn as child of the tile (so if tile is destroyed, building goes too)
            var go = Instantiate(SelectedBuilding.prefab, tile.transform);
            go.name = $"B_{(string.IsNullOrWhiteSpace(SelectedBuilding.displayName) ? SelectedBuilding.name : SelectedBuilding.displayName)}";

            go.transform.localPosition = SelectedBuilding.localOffset;
            go.transform.localRotation = Quaternion.Euler(SelectedBuilding.localEuler);

            var inst = go.GetComponent<HexWorldBuildingInstance>();
            if (!inst) inst = go.AddComponent<HexWorldBuildingInstance>();

            bool consumes = SelectedBuilding.consumesActiveSlot;
            bool wantsActive = SelectedBuilding.defaultActive;
            bool canActivate = !consumes || (_activeBuildingsUsed < ActiveSlotsTotal);
            bool isActive = wantsActive && canActivate;

            inst.Set(coord, SelectedBuilding.name, consumesActiveSlot: consumes, isActive: isActive, level: 1);

            _buildings[coord] = inst;
            _buildingNameByCoord[coord] = SelectedBuilding.name;

            if (wantsActive && !isActive)
                RequestToast("No Active Slots available. Building placed Dormant.");

            RecomputeActiveSlotsAndNotify();
        }

        private void TryToggleBuildingActiveAtCoord(HexCoord coord)
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
                RequestToast("No Active Slots available.");
                return;
            }

            inst.SetActive(true);
            RequestToast("Building activated.");
            RecomputeActiveSlotsAndNotify();
        }

        // RMB delete priority logic:
        // - if hit building -> remove building
        // - else if hit owned tile:
        //      - if has building -> remove building
        //      - else remove tile
        private void TryRemoveBuildingOrTileUnderMouse()
        {
            if (!mainCamera) return;

            Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

            if (!Physics.Raycast(ray, out RaycastHit hit, 5000f))
                return;

            // 1) If we clicked a building collider, delete that building
            var buildingHit = hit.collider.GetComponentInParent<HexWorldBuildingInstance>();
            if (buildingHit)
            {
                var c = buildingHit.Coord;
                Destroy(buildingHit.gameObject);
                _buildings.Remove(c);
                _buildingNameByCoord.Remove(c);
                RecomputeActiveSlotsAndNotify();
                return;
            }

            // 2) Else, we clicked a tile (or something under tile)
            var tile = hit.collider.GetComponentInParent<HexWorld3DTile>();
            if (!tile || tile.IsFrontier)
                return;

            var coord = tile.Coord;

            // If tile has a building, remove building first
            if (_buildings.TryGetValue(coord, out var b) && b)
            {
                Destroy(b.gameObject);
                _buildings.Remove(coord);
                _buildingNameByCoord.Remove(coord);
                RecomputeActiveSlotsAndNotify();
                return;
            }

            // Otherwise remove tile
            if (_owned.TryGetValue(coord, out var ownedTile) && ownedTile)
            {
                // safety: if something went out of sync
                if (_buildings.TryGetValue(coord, out var b2) && b2)
                {
                    Destroy(b2.gameObject);
                    _buildings.Remove(coord);
                    _buildingNameByCoord.Remove(coord);
                    RecomputeActiveSlotsAndNotify();
                }

                Destroy(ownedTile.gameObject);
                _owned.Remove(coord);
                _ownedStyleName.Remove(coord);

                if (refundOnRemove)
                {
                    _tilesLeftToPlace++;
                    TilesLeftChanged?.Invoke(_tilesLeftToPlace);
                }

                TilesPlacedChanged?.Invoke(_owned.Count, TileCapacityMax);
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

            int tileCost = (style != null && style.cost > 0) ? style.cost : costPerTile;

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

        private void AddOwned(HexCoord coord, HexWorldTileStyle style)
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
            _cursorGhost.SetActive(_paletteMode == PaletteMode.Tiles && SelectedStyle != null);
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

            if (_paletteMode != PaletteMode.Tiles || !SelectedStyle)
            {
                _cursorGhost.SetActive(false);
                return;
            }

            _cursorGhost.SetActive(true);

            if (!TryGetCursorCoord(out var coord))
                return;

            _cursorCoord = coord;
            _cursorValid = IsPlacementValid(coord);

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
                    save.tiles.Add(new TileSave
                    {
                        q = c.q,
                        r = c.r,
                        styleName = _ownedStyleName.TryGetValue(c, out var s) ? s : ""
                    });
                }

                foreach (var kv in _buildings)
                {
                    var c = kv.Key;
                    var inst = kv.Value;
                    if (!inst) continue;

                    save.buildings.Add(new BuildingSave
                    {
                        q = c.q,
                        r = c.r,
                        buildingName = inst.buildingName,
                        isActive = inst.IsActive,
                        level = inst.Level
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
                    AddOwned(coord, style);
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

                RecomputeActiveSlotsAndNotify(enforceLimit: true);

                // Reset UI selection state
                SetSelectedStyle(null);
                SetSelectedBuilding(null);
                SetPaletteModeTiles();

                // Notify tiles placed
                TilesPlacedChanged?.Invoke(_owned.Count, TileCapacityMax);

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

            var go = Instantiate(def.prefab, tile.transform);
            go.name = $"B_{(string.IsNullOrWhiteSpace(def.displayName) ? def.name : def.displayName)}";

            go.transform.localPosition = def.localOffset;
            go.transform.localRotation = Quaternion.Euler(def.localEuler);

            var inst = go.GetComponent<HexWorldBuildingInstance>();
            if (!inst) inst = go.AddComponent<HexWorldBuildingInstance>();

            // Back-compat:
            // - saveVersion <= 1 (or missing) means buildings did not store active/level; default to Active and Level 1.
            bool consumes = def.consumesActiveSlot;
            bool isActive = (saveVersion <= 1) ? def.defaultActive : save.isActive;
            int lvl = (saveVersion <= 1 || save.level <= 0) ? 1 : save.level;
            inst.Set(coord, def.name, consumesActiveSlot: consumes, isActive: isActive, level: lvl);

            _buildings[coord] = inst;
            _buildingNameByCoord[coord] = def.name;
        }

        private void EnsureWarehouse()
        {
            if (warehouse) return;
            warehouse = GetComponent<HexWorldWarehouseInventory>();
            if (!warehouse)
                warehouse = gameObject.AddComponent<HexWorldWarehouseInventory>();
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
                    if (b.name == buildingName) return b;
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
    }
}
