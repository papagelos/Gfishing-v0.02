// Assets/Minigames/HexWorld3D/Scripts/Village/HexWorldBuildingContextMenu.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using GalacticFishing.Progress;

namespace GalacticFishing.Minigames.HexWorld
{
    /// <summary>
    /// UI Toolkit context menu that appears when clicking a building or tile.
    /// Supports toggling between Building view and Tile stats view.
    /// </summary>
    public sealed class HexWorldBuildingContextMenu : MonoBehaviour
    {
        [Header("UI Toolkit")]
        [Tooltip("UIDocument containing the context menu UXML.")]
        [SerializeField] private UIDocument uiDocument;

        [Header("References")]
        [Tooltip("Reference to the main controller (for accessing building catalog, etc.).")]
        [SerializeField] private HexWorld3DController controller;

        [Tooltip("Reference to the production ticker (for calculating synergy bonuses).")]
        [SerializeField] private HexWorldProductionTicker productionTicker;

        [Tooltip("Reference to the warehouse inventory (for storage display).")]
        [SerializeField] private HexWorldWarehouseInventory warehouse;

        [Tooltip("Reference to the Quarry Minigame UI panel.")]
        [SerializeField] private QuarryMinigameUI quarryMinigameUI;

        [Tooltip("Reference to the Forestry Minigame UI panel.")]
        [SerializeField] private ForestryMinigameUI forestryMinigameUI;

        // Cached UI elements
        private VisualElement _screenRoot;
        private VisualElement _contextMenuRoot;
        private Label _titleLabel;
        private Button _closeButton;

        // Stats
        private Label _stat1Name;
        private Label _stat1Value;
        private Label _stat2Name;
        private Label _stat2Value;
        private Label _stat3Name;
        private Label _stat3Value;
        private VisualElement _statsSection;

        // Options
        private Toggle _toggleBuildingActiveYes;
        private Toggle _toggleBuildingActiveNo;
        private Toggle _toggleShowTileStats;
        private Label _modeDescription;
        private VisualElement _optionsSection;

        // Buttons
        private Button _button1;
        private Button _button2;
        private Button _button3;
        private VisualElement _buttonsSection;
        private VisualElement _slotsSection;

        // Synergy Checklist
        private VisualElement _synergyListScroll;
        private VisualElement _synergySection;

        // Tool Slot UI (TICKET 24)
        private VisualElement _toolSlotContainer;
        private Label _toolSlotLabel;
        private Label _toolQualityPill;
        private VisualElement _toolboxPopup;
        private ScrollView _toolboxList;
        private bool _toolboxOpen = false;

        // State
        private HexWorldBuildingInstance _currentTarget;
        private HexWorld3DTile _currentTile;
        private HexWorldTileStyle _currentTileStyle;
        private HexWorldBuildingDefinition _currentBuildingDef;
        private bool _showingTileStats = false;
        private bool _wired = false;

        private void OnEnable()
        {
            // Force wiring on enable so buttons work immediately even if the menu
            // is visible by default or opened via external inspector toggle.
            EnsureWired();
        }

        private void OnDisable()
        {
            if (_wired)
            {
                UnregisterCallbacks();
                _wired = false;
            }
        }

        /// <summary>
        /// Ensures all UI elements are queried and callbacks registered.
        /// Safe to call multiple times.
        /// </summary>
        private void EnsureWired()
        {
            if (_wired) return;

            if (!uiDocument)
            {
                uiDocument = GetComponent<UIDocument>();
                if (!uiDocument)
                {
                    Debug.LogWarning($"[{nameof(HexWorldBuildingContextMenu)}] Missing UIDocument reference.");
                    return;
                }
            }

            var root = uiDocument.rootVisualElement;
            if (root == null)
            {
                Debug.LogWarning($"[{nameof(HexWorldBuildingContextMenu)}] UIDocument has no rootVisualElement yet.");
                return;
            }

            QueryAndCacheElements(root);
            RegisterCallbacks();
            _wired = true;

            // Auto-find production ticker and warehouse if not assigned
            if (!productionTicker)
                productionTicker = FindObjectOfType<HexWorldProductionTicker>(true);
            if (!warehouse)
                warehouse = FindObjectOfType<HexWorldWarehouseInventory>(true);

            // Start hidden
            SetVisible(false);
        }

        private void QueryAndCacheElements(VisualElement root)
        {
            // Root containers
            _screenRoot = root.Q<VisualElement>("ScreenRoot");
            _contextMenuRoot = root.Q<VisualElement>("ContextMenuRoot");

            if (_screenRoot == null && _contextMenuRoot == null)
            {
                Debug.LogWarning($"[{nameof(HexWorldBuildingContextMenu)}] Could not find ScreenRoot or ContextMenuRoot in UXML.");
            }

            // Header
            _titleLabel = root.Q<Label>("TitleLabel");
            _closeButton = root.Q<Button>("CloseButton");

            // Stats
            _statsSection = root.Q<VisualElement>("StatsSection");
            _stat1Name = root.Q<Label>("Stat1Name");
            _stat1Value = root.Q<Label>("Stat1Value");
            _stat2Name = root.Q<Label>("Stat2Name");
            _stat2Value = root.Q<Label>("Stat2Value");
            _stat3Name = root.Q<Label>("Stat3Name");
            _stat3Value = root.Q<Label>("Stat3Value");

            // Options
            _optionsSection = root.Q<VisualElement>("OptionsSection");
            _toggleBuildingActiveYes = root.Q<Toggle>("Toggle_BuildingActiveYes");
            _toggleBuildingActiveNo = root.Q<Toggle>("Toggle_BuildingActiveNo");
            _toggleShowTileStats = root.Q<Toggle>("Toggle_ShowTileStats");
            _modeDescription = root.Q<Label>("ModeDescription");

            // Buttons
            _buttonsSection = root.Q<VisualElement>("ButtonsSection");
            _button1 = root.Q<Button>("Button1");
            _button2 = root.Q<Button>("Button2");
            _button3 = root.Q<Button>("Button3");
            _slotsSection = root.Q<VisualElement>("SlotsSection");

            // Synergy Checklist
            _synergySection = root.Q<VisualElement>("SynergySection");
            _synergyListScroll = root.Q<VisualElement>("SynergyListScroll");

            // Log warnings for missing critical elements
            var missing = new List<string>();
            if (_titleLabel == null) missing.Add("TitleLabel");
            if (_closeButton == null) missing.Add("CloseButton");
            if (_toggleShowTileStats == null) missing.Add("Toggle_ShowTileStats");
            if (_button1 == null) missing.Add("Button1");
            if (_stat1Name == null) missing.Add("Stat1Name");
            if (_stat1Value == null) missing.Add("Stat1Value");
            if (_stat2Name == null) missing.Add("Stat2Name");
            if (_stat2Value == null) missing.Add("Stat2Value");

            if (missing.Count > 0)
            {
                Debug.LogWarning($"[{nameof(HexWorldBuildingContextMenu)}] Missing UXML elements: {string.Join(", ", missing)}");
            }
        }

        private void RegisterCallbacks()
        {
            _closeButton?.RegisterCallback<ClickEvent>(OnCloseClicked);
            _button1?.RegisterCallback<ClickEvent>(OnButton1Clicked);
            _button2?.RegisterCallback<ClickEvent>(OnButton2Clicked);
            _button3?.RegisterCallback<ClickEvent>(OnButton3Clicked);
            _toggleShowTileStats?.RegisterValueChangedCallback(OnToggleShowTileStatsChanged);
            _toggleBuildingActiveYes?.RegisterValueChangedCallback(OnToggleActiveYesChanged);
            _toggleBuildingActiveNo?.RegisterValueChangedCallback(OnToggleActiveNoChanged);
        }

        private void UnregisterCallbacks()
        {
            _closeButton?.UnregisterCallback<ClickEvent>(OnCloseClicked);
            _button1?.UnregisterCallback<ClickEvent>(OnButton1Clicked);
            _button2?.UnregisterCallback<ClickEvent>(OnButton2Clicked);
            _button3?.UnregisterCallback<ClickEvent>(OnButton3Clicked);
            _toggleShowTileStats?.UnregisterValueChangedCallback(OnToggleShowTileStatsChanged);
            _toggleBuildingActiveYes?.UnregisterValueChangedCallback(OnToggleActiveYesChanged);
            _toggleBuildingActiveNo?.UnregisterValueChangedCallback(OnToggleActiveNoChanged);
        }

        // ─────────────────────────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Shows the context menu for the given building instance.
        /// Default view is Building view with upgrades.
        /// </summary>
        public void Show(HexWorldBuildingInstance target)
        {
            Show(target, null);
        }

        /// <summary>
        /// Shows the context menu for the given building instance with an associated tile style.
        /// </summary>
        public void Show(HexWorldBuildingInstance target, HexWorldTileStyle tileStyle)
        {
            EnsureWired();
            if (!_wired) return;
            if (!target) return;

            _currentTarget = target;
            _currentTile = target.GetComponentInParent<HexWorld3DTile>();
            _currentTileStyle = tileStyle;
            _showingTileStats = false;

            // Get building definition from controller
            _currentBuildingDef = controller ? controller.ResolveBuildingByName(target.buildingName) : null;
            if (!_currentBuildingDef)
            {
                Debug.LogWarning($"[{nameof(HexWorldBuildingContextMenu)}] Could not resolve building definition for {target.buildingName}");
                Hide();
                return;
            }

            // Reset toggle state
            if (_toggleShowTileStats != null)
                _toggleShowTileStats.SetValueWithoutNotify(false);

            // Configure Active toggles
            ConfigureActiveToggles(target);

            // Show building view
            ShowBuildingView();

            // Show the menu
            SetVisible(true);
        }

        /// <summary>
        /// Shows the context menu for a tile style (data-driven stats).
        /// Called when clicking a tile with no building.
        /// </summary>
        public void ShowTile(HexWorldTileStyle style)
        {
            EnsureWired();
            if (!_wired) return;

            _currentTarget = null;
            _currentTile = null;
            _currentTileStyle = style;
            _currentBuildingDef = null;
            _showingTileStats = true;

            // Set header label
            if (_titleLabel != null)
            {
                string styleName = style != null
                    ? (!string.IsNullOrWhiteSpace(style.displayName) ? style.displayName : style.name)
                    : "Unknown";
                _titleLabel.text = $"{styleName.ToUpper()} TILE";
            }

            // Hide building-specific UI
            if (_optionsSection != null)
                _optionsSection.style.display = DisplayStyle.None;

            // Hide all buttons (no upgrades for tile-only view)
            HideAllButtons();

            // Hide synergy checklist (no synergies for tile-only view)
            ClearSynergyChecklist();

            // Populate tile stats
            PopulateTileStatsView(style);

            // Hide slots section
            if (_slotsSection != null)
                _slotsSection.style.display = DisplayStyle.None;

            // Show the menu
            SetVisible(true);
        }

        /// <summary>
        /// Hides the context menu.
        /// </summary>
        public void Hide()
        {
            _currentTarget = null;
            _currentTile = null;
            _currentTileStyle = null;
            _currentBuildingDef = null;
            _showingTileStats = false;

            SetVisible(false);
        }

        /// <summary>
        /// Returns true if the context menu is currently visible.
        /// </summary>
        public bool IsVisible
        {
            get
            {
                if (!_wired) return false;

                var container = _screenRoot ?? _contextMenuRoot;
                if (container == null) return false;

                return container.resolvedStyle.display == DisplayStyle.Flex;
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // View Switching
        // ─────────────────────────────────────────────────────────────────

        private void ShowBuildingView()
        {
            if (_currentBuildingDef == null) return;

            // Set header
            if (_titleLabel != null)
            {
                string name = !string.IsNullOrWhiteSpace(_currentBuildingDef.displayName)
                    ? _currentBuildingDef.displayName
                    : _currentBuildingDef.name;
                _titleLabel.text = name.ToUpper();
            }

            // Populate building stats
            PopulateBuildingStats();

            // Populate synergy checklist
            PopulateSynergyChecklist();

            // Show options section (active toggle, show tile stats)
            if (_optionsSection != null)
                _optionsSection.style.display = DisplayStyle.Flex;

            // Configure upgrade buttons
            ConfigureUpgradeButtons();

            // Set description
            if (_modeDescription != null)
            {
                _modeDescription.text = _currentBuildingDef.kind == HexWorldBuildingDefinition.BuildingKind.Producer
                    ? "Toggle to view tile terrain bonuses."
                    : "Toggle to view tile info.";
            }

            // Show tool slot section for Processor buildings (TICKET 24)
            if (IsProcessorBuilding())
            {
                ShowToolSlotSection();
            }
            else
            {
                HideToolSlotSection();
            }
        }

        private void ShowTileStatsView()
        {
            // Set header to tile name
            if (_titleLabel != null && _currentTileStyle != null)
            {
                string name = !string.IsNullOrWhiteSpace(_currentTileStyle.displayName)
                    ? _currentTileStyle.displayName
                    : _currentTileStyle.name;
                _titleLabel.text = $"{name.ToUpper()} TILE";
            }
            else if (_titleLabel != null)
            {
                _titleLabel.text = "TILE";
            }

            // Populate tile stats
            PopulateTileStatsView(_currentTileStyle);

            // Hide upgrade buttons in tile view
            HideAllButtons();

            // Hide synergy checklist in tile view
            ClearSynergyChecklist();

            // Hide active toggles in tile view
            if (_toggleBuildingActiveYes != null)
                _toggleBuildingActiveYes.style.display = DisplayStyle.None;
            if (_toggleBuildingActiveNo != null)
                _toggleBuildingActiveNo.style.display = DisplayStyle.None;

            // Update description
            if (_modeDescription != null)
            {
                _modeDescription.text = "Toggle to view building info.";
            }
        }

        private void PopulateBuildingStats()
        {
            if (_currentTarget == null) return;

            bool isProducer = _currentBuildingDef != null &&
                              _currentBuildingDef.kind == HexWorldBuildingDefinition.BuildingKind.Producer;
            bool isProcessor = IsProcessorBuilding();

            // Handle Processor buildings specially (TICKET 22)
            if (isProcessor)
            {
                PopulateProcessorStats();
                return;
            }

            // Row 1: Production Per Tick (for Producer buildings) or Building Level (for others)
            if (_stat1Name != null && _stat1Value != null)
            {
                if (isProducer)
                {
                    _stat1Name.text = "Production";
                    _stat1Value.text = FormatProductionPerTick();
                }
                else
                {
                    _stat1Name.text = "Building Level";
                    _stat1Value.text = _currentTarget.Level.ToString();
                }
                SetStatRowVisible(1, true);
            }

            // Row 2: Total Bonus % (for Producer buildings) or hide
            if (_stat2Name != null && _stat2Value != null)
            {
                if (isProducer)
                {
                    _stat2Name.text = "Synergy Bonus";
                    _stat2Value.text = FormatTotalBonus();
                    SetStatRowVisible(2, true);
                }
                else
                {
                    SetStatRowVisible(2, false);
                }
            }

            // Row 3: Warehouse Storage
            if (_stat3Name != null && _stat3Value != null)
            {
                _stat3Name.text = "Storage";
                _stat3Value.text = FormatWarehouseStorage();
                SetStatRowVisible(3, true);
            }
        }

        /// <summary>
        /// Populates stats specifically for Processor buildings (TICKET 22).
        /// Shows quality transformation, tool quality, and material cap.
        /// </summary>
        private void PopulateProcessorStats()
        {
            var processorCtrl = GetProcessorController();

            // Row 1: Quality Transformation (Q_in → Q_out)
            if (_stat1Name != null && _stat1Value != null)
            {
                _stat1Name.text = "Quality";
                _stat1Value.text = FormatQualityTransformation(processorCtrl);
                SetStatRowVisible(1, true);
            }

            // Row 2: Tool Quality
            if (_stat2Name != null && _stat2Value != null)
            {
                _stat2Name.text = "Tool Quality";
                if (processorCtrl != null)
                {
                    _stat2Value.text = $"Q{Mathf.RoundToInt(processorCtrl.InstalledToolQuality)}";
                }
                else
                {
                    _stat2Value.text = "-";
                }
                SetStatRowVisible(2, true);
            }

            // Row 3: Output per Tick (production rate)
            if (_stat3Name != null && _stat3Value != null)
            {
                _stat3Name.text = "Output/Tick";
                _stat3Value.text = FormatProcessorOutput(processorCtrl);
                SetStatRowVisible(3, true);
            }
        }

        /// <summary>
        /// Formats the quality transformation as "Q_in [input] → Q_out [output]".
        /// Shows the quality gain or loss explicitly.
        /// </summary>
        private string FormatQualityTransformation(HexWorldProcessorController processorCtrl)
        {
            if (processorCtrl == null || processorCtrl.Recipe == null)
                return "- → -";

            var recipe = processorCtrl.Recipe;

            // Get input material quality from PlayerProgressManager
            string inputMaterialId = recipe.input.id.ToString();
            int inputQuality = PlayerProgressManager.Instance?.GetMaterialQuality(inputMaterialId) ?? 0;

            // Calculate preview output quality
            int outputQuality = processorCtrl.GetPreviewOutputQuality();

            // Format with quality change indicator
            string changeIndicator;
            int diff = outputQuality - inputQuality;
            if (diff > 0)
                changeIndicator = $" (+{diff})";
            else if (diff < 0)
                changeIndicator = $" ({diff})";
            else
                changeIndicator = " (=)";

            return $"Q{inputQuality} {recipe.input.id} → Q{outputQuality} {recipe.outputId}{changeIndicator}";
        }

        /// <summary>
        /// Formats the processor output rate per tick.
        /// </summary>
        private string FormatProcessorOutput(HexWorldProcessorController processorCtrl)
        {
            if (processorCtrl == null || processorCtrl.Recipe == null)
                return "- / tick";

            var recipe = processorCtrl.Recipe;
            int outputAmount = Mathf.RoundToInt(recipe.baseOutputAmount);
            if (outputAmount <= 0) outputAmount = 1;

            return $"{outputAmount} {recipe.outputId} / tick";
        }

        /// <summary>
        /// Formats production per tick as "X ResourceName / tick".
        /// Returns "0 / tick" if building is dormant.
        /// </summary>
        private string FormatProductionPerTick()
        {
            if (_currentTarget == null) return "- / tick";

            // If building is dormant, production is 0
            if (!_currentTarget.IsActive)
                return "0 / tick";

            // Try to get production from ticker
            if (productionTicker != null)
            {
                var production = productionTicker.CalculateEffectiveProduction(_currentTarget);
                if (production != null && production.Count > 0)
                {
                    // Format first resource (most buildings produce one resource type)
                    var first = production[0];
                    return $"{first.amount} {first.id} / tick";
                }
            }

            // Fallback: try to read base output directly
            var profile = _currentTarget.GetComponent<HexWorldBuildingProductionProfile>();
            if (profile != null && profile.baseOutputPerTick != null && profile.baseOutputPerTick.Count > 0)
            {
                var first = profile.baseOutputPerTick[0];
                if (first.id != HexWorldResourceId.None && first.amount > 0)
                    return $"{first.amount} {first.id} / tick";
            }

            return "- / tick";
        }

        /// <summary>
        /// Formats total synergy bonus as "+X%" or "+0%" if no bonus.
        /// </summary>
        private string FormatTotalBonus()
        {
            if (_currentTarget == null || _currentBuildingDef == null)
                return "+0%";

            if (productionTicker != null)
            {
                float bonus = productionTicker.CalculateSynergyBonus(_currentTarget.Coord, _currentBuildingDef);
                int bonusPct = Mathf.RoundToInt(bonus * 100f);
                return bonusPct >= 0 ? $"+{bonusPct}%" : $"{bonusPct}%";
            }

            return "+0%";
        }

        /// <summary>
        /// Formats warehouse storage as "Current/Max".
        /// </summary>
        private string FormatWarehouseStorage()
        {
            if (warehouse != null)
            {
                return $"{warehouse.TotalStored}/{warehouse.Capacity}";
            }

            // Try to find warehouse if not assigned
            var wh = FindObjectOfType<HexWorldWarehouseInventory>();
            if (wh != null)
            {
                return $"{wh.TotalStored}/{wh.Capacity}";
            }

            return "-/-";
        }

        // ─────────────────────────────────────────────────────────────────
        // Synergy Checklist
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Populates the synergy checklist with rows showing ✅/❌, label, and bonus value.
        /// For Processor buildings, shows recipe requirements instead (TICKET 22).
        /// </summary>
        private void PopulateSynergyChecklist()
        {
            if (_synergyListScroll == null) return;

            // Clear existing content
            _synergyListScroll.Clear();

            // Handle Processor buildings specially - show recipe requirements
            if (IsProcessorBuilding())
            {
                PopulateProcessorRecipeInfo();
                return;
            }

            // Hide section if no building or not a producer
            bool isProducer = _currentBuildingDef != null &&
                              _currentBuildingDef.kind == HexWorldBuildingDefinition.BuildingKind.Producer;

            if (_currentTarget == null || _currentBuildingDef == null || !isProducer)
            {
                if (_synergySection != null)
                    _synergySection.style.display = DisplayStyle.None;
                return;
            }

            // Check if building has any synergy rules
            if (_currentBuildingDef.synergyRules == null || _currentBuildingDef.synergyRules.Count == 0)
            {
                // Show section with "No synergies" message
                if (_synergySection != null)
                    _synergySection.style.display = DisplayStyle.Flex;

                var noSynergiesLabel = new Label("No synergies available");
                noSynergiesLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
                noSynergiesLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                noSynergiesLabel.style.paddingTop = 8;
                noSynergiesLabel.style.paddingBottom = 8;
                _synergyListScroll.Add(noSynergiesLabel);
                return;
            }

            // Show the section
            if (_synergySection != null)
                _synergySection.style.display = DisplayStyle.Flex;

            // Get detailed synergy results from production ticker
            List<HexWorldProductionTicker.SynergyRuleResult> results = null;
            if (productionTicker != null)
            {
                results = productionTicker.EvaluateSynergyRulesDetailed(_currentTarget.Coord, _currentBuildingDef);
            }

            // Create a row for each synergy rule
            for (int i = 0; i < _currentBuildingDef.synergyRules.Count; i++)
            {
                var rule = _currentBuildingDef.synergyRules[i];
                if (rule == null) continue;

                // Find matching result (if available)
                bool isSatisfied = false;
                float bonusValue = rule.amountPct; // Default to rule's base bonus

                if (results != null && i < results.Count)
                {
                    isSatisfied = results[i].isSatisfied;
                    bonusValue = results[i].bonusValue;
                }

                // Create row container
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.justifyContent = Justify.SpaceBetween;
                row.style.alignItems = Align.Center;
                row.style.paddingTop = 2;
                row.style.paddingBottom = 2;
                row.style.paddingLeft = 4;
                row.style.paddingRight = 4;

                // Status icon and label
                string statusIcon = isSatisfied ? "✓" : "✗";
                string labelText = !string.IsNullOrWhiteSpace(rule.label) ? rule.label : rule.type.ToString();

                var statusLabel = new Label($"{statusIcon} {labelText}");
                statusLabel.style.color = isSatisfied ? new Color(0.4f, 0.9f, 0.4f) : new Color(0.7f, 0.7f, 0.7f);
                statusLabel.style.flexGrow = 1;

                // Bonus value label
                int bonusPct = Mathf.RoundToInt(rule.amountPct * 100f);
                string bonusText = bonusPct >= 0 ? $"+{bonusPct}%" : $"{bonusPct}%";

                // For PerCount rules, show potential stacking info
                if (rule.stacking == SynergyStacking.PerCount && rule.maxStacks > 1)
                {
                    bonusText = $"+{bonusPct}% ea";
                }

                var bonusLabel = new Label(bonusText);
                bonusLabel.style.color = isSatisfied ? new Color(0.4f, 0.9f, 0.4f) : new Color(0.5f, 0.5f, 0.5f);
                bonusLabel.style.minWidth = 50;
                bonusLabel.style.unityTextAlign = TextAnchor.MiddleRight;

                row.Add(statusLabel);
                row.Add(bonusLabel);

                _synergyListScroll.Add(row);
            }
        }

        /// <summary>
        /// Clears the synergy checklist.
        /// </summary>
        private void ClearSynergyChecklist()
        {
            if (_synergyListScroll != null)
                _synergyListScroll.Clear();

            if (_synergySection != null)
                _synergySection.style.display = DisplayStyle.None;
        }

        /// <summary>
        /// Populates the synergy section with processor recipe info (TICKET 22).
        /// Shows input requirements, quality factors, and conversion preview.
        /// </summary>
        private void PopulateProcessorRecipeInfo()
        {
            if (_synergySection != null)
                _synergySection.style.display = DisplayStyle.Flex;

            var processorCtrl = GetProcessorController();
            if (processorCtrl == null || processorCtrl.Recipe == null)
            {
                var noRecipeLabel = new Label("No recipe configured");
                noRecipeLabel.style.color = new Color(0.7f, 0.5f, 0.5f);
                noRecipeLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                noRecipeLabel.style.paddingTop = 8;
                noRecipeLabel.style.paddingBottom = 8;
                _synergyListScroll.Add(noRecipeLabel);
                return;
            }

            var recipe = processorCtrl.Recipe;

            // Row 1: Recipe name / conversion
            var recipeRow = CreateInfoRow("Recipe", $"{recipe.input.id} → {recipe.outputId}");
            _synergyListScroll.Add(recipeRow);

            // Row 2: Input requirement
            int inputAvailable = warehouse?.Get(recipe.input.id) ?? 0;
            int inputRequired = recipe.input.amount;
            bool hasEnough = inputAvailable >= inputRequired;

            var inputRow = CreateInfoRow(
                $"✓ Input ({recipe.input.id})",
                $"{inputAvailable}/{inputRequired}",
                hasEnough ? new Color(0.4f, 0.9f, 0.4f) : new Color(0.9f, 0.5f, 0.5f)
            );
            _synergyListScroll.Add(inputRow);

            // Row 3: Gain Factor (quality improvement potential)
            int gainPct = Mathf.RoundToInt(recipe.gainFactor * 100f);
            var gainRow = CreateInfoRow("Quality Gain", $"+{gainPct}% max");
            _synergyListScroll.Add(gainRow);

            // Row 4: Loss Factor (quality degradation risk)
            int lossPct = Mathf.RoundToInt(recipe.lossFactor * 100f);
            var lossRow = CreateInfoRow("Quality Loss", $"-{lossPct}% max");
            _synergyListScroll.Add(lossRow);

            // Row 5: Status summary
            string statusText = processorCtrl.GetStatusSummary();
            var statusRow = CreateInfoRow("Status", statusText, new Color(0.7f, 0.7f, 0.7f));
            _synergyListScroll.Add(statusRow);
        }

        /// <summary>
        /// Creates a simple info row with label and value for the synergy list.
        /// </summary>
        private VisualElement CreateInfoRow(string label, string value, Color? valueColor = null)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.alignItems = Align.Center;
            row.style.paddingTop = 2;
            row.style.paddingBottom = 2;
            row.style.paddingLeft = 4;
            row.style.paddingRight = 4;

            var labelEl = new Label(label);
            labelEl.style.color = new Color(0.7f, 0.7f, 0.7f);
            labelEl.style.flexGrow = 1;

            var valueEl = new Label(value);
            valueEl.style.color = valueColor ?? new Color(0.9f, 0.9f, 0.9f);
            valueEl.style.minWidth = 80;
            valueEl.style.unityTextAlign = TextAnchor.MiddleRight;

            row.Add(labelEl);
            row.Add(valueEl);

            return row;
        }

        // ─────────────────────────────────────────────────────────────────
        // Tool Slot UI (TICKET 24)
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Shows the tool slot section for Processor buildings.
        /// Creates a clickable tool slot that opens a toolbox popup.
        /// </summary>
        private void ShowToolSlotSection()
        {
            var processorCtrl = GetProcessorController();
            if (processorCtrl == null) return;

            // Ensure slots section exists and is visible
            if (_slotsSection == null)
            {
                // Create the slots section dynamically if it doesn't exist in UXML
                _slotsSection = new VisualElement();
                _slotsSection.name = "SlotsSection";
                _slotsSection.style.marginTop = 8;
                _slotsSection.style.marginBottom = 8;

                // Add to synergy section's parent (or context menu root)
                var parent = _synergySection?.parent ?? _contextMenuRoot;
                parent?.Add(_slotsSection);
            }

            _slotsSection.style.display = DisplayStyle.Flex;
            _slotsSection.Clear();

            // Create tool slot card
            var card = new VisualElement();
            card.style.backgroundColor = new Color(0.15f, 0.15f, 0.18f);
            card.style.borderTopLeftRadius = 6;
            card.style.borderTopRightRadius = 6;
            card.style.borderBottomLeftRadius = 6;
            card.style.borderBottomRightRadius = 6;
            card.style.paddingTop = 8;
            card.style.paddingBottom = 8;
            card.style.paddingLeft = 12;
            card.style.paddingRight = 12;
            card.style.marginBottom = 8;

            // Header row with title and quality pill
            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.justifyContent = Justify.SpaceBetween;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.marginBottom = 8;

            var titleLabel = new Label("TOOL SLOT");
            titleLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            titleLabel.style.fontSize = 10;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

            // Quality pill showing current tool strength
            _toolQualityPill = new Label($"Q{Mathf.RoundToInt(processorCtrl.InstalledToolQuality)}");
            _toolQualityPill.style.backgroundColor = new Color(0.2f, 0.5f, 0.3f);
            _toolQualityPill.style.color = Color.white;
            _toolQualityPill.style.paddingLeft = 8;
            _toolQualityPill.style.paddingRight = 8;
            _toolQualityPill.style.paddingTop = 2;
            _toolQualityPill.style.paddingBottom = 2;
            _toolQualityPill.style.borderTopLeftRadius = 10;
            _toolQualityPill.style.borderTopRightRadius = 10;
            _toolQualityPill.style.borderBottomLeftRadius = 10;
            _toolQualityPill.style.borderBottomRightRadius = 10;
            _toolQualityPill.style.fontSize = 11;

            headerRow.Add(titleLabel);
            headerRow.Add(_toolQualityPill);
            card.Add(headerRow);

            // Tool slot button (clickable to open toolbox)
            _toolSlotContainer = new VisualElement();
            _toolSlotContainer.style.flexDirection = FlexDirection.Row;
            _toolSlotContainer.style.justifyContent = Justify.SpaceBetween;
            _toolSlotContainer.style.alignItems = Align.Center;
            _toolSlotContainer.style.backgroundColor = new Color(0.1f, 0.1f, 0.12f);
            _toolSlotContainer.style.borderTopLeftRadius = 4;
            _toolSlotContainer.style.borderTopRightRadius = 4;
            _toolSlotContainer.style.borderBottomLeftRadius = 4;
            _toolSlotContainer.style.borderBottomRightRadius = 4;
            _toolSlotContainer.style.paddingTop = 8;
            _toolSlotContainer.style.paddingBottom = 8;
            _toolSlotContainer.style.paddingLeft = 10;
            _toolSlotContainer.style.paddingRight = 10;

            // Tool type label
            string toolTypeName = FormatToolTypeName(processorCtrl.ToolSlotType);
            _toolSlotLabel = new Label($"{toolTypeName} (Q{Mathf.RoundToInt(processorCtrl.InstalledToolQuality)})");
            _toolSlotLabel.style.color = new Color(0.9f, 0.9f, 0.9f);
            _toolSlotLabel.style.flexGrow = 1;

            // Change button indicator
            var changeLabel = new Label("CHANGE ▼");
            changeLabel.style.color = new Color(0.5f, 0.7f, 0.9f);
            changeLabel.style.fontSize = 10;

            _toolSlotContainer.Add(_toolSlotLabel);
            _toolSlotContainer.Add(changeLabel);
            _toolSlotContainer.RegisterCallback<ClickEvent>(OnToolSlotClicked);

            card.Add(_toolSlotContainer);

            // Toolbox popup (initially hidden)
            _toolboxPopup = new VisualElement();
            _toolboxPopup.style.display = DisplayStyle.None;
            _toolboxPopup.style.backgroundColor = new Color(0.12f, 0.12f, 0.15f);
            _toolboxPopup.style.borderTopWidth = 1;
            _toolboxPopup.style.borderTopColor = new Color(0.3f, 0.3f, 0.35f);
            _toolboxPopup.style.marginTop = 4;
            _toolboxPopup.style.borderTopLeftRadius = 4;
            _toolboxPopup.style.borderTopRightRadius = 4;
            _toolboxPopup.style.borderBottomLeftRadius = 4;
            _toolboxPopup.style.borderBottomRightRadius = 4;
            _toolboxPopup.style.maxHeight = 150;

            _toolboxList = new ScrollView(ScrollViewMode.Vertical);
            _toolboxList.style.flexGrow = 1;
            _toolboxPopup.Add(_toolboxList);

            card.Add(_toolboxPopup);
            _slotsSection.Add(card);

            _toolboxOpen = false;
        }

        /// <summary>
        /// Hides the tool slot section.
        /// </summary>
        private void HideToolSlotSection()
        {
            if (_slotsSection != null)
                _slotsSection.style.display = DisplayStyle.None;

            _toolboxOpen = false;
        }

        /// <summary>
        /// Handles click on the tool slot to toggle the toolbox popup.
        /// </summary>
        private void OnToolSlotClicked(ClickEvent evt)
        {
            _toolboxOpen = !_toolboxOpen;

            if (_toolboxOpen)
            {
                PopulateToolboxList();
                if (_toolboxPopup != null)
                    _toolboxPopup.style.display = DisplayStyle.Flex;
            }
            else
            {
                if (_toolboxPopup != null)
                    _toolboxPopup.style.display = DisplayStyle.None;
            }
        }

        /// <summary>
        /// Populates the toolbox list with available tools from the warehouse.
        /// </summary>
        private void PopulateToolboxList()
        {
            if (_toolboxList == null) return;
            _toolboxList.Clear();

            var processorCtrl = GetProcessorController();
            if (processorCtrl == null) return;

            var toolType = processorCtrl.ToolSlotType;

            // Get available tools of this type from warehouse
            int toolCount = warehouse?.Get(toolType) ?? 0;

            if (toolCount <= 0)
            {
                var noToolsLabel = new Label($"No {FormatToolTypeName(toolType)} in warehouse");
                noToolsLabel.style.color = new Color(0.6f, 0.5f, 0.5f);
                noToolsLabel.style.paddingTop = 12;
                noToolsLabel.style.paddingBottom = 12;
                noToolsLabel.style.paddingLeft = 10;
                noToolsLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                _toolboxList.Add(noToolsLabel);
                return;
            }

            // Get tool quality from PlayerProgressManager
            string toolMaterialId = toolType.ToString();
            int toolQuality = PlayerProgressManager.Instance?.GetMaterialQuality(toolMaterialId) ?? 0;

            // Create selectable tool entry
            var toolRow = new VisualElement();
            toolRow.style.flexDirection = FlexDirection.Row;
            toolRow.style.justifyContent = Justify.SpaceBetween;
            toolRow.style.alignItems = Align.Center;
            toolRow.style.paddingTop = 8;
            toolRow.style.paddingBottom = 8;
            toolRow.style.paddingLeft = 10;
            toolRow.style.paddingRight = 10;
            toolRow.style.backgroundColor = new Color(0.15f, 0.15f, 0.18f);
            toolRow.style.marginTop = 2;
            toolRow.style.marginBottom = 2;
            toolRow.style.marginLeft = 4;
            toolRow.style.marginRight = 4;
            toolRow.style.borderTopLeftRadius = 4;
            toolRow.style.borderTopRightRadius = 4;
            toolRow.style.borderBottomLeftRadius = 4;
            toolRow.style.borderBottomRightRadius = 4;

            var toolLabel = new Label($"{FormatToolTypeName(toolType)} (x{toolCount})");
            toolLabel.style.color = new Color(0.9f, 0.9f, 0.9f);
            toolLabel.style.flexGrow = 1;

            var qualityLabel = new Label($"Q{toolQuality}");
            qualityLabel.style.color = toolQuality > processorCtrl.InstalledToolQuality
                ? new Color(0.4f, 0.9f, 0.4f)  // Green if better
                : new Color(0.9f, 0.9f, 0.9f); // White otherwise
            qualityLabel.style.minWidth = 40;
            qualityLabel.style.unityTextAlign = TextAnchor.MiddleRight;

            var selectBtn = new Button(() => SelectTool(toolType, toolQuality));
            selectBtn.text = "EQUIP";
            selectBtn.style.marginLeft = 8;
            selectBtn.style.paddingLeft = 12;
            selectBtn.style.paddingRight = 12;
            selectBtn.style.backgroundColor = new Color(0.2f, 0.4f, 0.6f);
            selectBtn.style.color = Color.white;
            selectBtn.style.borderTopLeftRadius = 4;
            selectBtn.style.borderTopRightRadius = 4;
            selectBtn.style.borderBottomLeftRadius = 4;
            selectBtn.style.borderBottomRightRadius = 4;

            // Disable if same or lower quality
            if (toolQuality <= processorCtrl.InstalledToolQuality)
            {
                selectBtn.SetEnabled(false);
                selectBtn.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f);
            }

            toolRow.Add(toolLabel);
            toolRow.Add(qualityLabel);
            toolRow.Add(selectBtn);

            // Hover effect
            toolRow.RegisterCallback<MouseEnterEvent>(e =>
                toolRow.style.backgroundColor = new Color(0.2f, 0.2f, 0.25f));
            toolRow.RegisterCallback<MouseLeaveEvent>(e =>
                toolRow.style.backgroundColor = new Color(0.15f, 0.15f, 0.18f));

            _toolboxList.Add(toolRow);
        }

        /// <summary>
        /// Handles selecting a tool from the toolbox.
        /// Updates the processor's installed tool quality and removes the tool from warehouse.
        /// </summary>
        private void SelectTool(HexWorldResourceId toolType, int toolQuality)
        {
            var processorCtrl = GetProcessorController();
            if (processorCtrl == null) return;

            // Remove tool from warehouse
            if (warehouse != null && warehouse.TryRemove(toolType, 1))
            {
                // Update processor's installed tool quality
                processorCtrl.InstalledToolQuality = toolQuality;

                // Close toolbox
                _toolboxOpen = false;
                if (_toolboxPopup != null)
                    _toolboxPopup.style.display = DisplayStyle.None;

                // Refresh the tool slot display
                RefreshToolSlotDisplay();

                // Refresh processor stats to show updated quality transformation
                PopulateProcessorStats();

                Debug.Log($"[ContextMenu] Equipped {toolType} Q{toolQuality} to {_currentTarget?.buildingName}");
            }
            else
            {
                Debug.LogWarning($"[ContextMenu] Failed to remove {toolType} from warehouse");
            }
        }

        /// <summary>
        /// Refreshes the tool slot display after equipping a new tool.
        /// </summary>
        private void RefreshToolSlotDisplay()
        {
            var processorCtrl = GetProcessorController();
            if (processorCtrl == null) return;

            int quality = Mathf.RoundToInt(processorCtrl.InstalledToolQuality);
            string toolTypeName = FormatToolTypeName(processorCtrl.ToolSlotType);

            if (_toolSlotLabel != null)
                _toolSlotLabel.text = $"{toolTypeName} (Q{quality})";

            if (_toolQualityPill != null)
                _toolQualityPill.text = $"Q{quality}";
        }

        /// <summary>
        /// Formats a tool resource ID into a display name.
        /// </summary>
        private static string FormatToolTypeName(HexWorldResourceId toolType)
        {
            string name = toolType.ToString();
            if (name.StartsWith("Tool_"))
                name = name.Substring(5);
            return name;
        }

        private void PopulateTileStatsView(HexWorldTileStyle style)
        {
            if (style != null)
            {
                // Row 1: Terrain Type
                if (_stat1Name != null && _stat1Value != null)
                {
                    _stat1Name.text = "Terrain Type";
                    _stat1Value.text = style.terrainType.ToString();
                    SetStatRowVisible(1, true);
                }

                // Row 2: Check if tile has properties defined
                if (style.properties != null && style.properties.Count > 0)
                {
                    var prop = style.properties[0];
                    if (_stat2Name != null && _stat2Value != null)
                    {
                        _stat2Name.text = prop.label ?? "Property";
                        _stat2Value.text = prop.value ?? "-";
                        SetStatRowVisible(2, true);
                    }

                    // Row 3: Second property if available
                    if (style.properties.Count > 1)
                    {
                        var prop2 = style.properties[1];
                        if (_stat3Name != null && _stat3Value != null)
                        {
                            _stat3Name.text = prop2.label ?? "Property";
                            _stat3Value.text = prop2.value ?? "-";
                            SetStatRowVisible(3, true);
                        }
                    }
                    else
                    {
                        SetStatRowVisible(3, false);
                    }
                }
                else
                {
                    // Fallback: show tile name
                    if (_stat2Name != null && _stat2Value != null)
                    {
                        _stat2Name.text = "Tile";
                        _stat2Value.text = !string.IsNullOrWhiteSpace(style.displayName) ? style.displayName : style.name;
                        SetStatRowVisible(2, true);
                    }
                    SetStatRowVisible(3, false);
                }
            }
            else
            {
                // No style - minimal fallback
                if (_stat1Name != null && _stat1Value != null)
                {
                    _stat1Name.text = "Tile";
                    _stat1Value.text = "Unknown";
                    SetStatRowVisible(1, true);
                }
                SetStatRowVisible(2, false);
                SetStatRowVisible(3, false);
            }
        }

        private void SetStatRowVisible(int rowNum, bool visible)
        {
            Label nameLabel = null;
            Label valueLabel = null;

            switch (rowNum)
            {
                case 1:
                    nameLabel = _stat1Name;
                    valueLabel = _stat1Value;
                    break;
                case 2:
                    nameLabel = _stat2Name;
                    valueLabel = _stat2Value;
                    break;
                case 3:
                    nameLabel = _stat3Name;
                    valueLabel = _stat3Value;
                    break;
            }

            if (nameLabel != null)
            {
                var parent = nameLabel.parent;
                if (parent != null)
                    parent.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // Upgrade Buttons
        // ─────────────────────────────────────────────────────────────────

        private void ConfigureUpgradeButtons()
        {
            HideAllButtons();

            if (_currentBuildingDef == null) return;

            // Check if building is a Quarry - show OPEN DRILL button
            if (IsQuarryBuilding())
            {
                ConfigureQuarryButton();
                // Still show upgrades if available (starting from button2)
                ConfigureUpgradeButtonsStartingAt(1);
                return;
            }

            // Check if building is a Forestry Station - show MANAGE PLOTS button
            if (IsForestryBuilding())
            {
                ConfigureForestryButton();
                // Still show upgrades if available (starting from button2)
                ConfigureUpgradeButtonsStartingAt(1);
                return;
            }

            // Check if building is a Processor - show VIEW PROCESSOR / CHANGE RECIPE button (TICKET 22)
            if (IsProcessorBuilding())
            {
                ConfigureProcessorButtons();
                return;
            }

            if (_currentBuildingDef.availableUpgrades == null || _currentBuildingDef.availableUpgrades.Count == 0)
                return;

            // Button1 = first upgrade (MVP)
            if (_currentBuildingDef.availableUpgrades.Count >= 1)
            {
                var upgrade = _currentBuildingDef.availableUpgrades[0];
                if (upgrade != null && _button1 != null)
                {
                    string baseName = !string.IsNullOrWhiteSpace(upgrade.upgradeName) ? upgrade.upgradeName : upgrade.name;
                    string costSuffix = FormatUpgradeCost(upgrade);
                    _button1.text = baseName + costSuffix;
                    _button1.style.display = DisplayStyle.Flex;
                }
            }

            // Button2 = second upgrade (if available)
            if (_currentBuildingDef.availableUpgrades.Count >= 2)
            {
                var upgrade = _currentBuildingDef.availableUpgrades[1];
                if (upgrade != null && _button2 != null)
                {
                    string baseName = !string.IsNullOrWhiteSpace(upgrade.upgradeName) ? upgrade.upgradeName : upgrade.name;
                    string costSuffix = FormatUpgradeCost(upgrade);
                    _button2.text = baseName + costSuffix;
                    _button2.style.display = DisplayStyle.Flex;
                }
            }

            // Button3 = third upgrade (if available)
            if (_currentBuildingDef.availableUpgrades.Count >= 3)
            {
                var upgrade = _currentBuildingDef.availableUpgrades[2];
                if (upgrade != null && _button3 != null)
                {
                    string baseName = !string.IsNullOrWhiteSpace(upgrade.upgradeName) ? upgrade.upgradeName : upgrade.name;
                    string costSuffix = FormatUpgradeCost(upgrade);
                    _button3.text = baseName + costSuffix;
                    _button3.style.display = DisplayStyle.Flex;
                }
            }
        }

        /// <summary>
        /// Checks if the current building is a Quarry (case-insensitive).
        /// </summary>
        private bool IsQuarryBuilding()
        {
            if (_currentTarget == null) return false;

            string buildingName = _currentTarget.buildingName;
            if (string.IsNullOrEmpty(buildingName)) return false;

            return buildingName.IndexOf("quarry", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Checks if the current building is a Forestry Station (case-insensitive).
        /// Matches "lumberyard" or "forestry" in the building name.
        /// </summary>
        private bool IsForestryBuilding()
        {
            if (_currentTarget == null) return false;

            string buildingName = _currentTarget.buildingName;
            if (string.IsNullOrEmpty(buildingName)) return false;

            return buildingName.IndexOf("lumberyard", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   buildingName.IndexOf("forestry", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Checks if the current building is a Processor (Sawmill, Smelter, etc.).
        /// </summary>
        private bool IsProcessorBuilding()
        {
            if (_currentBuildingDef == null) return false;
            return _currentBuildingDef.kind == HexWorldBuildingDefinition.BuildingKind.Processor;
        }

        /// <summary>
        /// Gets the HexWorldProcessorController from the current building (if any).
        /// </summary>
        private HexWorldProcessorController GetProcessorController()
        {
            if (_currentTarget == null) return null;
            return _currentTarget.GetComponent<HexWorldProcessorController>();
        }

        /// <summary>
        /// Checks if the current building has a special primary button (Quarry, Forestry, or Processor).
        /// </summary>
        private bool HasMinigameButton()
        {
            return IsQuarryBuilding() || IsForestryBuilding() || IsProcessorBuilding();
        }

        /// <summary>
        /// Configures Button1 as "OPEN DRILL" for Quarry buildings.
        /// </summary>
        private void ConfigureQuarryButton()
        {
            if (_button1 == null) return;

            _button1.text = "OPEN DRILL";
            _button1.style.display = DisplayStyle.Flex;
        }

        /// <summary>
        /// Configures Button1 as "MANAGE PLOTS" for Forestry buildings.
        /// </summary>
        private void ConfigureForestryButton()
        {
            if (_button1 == null) return;

            _button1.text = "MANAGE PLOTS";
            _button1.style.display = DisplayStyle.Flex;
        }

        /// <summary>
        /// Configures buttons for Processor buildings (TICKET 22).
        /// Shows PROCESS NOW as primary action, and CHANGE RECIPE if multiple recipes supported.
        /// </summary>
        private void ConfigureProcessorButtons()
        {
            var processorCtrl = GetProcessorController();

            // Button1: PROCESS NOW (manual trigger)
            if (_button1 != null)
            {
                if (processorCtrl != null && processorCtrl.CanConvert())
                {
                    _button1.text = "PROCESS NOW";
                    _button1.SetEnabled(true);
                }
                else
                {
                    _button1.text = "PROCESS NOW";
                    _button1.SetEnabled(false);
                }
                _button1.style.display = DisplayStyle.Flex;
            }

            // Button2: CHANGE RECIPE (if processor supports multiple recipes - placeholder for future)
            // For now, we'll just show upgrades starting at button2
            ConfigureUpgradeButtonsStartingAt(1);
        }

        /// <summary>
        /// Configures upgrade buttons starting from the specified button index.
        /// Used when Button1 is reserved for a special action (like OPEN DRILL).
        /// </summary>
        private void ConfigureUpgradeButtonsStartingAt(int startButtonIndex)
        {
            if (_currentBuildingDef?.availableUpgrades == null) return;

            Button[] buttons = { _button1, _button2, _button3 };
            int upgradeIndex = 0;

            for (int btnIdx = startButtonIndex; btnIdx < buttons.Length && upgradeIndex < _currentBuildingDef.availableUpgrades.Count; btnIdx++)
            {
                var btn = buttons[btnIdx];
                var upgrade = _currentBuildingDef.availableUpgrades[upgradeIndex];

                if (btn != null && upgrade != null)
                {
                    string baseName = !string.IsNullOrWhiteSpace(upgrade.upgradeName) ? upgrade.upgradeName : upgrade.name;
                    string costSuffix = FormatUpgradeCost(upgrade);
                    btn.text = baseName + costSuffix;
                    btn.style.display = DisplayStyle.Flex;
                }

                upgradeIndex++;
            }
        }

        private void HideAllButtons()
        {
            if (_button1 != null) _button1.style.display = DisplayStyle.None;
            if (_button2 != null) _button2.style.display = DisplayStyle.None;
            if (_button3 != null) _button3.style.display = DisplayStyle.None;
        }

        private static string FormatUpgradeCost(HexWorldUpgradeDefinition up)
        {
            if (!up) return string.Empty;

            var parts = new List<string>(8);

            if (up.creditCost > 0)
                parts.Add($"{up.creditCost} Cr");

            if (up.resourceCosts != null)
            {
                for (int i = 0; i < up.resourceCosts.Count; i++)
                {
                    var s = up.resourceCosts[i];
                    if (s.id == HexWorldResourceId.None) continue;
                    if (s.amount <= 0) continue;
                    parts.Add($"{s.amount} {s.id}");
                }
            }

            if (parts.Count == 0) return string.Empty;
            return $" ({string.Join(", ", parts)})";
        }

        // ─────────────────────────────────────────────────────────────────
        // Active Toggle
        // ─────────────────────────────────────────────────────────────────

        private void ConfigureActiveToggles(HexWorldBuildingInstance target)
        {
            if (_toggleBuildingActiveYes == null || _toggleBuildingActiveNo == null) return;

            // Show the toggles
            _toggleBuildingActiveYes.style.display = DisplayStyle.Flex;
            _toggleBuildingActiveNo.style.display = DisplayStyle.Flex;

            // Set current state without triggering callbacks
            _toggleBuildingActiveYes.SetValueWithoutNotify(target.IsActive);
            _toggleBuildingActiveNo.SetValueWithoutNotify(!target.IsActive);
        }

        private void OnToggleActiveYesChanged(ChangeEvent<bool> evt)
        {
            if (!evt.newValue) return; // Only handle when toggled ON
            if (_currentTarget == null || controller == null) return;

            // Only try to activate if building is currently inactive
            // This prevents toggling OFF an already-active building
            if (!_currentTarget.IsActive)
            {
                controller.TryToggleBuildingActiveAtCoord(_currentTarget.Coord);
            }

            // Sync both toggles to reflect actual state (handles failed activation)
            SyncActiveToggles();

            // Refresh stats and synergy checklist
            PopulateBuildingStats();
            PopulateSynergyChecklist();
        }

        private void OnToggleActiveNoChanged(ChangeEvent<bool> evt)
        {
            if (!evt.newValue) return; // Only handle when toggled ON
            if (_currentTarget == null || controller == null) return;

            // Only try to deactivate if building is currently active
            if (_currentTarget.IsActive)
            {
                controller.TryToggleBuildingActiveAtCoord(_currentTarget.Coord);
            }

            // Sync both toggles to reflect actual state
            SyncActiveToggles();

            // Refresh stats and synergy checklist
            PopulateBuildingStats();
            PopulateSynergyChecklist();
        }

        /// <summary>
        /// Syncs the active toggles to match the current building's IsActive state.
        /// Call this after any activation attempt to ensure UI reflects actual state.
        /// </summary>
        private void SyncActiveToggles()
        {
            if (_currentTarget == null) return;

            _toggleBuildingActiveYes?.SetValueWithoutNotify(_currentTarget.IsActive);
            _toggleBuildingActiveNo?.SetValueWithoutNotify(!_currentTarget.IsActive);
        }

        // ─────────────────────────────────────────────────────────────────
        // Callbacks
        // ─────────────────────────────────────────────────────────────────

        private void OnCloseClicked(ClickEvent evt)
        {
            Hide();
        }

        private void OnToggleShowTileStatsChanged(ChangeEvent<bool> evt)
        {
            _showingTileStats = evt.newValue;

            if (_showingTileStats)
            {
                ShowTileStatsView();
            }
            else
            {
                ShowBuildingView();
            }
        }

        private void OnButton1Clicked(ClickEvent evt)
        {
            // Check if this is a Quarry building - open drill UI instead of purchasing upgrade
            if (IsQuarryBuilding())
            {
                OpenQuarryMinigame();
                return;
            }

            // Check if this is a Forestry building - open plot manager UI
            if (IsForestryBuilding())
            {
                OpenForestryMinigame();
                return;
            }

            // Check if this is a Processor building - trigger manual conversion (TICKET 22)
            if (IsProcessorBuilding())
            {
                TriggerProcessorConversion();
                return;
            }

            TryPurchaseUpgrade(0);
        }

        /// <summary>
        /// Triggers a manual conversion cycle on the Processor building (TICKET 22).
        /// </summary>
        private void TriggerProcessorConversion()
        {
            var processorCtrl = GetProcessorController();
            if (processorCtrl == null)
            {
                Debug.LogWarning($"[{nameof(HexWorldBuildingContextMenu)}] Processor building has no HexWorldProcessorController.");
                return;
            }

            processorCtrl.TryProcessConversion();

            // Refresh the stats to show updated quality values
            PopulateProcessorStats();

            // Update button enabled state
            if (_button1 != null)
            {
                _button1.SetEnabled(processorCtrl.CanConvert());
            }
        }

        /// <summary>
        /// Opens the Quarry Strata Drill minigame UI for the current building.
        /// </summary>
        private void OpenQuarryMinigame()
        {
            if (_currentTarget == null) return;

            // Find the QuarryMinigameController on the building
            var quarryController = _currentTarget.GetComponent<QuarryMinigameController>();
            if (quarryController == null)
            {
                Debug.LogWarning($"[{nameof(HexWorldBuildingContextMenu)}] Quarry building has no QuarryMinigameController: {_currentTarget.buildingName}");
                return;
            }

            // Auto-find QuarryMinigameUI if not assigned
            if (quarryMinigameUI == null)
            {
                quarryMinigameUI = FindObjectOfType<QuarryMinigameUI>(true);
                if (quarryMinigameUI == null)
                {
                    Debug.LogWarning($"[{nameof(HexWorldBuildingContextMenu)}] Could not find QuarryMinigameUI in scene.");
                    return;
                }
            }

            // Show the quarry drill UI
            quarryMinigameUI.Show(quarryController);

            // Optionally hide the context menu
            Hide();
        }

        /// <summary>
        /// Opens the Forestry Plot Manager minigame UI for the current building.
        /// </summary>
        private void OpenForestryMinigame()
        {
            if (_currentTarget == null) return;

            // Find the ForestryMinigameController on the building
            var forestryController = _currentTarget.GetComponent<ForestryMinigameController>();
            if (forestryController == null)
            {
                Debug.LogWarning($"[{nameof(HexWorldBuildingContextMenu)}] Forestry building has no ForestryMinigameController: {_currentTarget.buildingName}");
                return;
            }

            // Auto-find ForestryMinigameUI if not assigned
            if (forestryMinigameUI == null)
            {
                forestryMinigameUI = FindObjectOfType<ForestryMinigameUI>(true);
                if (forestryMinigameUI == null)
                {
                    Debug.LogWarning($"[{nameof(HexWorldBuildingContextMenu)}] Could not find ForestryMinigameUI in scene.");
                    return;
                }
            }

            // Show the forestry plot manager UI
            forestryMinigameUI.Show(forestryController);

            // Optionally hide the context menu
            Hide();
        }

        private void OnButton2Clicked(ClickEvent evt)
        {
            // For minigame buildings (Quarry, Forestry), Button2 is the first upgrade (index 0)
            // For other buildings, Button2 is the second upgrade (index 1)
            int upgradeIndex = HasMinigameButton() ? 0 : 1;
            TryPurchaseUpgrade(upgradeIndex);
        }

        private void OnButton3Clicked(ClickEvent evt)
        {
            // For minigame buildings (Quarry, Forestry), Button3 is the second upgrade (index 1)
            // For other buildings, Button3 is the third upgrade (index 2)
            int upgradeIndex = HasMinigameButton() ? 1 : 2;
            TryPurchaseUpgrade(upgradeIndex);
        }

        private void TryPurchaseUpgrade(int index)
        {
            if (_currentTarget == null || _currentBuildingDef == null) return;
            if (_currentBuildingDef.availableUpgrades == null) return;
            if (index < 0 || index >= _currentBuildingDef.availableUpgrades.Count) return;

            var upgrade = _currentBuildingDef.availableUpgrades[index];
            if (upgrade == null) return;

            if (controller != null)
            {
                bool ok = controller.TryPurchaseBuildingUpgrade(_currentTarget.Coord, upgrade);
                if (ok)
                {
                    Hide();
                }
                // else: keep menu open (toast already shown by controller)
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // Visibility
        // ─────────────────────────────────────────────────────────────────

        private void SetVisible(bool visible)
        {
            var container = _screenRoot ?? _contextMenuRoot;
            if (container != null)
            {
                container.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }
    }
}
