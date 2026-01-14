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
        public enum PaletteMode { Tiles, Buildings }

        [Header("Buildings")]
        [Tooltip("Catalog used to resolve saved building names back to assets.")]
        [SerializeField] private HexWorldBuildingDefinition[] buildingCatalog;

        [Tooltip("Optional parent for spawned buildings. If empty, creates/uses child named 'Buildings' under this controller.")]
        [SerializeField] private Transform buildingsParent;

        [Tooltip("Allow placing a building to replace an existing building on the same tile.")]
        [SerializeField] private bool allowReplaceBuilding = true;

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
        public event Action ExitRequested;

        public HexWorldTileStyle SelectedStyle { get; private set; }
        public HexWorldBuildingDefinition SelectedBuilding { get; private set; }
        public PaletteMode CurrentPaletteMode => _paletteMode;

        public int TilesLeftToPlace => _tilesLeftToPlace;
        public int Credits => _credits;

        private PaletteMode _paletteMode = PaletteMode.Tiles;

        private readonly Dictionary<HexCoord, HexWorld3DTile> _owned = new();
        private readonly Dictionary<HexCoord, HexWorld3DTile> _frontier = new();

        // Coord -> style name (since HexWorld3DTile doesn't store style)
        private readonly Dictionary<HexCoord, string> _ownedStyleName = new();

        // Coord -> building instance
        private readonly Dictionary<HexCoord, HexWorldBuildingInstance> _buildings = new();
        private readonly Dictionary<HexCoord, string> _buildingNameByCoord = new();

        private int _credits;

        // Tile budget state
        private int _tilesLeftToPlace;
        private GameObject _cursorGhost;
        private HexCoord _cursorCoord;
        private bool _cursorValid;

        // MaterialPropertyBlock for ghost tint (no allocations, no shared material edits)
        private MaterialPropertyBlock _ghostMpb;

        private void Awake()
        {
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
        }

        private void Start()
        {
            if (enableTileBudgetPlacement)
            {
                ClearAllTiles();

                _tilesLeftToPlace = Mathf.Max(0, startingTilesToPlace);
                TilesLeftChanged?.Invoke(_tilesLeftToPlace);

                EnsureCursorGhost();
                SetPaletteModeTiles();
                SetSelectedStyle(null);
                SetSelectedBuilding(null);

                if (autoLoadOnStart)
                {
                    if (TryLoadAutosave())
                        RequestToast("Village loaded.");
                    else
                        RequestToast($"Village start: Tiles left to place: {_tilesLeftToPlace}");
                }
                else
                {
                    RequestToast($"Village start: Tiles left to place: {_tilesLeftToPlace}");
                }

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
                else
                {
                    // Buildings mode: no tile ghost
                    if (_cursorGhost) _cursorGhost.SetActive(false);
                    _dragHasLast = false;

                    // Place building on click
                    if (Mouse.current.leftButton.wasPressedThisFrame)
                    {
                        if (!(EventSystem.current && EventSystem.current.IsPointerOverGameObject()))
                        {
                            if (SelectedBuilding && TryGetCursorCoord(out var coord))
                                TryPlaceBuildingAtCoord(coord);
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
                }
                return;
            }

            if (_tilesLeftToPlace <= 0)
                return;

            if (!IsPlacementValid(coord))
                return;

            AddOwned(coord, SelectedStyle);

            _tilesLeftToPlace--;
            TilesLeftChanged?.Invoke(_tilesLeftToPlace);
        }

        // ============================
        // Village: Buildings (NEW)
        // ============================
        private void TryPlaceBuildingAtCoord(HexCoord coord)
        {
            if (!SelectedBuilding) return;

            if (!_owned.TryGetValue(coord, out var tile) || !tile)
            {
                RequestToast("Place a tile first.");
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
            inst.Set(coord, SelectedBuilding.name);

            _buildings[coord] = inst;
            _buildingNameByCoord[coord] = SelectedBuilding.name;
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
                }

                Destroy(ownedTile.gameObject);
                _owned.Remove(coord);
                _ownedStyleName.Remove(coord);

                if (refundOnRemove)
                {
                    _tilesLeftToPlace++;
                    TilesLeftChanged?.Invoke(_tilesLeftToPlace);
                }
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

            var marker = go.GetComponent<HexWorld3DTile>();
            if (!marker) marker = go.AddComponent<HexWorld3DTile>();
            marker.Set(coord, isFrontier: false);

            ApplyStyleToTile(go, style);

            _owned.Add(coord, marker);
            _ownedStyleName[coord] = style ? style.name : "";
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
        }

        [Serializable]
        private class VillageSave
        {
            public int tilesLeft;
            public int credits;
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
                    tilesLeft = _tilesLeftToPlace,
                    credits = _credits
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

                foreach (var kv in _buildingNameByCoord)
                {
                    var c = kv.Key;
                    save.buildings.Add(new BuildingSave
                    {
                        q = c.q,
                        r = c.r,
                        buildingName = kv.Value
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

                ClearAllTiles();

                _tilesLeftToPlace = Mathf.Max(0, save.tilesLeft);
                TilesLeftChanged?.Invoke(_tilesLeftToPlace);

                _credits = save.credits;
                CreditsChanged?.Invoke(_credits);

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
                        PlaceBuildingFromLoad(coord, def);
                    }
                }

                // Reset UI selection state
                SetSelectedStyle(null);
                SetSelectedBuilding(null);
                SetPaletteModeTiles();

                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"HexWorldVillage load failed: {e.Message}");
                return false;
            }
        }

        private void PlaceBuildingFromLoad(HexCoord coord, HexWorldBuildingDefinition def)
        {
            if (!def || !def.prefab) return;
            if (!_owned.TryGetValue(coord, out var tile) || !tile) return;

            var go = Instantiate(def.prefab, tile.transform);
            go.name = $"B_{(string.IsNullOrWhiteSpace(def.displayName) ? def.name : def.displayName)}";

            go.transform.localPosition = def.localOffset;
            go.transform.localRotation = Quaternion.Euler(def.localEuler);

            var inst = go.GetComponent<HexWorldBuildingInstance>();
            if (!inst) inst = go.AddComponent<HexWorldBuildingInstance>();
            inst.Set(coord, def.name);

            _buildings[coord] = inst;
            _buildingNameByCoord[coord] = def.name;
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

        private HexWorldBuildingDefinition ResolveBuildingByName(string buildingName)
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
