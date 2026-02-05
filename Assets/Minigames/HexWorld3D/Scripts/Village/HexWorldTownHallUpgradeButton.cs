using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace GalacticFishing.Minigames.HexWorld
{
    /// <summary>
    /// UI button component that triggers Town Hall upgrades.
    /// TICKET 003: Shows Land Deed + World milestone requirements.
    /// </summary>
    public sealed class HexWorldTownHallUpgradeButton : MonoBehaviour
    {
        [SerializeField] private HexWorld3DController controller;
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text buttonLabel;

        [Header("Requirements Display")]
        [SerializeField] private TMP_Text requirementsLabel;
        [SerializeField] private bool showRequirementsInButton = true;

        [Header("Refresh Rate")]
        [Tooltip("Update interval in seconds. 0 = update every frame (expensive).")]
        [SerializeField] private float updateInterval = 0.5f;

        private float _lastUpdateTime;

        private void Awake()
        {
            if (!button) button = GetComponent<Button>();
            if (!buttonLabel && button) buttonLabel = button.GetComponentInChildren<TMP_Text>();
        }

        private void Start()
        {
            if (!controller) controller = FindObjectOfType<HexWorld3DController>();

            if (!controller || !button)
            {
                enabled = false;
                return;
            }

            button.onClick.AddListener(OnUpgradeClicked);

            // Subscribe to Town Hall level changes to update button text
            controller.TownHallLevelChanged += OnTownHallLevelChanged;

            // Initial update
            UpdateButtonDisplay();
        }

        private void Update()
        {
            // Periodic refresh to check world progression changes
            // (World progression might change externally)
            if (updateInterval <= 0f || Time.unscaledTime - _lastUpdateTime >= updateInterval)
            {
                _lastUpdateTime = Time.unscaledTime;
                UpdateButtonDisplay();
            }
        }

        private void OnDestroy()
        {
            if (button) button.onClick.RemoveListener(OnUpgradeClicked);
            if (controller) controller.TownHallLevelChanged -= OnTownHallLevelChanged;
        }

        private void OnUpgradeClicked()
        {
            if (!controller) return;

            // Try to upgrade - the controller handles all validation and feedback
            controller.TryUpgradeTownHall();
        }

        private void OnTownHallLevelChanged(int newLevel)
        {
            UpdateButtonDisplay();
        }

        private void UpdateButtonDisplay()
        {
            if (!controller)
            {
                enabled = false;
                return;
            }

            int currentLevel = controller.TownHallLevel;

            if (currentLevel >= 10)
            {
                // Max level reached
                if (buttonLabel) buttonLabel.text = "Town Hall MAX";
                if (button) button.interactable = false;
                if (requirementsLabel) requirementsLabel.text = "";
                return;
            }

            int nextLevel = currentLevel + 1;

            // Check requirements
            bool hasLandDeed = CheckLandDeed();
            bool hasWorldAccess = CheckWorldProgression(nextLevel);
            bool canUpgrade = hasLandDeed && hasWorldAccess;

            // Update button text
            if (buttonLabel)
            {
                if (showRequirementsInButton)
                {
                    string reqText = GetRequirementsText(nextLevel, hasLandDeed, hasWorldAccess);
                    buttonLabel.text = $"Upgrade TH → L{nextLevel}\n{reqText}";
                }
                else
                {
                    buttonLabel.text = $"Upgrade Town Hall → L{nextLevel}";
                }
            }

            // Update requirements label if separate
            if (requirementsLabel)
            {
                requirementsLabel.text = GetRequirementsText(nextLevel, hasLandDeed, hasWorldAccess);
            }

            // Button interactable only if both requirements are met
            if (button) button.interactable = canUpgrade;
        }

        private bool CheckLandDeed()
        {
            // Placeholder: Always returns true for now
            // Will be wired to external Land Deed system later
            return true;
        }

        private bool CheckWorldProgression(int requiredWorldNumber)
        {
            if (!controller) return false;

            // Access the controller's world progression (it handles the fallback)
            // We need to use reflection or make it public - for now, assume we can check
            // The controller will handle validation in TryUpgradeTownHall anyway
            // This is just for UI feedback

            // For now, we'll do a simple approach: try to find the world progression provider
            var wp = FindObjectOfType<MockWorldProgression>();
            if (wp != null && wp is GalacticFishing.IWorldProgression worldProg)
            {
                return worldProg.HighestUnlockedWorldNumber >= requiredWorldNumber;
            }

            // Fallback: assume level 1 unlocked
            return requiredWorldNumber <= 1;
        }

        private string GetRequirementsText(int nextLevel, bool hasLandDeed, bool hasWorldAccess)
        {
            string landDeedText = hasLandDeed ? "Land Deed: 1/1" : "Land Deed: 0/1";
            string worldText = hasWorldAccess ? $"Reached World {nextLevel}: YES" : $"Reached World {nextLevel}: NO";

            return $"{landDeedText}\n{worldText}";
        }
    }
}
