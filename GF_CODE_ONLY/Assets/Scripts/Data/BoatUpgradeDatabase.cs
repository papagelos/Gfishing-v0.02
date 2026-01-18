// Assets/Scripts/Data/BoatUpgradeDatabase.cs
using System.Collections.Generic;
using UnityEngine;

namespace GalacticFishing.Data
{
    [CreateAssetMenu(
        menuName = "GalacticFishing/Gear/Boat Upgrade Database",
        fileName = "BoatUpgradeDB")]
    public class BoatUpgradeDatabase : ScriptableObject
    {
        [SerializeField]
        private List<BoatUpgradeDefinition> upgrades = new List<BoatUpgradeDefinition>();

        public IReadOnlyList<BoatUpgradeDefinition> Upgrades => upgrades;

        public BoatUpgradeDefinition GetById(string upgradeId)
        {
            if (string.IsNullOrWhiteSpace(upgradeId) || upgrades == null)
                return null;

            for (int i = 0; i < upgrades.Count; i++)
            {
                var u = upgrades[i];
                if (u != null && u.id == upgradeId)
                    return u;
            }

            return null;
        }
    }
}
