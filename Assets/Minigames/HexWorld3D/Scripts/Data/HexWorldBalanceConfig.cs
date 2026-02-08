// Assets/Minigames/HexWorld3D/Scripts/Data/HexWorldBalanceConfig.cs
using UnityEngine;

namespace GalacticFishing.Minigames.HexWorld
{
    [CreateAssetMenu(menuName = "Galactic Fishing/Hex World/Balance Config", fileName = "HexWorldBalanceConfig")]
    public sealed class HexWorldBalanceConfig : ScriptableObject
    {
        public const float DefaultTickSeconds = 60f;
        public const float DefaultRoadAdjBonusPct = 0.05f;
        public const float DefaultTownHallConnBonusPct = 0.10f;
        public const float DefaultTotalBonusCapPct = 2.0f;
        public const float DefaultMinBonusFloorPct = -0.8f;
        public const float DefaultDemolitionRefundFactor = 0.30f;

        [Header("Ticker")]
        [Tooltip("Seconds between automatic production ticks.")]
        [Min(1f)] public float tickSeconds = DefaultTickSeconds;

        [Header("Road Bonuses")]
        [Tooltip("Bonus applied when a building touches any road tile (0.05 = +5%).")]
        [Range(0f, 2f)] public float roadAdjBonusPct = DefaultRoadAdjBonusPct;
        [Tooltip("Bonus applied when a building is connected to Town Hall via roads.")]
        [Range(0f, 2f)] public float townHallConnBonusPct = DefaultTownHallConnBonusPct;

        [Header("Synergy Caps")]
        [Tooltip("Maximum total synergy bonus (2.0 = +200%).")]
        [Range(0f, 5f)] public float totalBonusCapPct = DefaultTotalBonusCapPct;
        [Tooltip("Minimum floor applied to total bonus before converting to multiplier (-0.8 = -80%).")]
        [Range(-0.99f, 0f)] public float minBonusFloorPct = DefaultMinBonusFloorPct;

        [Header("Economy")]
        [Tooltip("Credits refunded on eligible demolition (0.30 = 30%).")]
        [Range(0f, 1f)] public float demolitionRefundFactor = DefaultDemolitionRefundFactor;

        [Tooltip("Tile tier upgrade credit costs where index=tier (index 0 should be 0, index 1 = Tier1 upgrade cost, etc.).")]
        public int[] tileTierUpgradeCreditCostByTier = { 0, 50, 50 };

        [Tooltip("IP reward table where index = target level (e.g., index 2 rewards reaching level 2).")]
        public int[] upgradeIpByLevel = { 0, 0, 10, 15, 25, 40, 10 };

        public int GetUpgradeIpRewardAtLevel(int targetLevel)
        {
            if (upgradeIpByLevel == null || upgradeIpByLevel.Length == 0)
                return 0;

            if (targetLevel < 0)
                return 0;

            int idx = Mathf.Clamp(targetLevel, 0, upgradeIpByLevel.Length - 1);
            return Mathf.Max(0, upgradeIpByLevel[idx]);
        }
    }
}
