// Assets/Minigames/HexWorld3D/Scripts/Village/Minigames/QuarryMinigameUI.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace GalacticFishing.Minigames.HexWorld
{
    /// <summary>
    /// UI controller for the Quarry Strata Drill panel.
    /// Binds to a QuarryMinigameController and updates UI elements via events.
    /// </summary>
    public sealed class QuarryMinigameUI : MonoBehaviour
    {
        [Header("UI")]
        [Tooltip("UIDocument containing the Quarry Drill Panel UXML.")]
        [SerializeField] private UIDocument uiDocument;

        [Header("References")]
        [Tooltip("The currently bound quarry controller.")]
        [SerializeField] private QuarryMinigameController boundController;

        // Cached UI elements
        private VisualElement _root;
        private VisualElement _quarryMinigameRoot;
        private Button _closeButton;
        private Label _drillDepthLabel;
        private Label _nextStratumLabel;
        private Label _nextStratumDepthLabel;
        private ProgressBar _drillProgressBar;
        private ProgressBar _energyProgressBar;
        private ScrollView _strataUnlockList;
        private Button _btnPushDrill;
        private Button _btnPushDrill10;
        private Button _btnPushDrillMax;
        private Label _drillResultLabel;
        private Label _outputSummaryLabel;

        private bool _wired = false;

        // Strata thresholds for display (matches QuarryMinigameController)
        private static readonly (string name, float depth)[] StrataList = new[]
        {
            ("Stone", 0f),
            ("Coal", 0f),    // Coal unlocks at 0m in the controller
            ("Copper", 100f),
            ("Iron", 300f),
            ("Gold", 600f)
        };

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

        private void EnsureWired()
        {
            if (_wired) return;

            if (!uiDocument)
            {
                uiDocument = GetComponent<UIDocument>();
                if (!uiDocument)
                {
                    Debug.LogWarning($"[{nameof(QuarryMinigameUI)}] Missing UIDocument reference.");
                    return;
                }
            }

            _root = uiDocument.rootVisualElement;
            if (_root == null)
            {
                Debug.LogWarning($"[{nameof(QuarryMinigameUI)}] UIDocument has no rootVisualElement yet.");
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
            _quarryMinigameRoot = root.Q<VisualElement>("QuarryMinigameRoot");
            _closeButton = root.Q<Button>("Btn_Close");
            _drillDepthLabel = root.Q<Label>("DrillDepthLabel");
            _nextStratumLabel = root.Q<Label>("NextStratumLabel");
            _nextStratumDepthLabel = root.Q<Label>("NextStratumDepthLabel");
            _drillProgressBar = root.Q<ProgressBar>("DrillProgressBar");
            _energyProgressBar = root.Q<ProgressBar>("EnergyProgressBar");
            _strataUnlockList = root.Q<ScrollView>("StrataUnlockList");
            _btnPushDrill = root.Q<Button>("Btn_PushDrill");
            _btnPushDrill10 = root.Q<Button>("Btn_PushDrill10");
            _btnPushDrillMax = root.Q<Button>("Btn_PushDrillMax");
            _drillResultLabel = root.Q<Label>("DrillResultLabel");
            _outputSummaryLabel = root.Q<Label>("OutputSummaryLabel");
        }

        private void RegisterCallbacks()
        {
            _closeButton?.RegisterCallback<ClickEvent>(OnCloseClicked);
            _btnPushDrill?.RegisterCallback<ClickEvent>(OnPushDrillClicked);
            _btnPushDrill10?.RegisterCallback<ClickEvent>(OnPushDrill10Clicked);
            _btnPushDrillMax?.RegisterCallback<ClickEvent>(OnPushDrillMaxClicked);
        }

        private void UnregisterCallbacks()
        {
            _closeButton?.UnregisterCallback<ClickEvent>(OnCloseClicked);
            _btnPushDrill?.UnregisterCallback<ClickEvent>(OnPushDrillClicked);
            _btnPushDrill10?.UnregisterCallback<ClickEvent>(OnPushDrill10Clicked);
            _btnPushDrillMax?.UnregisterCallback<ClickEvent>(OnPushDrillMaxClicked);
        }

        // ─────────────────────────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Shows the quarry drill UI and binds to the given controller.
        /// </summary>
        public void Show(QuarryMinigameController controller)
        {
            EnsureWired();
            if (!_wired) return;

            BindController(controller);
            RefreshUI();
            SetVisible(true);
        }

        /// <summary>
        /// Hides the quarry drill UI.
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
                if (!_wired || _quarryMinigameRoot == null) return false;
                return _quarryMinigameRoot.resolvedStyle.display == DisplayStyle.Flex;
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // Controller Binding
        // ─────────────────────────────────────────────────────────────────

        private void BindController(QuarryMinigameController controller)
        {
            UnbindController();

            boundController = controller;
            if (boundController == null) return;

            boundController.DrillDepthChanged += OnDrillDepthChanged;
            boundController.DrillEnergyChanged += OnDrillEnergyChanged;
            boundController.StrataUnlocked += OnStrataUnlocked;
        }

        private void UnbindController()
        {
            if (boundController != null)
            {
                boundController.DrillDepthChanged -= OnDrillDepthChanged;
                boundController.DrillEnergyChanged -= OnDrillEnergyChanged;
                boundController.StrataUnlocked -= OnStrataUnlocked;
                boundController = null;
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // Event Handlers (from Controller)
        // ─────────────────────────────────────────────────────────────────

        private void OnDrillDepthChanged(float depth)
        {
            UpdateDepthDisplay();
            UpdateStrataList();
            UpdateOutputSummary();
            UpdateDrillButtons();
        }

        private void OnDrillEnergyChanged(float energy)
        {
            UpdateEnergyDisplay();
            UpdateDrillButtons();
        }

        private void OnStrataUnlocked(string stratumName)
        {
            if (_drillResultLabel != null)
                _drillResultLabel.text = $"Unlocked: {stratumName}!";

            UpdateStrataList();
            UpdateOutputSummary();
        }

        // ─────────────────────────────────────────────────────────────────
        // UI Updates
        // ─────────────────────────────────────────────────────────────────

        private void RefreshUI()
        {
            UpdateDepthDisplay();
            UpdateEnergyDisplay();
            UpdateStrataList();
            UpdateOutputSummary();
            UpdateDrillButtons();

            if (_drillResultLabel != null)
                _drillResultLabel.text = "";
        }

        private void UpdateDepthDisplay()
        {
            if (boundController == null) return;

            float depth = boundController.DrillDepth;

            // Update depth label
            if (_drillDepthLabel != null)
                _drillDepthLabel.text = $"{depth:F0}m";

            // Update next stratum info
            var next = boundController.GetNextStratum();
            if (next.HasValue)
            {
                if (_nextStratumLabel != null)
                    _nextStratumLabel.text = $"Next: {next.Value.name}";
                if (_nextStratumDepthLabel != null)
                    _nextStratumDepthLabel.text = $"{next.Value.depth:F0}m";

                // Calculate progress toward next stratum
                if (_drillProgressBar != null)
                {
                    // Find current stratum depth
                    float currentStratumDepth = 0f;
                    if (boundController.HasGold) currentStratumDepth = 600f;
                    else if (boundController.HasIron) currentStratumDepth = 300f;
                    else if (boundController.HasCopper) currentStratumDepth = 100f;
                    else if (boundController.HasCoal) currentStratumDepth = 0f;

                    float range = next.Value.depth - currentStratumDepth;
                    float progress = range > 0 ? (depth - currentStratumDepth) / range * 100f : 0f;
                    _drillProgressBar.value = Mathf.Clamp(progress, 0f, 100f);
                }
            }
            else
            {
                // All strata unlocked
                if (_nextStratumLabel != null)
                    _nextStratumLabel.text = "All Unlocked!";
                if (_nextStratumDepthLabel != null)
                    _nextStratumDepthLabel.text = "-";
                if (_drillProgressBar != null)
                    _drillProgressBar.value = 100f;
            }
        }

        private void UpdateEnergyDisplay()
        {
            if (boundController == null || _energyProgressBar == null) return;

            _energyProgressBar.lowValue = 0f;
            _energyProgressBar.highValue = boundController.MaxDrillEnergy;
            _energyProgressBar.value = boundController.DrillEnergy;
            _energyProgressBar.title = $"Energy: {boundController.DrillEnergy:F0}/{boundController.MaxDrillEnergy:F0}";
        }

        private void UpdateStrataList()
        {
            if (boundController == null || _strataUnlockList == null) return;

            _strataUnlockList.Clear();

            // Stone is always unlocked
            AddStrataRow("Stone", 0f, true);

            // Coal (unlocks at 0m in the controller)
            AddStrataRow("Coal", 0f, boundController.HasCoal);

            // Copper
            AddStrataRow("Copper", 100f, boundController.HasCopper);

            // Iron
            AddStrataRow("Iron", 300f, boundController.HasIron);

            // Gold
            AddStrataRow("Gold", 600f, boundController.HasGold);
        }

        private void AddStrataRow(string name, float depth, bool unlocked)
        {
            string icon = unlocked ? "\u2713" : "\u2717"; // ✓ or ✗
            var label = new Label($"{icon} {name} ({depth:F0}m)");
            label.AddToClassList("qd-list-row");
            label.AddToClassList(unlocked ? "is-ok" : "is-locked");
            _strataUnlockList.Add(label);
        }

        private void UpdateOutputSummary()
        {
            if (boundController == null || _outputSummaryLabel == null) return;

            _outputSummaryLabel.text = boundController.GetStrataStatusSummary();
        }

        private void UpdateDrillButtons()
        {
            if (boundController == null) return;

            // Calculate energy cost for one drill action
            // Assuming 10 energy per drill (10m depth * 1 energy/m)
            float energyCostPerDrill = 10f;
            bool canDrill = boundController.DrillEnergy >= energyCostPerDrill;

            if (_btnPushDrill != null)
                _btnPushDrill.SetEnabled(canDrill);

            if (_btnPushDrill10 != null)
                _btnPushDrill10.SetEnabled(boundController.DrillEnergy >= energyCostPerDrill);

            if (_btnPushDrillMax != null)
                _btnPushDrillMax.SetEnabled(canDrill);
        }

        // ─────────────────────────────────────────────────────────────────
        // Button Callbacks
        // ─────────────────────────────────────────────────────────────────

        private void OnCloseClicked(ClickEvent evt)
        {
            Hide();
        }

        private void OnPushDrillClicked(ClickEvent evt)
        {
            if (boundController == null) return;

            bool success = boundController.PushDrill();
            if (!success && _drillResultLabel != null)
            {
                _drillResultLabel.text = "Not enough energy!";
            }
            else if (success && _drillResultLabel != null)
            {
                _drillResultLabel.text = $"Drilled to {boundController.DrillDepth:F0}m";
            }
        }

        private void OnPushDrill10Clicked(ClickEvent evt)
        {
            if (boundController == null) return;

            int drilled = boundController.PushDrillMultiple(10);
            if (_drillResultLabel != null)
            {
                if (drilled == 0)
                    _drillResultLabel.text = "Not enough energy!";
                else
                    _drillResultLabel.text = $"Drilled {drilled}x to {boundController.DrillDepth:F0}m";
            }
        }

        private void OnPushDrillMaxClicked(ClickEvent evt)
        {
            if (boundController == null) return;

            int drilled = boundController.PushDrillMultiple(1000); // Large number to drill as much as possible
            if (_drillResultLabel != null)
            {
                if (drilled == 0)
                    _drillResultLabel.text = "Not enough energy!";
                else
                    _drillResultLabel.text = $"Drilled {drilled}x to {boundController.DrillDepth:F0}m";
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // Visibility
        // ─────────────────────────────────────────────────────────────────

        private void SetVisible(bool visible)
        {
            if (_quarryMinigameRoot != null)
            {
                _quarryMinigameRoot.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }
    }
}
