// Assets/Minigames/HexWorld3D/Scripts/HexWorldBuildingDefinition.cs
using UnityEngine;

namespace GalacticFishing.Minigames.HexWorld
{
    [CreateAssetMenu(menuName = "Galactic Fishing/Hex World/Building Definition", fileName = "Building_")]
    public sealed class HexWorldBuildingDefinition : ScriptableObject
    {
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
    }
}
