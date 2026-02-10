// Assets/Minigames/HexWorld3D/Scripts/HexWorldBuildingSlotUI.cs
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GalacticFishing.Minigames.HexWorld
{
    public sealed class HexWorldBuildingSlotUI : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text label;
        [SerializeField] private Image icon;

        [Header("State Visuals (optional)")]
        [SerializeField] private GameObject selectedHighlight;
        [SerializeField] private GameObject lockedOverlay;

        public Button Button => button;
        public bool IsUnlocked { get; private set; }
        public HexWorldBuildingDefinition Definition { get; private set; }

        public void Bind(HexWorldBuildingDefinition def, bool unlocked)
        {
            Definition = def;
            SetUnlocked(unlocked);

            if (label)
            {
                string displayName = string.IsNullOrWhiteSpace(def.displayName) ? def.name : def.displayName;
                label.text = unlocked ? displayName : "???";
            }
            if (icon)
            {
                icon.sprite = def.icon;
                icon.enabled = def.icon != null;
            }

            SetSelectedVisual(false);
        }

        public void SetUnlocked(bool unlocked)
        {
            IsUnlocked = unlocked;

            if (lockedOverlay) lockedOverlay.SetActive(!unlocked);

            if (button)
            {
                button.interactable = unlocked;
            }
        }

        public void SetSelectedVisual(bool selected)
        {
            if (selectedHighlight) selectedHighlight.SetActive(selected);
        }
    }
}
