using System.Collections.Generic;
using UnityEngine;

namespace GalacticFishing.Minigames.HexWorld
{
    [CreateAssetMenu(menuName = "Galactic Fishing/Hex World/Tile Definition", fileName = "TileDef_")]
    public class TileDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string id = "grass";
        public string displayName = "Grassland";
        public TileCategory category = TileCategory.Nature;

        [Header("Visual")]
        public Sprite sprite;

        [Header("Build Weight (only if random placement is enabled)")]
        [Min(1)] public int weight = 1;

        [Header("Base Production (per second)")]
        public List<ResourceRate> baseProductionPerSecond = new();

        [Header("Base Stat Bonuses")]
        public List<StatBonus> statBonuses = new();

        [Header("Adjacency Bonuses (per matching neighbor)")]
        public List<NeighborBonusRule> neighborBonuses = new();
    }
}
