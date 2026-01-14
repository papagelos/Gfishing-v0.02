using UnityEngine;

namespace GalacticFishing.Minigames.HexWorld
{
    [CreateAssetMenu(menuName = "Galactic Fishing/Hex World/Tile Style", fileName = "TileStyle_")]
    public sealed class HexWorldTileStyle : ScriptableObject
    {
        [Header("UI")]
        public string displayName = "New Tile";
        public Color edgeTint = Color.white;

        public Sprite thumbnail;

        [Header("Rendering")]
        [Tooltip("Assign 1 material (single-submesh) or 2 materials (top + sides), matching your tile mesh renderer.")]
        public Material[] materials;

        [Header("Economy (optional)")]
        [Tooltip("If <= 0, the controller will use its default costPerTile.")]
        public int cost = 0;

        // Back-compat properties (older scripts may reference these names)
        public int costOverride => cost;
        public Material material => (materials != null && materials.Length > 0) ? materials[0] : null;
        public Material topMaterial => (materials != null && materials.Length > 0) ? materials[0] : null;
        public Material sideMaterial => (materials != null && materials.Length > 1) ? materials[1] : null;
    }
}
