// Assets/Minigames/HexWorld3D/Scripts/HexWorldTilesLeftLabelUI.cs
using TMPro;
using UnityEngine;

namespace GalacticFishing.Minigames.HexWorld
{
    public sealed class HexWorldTilesLeftLabelUI : MonoBehaviour
    {
        [SerializeField] private HexWorld3DController controller;
        [SerializeField] private TMP_Text label;
        [SerializeField] private string prefix = "Tiles left to place: ";

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

            controller.TilesLeftChanged += OnTilesLeftChanged;
            OnTilesLeftChanged(controller.TilesLeftToPlace);
        }

        private void OnDestroy()
        {
            if (controller) controller.TilesLeftChanged -= OnTilesLeftChanged;
        }

        private void OnTilesLeftChanged(int v)
        {
            label.text = prefix + v;
        }
    }
}
