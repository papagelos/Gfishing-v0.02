// Assets/Minigames/HexWorld3D/Scripts/Village/Minigames/ForestryMinigameUI.cs
using UnityEngine;
using UnityEngine.UIElements;

namespace GalacticFishing.Minigames.HexWorld
{
    /// <summary>
    /// UI controller for the Forestry Station Plot Manager panel.
    /// Binds to a ForestryMinigameController and updates UI elements via events.
    /// </summary>
    public sealed class ForestryMinigameUI : MonoBehaviour
    {
        private const int MaxPlotRows = 6;

        [Header("UI")]
        [Tooltip("UIDocument containing the Forestry Plot Panel UXML.")]
        [SerializeField] private UIDocument uiDocument;

        [Header("References")]
        [Tooltip("The currently bound forestry controller.")]
        [SerializeField] private ForestryMinigameController boundController;

        // Cached UI elements
        private VisualElement _root;
        private VisualElement _forestryMinigameRoot;
        private Button _closeButton;

        // Stats labels
        private Label _growthSpeedLabel;
        private Label _growthBonusLabel;
        private Label _woodOutputLabel;
        private Label _fiberOutputLabel;
        private Label _statusSummaryLabel;
        private Label _plotsUnlockedLabel;

        // Plot rows
        private VisualElement[] _plotRows = new VisualElement[MaxPlotRows];
        private ProgressBar[] _plotProgressBars = new ProgressBar[MaxPlotRows];
        private Label[] _plotStatusLabels = new Label[MaxPlotRows];

        // Action buttons
        private Button _btnHarvestAll;
        private Button _btnPlantAll;
        private Label _actionResultLabel;

        private bool _wired = false;

        private void OnEnable()
        {
            EnsureWired();
        }

        private void OnDisable()
        {
            if (_wired)
            {
                UnregisterCallbacks();
                UnbindController();
                _wired = false;
            }
        }

        private void Update()
        {
            // Poll for UI updates when visible (for smooth progress bar animation)
            if (_wired && boundController != null && IsVisible)
            {
                UpdatePlotProgressBars();
                UpdateActionButtons();
            }
        }

        private void EnsureWired()
        {
            if (_wired) return;

            if (!uiDocument)
            {
                uiDocument = GetComponent<UIDocument>();
                if (!uiDocument)
                {
                    Debug.LogWarning($"[{nameof(ForestryMinigameUI)}] Missing UIDocument reference.");
                    return;
                }
            }

            _root = uiDocument.rootVisualElement;
            if (_root == null)
            {
                Debug.LogWarning($"[{nameof(ForestryMinigameUI)}] UIDocument has no rootVisualElement yet.");
                return;
            }

            QueryAndCacheElements(_root);
            RegisterCallbacks();
            _wired = true;

            // Start hidden
            SetVisible(false);
        }

        private void QueryAndCacheElements(VisualElement root)
        {
            _forestryMinigameRoot = root.Q<VisualElement>("ForestryMinigameRoot");
            _closeButton = root.Q<Button>("Btn_Close");

            // Stats labels
            _growthSpeedLabel = root.Q<Label>("GrowthSpeedLabel");
            _growthBonusLabel = root.Q<Label>("GrowthBonusLabel");
            _woodOutputLabel = root.Q<Label>("WoodOutputLabel");
            _fiberOutputLabel = root.Q<Label>("FiberOutputLabel");
            _statusSummaryLabel = root.Q<Label>("StatusSummaryLabel");
            _plotsUnlockedLabel = root.Q<Label>("PlotsUnlockedLabel");

            // Plot rows (0-5)
            for (int i = 0; i < MaxPlotRows; i++)
            {
                _plotRows[i] = root.Q<VisualElement>($"PlotRow_{i}");
                _plotProgressBars[i] = root.Q<ProgressBar>($"PlotProgress_{i}");
                _plotStatusLabels[i] = root.Q<Label>($"PlotStatus_{i}");
            }

            // Action buttons
            _btnHarvestAll = root.Q<Button>("Btn_HarvestAll");
            _btnPlantAll = root.Q<Button>("Btn_PlantAll");
            _actionResultLabel = root.Q<Label>("ActionResultLabel");
        }

        private void RegisterCallbacks()
        {
            _closeButton?.RegisterCallback<ClickEvent>(OnCloseClicked);
            _btnHarvestAll?.RegisterCallback<ClickEvent>(OnHarvestAllClicked);
            _btnPlantAll?.RegisterCallback<ClickEvent>(OnPlantAllClicked);
        }

        private void UnregisterCallbacks()
        {
            _closeButton?.UnregisterCallback<ClickEvent>(OnCloseClicked);
            _btnHarvestAll?.UnregisterCallback<ClickEvent>(OnHarvestAllClicked);
            _btnPlantAll?.UnregisterCallback<ClickEvent>(OnPlantAllClicked);
        }

        // ─────────────────────────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Shows the forestry UI and binds to the given controller.
        /// </summary>
        public void Show(ForestryMinigameController controller)
        {
            EnsureWired();
            if (!_wired) return;

            BindController(controller);
            RefreshUI();
            SetVisible(true);
        }

        /// <summary>
        /// Hides the forestry UI.
        /// </summary>
        public void Hide()
        {
            SetVisible(false);
            UnbindController();
        }

        /// <summary>
        /// Returns true if the UI is currently visible.
        /// </summary>
        public bool IsVisible
        {
            get
            {
                if (!_wired || _forestryMinigameRoot == null) return false;
                return _forestryMinigameRoot.resolvedStyle.display == DisplayStyle.Flex;
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // Controller Binding
        // ─────────────────────────────────────────────────────────────────

        private void BindController(ForestryMinigameController controller)
        {
            UnbindController();

            boundController = controller;
            if (boundController == null) return;

            boundController.PlotGrowthChanged += OnPlotGrowthChanged;
            boundController.PlotHarvested += OnPlotHarvested;
            boundController.PlotCountChanged += OnPlotCountChanged;
        }

        private void UnbindController()
        {
            if (boundController != null)
            {
                boundController.PlotGrowthChanged -= OnPlotGrowthChanged;
                boundController.PlotHarvested -= OnPlotHarvested;
                boundController.PlotCountChanged -= OnPlotCountChanged;
                boundController = null;
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // Event Handlers (from Controller)
        // ─────────────────────────────────────────────────────────────────

        private void OnPlotGrowthChanged(int plotIndex, ForestPlot plot)
        {
            UpdatePlotRow(plotIndex, plot);
            UpdateStatusSummary();
            UpdateActionButtons();
        }

        private void OnPlotHarvested(int plotIndex)
        {
            if (_actionResultLabel != null)
                _actionResultLabel.text = $"Plot {plotIndex + 1} harvested!";

            UpdateStatusSummary();
            UpdateActionButtons();
        }

        private void OnPlotCountChanged(int newCount)
        {
            RefreshPlotRows();
            UpdateStatusSummary();
        }

        // ─────────────────────────────────────────────────────────────────
        // UI Updates
        // ─────────────────────────────────────────────────────────────────

        private void RefreshUI()
        {
            UpdateGrowthSpeedDisplay();
            UpdateOutputDisplay();
            UpdateStatusSummary();
            RefreshPlotRows();
            UpdateActionButtons();

            if (_actionResultLabel != null)
                _actionResultLabel.text = "";
        }

        private void UpdateGrowthSpeedDisplay()
        {
            if (boundController == null) return;

            // Calculate total growth speed as percentage
            float baseGrowth = boundController.BaseGrowthPerTick;
            float bonusGrowth = boundController.GetForestAdjacencyBonusPercent();
            float totalGrowth = baseGrowth + bonusGrowth;

            // Display as percentage (e.g., 0.12 = 12% per tick)
            int totalPct = Mathf.RoundToInt(totalGrowth * 100f);
            int bonusPct = Mathf.RoundToInt(bonusGrowth * 100f);
            int forestCount = boundController.GetAdjacentForestCount();

            if (_growthSpeedLabel != null)
                _growthSpeedLabel.text = $"{totalPct}%";

            if (_growthBonusLabel != null)
            {
                if (bonusPct > 0)
                    _growthBonusLabel.text = $"+{bonusPct}% ({forestCount} forest tiles)";
                else
                    _growthBonusLabel.text = "+0% (0 forest tiles)";
            }
        }

        private void UpdateOutputDisplay()
        {
            if (boundController == null) return;

            if (_woodOutputLabel != null)
                _woodOutputLabel.text = $"Wood: {boundController.WoodPerHarvest}";

            if (_fiberOutputLabel != null)
                _fiberOutputLabel.text = $"Fiber: {boundController.FiberPerHarvest}";
        }

        private void UpdateStatusSummary()
        {
            if (boundController == null) return;

            if (_statusSummaryLabel != null)
                _statusSummaryLabel.text = boundController.GetStatusSummary();

            if (_plotsUnlockedLabel != null)
                _plotsUnlockedLabel.text = $"Plots: {boundController.PlotCount}/{boundController.MaxPlots} unlocked";
        }

        private void RefreshPlotRows()
        {
            if (boundController == null) return;

            var plots = boundController.Plots;
            int plotCount = boundController.PlotCount;

            for (int i = 0; i < MaxPlotRows; i++)
            {
                if (_plotRows[i] == null) continue;

                if (i < plotCount)
                {
                    _plotRows[i].style.display = DisplayStyle.Flex;
                    UpdatePlotRow(i, plots[i]);
                }
                else
                {
                    _plotRows[i].style.display = DisplayStyle.None;
                }
            }
        }

        private void UpdatePlotRow(int index, ForestPlot plot)
        {
            if (index < 0 || index >= MaxPlotRows) return;

            var progressBar = _plotProgressBars[index];
            var statusLabel = _plotStatusLabels[index];

            if (progressBar != null)
            {
                float progress = plot.isOccupied ? plot.growthProgress * 100f : 0f;
                progressBar.value = progress;
            }

            if (statusLabel != null)
            {
                if (!plot.isOccupied)
                {
                    statusLabel.text = "Empty";
                }
                else if (plot.growthProgress >= 1f)
                {
                    statusLabel.text = "Ready!";
                }
                else
                {
                    statusLabel.text = $"{Mathf.RoundToInt(plot.growthProgress * 100f)}%";
                }
            }
        }

        private void UpdatePlotProgressBars()
        {
            if (boundController == null) return;

            var plots = boundController.Plots;
            for (int i = 0; i < plots.Count && i < MaxPlotRows; i++)
            {
                UpdatePlotRow(i, plots[i]);
            }
        }

        private void UpdateActionButtons()
        {
            if (boundController == null) return;

            // Enable Harvest button only if there are ready plots
            if (_btnHarvestAll != null)
            {
                bool hasReady = boundController.HasReadyPlots();
                _btnHarvestAll.SetEnabled(hasReady);

                int readyCount = boundController.GetReadyPlotCount();
                _btnHarvestAll.text = readyCount > 0 ? $"HARVEST READY ({readyCount})" : "HARVEST READY";
            }

            // Enable Plant button only if there are empty plots
            if (_btnPlantAll != null)
            {
                bool hasEmpty = boundController.HasEmptyPlots();
                _btnPlantAll.SetEnabled(hasEmpty);
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // Button Callbacks
        // ─────────────────────────────────────────────────────────────────

        private void OnCloseClicked(ClickEvent evt)
        {
            Hide();
        }

        private void OnHarvestAllClicked(ClickEvent evt)
        {
            if (boundController == null) return;

            var (wood, fiber) = boundController.HarvestAllReady();

            if (_actionResultLabel != null)
            {
                if (wood > 0 || fiber > 0)
                    _actionResultLabel.text = $"Harvested! +{wood} Wood, +{fiber} Fiber";
                else
                    _actionResultLabel.text = "No trees ready to harvest.";
            }

            UpdateStatusSummary();
            UpdateActionButtons();
        }

        private void OnPlantAllClicked(ClickEvent evt)
        {
            if (boundController == null) return;

            int planted = boundController.PlantAllEmpty();

            if (_actionResultLabel != null)
            {
                if (planted > 0)
                    _actionResultLabel.text = $"Planted {planted} new trees!";
                else
                    _actionResultLabel.text = "No empty plots available.";
            }

            UpdateStatusSummary();
            UpdateActionButtons();
        }

        // ─────────────────────────────────────────────────────────────────
        // Visibility
        // ─────────────────────────────────────────────────────────────────

        private void SetVisible(bool visible)
        {
            if (_forestryMinigameRoot != null)
            {
                _forestryMinigameRoot.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }
    }
}
