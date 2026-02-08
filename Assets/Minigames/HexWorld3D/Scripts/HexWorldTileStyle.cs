using UnityEngine;
using System;
using System.Collections.Generic;

namespace GalacticFishing.Minigames.HexWorld
{
    /// <summary>
    /// Distinguishes between free cosmetic paints and paid gameplay tiles.
    /// </summary>
    public enum TileCategory
    {
        /// <summary>Free visual-only paint with no gameplay effect.</summary>
        Cosmetic = 0,
        /// <summary>Paid tile that affects gameplay (resources, buildings, etc.).</summary>
        Gameplay = 1
    }

    [CreateAssetMenu(menuName = "Galactic Fishing/Hex World/Tile Style", fileName = "TileStyle_")]
    public sealed class HexWorldTileStyle : ScriptableObject
    {
        [Header("UI")]
        public string displayName = "New Tile";
        public Color edgeTint = Color.white;
        public Sprite thumbnail;

        [Header("Category & Cost")]
        [Tooltip("Cosmetic tiles are free visual paints. Gameplay tiles cost resources and affect mechanics.")]
        public TileCategory category = TileCategory.Cosmetic;

        [Tooltip("Minimum Town Hall tier required before this tile appears in progression-aware UI flows.")]
        [Min(1)]
        public int unlockTownTier = 1;

        [Tooltip("Placement prerequisites. This tile becomes usable only after all listed buildingName IDs have been placed at least once this session.")]
        public List<string> requiredBuildingPlacementNames = new List<string>();

        [Tooltip("Resources required to paint/place this tile. Empty = free.")]
        public List<HexWorldResourceStack> paintCost = new List<HexWorldResourceStack>();

        [Tooltip("Fraction of original cost refunded on demolition (0.0 to 1.0).")]
        [Range(0f, 1f)]
        public float demolitionFeeFactor = 0.30f;

        [Tooltip("Tags for gameplay systems (e.g., 'resource_node', 'buildable', 'water').")]
        public List<string> gameplayTags = new List<string>();

        [Header("Rendering")]
        public Material[] materials;

        [Header("Decorations")]
        [Tooltip("List of potential props (trees, stones, etc.) with per-object settings.")]
        public List<DecorationEntry> decorations = new List<DecorationEntry>();

        [Tooltip("If ON, each prop is picked randomly from the list. If OFF, one species is picked for the whole tile.")]
        public bool pickRandomlyPerInstance = true;

        public int minCount = 3;
        public int maxCount = 5;

        [Header("Terrain Type")]
        public HexWorldTerrainType terrainType = HexWorldTerrainType.None;

        [Header("Stats Display")]
        [Tooltip("Key/value properties shown in the context menu when clicking this tile.")]
        public List<WorldProperty> properties = new List<WorldProperty>();

        /// <summary>
        /// Returns true if this tile has any associated cost.
        /// </summary>
        public bool HasCost => paintCost != null && paintCost.Count > 0;

        /// <summary>
        /// Returns true if this tile has a specific gameplay tag.
        /// </summary>
        public bool HasTag(string tag)
        {
            if (string.IsNullOrEmpty(tag) || gameplayTags == null) return false;
            return gameplayTags.Contains(tag);
        }
    }

    [Serializable]
    public class DecorationEntry
    {
        public GameObject prefab;
        [Tooltip("The baseline scale for this object.")]
        [Range(0.1f, 10f)] public float scale = 1.0f;

        [Tooltip("Random variance % relative to the baseline. X = min %, Y = max %. (e.g., -20 and 30)")]
        public Vector2 randomScaleRange = new Vector2(-20f, 30f);
    }
}
