// Assets/Minigames/HexWorld3D/Scripts/HexWorldPaletteTabsUI.cs
using UnityEngine;
using UnityEngine.UI;

namespace GalacticFishing.Minigames.HexWorld
{
    public sealed class HexWorldPaletteTabsUI : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private HexWorld3DController controller;

        [Header("UI Roots")]
        [SerializeField] private GameObject tilesPanelRoot;
        [SerializeField] private GameObject buildingsPanelRoot;

        [Header("Buttons (optional)")]
        [SerializeField] private Button tilesButton;
        [SerializeField] private Button buildingsButton;

        private void Start()
        {
            if (tilesButton)
                tilesButton.onClick.AddListener(ShowTiles);

            if (buildingsButton)
                buildingsButton.onClick.AddListener(ShowBuildings);

            // default
            ShowTiles();
        }

        public void ShowTiles()
        {
            if (tilesPanelRoot) tilesPanelRoot.SetActive(true);
            if (buildingsPanelRoot) buildingsPanelRoot.SetActive(false);

            if (controller) controller.SetPaletteModeTiles();
        }

        public void ShowBuildings()
        {
            if (tilesPanelRoot) tilesPanelRoot.SetActive(false);
            if (buildingsPanelRoot) buildingsPanelRoot.SetActive(true);

            if (controller) controller.SetPaletteModeBuildings();
        }
    }
}
