// Assets/Minigames/HexWorld3D/Scripts/Village/HexWorldActiveSlotsLabelUI.cs
using TMPro;
using UnityEngine;

namespace GalacticFishing.Minigames.HexWorld
{
    /// <summary>
    /// Simple HUD label: "Active: 1/2 (TH L1)".
    /// </summary>
    public sealed class HexWorldActiveSlotsLabelUI : MonoBehaviour
    {
        [SerializeField] private HexWorld3DController controller;
        [SerializeField] private TMP_Text label;
        [SerializeField] private string prefix = "Active: ";

        private void Awake()
        {
            if (!label) label = GetComponent<TMP_Text>();
        }

        private void Start()
        {
            if (!controller) controller = FindObjectOfType<HexWorld3DController>();
            if (!controller || !label)
            {
                enabled = false;
                return;
            }

            controller.ActiveSlotsChanged += OnActiveSlotsChanged;
            OnActiveSlotsChanged(controller.ActiveBuildingsUsed, controller.ActiveSlotsTotal, controller.TownHallLevel);
        }

        private void OnDestroy()
        {
            if (controller) controller.ActiveSlotsChanged -= OnActiveSlotsChanged;
        }

        private void OnActiveSlotsChanged(int used, int total, int townHallLevel)
        {
            label.text = prefix + used + "/" + total + " (TH L" + townHallLevel + ")";
        }
    }
}
