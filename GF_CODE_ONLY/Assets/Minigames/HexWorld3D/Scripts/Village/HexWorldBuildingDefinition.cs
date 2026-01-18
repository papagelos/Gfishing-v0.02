// Assets/Minigames/HexWorld3D/Scripts/HexWorldBuildingDefinition.cs
using UnityEngine;

namespace GalacticFishing.Minigames.HexWorld
{
    [CreateAssetMenu(menuName = "Galactic Fishing/Hex World/Building Definition", fileName = "Building_")]
    public sealed class HexWorldBuildingDefinition : ScriptableObject
    {
        public enum BuildingKind
        {
            Producer,
            TownHall,
            Warehouse,
            Other
        }

        [Header("UI")]
        public string displayName = "New Building";
        public Sprite icon;

        [Header("Prefab")]
        public GameObject prefab;

        [Header("Placement")]
        [Tooltip("Local offset from the tile center (Y usually lifts it a bit).")]
        public Vector3 localOffset = new Vector3(0f, 0.02f, 0f);

        [Tooltip("Local rotation (degrees) applied after placement.")]
        public Vector3 localEuler = Vector3.zero;

        [Header("Capacity / Activity")]
        [Tooltip("What role this building plays. Used for defaults (e.g., TownHall/Warehouse don't consume active slots).")]
        public BuildingKind kind = BuildingKind.Producer;

        [Tooltip("If true, this building counts toward Town Hall Active Slots when Active.")]
        public bool consumesActiveSlot = true;

        [Tooltip("Newly placed building starts Active (if a slot is available).")]
        public bool defaultActive = true;

        [Header("District Bonus")]
        [Tooltip("The terrain type this building benefits from for district bonuses. Set to None if no bonus.")]
        public HexWorldTerrainType preferredTerrainType = HexWorldTerrainType.None;

        private void OnValidate()
        {
            // Reasonable defaults: infrastructure shouldn't consume active slots.
            if (kind == BuildingKind.TownHall || kind == BuildingKind.Warehouse)
                consumesActiveSlot = false;
        }
    }
}
