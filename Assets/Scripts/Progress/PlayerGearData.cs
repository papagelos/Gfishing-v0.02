// Assets/Scripts/Progress/PlayerGearData.cs
using System;
using System.Collections.Generic;

namespace GalacticFishing.Progress
{
    /// <summary>
    /// All persistent information about player gear:
    /// which rods / boats they own, what is equipped,
    /// and which upgrades have been unlocked/leveled.
    /// </summary>
    [Serializable]
    public class PlayerGearData
    {
        // --------- Rods ---------
        public List<string> ownedRodIds = new List<string>();
        public string equippedRodId;

        // NEW: per-rod upgrade levels (keyed by rod id)
        public List<LevelEntry> rodUpgradeLevels = new List<LevelEntry>();

        // --------- Boats --------
        public List<string> ownedBoatIds = new List<string>();
        public string equippedBoatId;

        // --------- Boat upgrades ---------
        /// <summary>
        /// Ids of boat upgrades the player has unlocked.
        /// These ids match BoatUpgradeDefinition.id and we’ll
        /// interpret levels / details in PlayerProgressManager.
        /// </summary>
        public List<string> unlockedBoatUpgradeIds = new List<string>();

        /// <summary>
        /// Canonical building IDs the player has permanently unlocked for the HexWorld palette.
        /// </summary>
        public List<string> unlockedBlueprintIds = new List<string>();

        // NEW: generic workshop upgrade levels (keyed by WorkshopUpgrade.id)
        public List<LevelEntry> workshopUpgradeLevels = new List<LevelEntry>();

        [Serializable]
        public class LevelEntry
        {
            public string id;
            public int level;
        }
    }
}
