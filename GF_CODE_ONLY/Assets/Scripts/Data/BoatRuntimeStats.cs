// Assets/Scripts/Data/BoatRuntimeStats.cs
namespace GalacticFishing.Data
{
    /// <summary>
    /// Merged view of the currently equipped boat + all unlocked boat upgrades.
    /// The rest of the game should read from this instead of BoatDefinition directly.
    /// </summary>
    [System.Serializable]
    public struct BoatRuntimeStats
    {
        // Base boat
        public string boatId;
        public string displayName;
        public int capacitySlots;
        public float maxWeightKg;
        public float travelSpeed;

        // Stability (reaction minigame)
        public float greenZoneSpeedMultiplier;
        public float reactionWindowMultiplier;
        public bool noiseVisualizationUnlocked;

        // Mooring / fish control
        public float biteRateMultiplier;
        public float quantityFactorMultiplier;
        public float bigFishBiasBonus;

        public bool sonarUnlocked;
        public bool depthScannerUnlocked;
        public bool autoFilletUnlocked;
        public bool waterPurifierUnlocked;
        public int harpoonExtraRolls;

        // Robot fleet
        public int extraRobotSlots;
        public int rareDroneQueueBonus;
        public float robotPowerMultiplier;
        public float robotTargetingBonus;
        public float robotCatchSpeedMultiplier;

        // Breeding support
        public bool liveTanksUnlocked;
        public float breedingSuccessBonus;
        public bool hormoneInjectorsUnlocked;
        public bool quarantineChamberUnlocked;

        // Mining support
        public bool oreStorageModuleUnlocked;
        public float miningYieldMultiplier;
        public bool deepScannerRelayUnlocked;
        public bool drillCoolingUnlocked;

        // --------------------------------------------------
        // Construction
        // --------------------------------------------------

        public static BoatRuntimeStats FromBaseBoat(BoatDefinition boat)
        {
            if (boat == null)
                return default;

            var stats = new BoatRuntimeStats
            {
                boatId = boat.id,
                displayName = boat.displayName,
                capacitySlots = boat.capacitySlots,
                maxWeightKg = boat.maxWeightKg,
                travelSpeed = boat.travelSpeed,

                // neutral multipliers
                greenZoneSpeedMultiplier = 1f,
                reactionWindowMultiplier = 1f,

                biteRateMultiplier = 1f,
                quantityFactorMultiplier = 1f,
                bigFishBiasBonus = 0f,

                robotPowerMultiplier = 1f,
                robotTargetingBonus = 0f,
                robotCatchSpeedMultiplier = 1f,

                breedingSuccessBonus = 0f,

                miningYieldMultiplier = 1f
            };

            return stats;
        }

        public void ApplyUpgrade(BoatUpgradeDefinition upgrade)
        {
            if (upgrade == null)
                return;

            switch (upgrade.category)
            {
                case BoatUpgradeCategory.Stability:
                    greenZoneSpeedMultiplier *= upgrade.greenZoneSpeedMultiplier;
                    reactionWindowMultiplier *= upgrade.reactionWindowMultiplier;
                    if (upgrade.unlocksNoiseVisualization)
                        noiseVisualizationUnlocked = true;
                    break;

                case BoatUpgradeCategory.MooringAnchors:
                    biteRateMultiplier *= upgrade.biteRateMultiplier;
                    quantityFactorMultiplier *= upgrade.quantityFactorMultiplier;
                    bigFishBiasBonus += upgrade.bigFishBiasBonus;

                    if (upgrade.sonarModule)
                        sonarUnlocked = true;
                    if (upgrade.depthScanner)
                        depthScannerUnlocked = true;
                    if (upgrade.autoFilletStation)
                        autoFilletUnlocked = true;
                    if (upgrade.waterPurifier)
                        waterPurifierUnlocked = true;

                    harpoonExtraRolls += upgrade.harpoonPodExtraRolls;
                    break;

                case BoatUpgradeCategory.RobotFleet:
                    extraRobotSlots += upgrade.extraRobotSlots;
                    rareDroneQueueBonus += upgrade.rareDroneQueueBonus;
                    robotPowerMultiplier *= upgrade.robotPowerMultiplier;
                    robotCatchSpeedMultiplier *= upgrade.robotCatchSpeedMultiplier;
                    robotTargetingBonus += upgrade.robotTargetingBonus;
                    break;

                case BoatUpgradeCategory.BreedingSupport:
                    if (upgrade.liveTanks)
                        liveTanksUnlocked = true;
                    breedingSuccessBonus += upgrade.breedingSuccessBonus;
                    if (upgrade.hormoneInjectors)
                        hormoneInjectorsUnlocked = true;
                    if (upgrade.quarantineChamber)
                        quarantineChamberUnlocked = true;
                    break;

                case BoatUpgradeCategory.MiningSupport:
                    if (upgrade.oreStorageModule)
                        oreStorageModuleUnlocked = true;
                    miningYieldMultiplier *= upgrade.miningYieldMultiplier;
                    if (upgrade.deepScannerRelay)
                        deepScannerRelayUnlocked = true;
                    if (upgrade.drillCoolingSystem)
                        drillCoolingUnlocked = true;
                    break;
            }
        }
    }
}
