// Assets/Scripts/Data/BoatUpgradeDefinition.cs
using UnityEngine;

namespace GalacticFishing.Data
{
    public enum BoatUpgradeCategory
    {
        Stability,
        MooringAnchors,
        RobotFleet,
        BreedingSupport,
        MiningSupport
    }

    [CreateAssetMenu(
        menuName = "GalacticFishing/Gear/Boat Upgrade",
        fileName = "BoatUpgrade_")]
    public class BoatUpgradeDefinition : ScriptableObject
    {
        [Header("Id (stable, used in saves)")]
        [Tooltip("Stable id like 'boat_upgrade_stability_01'. Never change once shipped.")]
        public string id = "boat_upgrade_";

        [Header("Display")]
        public string displayName = "Upgrade";
        [Tooltip("Optional short label for buttons/cards; falls back to displayName if empty.")]
        public string shortLabel;
        [TextArea] public string description;
        [Tooltip("Art for the 2x5 upgrade card (can be null).")]
        public Sprite cardArt;

        [Header("Grouping")]
        public BoatUpgradeCategory category = BoatUpgradeCategory.Stability;
        [Tooltip("Progression tier within its category (1 = first, 2 = second, etc.).")]
        public int tier = 1;

        // --------------------------------------------------
        // A) STABILITY (reaction minigame)
        // --------------------------------------------------
        [Header("Stability (reaction minigame)")]
        [Tooltip("Multiplier for green zone movement speed. <1 = slower/easier.")]
        public float greenZoneSpeedMultiplier = 1f;

        [Tooltip("Multiplier for input reaction window. >1 = more forgiving.")]
        public float reactionWindowMultiplier = 1f;

        [Tooltip("Unlocks extra noise/boat-movement visualization overlays.")]
        public bool unlocksNoiseVisualization = false;

        // --------------------------------------------------
        // B) MOORING / FISH SPAWN CONTROL
        // --------------------------------------------------
        [Header("Mooring / fish spawn control")]
        [Tooltip("Multiplier for bite rate.")]
        public float biteRateMultiplier = 1f;

        [Tooltip("Multiplier for quantity factor in the lake.")]
        public float quantityFactorMultiplier = 1f;

        [Tooltip("Additive bias towards larger fish (0..1, small values).")]
        public float bigFishBiasBonus = 0f;

        [Header("Mooring Modules")]
        public bool sonarModule = false;
        public bool depthScanner = false;
        public bool autoFilletStation = false;
        public bool waterPurifier = false;

        [Tooltip("Extra rare-fish rerolls/harpoon chances this upgrade grants.")]
        public int harpoonPodExtraRolls = 0;

        // --------------------------------------------------
        // C) ROBOT FLEET INTEGRATION
        // --------------------------------------------------
        [Header("Robot Fleet Integration")]
        [Tooltip("Additional autonomous robot fishing slots.")]
        public int extraRobotSlots = 0;

        [Tooltip("Extra size of rare-drone queue (e.g. +1, +2).")]
        public int rareDroneQueueBonus = 0;

        [Tooltip("Multiplier for robot catch power.")]
        public float robotPowerMultiplier = 1f;

        [Tooltip("Bonus for robot targeting logic (additive; how you interpret it is up to the minigame).")]
        public float robotTargetingBonus = 0f;

        [Tooltip("Multiplier for robot catch speed.")]
        public float robotCatchSpeedMultiplier = 1f;

        // --------------------------------------------------
        // D) BREEDING SUPPORT
        // --------------------------------------------------
        [Header("Breeding Support")]
        [Tooltip("Fish stay alive in tanks until release.")]
        public bool liveTanks = false;

        [Tooltip("Additive breeding success bonus (0..1).")]
        public float breedingSuccessBonus = 0f;

        [Tooltip("Pre-augmentation for breeding before entering pond.")]
        public bool hormoneInjectors = false;

        [Tooltip("Protects from disease events in breeding.")]
        public bool quarantineChamber = false;

        // --------------------------------------------------
        // E) MINING SUPPORT
        // --------------------------------------------------
        [Header("Mining Support")]
        [Tooltip("Unlocks extra ore storage in the boat.")]
        public bool oreStorageModule = false;

        [Tooltip("Multiplier for mining yield while this boat is active.")]
        public float miningYieldMultiplier = 1f;

        [Tooltip("Unlocks deep scanner relay for deeper or richer nodes.")]
        public bool deepScannerRelay = false;

        [Tooltip("Unlocks drill cooling to go deeper strata.")]
        public bool drillCoolingSystem = false;
    }
}
