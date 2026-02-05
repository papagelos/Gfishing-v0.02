// Assets/Minigames/HexWorld3D/Scripts/Village/QualityLabController.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using GalacticFishing.Progress;

namespace GalacticFishing.Minigames.HexWorld
{
    /// <summary>
    /// Controller for the Quality Lab UI panel.
    /// Bridges UI Toolkit elements with game data (credits, materials).
    /// </summary>
    public sealed class QualityLabController : MonoBehaviour
    {
        #region MaterialData

        /// <summary>
        /// Data structure representing a material in the Quality Lab.
        /// </summary>
        [Serializable]
        public struct MaterialData
        {
            public string MaterialId;
            public string DisplayName;
            public int GlobalQualityQ;
            public int MaxQualityQ;
            public string Description;

            public MaterialData(string id, string displayName, int globalQ, int maxQ, string description)
            {
                MaterialId = id;
                DisplayName = displayName;
                GlobalQualityQ = globalQ;
                MaxQualityQ = maxQ;
                Description = description;
            }
        }

        #endregion

        #region Constants

        private const string ResourceButtonPrefix = "ResBtn_";
        private const string SelectedClass = "is-selected";
        private const string SuccessClass = "is-success";
        private const string FailClass = "is-fail";
        private const string LockedClass = "is-locked";
        private const string PositiveClass = "is-positive";
        private const string NegativeClass = "is-negative";

        #endregion

        #region Bonus Data

        #region Events

public event Action OnOpenTownHallRequested;
public event Action OnOpenWarehouseRequested;
public event Action OnOpenDungeonRequested;
public event Action<int> OnBoosterSlotClicked; // slot index 0-2

#endregion


        /// <summary>
        /// Represents an active bonus affecting improvement chances.
        /// </summary>
        [Serializable]
        public struct ActiveBonus
        {
            public string Source;
            public string Description;
            public float BonusPercent;
            public string AffectedMaterialId; // Empty = all materials

            public ActiveBonus(string source, string description, float bonus, string affectedMaterial = "")
            {
                Source = source;
                Description = description;
                BonusPercent = bonus;
                AffectedMaterialId = affectedMaterial;
            }
        }

        #endregion

        #region Inspector References

        [Header("UI Toolkit")]
        [SerializeField] private UIDocument uiDocument;

        [Header("Game Data Bindings")]
        [SerializeField] private HexWorldWarehouseInventory warehouseInventory;

        [Header("Material Configuration")]
        [Tooltip("List of materials available in the Quality Lab. If empty, uses defaults.")]
        [SerializeField] private List<MaterialData> materials = new List<MaterialData>();

        [Header("Improvement Costs")]
        [Tooltip("Refinement tokens required per improvement attempt.")]
        [SerializeField] private int attemptCostTokens = 1;

        [Tooltip("Credits required per improvement attempt.")]
        [SerializeField] private int attemptCostCredits = 250;

        [Tooltip("Common materials required per improvement attempt.")]
        [SerializeField] private int attemptCostCommonMats = 10;

        [Header("Success Chance Formula")]
        [Tooltip("Base success chance percentage (e.g., 50 = 50%).")]
        [SerializeField] private float baseSuccessChance = 50f;

        [Tooltip("Penalty per quality level above 1 (e.g., 2 = -2% per level).")]
        [SerializeField] private float penaltyPerLevel = 2f;

        [Tooltip("Minimum success chance percentage.")]
        [SerializeField] private float minSuccessChance = 5f;

        [Tooltip("Quality level at which 'late game' penalty kicks in harder.")]
        [SerializeField] private int lateGameThreshold = 10;

        [Tooltip("Additional penalty per level above the late game threshold.")]
        [SerializeField] private float lateGameExtraPenalty = 1f;

        [Header("Boosters")]
        [Tooltip("Number of booster slots unlocked (0-3).")]
        [SerializeField, Range(0, 3)] private int boosterSlotsUnlocked = 1;

        [Header("Bonuses")]
        [Tooltip("Active bonuses from buildings, reputation, etc.")]
        [SerializeField] private List<ActiveBonus> activeBonuses = new List<ActiveBonus>();

        [Tooltip("Bonus from buildings (summed).")]
        [SerializeField] private float buildingsBonusPercent = 3f;

        [Tooltip("Bonus from reputation.")]
        [SerializeField] private float reputationBonusPercent = 0f;

        [Header("Daily Limits")]
        [Tooltip("Global daily refinement attempts cap (shared across all materials).")]
        [SerializeField] private int globalAttemptsCap = 6;

        #endregion

        #region Cached UI Elements

        private VisualElement _qualityLabRoot;
        private VisualElement _root; 
        private VisualElement _leftContainer;
        private VisualElement _centerContainer;
        private VisualElement _rightContainer;

        // Left column elements
        private TextField _resourceSearchField;
        private ScrollView _resourceList;
        private Label _dailyCapSummaryLabel;
        private Label _tokenSummaryLabel;

        // Center column elements
        private Label _selectedResourceLabel;
        private Label _selectedResourceQLabel;
        private Label _selectedResourceDesc;
        private Label _currentQLabel;
        private Label _targetQLabel;
        private Label _chanceLabel;
        private Label _costTokenLabel;
        private Label _costCreditsLabel;
        private Label _costMatsLabel;
        private Button _btnImproveOnce;
        private Button _btnImproveMax;
        private Label _resultLine;
        private ScrollView _recentResultsList;

        // Booster slots (center column)
        private Button _boosterSlot1;
        private Button _boosterSlot2;
        private Button _boosterSlot3;

        // Right column elements
        private Label _breakBaseChance;
        private Label _breakLatePenalty;
        private Label _breakBuildingsBonus;
        private Label _breakRepBonus;
        private ScrollView _activeBonusesList;

        // Quick links (right column)
        private Button _btnOpenTownHall;
        private Button _btnOpenWarehouse;
        private Button _btnOpenDungeon;

        // Footer
        private Label _footerStatusLabel;

        // Close button
        private Button _closeButton;

        private bool _wired;

        // Last action result for footer
        private string _lastActionResult = "";

        #endregion

        #region Player Resources State

        private int _refinementTokens = 20; // TODO: Wire to actual player data
        private int _globalAttemptsUsed = 0;

        #endregion

        #region Selection State

        private string _selectedMaterialId;
        private readonly List<Button> _resourceButtons = new List<Button>();

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            // Wire UI elements in Start to ensure UIDocument is fully initialized
            EnsureWired();
        }

        private void OnEnable()
        {
            // Subscribe to data changes
            if (warehouseInventory != null)
                warehouseInventory.InventoryChanged += OnInventoryChanged;
        }

        private void OnDisable()
        {
            // Unsubscribe from data changes
            if (warehouseInventory != null)
                warehouseInventory.InventoryChanged -= OnInventoryChanged;
        }

        #endregion

        #region UI Wiring

        private void EnsureWired()
        {
            if (_wired) return;
            if (uiDocument == null) return;

            _root = uiDocument.rootVisualElement;
            if (_root == null) return;

            // Find the main Quality Lab root container
            _qualityLabRoot = _root.Q<VisualElement>("QualityLabRoot");
            if (_qualityLabRoot == null)
            {
                Debug.LogWarning("[QualityLabController] QualityLabRoot not found in UXML.");
                return;
            }

            // Cache main layout containers (using actual UXML names)
            _leftContainer = _qualityLabRoot.Q<VisualElement>("LeftColumn");
            _centerContainer = _qualityLabRoot.Q<VisualElement>("CenterColumn");
            _rightContainer = _qualityLabRoot.Q<VisualElement>("RightColumn");

            // Cache Left column elements
            _resourceSearchField = _qualityLabRoot.Q<TextField>("ResourceSearchField");
            _resourceList = _qualityLabRoot.Q<ScrollView>("ResourceList");
            _dailyCapSummaryLabel = _qualityLabRoot.Q<Label>("DailyCapSummaryLabel");
            _tokenSummaryLabel = _qualityLabRoot.Q<Label>("TokenSummaryLabel");

            // Cache Center column elements
            _selectedResourceLabel = _qualityLabRoot.Q<Label>("SelectedResourceLabel");
            _selectedResourceQLabel = _qualityLabRoot.Q<Label>("SelectedResourceQLabel");
            _selectedResourceDesc = _qualityLabRoot.Q<Label>("SelectedResourceDesc");
            _currentQLabel = _qualityLabRoot.Q<Label>("CurrentQLabel");
            _targetQLabel = _qualityLabRoot.Q<Label>("TargetQLabel");
            _chanceLabel = _qualityLabRoot.Q<Label>("ChanceLabel");
            _costTokenLabel = _qualityLabRoot.Q<Label>("CostTokenLabel");
            _costCreditsLabel = _qualityLabRoot.Q<Label>("CostCreditsLabel");
            _costMatsLabel = _qualityLabRoot.Q<Label>("CostMatsLabel");
            _btnImproveOnce = _qualityLabRoot.Q<Button>("Btn_ImproveOnce");
            _btnImproveMax = _qualityLabRoot.Q<Button>("Btn_ImproveMax");
            _resultLine = _qualityLabRoot.Q<Label>("ResultLine");
            _recentResultsList = _qualityLabRoot.Q<ScrollView>("RecentResultsList");

            // Wire search field callback
            if (_resourceSearchField != null)
            {
                _resourceSearchField.RegisterValueChangedCallback(OnSearchFieldChanged);
            }

            // Wire improvement button callbacks
            if (_btnImproveOnce != null)
            {
                _btnImproveOnce.RegisterCallback<ClickEvent>(OnImproveOnceClicked);
            }
            if (_btnImproveMax != null)
            {
                _btnImproveMax.RegisterCallback<ClickEvent>(OnImproveMaxClicked);
            }

            // Cache booster slots
            _boosterSlot1 = _qualityLabRoot.Q<Button>("BoosterSlot1");
            _boosterSlot2 = _qualityLabRoot.Q<Button>("BoosterSlot2");
            _boosterSlot3 = _qualityLabRoot.Q<Button>("BoosterSlot3");

            // Wire booster slot callbacks
            if (_boosterSlot1 != null)
                _boosterSlot1.RegisterCallback<ClickEvent>(evt => OnBoosterSlotClicked?.Invoke(0));
            if (_boosterSlot2 != null)
                _boosterSlot2.RegisterCallback<ClickEvent>(evt => OnBoosterSlotClicked?.Invoke(1));
            if (_boosterSlot3 != null)
                _boosterSlot3.RegisterCallback<ClickEvent>(evt => OnBoosterSlotClicked?.Invoke(2));

            // Cache right column elements
            _breakBaseChance = _qualityLabRoot.Q<Label>("Break_BaseChance");
            _breakLatePenalty = _qualityLabRoot.Q<Label>("Break_LatePenalty");
            _breakBuildingsBonus = _qualityLabRoot.Q<Label>("Break_BuildingsBonus");
            _breakRepBonus = _qualityLabRoot.Q<Label>("Break_RepBonus");
            _activeBonusesList = _qualityLabRoot.Q<ScrollView>("ActiveBonusesList");

            // Cache quick link buttons
            _btnOpenTownHall = _qualityLabRoot.Q<Button>("Btn_OpenTownHall");
            _btnOpenWarehouse = _qualityLabRoot.Q<Button>("Btn_OpenWarehouse");
            _btnOpenDungeon = _qualityLabRoot.Q<Button>("Btn_OpenDungeon");

            // Wire quick link callbacks
            if (_btnOpenTownHall != null)
                _btnOpenTownHall.RegisterCallback<ClickEvent>(OnOpenTownHallClicked);
            if (_btnOpenWarehouse != null)
                _btnOpenWarehouse.RegisterCallback<ClickEvent>(OnOpenWarehouseClicked);
            if (_btnOpenDungeon != null)
                _btnOpenDungeon.RegisterCallback<ClickEvent>(OnOpenDungeonClicked);

            // Cache footer elements
            _footerStatusLabel = _qualityLabRoot.Q<Label>("FooterStatusLabel");

            // Cache and wire close button
            _closeButton = _qualityLabRoot.Q<Button>("CloseButton");
            if (_closeButton != null)
                _closeButton.RegisterCallback<ClickEvent>(OnCloseButtonClicked);

            // Set initial visibility to hidden
            _qualityLabRoot.style.display = DisplayStyle.None;

            // Initialize default materials if none configured
            if (materials == null || materials.Count == 0)
            {
                InitializeDefaultMaterials();
            }

            _wired = true;
        }

        private void InitializeDefaultMaterials()
        {
            materials = new List<MaterialData>
            {
                new MaterialData("Wood", "Wood", 1, 20, "Basic construction material from forests."),
                new MaterialData("Stone", "Stone", 1, 20, "Durable building material from quarries."),
                new MaterialData("Fiber", "Fiber", 1, 20, "Flexible material for crafting and textiles."),
                new MaterialData("BaitIngredients", "Bait Ingredients", 1, 20, "Components for crafting fishing bait."),
            };
        }

        #endregion

        #region Public API

        /// <summary>
        /// Shows the Quality Lab panel.
        /// </summary>
        public void Show()
        {
            EnsureWired();
            if (_qualityLabRoot == null) return;

            _qualityLabRoot.style.display = DisplayStyle.Flex;
            RefreshUI();
        }

        /// <summary>
        /// Hides the Quality Lab panel.
        /// </summary>
        public void Hide()
        {
            if (_qualityLabRoot == null) return;
            _qualityLabRoot.style.display = DisplayStyle.None;
        }

        /// <summary>
        /// Returns whether the panel is currently visible.
        /// </summary>
        public bool IsVisible
        {
            get
            {
                if (_qualityLabRoot == null) return false;
                return _qualityLabRoot.resolvedStyle.display == DisplayStyle.Flex;
            }
        }

        #endregion

        #region Data Binding Accessors

        /// <summary>
        /// Gets the player's current credits from PlayerProgressManager.
        /// </summary>
        public float GetCredits()
        {
            var ppm = PlayerProgressManager.Instance;
            return ppm != null ? ppm.GetCredits() : 0f;
        }

        /// <summary>
        /// Gets the amount of a specific resource from the warehouse inventory.
        /// </summary>
        public int GetMaterialAmount(HexWorldResourceId resourceId)
        {
            if (warehouseInventory == null) return 0;
            return warehouseInventory.Get(resourceId);
        }

        /// <summary>
        /// Reference to the warehouse inventory for external access.
        /// </summary>
        public HexWorldWarehouseInventory WarehouseInventory => warehouseInventory;

        #endregion

        #region Event Handlers

        private void OnInventoryChanged()
        {
            if (IsVisible)
                RefreshUI();
        }

        private void OnSearchFieldChanged(ChangeEvent<string> evt)
        {
            FilterResourceList(evt.newValue);
        }

        private void OnResourceButtonClicked(ClickEvent evt, string materialId)
        {
            SelectMaterial(materialId);
        }

        private void OnImproveOnceClicked(ClickEvent evt)
        {
            TryImproveOnce();
        }

        private void OnImproveMaxClicked(ClickEvent evt)
        {
            TryImproveMax();
        }

        private void OnOpenTownHallClicked(ClickEvent evt)
        {
            OnOpenTownHallRequested?.Invoke();
        }

        private void OnOpenWarehouseClicked(ClickEvent evt)
        {
            OnOpenWarehouseRequested?.Invoke();
        }

        private void OnOpenDungeonClicked(ClickEvent evt)
        {
            OnOpenDungeonRequested?.Invoke();
        }

        private void OnCloseButtonClicked(ClickEvent evt)
        {
            Hide();
        }

        #endregion

        #region UI Refresh

        /// <summary>
        /// Refreshes all UI elements with current data.
        /// </summary>
        private void RefreshUI()
        {
            PopulateResourceList();
            UpdateDailyCapSummary();
            UpdateCenterPanel();
            UpdateTokenSummary();
            UpdateRightColumn();
            UpdateBoosterSlots();
            UpdateFooter();

            // Apply current search filter
            if (_resourceSearchField != null && !string.IsNullOrEmpty(_resourceSearchField.value))
            {
                FilterResourceList(_resourceSearchField.value);
            }
        }

        #endregion

        #region Resource List Population

        /// <summary>
        /// Populates the ResourceList ScrollView with buttons for each material.
        /// </summary>
        private void PopulateResourceList()
        {
            if (_resourceList == null) return;

            // Clear existing buttons and tracking list
            _resourceList.Clear();
            _resourceButtons.Clear();

            foreach (var mat in materials)
            {
                var btn = new Button();
                btn.name = ResourceButtonPrefix + mat.MaterialId;
                btn.text = mat.DisplayName;
                btn.AddToClassList("ql-list-item");

                // Store material ID in userData for lookup
                btn.userData = mat.MaterialId;

                // Register click handler
                string capturedId = mat.MaterialId;
                btn.RegisterCallback<ClickEvent>(evt => OnResourceButtonClicked(evt, capturedId));

                // Apply selected state if this is the selected material
                if (_selectedMaterialId == mat.MaterialId)
                {
                    btn.AddToClassList(SelectedClass);
                }

                _resourceList.Add(btn);
                _resourceButtons.Add(btn);
            }

            // Auto-select first material if none selected
            if (string.IsNullOrEmpty(_selectedMaterialId) && materials.Count > 0)
            {
                SelectMaterial(materials[0].MaterialId);
            }
        }

        #endregion

        #region Search Filtering

        /// <summary>
        /// Filters the resource list based on the search query.
        /// Hides buttons whose DisplayName does not match (case-insensitive).
        /// </summary>
        private void FilterResourceList(string query)
        {
            if (_resourceButtons == null) return;

            bool hasQuery = !string.IsNullOrWhiteSpace(query);
            string lowerQuery = hasQuery ? query.ToLowerInvariant() : string.Empty;

            foreach (var btn in _resourceButtons)
            {
                if (btn == null) continue;

                if (!hasQuery)
                {
                    // No query - show all
                    btn.style.display = DisplayStyle.Flex;
                    continue;
                }

                // Check if display name matches (stored in button text)
                string displayName = btn.text;
                bool matches = !string.IsNullOrEmpty(displayName) &&
                               displayName.ToLowerInvariant().Contains(lowerQuery);

                btn.style.display = matches ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        #endregion

        #region Selection

        /// <summary>
        /// Selects a material by ID and updates the UI accordingly.
        /// </summary>
        public void SelectMaterial(string materialId)
        {
            // Update selection state
            _selectedMaterialId = materialId;

            // Update button classes
            foreach (var btn in _resourceButtons)
            {
                if (btn == null) continue;

                string btnMaterialId = btn.userData as string;
                bool isSelected = btnMaterialId == materialId;

                if (isSelected)
                    btn.AddToClassList(SelectedClass);
                else
                    btn.RemoveFromClassList(SelectedClass);
            }

            // Update center panel with selected material details
            UpdateCenterPanel();
        }

        /// <summary>
        /// Gets the currently selected material data, or null if none selected.
        /// </summary>
        public MaterialData? GetSelectedMaterial()
        {
            if (string.IsNullOrEmpty(_selectedMaterialId)) return null;

            foreach (var mat in materials)
            {
                if (mat.MaterialId == _selectedMaterialId)
                    return mat;
            }

            return null;
        }

        /// <summary>
        /// Gets the currently selected material ID.
        /// </summary>
        public string SelectedMaterialId => _selectedMaterialId;

        #endregion

        #region Daily Cap Summary

        /// <summary>
        /// Updates the DailyCapSummaryLabel with global refinement attempts.
        /// Format: "Refinement Attempts: {used}/{cap}"
        /// </summary>
        private void UpdateDailyCapSummary()
        {
            if (_dailyCapSummaryLabel == null) return;

            _dailyCapSummaryLabel.text = $"Refinement Attempts: {_globalAttemptsUsed}/{globalAttemptsCap}";
        }

        /// <summary>
        /// Sets the global daily attempts used count.
        /// </summary>
        public void SetGlobalAttemptsUsed(int used)
        {
            _globalAttemptsUsed = Mathf.Max(0, used);
            if (IsVisible)
            {
                UpdateDailyCapSummary();
                UpdateCenterPanel();
                UpdateFooter();
            }
        }

        /// <summary>
        /// Sets the global daily attempts cap.
        /// </summary>
        public void SetGlobalAttemptsCap(int cap)
        {
            globalAttemptsCap = Mathf.Max(1, cap);
            if (IsVisible)
            {
                UpdateDailyCapSummary();
                UpdateCenterPanel();
                UpdateFooter();
            }
        }

        /// <summary>
        /// Gets the current global attempts used count.
        /// </summary>
        public int GlobalAttemptsUsed => _globalAttemptsUsed;

        /// <summary>
        /// Gets the global attempts cap.
        /// </summary>
        public int GlobalAttemptsCap => globalAttemptsCap;

        /// <summary>
        /// Updates the token summary label.
        /// </summary>
        private void UpdateTokenSummary()
        {
            if (_tokenSummaryLabel != null)
            {
                _tokenSummaryLabel.text = $"Refinement Tokens: {_refinementTokens}";
            }
        }

        #endregion

        #region Center Panel

        /// <summary>
        /// Updates the center panel with selected material details, costs, and button states.
        /// </summary>
        private void UpdateCenterPanel()
        {
            var selected = GetSelectedMaterial();

            if (selected == null)
            {
                // No selection - show placeholder state
                if (_selectedResourceLabel != null) _selectedResourceLabel.text = "None";
                if (_selectedResourceQLabel != null) _selectedResourceQLabel.text = "Q --";
                if (_selectedResourceDesc != null) _selectedResourceDesc.text = "Select a resource to improve.";
                if (_currentQLabel != null) _currentQLabel.text = "Q --";
                if (_targetQLabel != null) _targetQLabel.text = "Q --";
                if (_chanceLabel != null) _chanceLabel.text = "--%";
                UpdateCostLabels();
                UpdateButtonStates(false, "No resource selected");
                return;
            }

            var mat = selected.Value;

            // Update selected resource labels
            if (_selectedResourceLabel != null) _selectedResourceLabel.text = mat.DisplayName;
            if (_selectedResourceQLabel != null) _selectedResourceQLabel.text = $"Q {mat.GlobalQualityQ}";
            if (_selectedResourceDesc != null) _selectedResourceDesc.text = mat.Description;

            // Update current/target Q labels
            int currentQ = mat.GlobalQualityQ;
            int targetQ = currentQ + 1;
            if (_currentQLabel != null) _currentQLabel.text = $"Q {currentQ}";
            if (_targetQLabel != null) _targetQLabel.text = $"Q {targetQ}";

            // Calculate and display success chance
            float chance = CalculateSuccessChance(currentQ);
            if (_chanceLabel != null) _chanceLabel.text = $"{chance:F0}%";

            // Update cost labels
            UpdateCostLabels();

            // Validate and update button states
            ValidateAndUpdateButtons(mat);

            // Update right column with breakdown
            UpdateRightColumn();
        }

        /// <summary>
        /// Updates the cost labels with current attempt costs.
        /// </summary>
        private void UpdateCostLabels()
        {
            if (_costTokenLabel != null) _costTokenLabel.text = attemptCostTokens.ToString();
            if (_costCreditsLabel != null) _costCreditsLabel.text = attemptCostCredits.ToString();
            if (_costMatsLabel != null) _costMatsLabel.text = attemptCostCommonMats.ToString();
        }

        /// <summary>
        /// Validates resources and updates button enabled states.
        /// </summary>
        private void ValidateAndUpdateButtons(MaterialData mat)
        {
            // Check if at max quality
            if (mat.GlobalQualityQ >= mat.MaxQualityQ)
            {
                UpdateButtonStates(false, "Already at max quality");
                return;
            }

            // Check global daily limit
            if (_globalAttemptsUsed >= globalAttemptsCap)
            {
                UpdateButtonStates(false, "Daily limit reached");
                return;
            }

            // Check tokens
            if (_refinementTokens < attemptCostTokens)
            {
                UpdateButtonStates(false, "Not enough tokens");
                return;
            }

            // Check credits
            float credits = GetCredits();
            if (credits < attemptCostCredits)
            {
                UpdateButtonStates(false, "Not enough credits");
                return;
            }

            // Check common materials (use the selected material's resource ID)
            if (!TryGetResourceId(mat.MaterialId, out var resourceId))
            {
                UpdateButtonStates(false, "Invalid resource");
                return;
            }

            int materialAmount = GetMaterialAmount(resourceId);
            if (materialAmount < attemptCostCommonMats)
            {
                UpdateButtonStates(false, $"Not enough {mat.DisplayName}");
                return;
            }

            // All checks passed
            UpdateButtonStates(true, string.Empty);
        }

        /// <summary>
        /// Updates the enabled state of improve buttons.
        /// </summary>
        private void UpdateButtonStates(bool enabled, string reason)
        {
            if (_btnImproveOnce != null)
            {
                _btnImproveOnce.SetEnabled(enabled);
            }
            if (_btnImproveMax != null)
            {
                _btnImproveMax.SetEnabled(enabled);
            }

            // Show reason in result line if disabled
            if (!enabled && !string.IsNullOrEmpty(reason) && _resultLine != null)
            {
                _resultLine.text = reason;
                _resultLine.RemoveFromClassList(SuccessClass);
                _resultLine.RemoveFromClassList(FailClass);
            }
        }

        #endregion

        #region Improvement Logic

        /// <summary>
        /// Calculates the success chance for improving from the given quality level.
        /// Formula: BaseChance - (QualityLevel - 1) * PenaltyPerLevel - LateGamePenalty + Bonuses, clamped to minimum.
        /// </summary>
        private float CalculateSuccessChance(int currentQuality)
        {
            float chance = baseSuccessChance;

            // Standard penalty per level
            chance -= (currentQuality - 1) * penaltyPerLevel;

            // Late game extra penalty
            if (currentQuality > lateGameThreshold)
            {
                chance -= (currentQuality - lateGameThreshold) * lateGameExtraPenalty;
            }

            // Add bonuses
            chance += buildingsBonusPercent;
            chance += reputationBonusPercent;
            chance += GetMaterialSpecificBonus(_selectedMaterialId);

            return Mathf.Max(minSuccessChance, chance);
        }

        /// <summary>
        /// Gets additional bonus percentage for a specific material from active bonuses.
        /// </summary>
        private float GetMaterialSpecificBonus(string materialId)
        {
            float bonus = 0f;
            foreach (var ab in activeBonuses)
            {
                if (string.IsNullOrEmpty(ab.AffectedMaterialId) || ab.AffectedMaterialId == materialId)
                {
                    bonus += ab.BonusPercent;
                }
            }
            return bonus;
        }

        /// <summary>
        /// Gets the breakdown of chance components for display.
        /// </summary>
        private void GetChanceBreakdown(int currentQuality, out float baseChance, out float latePenalty,
            out float buildingsBonus, out float repBonus)
        {
            baseChance = baseSuccessChance - (currentQuality - 1) * penaltyPerLevel;

            latePenalty = 0f;
            if (currentQuality > lateGameThreshold)
            {
                latePenalty = (currentQuality - lateGameThreshold) * lateGameExtraPenalty;
            }

            buildingsBonus = buildingsBonusPercent + GetMaterialSpecificBonus(_selectedMaterialId);
            repBonus = reputationBonusPercent;
        }

        /// <summary>
        /// Attempts to improve the selected material once.
        /// Returns true if the attempt was made (regardless of success/failure).
        /// </summary>
        public bool TryImproveOnce()
        {
            var selected = GetSelectedMaterial();
            if (selected == null) return false;

            var mat = selected.Value;
            int matIndex = FindMaterialIndex(mat.MaterialId);
            if (matIndex < 0) return false;

            // Validation checks
            if (mat.GlobalQualityQ >= mat.MaxQualityQ)
            {
                SetResultLine("Already at max quality", false);
                return false;
            }

            if (_globalAttemptsUsed >= globalAttemptsCap)
            {
                SetResultLine("Daily limit reached", false);
                return false;
            }

            if (_refinementTokens < attemptCostTokens)
            {
                SetResultLine("Not enough tokens", false);
                return false;
            }

            float credits = GetCredits();
            if (credits < attemptCostCredits)
            {
                SetResultLine("Not enough credits", false);
                return false;
            }

            if (!TryGetResourceId(mat.MaterialId, out var resourceId))
            {
                SetResultLine("Invalid resource", false);
                return false;
            }

            int materialAmount = GetMaterialAmount(resourceId);
            if (materialAmount < attemptCostCommonMats)
            {
                SetResultLine($"Not enough {mat.DisplayName}", false);
                return false;
            }

            // Deduct costs
            _refinementTokens -= attemptCostTokens;

            var ppm = PlayerProgressManager.Instance;
            if (ppm != null)
            {
                ppm.AddCredits(-attemptCostCredits);
            }

            if (warehouseInventory != null)
            {
                warehouseInventory.TryRemove(resourceId, attemptCostCommonMats);
            }

            // Increment global daily attempts
            _globalAttemptsUsed++;

            // Roll for success
            float chance = CalculateSuccessChance(mat.GlobalQualityQ);
            float roll = UnityEngine.Random.Range(0f, 100f);
            bool success = roll < chance;

            int oldQ = mat.GlobalQualityQ;
            int newQ = oldQ;

            if (success)
            {
                mat.GlobalQualityQ++;
                newQ = mat.GlobalQualityQ;
            }

            // Update material in list
            materials[matIndex] = mat;

            // Log result
            string resultText = success
                ? $"Success: {mat.DisplayName} Q{oldQ} -> Q{newQ} (Chance {chance:F0}%)"
                : $"Fail: {mat.DisplayName} Q{oldQ} -> Q{oldQ} (Chance {chance:F0}%)";

            SetResultLine(resultText, success);
            AddResultToHistory(resultText, success);

            // Update footer with last action
            _lastActionResult = resultText;

            // Refresh UI
            UpdateCenterPanel();
            UpdateDailyCapSummary();
            UpdateTokenSummary();
            UpdateFooter();

            return true;
        }

        /// <summary>
        /// Attempts to improve the selected material repeatedly until:
        /// - Max quality is reached
        /// - Daily limit is hit
        /// - Resources run out
        /// </summary>
        public void TryImproveMax()
        {
            int attempts = 0;
            int successes = 0;
            const int maxAttempts = 100; // Safety limit

            while (attempts < maxAttempts)
            {
                var selected = GetSelectedMaterial();
                if (selected == null) break;

                var mat = selected.Value;

                // Check stopping conditions
                if (mat.GlobalQualityQ >= mat.MaxQualityQ) break;
                if (_globalAttemptsUsed >= globalAttemptsCap) break;
                if (_refinementTokens < attemptCostTokens) break;
                if (GetCredits() < attemptCostCredits) break;

                if (!TryGetResourceId(mat.MaterialId, out var resourceId)) break;
                if (GetMaterialAmount(resourceId) < attemptCostCommonMats) break;

                // Store old Q to check for success
                int oldQ = mat.GlobalQualityQ;

                if (!TryImproveOnce()) break;

                attempts++;

                // Check if quality increased
                var updated = GetSelectedMaterial();
                if (updated.HasValue && updated.Value.GlobalQualityQ > oldQ)
                {
                    successes++;
                }
            }

            if (attempts > 0)
            {
                string summary = $"Improved {attempts}x: {successes} success, {attempts - successes} fail";
                SetResultLine(summary, successes > 0);
            }
        }

        /// <summary>
        /// Sets the result line text and styling.
        /// </summary>
        private void SetResultLine(string text, bool isSuccess)
        {
            if (_resultLine == null) return;

            _resultLine.text = text;
            _resultLine.RemoveFromClassList(SuccessClass);
            _resultLine.RemoveFromClassList(FailClass);

            if (isSuccess)
                _resultLine.AddToClassList(SuccessClass);
            else
                _resultLine.AddToClassList(FailClass);
        }

        /// <summary>
        /// Adds a result entry to the recent results list, prepending to the top.
        /// </summary>
        private void AddResultToHistory(string text, bool isSuccess)
        {
            if (_recentResultsList == null) return;

            var label = new Label(text);
            label.AddToClassList("ql-log-row");

            if (isSuccess)
                label.AddToClassList(SuccessClass);
            else
                label.AddToClassList(FailClass);

            // Prepend to top (insert at index 0)
            _recentResultsList.Insert(0, label);

            // Limit history size
            const int maxHistoryEntries = 50;
            while (_recentResultsList.childCount > maxHistoryEntries)
            {
                _recentResultsList.RemoveAt(_recentResultsList.childCount - 1);
            }
        }

        #endregion

        #region Right Column (Chance Breakdown & Active Bonuses)

        /// <summary>
        /// Updates all right column elements: chance breakdown and active bonuses.
        /// </summary>
        private void UpdateRightColumn()
        {
            UpdateChanceBreakdown();
            UpdateActiveBonuses();
        }

        /// <summary>
        /// Updates the chance breakdown labels in the right column.
        /// </summary>
        private void UpdateChanceBreakdown()
        {
            var selected = GetSelectedMaterial();
            if (selected == null)
            {
                if (_breakBaseChance != null) SetBreakdownLabel(_breakBaseChance, "--%", false, false);
                if (_breakLatePenalty != null) SetBreakdownLabel(_breakLatePenalty, "--%", false, false);
                if (_breakBuildingsBonus != null) SetBreakdownLabel(_breakBuildingsBonus, "--%", false, false);
                if (_breakRepBonus != null) SetBreakdownLabel(_breakRepBonus, "--%", false, false);
                return;
            }

            var mat = selected.Value;
            GetChanceBreakdown(mat.GlobalQualityQ, out float baseChance, out float latePenalty,
                out float buildingsBonus, out float repBonus);

            // Base chance (can be positive or adjusted down by level)
            if (_breakBaseChance != null)
            {
                SetBreakdownLabel(_breakBaseChance, $"{baseChance:F0}%", false, false);
            }

            // Late game penalty (negative)
            if (_breakLatePenalty != null)
            {
                bool hasLatePenalty = latePenalty > 0;
                string latePenaltyText = hasLatePenalty ? $"-{latePenalty:F0}%" : "0%";
                SetBreakdownLabel(_breakLatePenalty, latePenaltyText, false, hasLatePenalty);
            }

            // Buildings bonus (positive)
            if (_breakBuildingsBonus != null)
            {
                bool hasBonus = buildingsBonus > 0;
                string bonusText = hasBonus ? $"+{buildingsBonus:F0}%" : "+0%";
                SetBreakdownLabel(_breakBuildingsBonus, bonusText, hasBonus, false);
            }

            // Reputation bonus (positive)
            if (_breakRepBonus != null)
            {
                bool hasBonus = repBonus > 0;
                string bonusText = hasBonus ? $"+{repBonus:F0}%" : "+0%";
                SetBreakdownLabel(_breakRepBonus, bonusText, hasBonus, false);
            }
        }

        /// <summary>
        /// Sets a breakdown label's text and applies positive/negative styling.
        /// </summary>
        private void SetBreakdownLabel(Label label, string text, bool isPositive, bool isNegative)
        {
            label.text = text;
            label.RemoveFromClassList(PositiveClass);
            label.RemoveFromClassList(NegativeClass);

            if (isPositive)
                label.AddToClassList(PositiveClass);
            else if (isNegative)
                label.AddToClassList(NegativeClass);
        }

        /// <summary>
        /// Updates the active bonuses list in the right column.
        /// </summary>
        private void UpdateActiveBonuses()
        {
            if (_activeBonusesList == null) return;

            _activeBonusesList.Clear();

            // Add entries for each active bonus
            foreach (var bonus in activeBonuses)
            {
                string text = $"{bonus.Source}: {bonus.Description}";
                var label = new Label(text);
                label.AddToClassList("ql-small");
                _activeBonusesList.Add(label);
            }

            // Add building bonus if any
            if (buildingsBonusPercent > 0)
            {
                var selected = GetSelectedMaterial();
                string matName = selected?.DisplayName ?? "All";
                var label = new Label($"Buildings: +{buildingsBonusPercent:F0}% {matName} improve chance");
                label.AddToClassList("ql-small");
                _activeBonusesList.Add(label);
            }

            // Add reputation bonus if any
            if (reputationBonusPercent > 0)
            {
                var label = new Label($"Reputation: +{reputationBonusPercent:F0}% improve chance");
                label.AddToClassList("ql-small");
                _activeBonusesList.Add(label);
            }

            // Show placeholder if no bonuses
            if (_activeBonusesList.childCount == 0)
            {
                var label = new Label("No active bonuses");
                label.AddToClassList("ql-small");
                label.AddToClassList("ql-small-muted");
                _activeBonusesList.Add(label);
            }
        }

        #endregion

        #region Booster Slots

        /// <summary>
        /// Updates booster slot button states based on unlock count.
        /// </summary>
        private void UpdateBoosterSlots()
        {
            UpdateBoosterSlot(_boosterSlot1, 0);
            UpdateBoosterSlot(_boosterSlot2, 1);
            UpdateBoosterSlot(_boosterSlot3, 2);
        }

        /// <summary>
        /// Updates a single booster slot's appearance and interactivity.
        /// </summary>
        private void UpdateBoosterSlot(Button slot, int index)
        {
            if (slot == null) return;

            bool isUnlocked = index < boosterSlotsUnlocked;

            slot.RemoveFromClassList(LockedClass);

            if (isUnlocked)
            {
                slot.text = "+ Add Booster";
                slot.SetEnabled(true);
            }
            else
            {
                slot.text = "Locked";
                slot.AddToClassList(LockedClass);
                slot.SetEnabled(false);
            }
        }

        /// <summary>
        /// Sets the number of unlocked booster slots.
        /// </summary>
        public void SetBoosterSlotsUnlocked(int count)
        {
            boosterSlotsUnlocked = Mathf.Clamp(count, 0, 3);
            if (IsVisible)
            {
                UpdateBoosterSlots();
            }
        }

        /// <summary>
        /// Gets the number of unlocked booster slots.
        /// </summary>
        public int BoosterSlotsUnlocked => boosterSlotsUnlocked;

        #endregion

        #region Footer

        /// <summary>
        /// Updates the footer status label with contextual information.
        /// </summary>
        private void UpdateFooter()
        {
            if (_footerStatusLabel == null) return;

            // Show last action result if available
            if (!string.IsNullOrEmpty(_lastActionResult))
            {
                _footerStatusLabel.text = _lastActionResult;
                return;
            }

            // Default: show global remaining attempts
            int remaining = globalAttemptsCap - _globalAttemptsUsed;
            if (remaining > 0)
            {
                _footerStatusLabel.text = $"{remaining} refinement attempts remaining today";
            }
            else
            {
                _footerStatusLabel.text = "Daily refinement limit reached";
            }
        }

        /// <summary>
        /// Sets the footer status text directly.
        /// </summary>
        public void SetFooterStatus(string status)
        {
            _lastActionResult = status;
            if (IsVisible)
            {
                UpdateFooter();
            }
        }

        #endregion

        #region Bonus Management

        /// <summary>
        /// Adds an active bonus.
        /// </summary>
        public void AddActiveBonus(ActiveBonus bonus)
        {
            activeBonuses.Add(bonus);
            if (IsVisible)
            {
                UpdateCenterPanel();
                UpdateActiveBonuses();
            }
        }

        /// <summary>
        /// Removes all bonuses from a specific source.
        /// </summary>
        public void RemoveBonusesFromSource(string source)
        {
            activeBonuses.RemoveAll(b => b.Source == source);
            if (IsVisible)
            {
                UpdateCenterPanel();
                UpdateActiveBonuses();
            }
        }

        /// <summary>
        /// Sets the buildings bonus percentage.
        /// </summary>
        public void SetBuildingsBonus(float percent)
        {
            buildingsBonusPercent = percent;
            if (IsVisible)
            {
                UpdateCenterPanel();
                UpdateActiveBonuses();
            }
        }

        /// <summary>
        /// Sets the reputation bonus percentage.
        /// </summary>
        public void SetReputationBonus(float percent)
        {
            reputationBonusPercent = percent;
            if (IsVisible)
            {
                UpdateCenterPanel();
                UpdateActiveBonuses();
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Finds the index of a material by ID.
        /// </summary>
        private int FindMaterialIndex(string materialId)
        {
            for (int i = 0; i < materials.Count; i++)
            {
                if (materials[i].MaterialId == materialId)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Tries to convert a material ID string to a HexWorldResourceId.
        /// </summary>
        private bool TryGetResourceId(string materialId, out HexWorldResourceId resourceId)
        {
            resourceId = HexWorldResourceId.None;

            if (string.IsNullOrEmpty(materialId))
                return false;

            // Try direct enum parse
            if (Enum.TryParse<HexWorldResourceId>(materialId, true, out var parsed))
            {
                resourceId = parsed;
                return true;
            }

            // Manual mapping for common cases
            switch (materialId.ToLowerInvariant())
            {
                case "wood":
                    resourceId = HexWorldResourceId.Wood;
                    return true;
                case "stone":
                    resourceId = HexWorldResourceId.Stone;
                    return true;
                case "fiber":
                    resourceId = HexWorldResourceId.Fiber;
                    return true;
                case "baitingredients":
                case "bait ingredients":
                case "bait":
                    resourceId = HexWorldResourceId.BaitIngredients;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Gets the current refinement token count.
        /// </summary>
        public int RefinementTokens => _refinementTokens;

        /// <summary>
        /// Sets the refinement token count.
        /// </summary>
        public void SetRefinementTokens(int tokens)
        {
            _refinementTokens = Mathf.Max(0, tokens);
            if (IsVisible)
            {
                UpdateTokenSummary();
                UpdateCenterPanel();
            }
        }

        /// <summary>
        /// Adds refinement tokens.
        /// </summary>
        public void AddRefinementTokens(int amount)
        {
            SetRefinementTokens(_refinementTokens + amount);
        }

        #endregion
    }
}
