using System;
using System.Collections.Generic;
using UnityEngine;

namespace GalacticFishing.Minigames.HexWorld
{
    public enum TileCategory { Nature, Industry, Science, Housing, Utility }
    public enum ResourceType { Credits, Biomass, Alloy, Research }

    public enum StatType
    {
        FishingPowerFlat,
        FishValuePercent,
        CraftSpeedPercent,
        ReputationGainPercent
    }

    [Serializable]
    public struct ResourceRate
    {
        public ResourceType type;
        public float perSecond;
    }

    [Serializable]
    public struct StatBonus
    {
        public StatType type;
        public float amount;
    }

    [Serializable]
    public class NeighborBonusRule
    {
        public TileCategory requiredNeighborCategory;
        public List<ResourceRate> bonusProductionPerNeighborPerSecond = new();
        public List<StatBonus> bonusStatsPerNeighbor = new();
    }
}
