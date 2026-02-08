// Assets/Minigames/HexWorld3D/Scripts/Data/TownTierDefinition.cs
using UnityEngine;

namespace GalacticFishing.Minigames.HexWorld
{
    /// <summary>
    /// Defines the parameters for a single Town Tier (T1-T10).
    /// Create one asset per tier in Assets/Minigames/HexWorld3D/TileStyles/ or similar.
    /// </summary>
    [CreateAssetMenu(
        fileName = "TownTier_T1",
        menuName = "HexWorld/Town Tier Definition",
        order = 100)]
    public class TownTierDefinition : ScriptableObject
    {
        [Header("Tier Identity")]
        [Tooltip("The tier number (1-10).")]
        public int tierNumber = 1;

        [Header("Map Size")]
        [Tooltip("Hex radius that determines how far from center tiles can be placed.")]
        public int hexRadius = 2;

        [Header("Caps")]
        [Tooltip("Maximum tiles the player can place at this tier.")]
        public int tileCap = 10;

        [Tooltip("Maximum buildings the player can place at this tier.")]
        public int buildingCap = 5;

        [Tooltip("Maximum active producers at this tier.")]
        public int activeProducerCap = 2;

        [Tooltip("Maximum building upgrade level allowed at this tier.")]
        public int maxBuildingLevel = 1;

        [Header("Quality")]
        [Tooltip("Baseline Material Quality cap at this tier.")]
        public int baselineMQCap = 10;

        [Header("Progression")]
        [Tooltip("QP granted when reaching this tier (one-time reward).")]
        public long qpGranted = 0;

        [Tooltip("IP required to unlock this tier (cumulative total).")]
        public long ipRequired = 0;

        [Header("Milestone Requirement")]
        [Tooltip("Warehouse resource required as milestone proof for this tier unlock.")]
        public HexWorldResourceId milestoneResourceId = HexWorldResourceId.None;

        [Tooltip("Amount of milestone resource required.")]
        public int milestoneQuantity = 0;

        [Tooltip("UI label shown for the milestone requirement.")]
        public string milestoneLabel = "";
    }
}
