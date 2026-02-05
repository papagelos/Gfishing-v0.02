// Assets/Minigames/HexWorld3D/Scripts/UI/HexWorldVillageHudToolkitController.cs
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace GalacticFishing.Minigames.HexWorld
{
    /// <summary>
    /// UI Toolkit bridge for the Village HUD strip.
    /// Wires UXML elements (tabs, pager, slot buttons) to HexWorld3DController.
    /// No UGUI dependencies.
    /// </summary>
    public sealed class HexWorldVillageHudToolkitController : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private HexWorld3DController controller;

        private const int SlotsPerPage = 7;
        private const string ActiveTabClass = "is-active";
        private const string SelectedSlotClass = "is-selected";
        private const string GameplaySlotClass = "is-gameplay";

        private enum HudMode { Tiles, Buildings }
        private enum TileFilter { Cosmetic, Upgraded }

        private HudMode _mode = HudMode.Tiles;
        private TileFilter _tileFilter = TileFilter.Cosmetic;
        private int _pageIndex;

        // Cached UI elements
        private Button _tabTiles;
        private Button _tabBuildings;
        private Button _filterCosmetic;
        private Button _filterUpgraded;
        private VisualElement _subFilterCol;
        private Button _prevButton;
        private Button _nextButton;
        private Label _pageLabel;
        private Label _costWarningLabel;
        private readonly Button[] _slotButtons = new Button[SlotsPerPage];

        private bool _wired;
        private bool _hasStarted;
        private Coroutine _initialRefreshCo;

        private void OnEnable()
        {
            if (!uiDocument || !controller)
            {
                Debug.LogWarning($"[{nameof(HexWorldVillageHudToolkitController)}] Missing UIDocument or Controller reference.", this);
                enabled = false;
                return;
            }

            var root = uiDocument.rootVisualElement;
            if (root == null)
            {
                Debug.LogWarning($"[{nameof(HexWorldVillageHudToolkitController)}] UIDocument has no rootVisualElement yet.", this);
                return;
            }

            QueryAndCacheElements(root);
            RegisterCallbacks();
            SubscribeToController();

            _wired = true;

            // IMPORTANT:
            // Do NOT sync/populate here. OnEnable often runs before HexWorld3DController.Start()
            // finishes loading catalogs/save data, which causes partial population.
            // Initial population is done in Start() (and re-enable refresh is handled below).
            if (_hasStarted)
            {
                RequestInitialRefresh();
            }
        }

        private void Start()
        {
            _hasStarted = true;
            RequestInitialRefresh();
        }

        private void OnDisable()
        {
            if (_initialRefreshCo != null)
            {
                StopCoroutine(_initialRefreshCo);
                _initialRefreshCo = null;
            }

            if (_wired)
            {
                UnregisterCallbacks();
                UnsubscribeFromController();
                _wired = false;
            }
        }

        private void RequestInitialRefresh()
        {
            if (!_wired || !controller) return;

            if (_initialRefreshCo != null)
            {
                StopCoroutine(_initialRefreshCo);
                _initialRefreshCo = null;
            }

            _initialRefreshCo = StartCoroutine(InitialRefreshRoutine());
        }

        /// <summary>
        /// Refresh over a few frames to ride out any controller catalog population timing.
        /// We stop early once the catalog counts stabilize.
        /// </summary>
        private IEnumerator InitialRefreshRoutine()
        {
            // First attempt right away (now that we're in Start or a re-enable later in play)
            SafeSyncFromController();

            int lastTiles = -1;
            int lastBuildings = -1;

            // Retry a handful of frames; stop early if counts stop changing.
            for (int frame = 0; frame < 10; frame++)
            {
                yield return null;

                if (!this || !_wired || controller == null)
                    yield break;

                int tiles = CountNonNull(controller.GetStyleCatalog());
                int buildings = CountNonNull(controller.GetBuildingCatalog());

                SafeSyncFromController();

                if (tiles == lastTiles && buildings == lastBuildings)
                    break;

                lastTiles = tiles;
                lastBuildings = buildings;
            }

            _initialRefreshCo = null;
        }

        private static int CountNonNull<T>(T[] arr) where T : class
        {
            if (arr == null) return 0;
            int count = 0;
            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i] != null) count++;
            }
            return count;
        }

        private void SafeSyncFromController()
        {
            if (!_wired || controller == null) return;
            SyncModeFromController(controller.CurrentPaletteMode);
        }

        private void QueryAndCacheElements(VisualElement root)
        {
            _tabTiles = root.Q<Button>("Tab_Tiles");
            _tabBuildings = root.Q<Button>("Tab_Buildings");
            _filterCosmetic = root.Q<Button>("Filter_Cosmetic");
            _filterUpgraded = root.Q<Button>("Filter_Upgraded");
            _subFilterCol = root.Q<VisualElement>("SubFilterCol");
            _prevButton = root.Q<Button>("PrevPage");
            _nextButton = root.Q<Button>("NextPage");
            _pageLabel = root.Q<Label>("PageLabel");
            _costWarningLabel = root.Q<Label>("CostWarningLabel");

            for (int i = 0; i < SlotsPerPage; i++)
            {
                _slotButtons[i] = root.Q<Button>($"SlotBtn{i}");
            }
        }

        private void RegisterCallbacks()
        {
            _tabTiles?.RegisterCallback<ClickEvent>(OnTabTilesClicked);
            _tabBuildings?.RegisterCallback<ClickEvent>(OnTabBuildingsClicked);
            _filterCosmetic?.RegisterCallback<ClickEvent>(OnFilterCosmeticClicked);
            _filterUpgraded?.RegisterCallback<ClickEvent>(OnFilterUpgradedClicked);
            _prevButton?.RegisterCallback<ClickEvent>(OnPrevPageClicked);
            _nextButton?.RegisterCallback<ClickEvent>(OnNextPageClicked);

            for (int i = 0; i < SlotsPerPage; i++)
            {
                int index = i; // Capture for closure
                _slotButtons[i]?.RegisterCallback<ClickEvent>(evt => OnSlotClicked(index));
            }
        }

        private void UnregisterCallbacks()
        {
            _tabTiles?.UnregisterCallback<ClickEvent>(OnTabTilesClicked);
            _tabBuildings?.UnregisterCallback<ClickEvent>(OnTabBuildingsClicked);
            _filterCosmetic?.UnregisterCallback<ClickEvent>(OnFilterCosmeticClicked);
            _filterUpgraded?.UnregisterCallback<ClickEvent>(OnFilterUpgradedClicked);
            _prevButton?.UnregisterCallback<ClickEvent>(OnPrevPageClicked);
            _nextButton?.UnregisterCallback<ClickEvent>(OnNextPageClicked);

            // Note: Slot button callbacks use lambdas, so they can't be unregistered individually.
            // They will be cleaned up when the VisualElement is destroyed.
        }

        private void SubscribeToController()
        {
            if (!controller) return;

            controller.PaletteModeChanged += OnPaletteModeChanged;
            controller.SelectedStyleChanged += OnSelectedStyleChanged;
            controller.SelectedBuildingChanged += OnSelectedBuildingChanged;
        }

        private void UnsubscribeFromController()
        {
            if (!controller) return;

            controller.PaletteModeChanged -= OnPaletteModeChanged;
            controller.SelectedStyleChanged -= OnSelectedStyleChanged;
            controller.SelectedBuildingChanged -= OnSelectedBuildingChanged;
        }

        // ─────────────────────────────────────────────────────────────────
        // Tab Callbacks
        // ─────────────────────────────────────────────────────────────────

        private void OnTabTilesClicked(ClickEvent evt)
        {
            if (_mode == HudMode.Tiles) return;

            _mode = HudMode.Tiles;
            _pageIndex = 0;

            controller.SetPaletteModeTiles();
            UpdateTabActiveClass();
            UpdateSubFilterVisibility();
            UpdateSubFilterActiveClass();
            RebuildCards();
        }

        private void OnTabBuildingsClicked(ClickEvent evt)
        {
            if (_mode == HudMode.Buildings) return;

            _mode = HudMode.Buildings;
            _pageIndex = 0;

            controller.SetPaletteModeBuildings();
            UpdateTabActiveClass();
            UpdateSubFilterVisibility();
            UpdateSubFilterActiveClass();
            HideCostWarning();
            RebuildCards();
        }

        private void OnFilterCosmeticClicked(ClickEvent evt)
        {
            if (_tileFilter == TileFilter.Cosmetic) return;

            _tileFilter = TileFilter.Cosmetic;
            _pageIndex = 0;

            UpdateSubFilterActiveClass();
            HideCostWarning();
            RebuildCards();
        }

        private void OnFilterUpgradedClicked(ClickEvent evt)
        {
            if (_tileFilter == TileFilter.Upgraded) return;

            _tileFilter = TileFilter.Upgraded;
            _pageIndex = 0;

            UpdateSubFilterActiveClass();
            RebuildCards();
        }

        // ─────────────────────────────────────────────────────────────────
        // Pager Callbacks
        // ─────────────────────────────────────────────────────────────────

        private void OnPrevPageClicked(ClickEvent evt) => PrevPage();
        private void OnNextPageClicked(ClickEvent evt) => NextPage();

        private void PrevPage()
        {
            if (_pageIndex > 0)
            {
                _pageIndex--;
                RebuildCards();
            }
        }

        private void NextPage()
        {
            _pageIndex++;
            RebuildCards();
        }

        // ─────────────────────────────────────────────────────────────────
        // Slot Callback
        // ─────────────────────────────────────────────────────────────────

        private void OnSlotClicked(int index)
        {
            if (index < 0 || index >= SlotsPerPage) return;
            if (!controller) return;

            var btn = _slotButtons[index];
            if (btn == null) return;

            if (_mode == HudMode.Tiles)
            {
                if (btn.userData is HexWorldTileStyle style)
                {
                    controller.SetSelectedStyle(style);
                    RefreshSelectionHighlight();

                    // Show warning for Gameplay tiles that may incur demolition fees
                    if (style.category == TileCategory.Gameplay)
                    {
                        ShowGameplayTileWarning(style);
                    }
                    else
                    {
                        HideCostWarning();
                    }
                }
            }
            else // Buildings
            {
                if (btn.userData is HexWorldBuildingDefinition def)
                {
                    controller.SetSelectedBuilding(def);
                    RefreshSelectionHighlight();
                    HideCostWarning();
                }
            }
        }

        private void ShowGameplayTileWarning(HexWorldTileStyle style)
        {
            if (style == null || style.category != TileCategory.Gameplay)
            {
                HideCostWarning();
                return;
            }

            var sb = new StringBuilder();
            sb.Append("Placing this tile will cost: ");

            // Show paint costs
            if (style.paintCost != null && style.paintCost.Count > 0)
            {
                bool first = true;
                foreach (var cost in style.paintCost)
                {
                    if (cost.amount <= 0) continue;
                    if (!first) sb.Append(", ");
                    sb.Append($"{cost.amount} {cost.id}");
                    first = false;
                }
            }
            else
            {
                sb.Append("(no cost)");
            }

            // Add demolition warning
            sb.Append($" | Repainting over existing Gameplay tiles incurs {style.demolitionFeeFactor * 100:F0}% demolition fee.");

            ShowCostWarning(sb.ToString());
        }

        // ─────────────────────────────────────────────────────────────────
        // Controller Event Handlers
        // ─────────────────────────────────────────────────────────────────

        private void OnPaletteModeChanged(HexWorld3DController.PaletteMode mode) => SyncModeFromController(mode);

        private void OnSelectedStyleChanged(HexWorldTileStyle _) => RefreshSelectionHighlight();
        private void OnSelectedBuildingChanged(HexWorldBuildingDefinition _) => RefreshSelectionHighlight();

        private void SyncModeFromController(HexWorld3DController.PaletteMode mode)
        {
            HudMode newMode = mode switch
            {
                HexWorld3DController.PaletteMode.Tiles => HudMode.Tiles,
                HexWorld3DController.PaletteMode.Buildings => HudMode.Buildings,
                _ => HudMode.Tiles
            };

            if (_mode != newMode)
            {
                _mode = newMode;
                _pageIndex = 0;
            }

            UpdateTabActiveClass();
            UpdateSubFilterVisibility();
            UpdateSubFilterActiveClass();
            RebuildCards();
        }

        // ─────────────────────────────────────────────────────────────────
        // USS Class Helpers
        // ─────────────────────────────────────────────────────────────────

        private void UpdateTabActiveClass()
        {
            if (_tabTiles != null)
                _tabTiles.EnableInClassList(ActiveTabClass, _mode == HudMode.Tiles);

            if (_tabBuildings != null)
                _tabBuildings.EnableInClassList(ActiveTabClass, _mode == HudMode.Buildings);
        }

        private void UpdateSubFilterVisibility()
        {
            if (_subFilterCol != null)
                _subFilterCol.style.display = (_mode == HudMode.Tiles) ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void UpdateSubFilterActiveClass()
        {
            if (_filterCosmetic != null)
                _filterCosmetic.EnableInClassList(ActiveTabClass, _tileFilter == TileFilter.Cosmetic);

            if (_filterUpgraded != null)
                _filterUpgraded.EnableInClassList(ActiveTabClass, _tileFilter == TileFilter.Upgraded);
        }

        private void ShowCostWarning(string message)
        {
            if (_costWarningLabel == null) return;
            _costWarningLabel.text = message;
            _costWarningLabel.style.display = DisplayStyle.Flex;
        }

        private void HideCostWarning()
        {
            if (_costWarningLabel == null) return;
            _costWarningLabel.style.display = DisplayStyle.None;
        }

        // ─────────────────────────────────────────────────────────────────
        // Card Rebuild & Selection Highlight
        // ─────────────────────────────────────────────────────────────────

        private void RebuildCards()
        {
            if (_mode == HudMode.Tiles)
                RebuildTilesCards();
            else
                RebuildBuildingsCards();

            RefreshSelectionHighlight();
        }

        private void RebuildTilesCards()
        {
            var catalog = controller ? controller.GetStyleCatalog() : null;

            // Filter tiles by current sub-filter (Cosmetic or Upgraded/Gameplay)
            var targetCategory = _tileFilter == TileFilter.Cosmetic
                ? TileCategory.Cosmetic
                : TileCategory.Gameplay;

            var tileList = new List<HexWorldTileStyle>();
            if (catalog != null)
            {
                for (int i = 0; i < catalog.Length; i++)
                {
                    var tile = catalog[i];
                    if (tile != null && tile.category == targetCategory)
                        tileList.Add(tile);
                }
            }

            int totalPages = Mathf.Max(1, Mathf.CeilToInt(tileList.Count / (float)SlotsPerPage));
            _pageIndex = Mathf.Clamp(_pageIndex, 0, totalPages - 1);

            if (_pageLabel != null)
                _pageLabel.text = $"{_pageIndex + 1}/{totalPages}";

            for (int i = 0; i < SlotsPerPage; i++)
            {
                var btn = _slotButtons[i];
                if (btn == null) continue;

                int dataIndex = _pageIndex * SlotsPerPage + i;

                if (dataIndex >= tileList.Count)
                {
                    btn.style.display = DisplayStyle.None;
                    btn.userData = null;
                    btn.text = string.Empty;
                    btn.tooltip = string.Empty;
                    btn.style.backgroundImage = StyleKeyword.None;
                    btn.EnableInClassList(GameplaySlotClass, false);
                    continue;
                }

                btn.style.display = DisplayStyle.Flex;

                var style = tileList[dataIndex];
                btn.userData = style;

                btn.text = string.IsNullOrWhiteSpace(style.displayName) ? style.name : style.displayName;

                var sp = style.thumbnail;
                btn.style.backgroundImage = (sp != null) ? Background.FromSprite(sp) : StyleKeyword.None;

                // Build tooltip with cost and tags for Gameplay tiles
                btn.tooltip = BuildTileTooltip(style);

                // Visual distinction for gameplay tiles
                btn.EnableInClassList(GameplaySlotClass, style.category == TileCategory.Gameplay);
            }
        }

        private string BuildTileTooltip(HexWorldTileStyle style)
        {
            if (style == null) return string.Empty;

            var sb = new StringBuilder();
            sb.Append(string.IsNullOrWhiteSpace(style.displayName) ? style.name : style.displayName);

            if (style.category == TileCategory.Cosmetic)
            {
                sb.Append("\n[Free Cosmetic]");
            }
            else if (style.category == TileCategory.Gameplay)
            {
                sb.Append("\n[Gameplay Tile]");

                // Show paint costs
                if (style.paintCost != null && style.paintCost.Count > 0)
                {
                    sb.Append("\nCost: ");
                    bool first = true;
                    foreach (var cost in style.paintCost)
                    {
                        if (cost.amount <= 0) continue;
                        if (!first) sb.Append(", ");
                        sb.Append($"{cost.amount} {cost.id}");
                        first = false;
                    }
                }

                // Show gameplay tags
                if (style.gameplayTags != null && style.gameplayTags.Count > 0)
                {
                    sb.Append("\nTags: ");
                    sb.Append(string.Join(", ", style.gameplayTags));
                }

                // Show demolition fee factor
                sb.Append($"\nDemolition: {style.demolitionFeeFactor * 100:F0}% of cost");
            }

            return sb.ToString();
        }

        private void RebuildBuildingsCards()
        {
            var catalog = controller ? controller.GetBuildingCatalog() : null;

            var buildingList = new List<HexWorldBuildingDefinition>();
            if (catalog != null)
            {
                for (int i = 0; i < catalog.Length; i++)
                {
                    if (catalog[i] != null)
                        buildingList.Add(catalog[i]);
                }
            }

            int totalPages = Mathf.Max(1, Mathf.CeilToInt(buildingList.Count / (float)SlotsPerPage));
            _pageIndex = Mathf.Clamp(_pageIndex, 0, totalPages - 1);

            if (_pageLabel != null)
                _pageLabel.text = $"{_pageIndex + 1}/{totalPages}";

            for (int i = 0; i < SlotsPerPage; i++)
            {
                var btn = _slotButtons[i];
                if (btn == null) continue;

                int dataIndex = _pageIndex * SlotsPerPage + i;

                if (dataIndex >= buildingList.Count)
                {
                    btn.style.display = DisplayStyle.None;
                    btn.userData = null;
                    btn.text = string.Empty;
                    btn.style.backgroundImage = StyleKeyword.None;
                    continue;
                }

                btn.style.display = DisplayStyle.Flex;

                var def = buildingList[dataIndex];
                btn.userData = def;

                btn.text = string.IsNullOrWhiteSpace(def.displayName) ? def.name : def.displayName;

                var sp = def.icon;
                btn.style.backgroundImage = (sp != null) ? Background.FromSprite(sp) : StyleKeyword.None;
            }
        }

        private void RefreshSelectionHighlight()
        {
            if (!controller) return;

            if (_mode == HudMode.Tiles)
            {
                var selected = controller.SelectedStyle;
                for (int i = 0; i < SlotsPerPage; i++)
                {
                    var btn = _slotButtons[i];
                    if (btn == null) continue;
                    btn.EnableInClassList(SelectedSlotClass, selected != null && btn.userData == selected);
                }
            }
            else
            {
                var selected = controller.SelectedBuilding;
                for (int i = 0; i < SlotsPerPage; i++)
                {
                    var btn = _slotButtons[i];
                    if (btn == null) continue;
                    btn.EnableInClassList(SelectedSlotClass, selected != null && btn.userData == selected);
                }
            }
        }
    }
}
