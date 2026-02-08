// Assets/Minigames/HexWorld3D/Scripts/HexWorldBuildingDefinition.cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace GalacticFishing.Minigames.HexWorld
{
    /// <summary>
    /// Defines what condition triggers a synergy bonus.
    /// </summary>
    public enum SynergyType
    {
        /// <summary>Building is adjacent to a road tile.</summary>
        RoadAdjacent,
        /// <summary>Building is connected to Town Hall via road network.</summary>
        RoadConnectedToTownHall,
        /// <summary>Adjacent tile has a specific gameplay tag.</summary>
        AdjacentTileTag,
        /// <summary>A building of a specific type exists within a radius.</summary>
        WithinRadiusBuildingType
    }

    /// <summary>
    /// Defines how multiple synergy matches are combined.
    /// </summary>
    public enum SynergyStacking
    {
        /// <summary>Bonus applies once if any match exists (on/off).</summary>
        Binary,
        /// <summary>Bonus stacks per matching count (e.g., +5% per adjacent forest).</summary>
        PerCount
    }

    /// <summary>
    /// Data-driven rule describing a production/efficiency bonus from surroundings.
    /// </summary>
    [Serializable]
    public class SynergyRule
    {
        [Tooltip("Display label for UI (e.g., 'Road Adjacent', 'Near Forest').")]
        public string label = "New Synergy";

        [Tooltip("The type of condition that triggers this synergy.")]
        public SynergyType type = SynergyType.AdjacentTileTag;

        [Tooltip("The tile tag or building ID required (depends on type). Leave empty for RoadAdjacent/RoadConnectedToTownHall.")]
        public string targetTagOrId = "";

        [Tooltip("Radius in tiles for WithinRadiusBuildingType checks. Ignored for other types.")]
        [Range(1, 5)]
        public int radius = 1;

        [Tooltip("Bonus percentage per match (e.g., 0.05 = +5%). Negative values for penalties.")]
        [Range(-1f, 1f)]
        public float amountPct = 0.05f;

        [Tooltip("How multiple matches combine: Binary (once) or PerCount (stacking).")]
        public SynergyStacking stacking = SynergyStacking.Binary;

        [Tooltip("Maximum stacks if using PerCount stacking. 0 = unlimited.")]
        [Min(0)]
        public int maxStacks = 0;
    }

    [CreateAssetMenu(menuName = "Galactic Fishing/Hex World/Building Definition", fileName = "Building_")]
    public sealed class HexWorldBuildingDefinition : ScriptableObject
    {
        public enum BuildingRarity
        {
            Common = 0,
            Uncommon = 1,
            Rare = 2,
            Epic = 3
        }

        public enum BuildingKind
        {
            Producer,
            TownHall,
            Warehouse,
            Processor,  // TICKET 20: Converts raw resources to refined goods
            Other,
            Support
        }

        [Header("UI")]
        public string displayName = "New Building";
        public Sprite icon;
        public string buildingName => name;

        [Header("Prefab")]
        public GameObject prefab;

        [Header("Placement")]
        [Tooltip("Local offset from the tile center (Y usually lifts it a bit).")]
        public Vector3 localOffset = new Vector3(0f, 0.02f, 0f);

        [Tooltip("Local rotation (degrees) applied after placement.")]
        public Vector3 localEuler = Vector3.zero;

        [Tooltip("Local scale applied to the building. Default is 0.09 for typical buildings.")]
        public Vector3 localScale = new Vector3(0.09f, 0.09f, 0.09f);

        [Header("Capacity / Activity")]
        [Tooltip("What role this building plays. Used for defaults (e.g., TownHall/Warehouse don't consume active slots).")]
        public BuildingKind kind = BuildingKind.Producer;

        [Header("Progression")]
        [Tooltip("Minimum Town Hall tier required before this building appears in progression-aware UI flows.")]
        [Min(1)]
        public int unlockTownTier = 1;

        [Tooltip("Placement prerequisites. This building becomes usable only after all listed buildingName IDs have been placed at least once this session.")]
        public List<string> requiredBuildingPlacementNames = new List<string>();

        [Tooltip("If true, this building counts toward Town Hall Active Slots when Active.")]
        public bool consumesActiveSlot = true;

        [Tooltip("Newly placed building starts Active (if a slot is available).")]
        public bool defaultActive = true;

        [Header("District Bonus")]
        [Tooltip("The terrain type this building benefits from for district bonuses. Set to None if no bonus.")]
        public HexWorldTerrainType preferredTerrainType = HexWorldTerrainType.None;

        [Header("Processor Settings (TICKET 20)")]
        [Tooltip("Tool slot type for Processor buildings (e.g., 'Saw', 'Chisel', 'Heat'). Leave empty for non-processors.")]
        public string toolSlotType = "";

        [Header("Upgrades (TICKET 1)")]
        [Tooltip("Available upgrades shown in the context menu when clicking this building.")]
        public List<HexWorldUpgradeDefinition> availableUpgrades = new List<HexWorldUpgradeDefinition>();

        [Header("Stats Display")]
        [Tooltip("Key/value properties shown in the context menu.")]
        public List<WorldProperty> properties = new List<WorldProperty>();

        [Header("Synergy Rules (TICKET 7)")]
        [Tooltip("Data-driven rules that define production bonuses based on surroundings.")]
        public List<SynergyRule> synergyRules = new List<SynergyRule>();

        [Header("Rewards (TICKET 19)")]
        [Tooltip("Rarity tier used for standardized building placement IP rewards.")]
        public BuildingRarity rarity = BuildingRarity.Common;

        [Tooltip("IP awarded when this building is placed. Range: 10-25 typical.")]
        [Range(0, 100)]
        public int ipReward = 10;

        private void OnValidate()
        {
            // Reasonable defaults: infrastructure shouldn't consume active slots.
            if (kind == BuildingKind.TownHall || kind == BuildingKind.Warehouse)
                consumesActiveSlot = false;
        }
    }
}
