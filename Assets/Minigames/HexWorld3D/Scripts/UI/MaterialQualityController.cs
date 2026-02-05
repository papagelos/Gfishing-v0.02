// Assets/Minigames/HexWorld3D/Scripts/UI/MaterialQualityController.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using GalacticFishing.Progress;

namespace GalacticFishing.Minigames.HexWorld
{
    /// <summary>
    /// Controller for the Material Quality Panel.
    /// Handles deterministic QP allocation to raise material quality levels.
    /// </summary>
    public sealed class MaterialQualityController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private HexWorld3DController hexWorldController;

        [Header("Tier Definitions")]
        [Tooltip("Array of TownTierDefinition assets (index 0 = T1, index 9 = T10).")]
        [SerializeField] private TownTierDefinition[] tierDefinitions;

        [Header("Material Definitions")]
        [Tooltip("List of material IDs that can be upgraded.")]
        [SerializeField] private string[] materialIds = { "Wood", "Stone", "Fiber", "Clay", "Metal", "Herbs" };

        // UI Elements - Points Card
        private Label _unspentPointsLabel;
        private Label _townTierPill;
        private Label _unspentPointsHint;

        // UI Elements - Materials List
        private ScrollView _materialList;
        private TextField _materialSearchField;

        // UI Elements - Selected Material
        private Label _selectedMaterialName;
        private Label _selectedMaterialQuality;
        private Label _selectedMaterialCap;
        private Label _selectedMaterialDesc;

        // UI Elements - Progress
        private Label _progressNumericLabel;
        private Label _nextQualityLabel;
        private ProgressBar _selectedMaterialProgressBar;
        private Label _progressHintLabel;

        // UI Elements - Allocation
        private Label _allocateModeHint;
        private Label _allocatePreviewDelta;
        private Button _btnAdd1, _btnAdd10, _btnAdd50, _btnAddMax;
        private TextField _customAmountField;
        private Button _btnApplyCustom, _btnClearPending;
        private Button _btnApplySpend, _btnUndoLast;
        private Label _allocationResultLine;

        // UI Elements - Cap Card
        private Label _capSummaryLabel;
        private Label _capNextHintLabel;
        private ScrollView _capRequirementsList;

        // UI Elements - Log
        private ScrollView _recentQualityLog;

        // UI Elements - Root & Controls
        private VisualElement _root;
        private VisualElement _backdrop;
        private Button _btnClose;

        // State
        private string _selectedMaterialId;
        private long _pendingQP;
        private readonly List<string> _activityLog = new();

        // Polling
        private float _nextPollTime;
        private const float PollInterval = 0.2f;

        public bool IsVisible => _root != null && _root.resolvedStyle.display != DisplayStyle.None;

        private void OnEnable()
        {
            if (uiDocument == null)
                uiDocument = GetComponent<UIDocument>();

            if (uiDocument == null)
            {
                Debug.LogError("[MaterialQualityController] Missing UIDocument.");
                return;
            }

            QueryElements();
            RegisterCallbacks();
            PopulateMaterialList();
            Hide();
        }

        private void OnDisable()
        {
            UnregisterCallbacks();
        }

        private void Update()
        {
            if (!IsVisible) return;

            if (Time.unscaledTime >= _nextPollTime)
            {
                _nextPollTime = Time.unscaledTime + PollInterval;
                RefreshAll();
            }
        }

        private void QueryElements()
        {
            var root = uiDocument.rootVisualElement;

            _root = root.Q<VisualElement>("MaterialQualityRoot");
            _backdrop = root.Q<VisualElement>("MaterialQualityBackdrop");
            _btnClose = root.Q<Button>("Btn_Close");

            // Points Card
            _unspentPointsLabel = root.Q<Label>("UnspentPointsLabel");
            _townTierPill = root.Q<Label>("TownTierPill");
            _unspentPointsHint = root.Q<Label>("UnspentPointsHint");

            // Materials List
            _materialList = root.Q<ScrollView>("MaterialList");
            _materialSearchField = root.Q<TextField>("MaterialSearchField");

            // Selected Material
            _selectedMaterialName = root.Q<Label>("SelectedMaterialName");
            _selectedMaterialQuality = root.Q<Label>("SelectedMaterialQuality");
            _selectedMaterialCap = root.Q<Label>("SelectedMaterialCap");
            _selectedMaterialDesc = root.Q<Label>("SelectedMaterialDesc");

            // Progress
            _progressNumericLabel = root.Q<Label>("ProgressNumericLabel");
            _nextQualityLabel = root.Q<Label>("NextQualityLabel");
            _selectedMaterialProgressBar = root.Q<ProgressBar>("SelectedMaterialProgressBar");
            _progressHintLabel = root.Q<Label>("ProgressHintLabel");

            // Allocation
            _allocateModeHint = root.Q<Label>("AllocateModeHint");
            _allocatePreviewDelta = root.Q<Label>("AllocatePreviewDelta");
            _btnAdd1 = root.Q<Button>("Btn_Add1");
            _btnAdd10 = root.Q<Button>("Btn_Add10");
            _btnAdd50 = root.Q<Button>("Btn_Add50");
            _btnAddMax = root.Q<Button>("Btn_AddMax");
            _customAmountField = root.Q<TextField>("CustomAmountField");
            _btnApplyCustom = root.Q<Button>("Btn_ApplyCustom");
            _btnClearPending = root.Q<Button>("Btn_ClearPending");
            _btnApplySpend = root.Q<Button>("Btn_ApplySpend");
            _btnUndoLast = root.Q<Button>("Btn_UndoLast");
            _allocationResultLine = root.Q<Label>("AllocationResultLine");

            // Cap Card
            _capSummaryLabel = root.Q<Label>("CapSummaryLabel");
            _capNextHintLabel = root.Q<Label>("CapNextHintLabel");
            _capRequirementsList = root.Q<ScrollView>("CapRequirementsList");

            // Log
            _recentQualityLog = root.Q<ScrollView>("RecentQualityLog");
        }

        private void RegisterCallbacks()
        {
            _btnClose?.RegisterCallback<ClickEvent>(_ => Hide());
            _backdrop?.RegisterCallback<ClickEvent>(_ => Hide());

            _btnAdd1?.RegisterCallback<ClickEvent>(_ => AddPendingQP(1));
            _btnAdd10?.RegisterCallback<ClickEvent>(_ => AddPendingQP(10));
            _btnAdd50?.RegisterCallback<ClickEvent>(_ => AddPendingQP(50));
            _btnAddMax?.RegisterCallback<ClickEvent>(_ => AddPendingQPMax());

            _btnApplyCustom?.RegisterCallback<ClickEvent>(_ => ApplyCustomAmount());
            _btnClearPending?.RegisterCallback<ClickEvent>(_ => ClearPending());
            _btnApplySpend?.RegisterCallback<ClickEvent>(_ => ApplySpend());
            _btnUndoLast?.RegisterCallback<ClickEvent>(_ => { /* Future: undo support */ });

            _materialSearchField?.RegisterValueChangedCallback(evt => FilterMaterialList(evt.newValue));
        }

        private void UnregisterCallbacks()
        {
            // UI Toolkit handles cleanup automatically when document is disabled
        }

        private void PopulateMaterialList()
        {
            if (_materialList == null || materialIds == null) return;

            _materialList.Clear();

            foreach (var matId in materialIds)
            {
                var btn = new Button { text = matId, name = $"MatBtn_{matId}" };
                btn.AddToClassList("mq-list-item");
                btn.clicked += () => SelectMaterial(matId);
                _materialList.Add(btn);
            }

            // Select first material by default
            if (materialIds.Length > 0)
                SelectMaterial(materialIds[0]);
        }

        private void FilterMaterialList(string filter)
        {
            if (_materialList == null) return;

            foreach (var child in _materialList.Children())
            {
                if (child is Button btn)
                {
                    bool match = string.IsNullOrEmpty(filter) ||
                                 btn.text.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
                    btn.style.display = match ? DisplayStyle.Flex : DisplayStyle.None;
                }
            }
        }

        private void SelectMaterial(string materialId)
        {
            _selectedMaterialId = materialId;
            _pendingQP = 0;

            // Update selection visual
            if (_materialList != null)
            {
                foreach (var child in _materialList.Children())
                {
                    if (child is Button btn)
                    {
                        bool selected = btn.text == materialId;
                        btn.EnableInClassList("is-selected", selected);
                    }
                }
            }

            RefreshSelectedMaterial();
        }

        public void Show()
        {
            if (_root != null)
            {
                _root.style.display = DisplayStyle.Flex;
                _pendingQP = 0;
                RefreshAll();
            }
        }

        public void Hide()
        {
            if (_root != null)
            {
                _root.style.display = DisplayStyle.None;
            }
        }

        public void Toggle()
        {
            if (IsVisible) Hide();
            else Show();
        }

        public void RefreshAll()
        {
            RefreshPointsCard();
            RefreshSelectedMaterial();
            RefreshAllocationPreview();
            RefreshCapCard();
            RefreshLog();
        }

        private void RefreshPointsCard()
        {
            long unspentQP = PlayerProgressManager.Instance?.UnspentQP ?? 0;
            int tier = GetCurrentTier();

            if (_unspentPointsLabel != null)
                _unspentPointsLabel.text = unspentQP.ToString("N0");

            if (_townTierPill != null)
                _townTierPill.text = $"Town Tier {tier}";

            if (_unspentPointsHint != null)
            {
                long available = unspentQP - _pendingQP;
                _unspentPointsHint.text = _pendingQP > 0
                    ? $"Available after pending: {available:N0}"
                    : "Unspent points can be allocated to any unlocked material.";
            }
        }

        private void RefreshSelectedMaterial()
        {
            if (string.IsNullOrEmpty(_selectedMaterialId)) return;

            var pm = PlayerProgressManager.Instance;
            int quality = pm?.GetMaterialQuality(_selectedMaterialId) ?? 0;
            float progress = pm?.GetMaterialProgress(_selectedMaterialId) ?? 0f;
            int mqCap = GetMQCap();

            // Selected Material Info
            if (_selectedMaterialName != null)
                _selectedMaterialName.text = _selectedMaterialId;

            if (_selectedMaterialQuality != null)
                _selectedMaterialQuality.text = $"Q {quality}";

            if (_selectedMaterialCap != null)
                _selectedMaterialCap.text = $"Cap: Q {mqCap}";

            if (_selectedMaterialDesc != null)
                _selectedMaterialDesc.text = "Quality affects production and crafting outputs using this material.";

            // Progress
            int qpForNextLevel = GetQPRequiredForLevel(quality + 1);
            int currentProgressQP = Mathf.RoundToInt(progress * qpForNextLevel);

            if (_progressNumericLabel != null)
                _progressNumericLabel.text = $"{currentProgressQP} / {qpForNextLevel}";

            if (_nextQualityLabel != null)
            {
                if (quality >= mqCap)
                    _nextQualityLabel.text = "At Cap";
                else
                    _nextQualityLabel.text = $"Next: Q {quality + 1}";
            }

            if (_selectedMaterialProgressBar != null)
            {
                _selectedMaterialProgressBar.value = progress * 100f;
                _selectedMaterialProgressBar.title = quality >= mqCap ? "At Quality Cap" : "Progress";
            }

            if (_progressHintLabel != null)
            {
                if (quality >= mqCap)
                    _progressHintLabel.text = "Raise Town Tier to increase the quality cap.";
                else
                    _progressHintLabel.text = $"Need {qpForNextLevel - currentProgressQP} more QP for +1 quality.";
            }
        }

        private void RefreshAllocationPreview()
        {
            if (_allocatePreviewDelta != null)
            {
                if (_pendingQP > 0)
                    _allocatePreviewDelta.text = $"+{_pendingQP} pending";
                else
                    _allocatePreviewDelta.text = "+0 this action";
            }

            // Enable/disable apply button
            if (_btnApplySpend != null)
            {
                bool canApply = _pendingQP > 0 && !string.IsNullOrEmpty(_selectedMaterialId);
                _btnApplySpend.SetEnabled(canApply);
            }

            if (_allocationResultLine != null)
            {
                if (_pendingQP > 0)
                {
                    int levelsGained = PreviewLevelsGained();
                    _allocationResultLine.text = levelsGained > 0
                        ? $"Preview: +{levelsGained} quality level(s)"
                        : "Preview: Progress will increase";
                }
                else
                {
                    _allocationResultLine.text = "";
                }
            }
        }

        private void RefreshCapCard()
        {
            int tier = GetCurrentTier();
            int mqCap = GetMQCap();

            if (_capSummaryLabel != null)
                _capSummaryLabel.text = $"Current MQ cap: Q{mqCap} (Town Tier {tier})";

            if (_capNextHintLabel != null)
            {
                var nextTierDef = GetTierDefinition(tier + 1);
                if (nextTierDef != null)
                    _capNextHintLabel.text = $"Next tier cap: Q{nextTierDef.baselineMQCap}";
                else
                    _capNextHintLabel.text = "Maximum tier reached.";
            }

            if (_capRequirementsList != null)
            {
                _capRequirementsList.Clear();
                var nextTierDef = GetTierDefinition(tier + 1);
                if (nextTierDef != null)
                {
                    long currentIP = PlayerProgressManager.Instance?.InfrastructurePoints ?? 0;
                    var label = new Label($"• Infrastructure: {currentIP:N0} / {nextTierDef.ipRequired:N0}");
                    label.AddToClassList("mq-small");
                    _capRequirementsList.Add(label);
                }
                else
                {
                    var label = new Label("• Maximum tier reached.");
                    label.AddToClassList("mq-small");
                    _capRequirementsList.Add(label);
                }
            }
        }

        private void RefreshLog()
        {
            if (_recentQualityLog == null) return;

            _recentQualityLog.Clear();
            int count = Mathf.Min(_activityLog.Count, 10);
            for (int i = _activityLog.Count - 1; i >= _activityLog.Count - count && i >= 0; i--)
            {
                var label = new Label(_activityLog[i]);
                label.AddToClassList("mq-log-row");
                _recentQualityLog.Add(label);
            }
        }

        private void AddPendingQP(long amount)
        {
            long available = (PlayerProgressManager.Instance?.UnspentQP ?? 0) - _pendingQP;
            _pendingQP += Math.Min(amount, available);
            RefreshAllocationPreview();
        }

        private void AddPendingQPMax()
        {
            long available = (PlayerProgressManager.Instance?.UnspentQP ?? 0) - _pendingQP;
            _pendingQP += available;
            RefreshAllocationPreview();
        }

        private void ApplyCustomAmount()
        {
            if (_customAmountField == null) return;

            if (long.TryParse(_customAmountField.value, out long amount) && amount > 0)
            {
                AddPendingQP(amount);
            }
        }

        private void ClearPending()
        {
            _pendingQP = 0;
            RefreshAllocationPreview();
        }

        private void ApplySpend()
        {
            if (_pendingQP <= 0 || string.IsNullOrEmpty(_selectedMaterialId)) return;

            var pm = PlayerProgressManager.Instance;
            if (pm == null) return;

            // Subtract QP first
            if (!pm.SubtractQP(_pendingQP))
            {
                LogActivity($"Failed to spend {_pendingQP} QP (insufficient)");
                return;
            }

            // Calculate progress to add
            int mqCap = GetMQCap();
            int currentQuality = pm.GetMaterialQuality(_selectedMaterialId);
            float currentProgress = pm.GetMaterialProgress(_selectedMaterialId);

            long remainingQP = _pendingQP;
            int levelsGained = 0;

            while (remainingQP > 0 && currentQuality < mqCap)
            {
                int qpForLevel = GetQPRequiredForLevel(currentQuality + 1);
                float progressNeeded = 1f - currentProgress;
                long qpNeeded = Mathf.CeilToInt(progressNeeded * qpForLevel);

                if (remainingQP >= qpNeeded)
                {
                    // Complete this level
                    remainingQP -= qpNeeded;
                    currentQuality++;
                    currentProgress = 0f;
                    levelsGained++;
                }
                else
                {
                    // Partial progress
                    float progressGain = (float)remainingQP / qpForLevel;
                    currentProgress += progressGain;
                    remainingQP = 0;
                }
            }

            // Save the result
            pm.SetMaterialQuality(_selectedMaterialId, currentQuality, currentProgress);

            // Log activity
            LogActivity($"Spent {_pendingQP} QP on {_selectedMaterialId}");
            if (levelsGained > 0)
            {
                LogActivity($"{_selectedMaterialId} reached Q{currentQuality}!");
            }

            _pendingQP = 0;
            RefreshAll();
        }

        private int PreviewLevelsGained()
        {
            if (_pendingQP <= 0 || string.IsNullOrEmpty(_selectedMaterialId)) return 0;

            var pm = PlayerProgressManager.Instance;
            if (pm == null) return 0;

            int mqCap = GetMQCap();
            int currentQuality = pm.GetMaterialQuality(_selectedMaterialId);
            float currentProgress = pm.GetMaterialProgress(_selectedMaterialId);

            long remainingQP = _pendingQP;
            int levelsGained = 0;

            while (remainingQP > 0 && currentQuality < mqCap)
            {
                int qpForLevel = GetQPRequiredForLevel(currentQuality + 1);
                float progressNeeded = 1f - currentProgress;
                long qpNeeded = Mathf.CeilToInt(progressNeeded * qpForLevel);

                if (remainingQP >= qpNeeded)
                {
                    remainingQP -= qpNeeded;
                    currentQuality++;
                    currentProgress = 0f;
                    levelsGained++;
                }
                else
                {
                    break;
                }
            }

            return levelsGained;
        }

        private void LogActivity(string message)
        {
            _activityLog.Add($"• {message}");
            if (_activityLog.Count > 50)
                _activityLog.RemoveAt(0);
        }

        #region QP Threshold Curve

        /// <summary>
        /// Returns the QP required to gain +1 quality at the target level.
        /// Aggressive Threshold Curve:
        /// Q1–20: 10 QP | Q21–50: 25 QP | Q51–80: 60 QP | Q81–95: 120 QP | Q96–100: 250 QP
        /// </summary>
        public static int GetQPRequiredForLevel(int targetQuality)
        {
            if (targetQuality <= 20) return 10;
            if (targetQuality <= 50) return 25;
            if (targetQuality <= 80) return 60;
            if (targetQuality <= 95) return 120;
            return 250; // Q96-100
        }

        #endregion

        #region Tier Helpers

        private int GetCurrentTier()
        {
            return hexWorldController?.TownHallLevel ?? 1;
        }

        private int GetMQCap()
        {
            var tierDef = GetTierDefinition(GetCurrentTier());
            return tierDef?.baselineMQCap ?? 10;
        }

        private TownTierDefinition GetTierDefinition(int tier)
        {
            if (tierDefinitions == null || tierDefinitions.Length == 0)
                return null;

            int index = tier - 1;
            if (index < 0 || index >= tierDefinitions.Length)
                return null;

            return tierDefinitions[index];
        }

        #endregion
    }
}
