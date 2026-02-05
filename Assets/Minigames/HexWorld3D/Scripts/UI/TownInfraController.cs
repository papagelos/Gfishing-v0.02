// Assets/Minigames/HexWorld3D/Scripts/UI/TownInfraController.cs
using UnityEngine;
using UnityEngine.UIElements;
using GalacticFishing.Progress;

namespace GalacticFishing.Minigames.HexWorld
{
    /// <summary>
    /// Controller for the Town Infrastructure Panel (fullscreen UI).
    /// Displays IP, tier progression, caps, and handles Town Hall upgrades.
    /// </summary>
    public sealed class TownInfraController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The UIDocument component for this panel.")]
        [SerializeField] private UIDocument uiDocument;

        [Tooltip("Reference to the HexWorld controller for game state.")]
        [SerializeField] private HexWorld3DController hexWorldController;

        [Header("Tier Definitions")]
        [Tooltip("Array of TownTierDefinition assets (index 0 = T1, index 9 = T10).")]
        [SerializeField] private TownTierDefinition[] tierDefinitions;

        // UI Elements - Left Column (Infrastructure)
        private Label _infraPointsLabel;
        private Label _infraTierLabel;
        private ProgressBar _infraProgressBar;
        private Label _infraProgressHintLabel;

        // UI Elements - Left Column (Caps)
        private Label _capTilesLabel;
        private Label _capBuildingsLabel;
        private Label _capActiveLabel;

        // UI Elements - Right Column (Town Hall Status)
        private Label _townHallLevelLabel;
        private Label _townHallNextLabel;
        private Label _townHallRequirementHint;
        private ScrollView _nextTierRequirementsList;
        private Button _btnUpgradeTownHall;
        private Button _btnClose;

        // UI Elements - Root
        private VisualElement _root;
        private VisualElement _backdrop;

        // Polling
        private float _nextPollTime;
        private const float PollInterval = 0.2f; // 5 times per second

        // Visibility state
        public bool IsVisible => _root != null && _root.resolvedStyle.display != DisplayStyle.None;

        private void OnEnable()
        {
            if (uiDocument == null)
            {
                uiDocument = GetComponent<UIDocument>();
            }

            if (uiDocument == null)
            {
                Debug.LogError("[TownInfraController] Missing UIDocument component.");
                return;
            }

            QueryElements();
            RegisterCallbacks();
            Hide(); // Start hidden
        }

        private void OnDisable()
        {
            UnregisterCallbacks();
        }

        private void Update()
        {
            if (!IsVisible) return;

            // Polling refresh
            if (Time.unscaledTime >= _nextPollTime)
            {
                _nextPollTime = Time.unscaledTime + PollInterval;
                RefreshAll();
            }
        }

        private void QueryElements()
        {
            var root = uiDocument.rootVisualElement;

            _root = root.Q<VisualElement>("TownInfraRoot");
            _backdrop = root.Q<VisualElement>("TownInfraBackdrop");

            // Infrastructure card
            _infraPointsLabel = root.Q<Label>("InfraPointsLabel");
            _infraTierLabel = root.Q<Label>("InfraTierLabel");
            _infraProgressBar = root.Q<ProgressBar>("InfraProgressBar");
            _infraProgressHintLabel = root.Q<Label>("InfraProgressHintLabel");

            // Caps card
            _capTilesLabel = root.Q<Label>("CapTilesLabel");
            _capBuildingsLabel = root.Q<Label>("CapBuildingsLabel");
            _capActiveLabel = root.Q<Label>("CapActiveLabel");

            // Town Hall Status card
            _townHallLevelLabel = root.Q<Label>("TownHallLevelLabel");
            _townHallNextLabel = root.Q<Label>("TownHallNextLabel");
            _townHallRequirementHint = root.Q<Label>("TownHallRequirementHint");
            _nextTierRequirementsList = root.Q<ScrollView>("NextTierRequirementsList");
            _btnUpgradeTownHall = root.Q<Button>("Btn_UpgradeTownHall");
            _btnClose = root.Q<Button>("Btn_Close");
        }

        private void RegisterCallbacks()
        {
            if (_btnClose != null)
                _btnClose.clicked += Hide;

            if (_btnUpgradeTownHall != null)
                _btnUpgradeTownHall.clicked += OnUpgradeTownHallClicked;

            if (_backdrop != null)
                _backdrop.RegisterCallback<ClickEvent>(OnBackdropClicked);
        }

        private void UnregisterCallbacks()
        {
            if (_btnClose != null)
                _btnClose.clicked -= Hide;

            if (_btnUpgradeTownHall != null)
                _btnUpgradeTownHall.clicked -= OnUpgradeTownHallClicked;

            if (_backdrop != null)
                _backdrop.UnregisterCallback<ClickEvent>(OnBackdropClicked);
        }

        private void OnBackdropClicked(ClickEvent evt)
        {
            Hide();
        }

        private void OnUpgradeTownHallClicked()
        {
            if (hexWorldController == null)
            {
                Debug.LogWarning("[TownInfraController] No HexWorld controller assigned.");
                return;
            }

            // Call TryUpgradeTownHall on the controller
            hexWorldController.TryUpgradeTownHall();
            RefreshAll();
        }

        /// <summary>
        /// Show the panel and refresh data.
        /// </summary>
        public void Show()
        {
            if (_root != null)
            {
                _root.style.display = DisplayStyle.Flex;
                RefreshAll();
            }
        }

        /// <summary>
        /// Hide the panel.
        /// </summary>
        public void Hide()
        {
            if (_root != null)
            {
                _root.style.display = DisplayStyle.None;
            }
        }

        /// <summary>
        /// Toggle panel visibility.
        /// </summary>
        public void Toggle()
        {
            if (IsVisible)
                Hide();
            else
                Show();
        }

        /// <summary>
        /// Refresh all displayed data.
        /// </summary>
        public void RefreshAll()
        {
            RefreshInfrastructure();
            RefreshCaps();
            RefreshTownHallStatus();
        }

        private void RefreshInfrastructure()
        {
            long currentIP = PlayerProgressManager.Instance?.InfrastructurePoints ?? 0;
            int currentTier = GetCurrentTier();
            var nextTierDef = GetTierDefinition(currentTier + 1);

            // IP with thousands separator
            if (_infraPointsLabel != null)
                _infraPointsLabel.text = currentIP.ToString("N0");

            // Tier label
            if (_infraTierLabel != null)
                _infraTierLabel.text = $"Town Tier {currentTier}";

            // Progress bar toward next tier
            if (_infraProgressBar != null)
            {
                if (nextTierDef != null)
                {
                    long prevIP = GetIPRequiredForTier(currentTier);
                    long nextIP = nextTierDef.ipRequired;
                    long range = nextIP - prevIP;

                    if (range > 0)
                    {
                        float progress = Mathf.Clamp01((float)(currentIP - prevIP) / range);
                        _infraProgressBar.value = progress * 100f;
                        _infraProgressBar.title = $"Next Tier: {currentIP:N0} / {nextIP:N0}";
                    }
                    else
                    {
                        _infraProgressBar.value = 100f;
                        _infraProgressBar.title = "Max Tier Reached";
                    }
                }
                else
                {
                    // At max tier
                    _infraProgressBar.value = 100f;
                    _infraProgressBar.title = "Max Tier Reached";
                }
            }

            // Hint label
            if (_infraProgressHintLabel != null)
            {
                if (nextTierDef != null)
                {
                    long needed = nextTierDef.ipRequired - currentIP;
                    if (needed > 0)
                        _infraProgressHintLabel.text = $"Need {needed:N0} more IP to unlock Tier {currentTier + 1}.";
                    else
                        _infraProgressHintLabel.text = "Ready to upgrade Town Hall!";
                }
                else
                {
                    _infraProgressHintLabel.text = "Maximum tier reached.";
                }
            }
        }

        private void RefreshCaps()
        {
            if (hexWorldController == null) return;

            int currentTier = GetCurrentTier();
            var tierDef = GetTierDefinition(currentTier);

            // Tiles
            int tilesPlaced = hexWorldController.TilesPlaced;
            int tileCap = hexWorldController.TileCapacityMax;
            if (_capTilesLabel != null)
                _capTilesLabel.text = $"Tiles: {tilesPlaced} / {tileCap}";

            // Buildings - use tile cap as proxy if no building cap defined
            // For now, buildings share the same cap concept as active slots
            int buildingsPlaced = hexWorldController.BuildingsPlaced;
            int buildingCap = tierDef?.buildingCap ?? tileCap;
            if (_capBuildingsLabel != null)
                _capBuildingsLabel.text = $"Buildings: {buildingsPlaced} / {buildingCap}";

            // Active Producers
            int activeUsed = hexWorldController.ActiveBuildingsUsed;
            int activeCap = hexWorldController.ActiveSlotsTotal;
            if (_capActiveLabel != null)
                _capActiveLabel.text = $"Active Producers: {activeUsed} / {activeCap}";
        }

        private void RefreshTownHallStatus()
        {
            int currentTier = GetCurrentTier();
            var nextTierDef = GetTierDefinition(currentTier + 1);
            long currentIP = PlayerProgressManager.Instance?.InfrastructurePoints ?? 0;

            // Town Hall level
            if (_townHallLevelLabel != null)
                _townHallLevelLabel.text = $"Town Hall: T{currentTier}";

            // Next tier
            if (_townHallNextLabel != null)
            {
                if (nextTierDef != null)
                    _townHallNextLabel.text = $"Next: T{currentTier + 1}";
                else
                    _townHallNextLabel.text = "Max";
            }

            // Requirement hint
            if (_townHallRequirementHint != null)
            {
                if (nextTierDef != null)
                    _townHallRequirementHint.text = "Upgrade requires Infrastructure threshold.";
                else
                    _townHallRequirementHint.text = "Town Hall is at maximum level.";
            }

            // Upgrade button state
            if (_btnUpgradeTownHall != null)
            {
                bool canUpgrade = nextTierDef != null && currentIP >= nextTierDef.ipRequired;
                _btnUpgradeTownHall.SetEnabled(canUpgrade);

                if (nextTierDef == null)
                    _btnUpgradeTownHall.text = "Max Level";
                else if (canUpgrade)
                    _btnUpgradeTownHall.text = "Upgrade Town Hall";
                else
                    _btnUpgradeTownHall.text = $"Need {nextTierDef.ipRequired:N0} IP";
            }

            // Update requirements list
            RefreshNextTierRequirements(currentTier, nextTierDef, currentIP);
        }

        private void RefreshNextTierRequirements(int currentTier, TownTierDefinition nextTierDef, long currentIP)
        {
            if (_nextTierRequirementsList == null) return;

            _nextTierRequirementsList.Clear();

            if (nextTierDef == null)
            {
                var maxLabel = new Label("• Maximum tier reached.");
                maxLabel.AddToClassList("ti-small");
                _nextTierRequirementsList.Add(maxLabel);
                return;
            }

            // IP requirement
            string ipStatus = currentIP >= nextTierDef.ipRequired ? "(Done)" : "";
            var ipLabel = new Label($"• Infrastructure: {currentIP:N0} / {nextTierDef.ipRequired:N0} {ipStatus}");
            ipLabel.AddToClassList("ti-small");
            _nextTierRequirementsList.Add(ipLabel);

            // Placeholder for future milestone requirements
            var placeholderLabel = new Label("• Additional milestones: Coming soon");
            placeholderLabel.AddToClassList("ti-small");
            _nextTierRequirementsList.Add(placeholderLabel);
        }

        /// <summary>
        /// Get the current town tier based on Town Hall level.
        /// </summary>
        private int GetCurrentTier()
        {
            if (hexWorldController != null)
                return hexWorldController.TownHallLevel;

            return 1;
        }

        /// <summary>
        /// Get the TownTierDefinition for the specified tier.
        /// Returns null if tier is out of range.
        /// </summary>
        private TownTierDefinition GetTierDefinition(int tier)
        {
            if (tierDefinitions == null || tierDefinitions.Length == 0)
                return null;

            int index = tier - 1; // Tier 1 = index 0
            if (index < 0 || index >= tierDefinitions.Length)
                return null;

            return tierDefinitions[index];
        }

        /// <summary>
        /// Get the IP required for a given tier (cumulative).
        /// </summary>
        private long GetIPRequiredForTier(int tier)
        {
            var def = GetTierDefinition(tier);
            return def?.ipRequired ?? 0;
        }
    }
}
