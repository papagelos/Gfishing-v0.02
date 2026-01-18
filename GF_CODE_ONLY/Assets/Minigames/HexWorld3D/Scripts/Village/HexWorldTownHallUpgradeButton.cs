using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace GalacticFishing.Minigames.HexWorld
{
    /// <summary>
    /// UI button component that triggers Town Hall upgrades.
    /// Shows the upgrade cost and handles the upgrade action.
    /// </summary>
    public sealed class HexWorldTownHallUpgradeButton : MonoBehaviour
    {
        [SerializeField] private HexWorld3DController controller;
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text buttonLabel;

        [Header("Optional Cost Display")]
        [SerializeField] private TMP_Text costLabel;
        [SerializeField] private bool showCostInButton = true;

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
            UpdateButtonDisplay(controller.TownHallLevel);
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
            UpdateButtonDisplay(newLevel);
        }

        private void UpdateButtonDisplay(int currentLevel)
        {
            if (currentLevel >= 10)
            {
                // Max level reached
                if (buttonLabel) buttonLabel.text = "Town Hall MAX";
                if (button) button.interactable = false;
                if (costLabel) costLabel.text = "";
                return;
            }

            int nextLevel = currentLevel + 1;

            if (showCostInButton && buttonLabel)
            {
                string costText = GetUpgradeCostText(nextLevel);
                buttonLabel.text = $"Upgrade TH → L{nextLevel}\n{costText}";
            }
            else if (buttonLabel)
            {
                buttonLabel.text = $"Upgrade Town Hall → L{nextLevel}";
            }

            if (costLabel)
            {
                costLabel.text = GetUpgradeCostText(nextLevel);
            }

            if (button) button.interactable = true;
        }

        private string GetUpgradeCostText(int nextLevel)
        {
            // Match the costs defined in HexWorld3DController.TryUpgradeTownHall()
            switch (nextLevel)
            {
                case 2: return "20W 15S 10F 100c";
                case 3: return "40W 30S 20F 200c";
                case 4: return "60W 45S 30F 300c";
                case 5: return "80W 60S 40F 400c";
                case 6: return "100W 75S 50F 500c";
                case 7: return "120W 90S 60F 600c";
                case 8: return "140W 105S 70F 700c";
                case 9: return "160W 120S 80F 800c";
                case 10: return "200W 150S 100F 1000c";
                default: return "";
            }
        }
    }
}
