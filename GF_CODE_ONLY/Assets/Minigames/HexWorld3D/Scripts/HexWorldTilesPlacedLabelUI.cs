// Assets/Minigames/HexWorld3D/Scripts/HexWorldTilesPlacedLabelUI.cs
using TMPro;
using UnityEngine;

namespace GalacticFishing.Minigames.HexWorld
{
    /// <summary>
    /// Displays the number of tiles placed out of max capacity.
    /// Example: "Tiles: 37/37" or "Tiles: 24/61"
    /// </summary>
    public sealed class HexWorldTilesPlacedLabelUI : MonoBehaviour
    {
        [SerializeField] private HexWorld3DController controller;
        [SerializeField] private TMP_Text label;
        [SerializeField] private string prefix = "Tiles: ";

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

            controller.TilesPlacedChanged += OnTilesPlacedChanged;
            OnTilesPlacedChanged(controller.TilesPlaced, controller.TileCapacityMax);
        }

        private void OnDestroy()
        {
            if (controller) controller.TilesPlacedChanged -= OnTilesPlacedChanged;
        }

        private void OnTilesPlacedChanged(int placed, int max)
        {
            label.text = prefix + placed + "/" + max;
        }
    }
}
