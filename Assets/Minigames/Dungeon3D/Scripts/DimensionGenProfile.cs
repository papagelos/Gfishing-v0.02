using System.Collections.Generic;
using GalacticFishing.Minigames.HexWorld;
using UnityEngine;

namespace GalacticFishing.Minigames.Dungeon3D
{
    [System.Serializable]
    public sealed class BiomeTileStyleGroup
    {
        public string biomeGroup = "MEADOW";
        public List<HexWorldTileStyle> tileStyles = new();
    }

    [CreateAssetMenu(
        menuName = "Galactic Fishing/Dungeon/Dimension Gen Profile",
        fileName = "DimensionGenProfile_")]
    public sealed class DimensionGenProfile : ScriptableObject
    {
        [Header("Layout Targets")]
        [Min(8)] public int spineMinLength = 180;
        [Min(8)] public int spineMaxLength = 240;
        [Min(2)] public int targetTileCount = 2000;
        [Min(2)] public int minBossDistance = 180;
        [Min(1)] public int bossArenaRadius = 7;

        [Header("Spine Bias")]
        [Min(0f)] public float towardBossBias = 5f;
        [Min(0f)] public float outwardBias = 2f;
        [Min(0f)] public float forwardDirectionBonus = 3f;
        [Min(0f)] public float sideDirectionBonus = 1f;

        [Header("Pockets")]
        [Min(0)] public int pocketSeedCount = 14;
        [Min(1)] public int pocketMinSize = 12;
        [Min(1)] public int pocketMaxSize = 50;
        [Min(0)] public int pocketStartPadding = 8;
        [Min(0)] public int pocketEndPadding = 8;

        [Header("Biome Patches")]
        [Min(1)] public int biomePatchSize = 48;
        public List<string> biomeGroups = new()
        {
            "MEADOW",
            "FOREST",
            "DESERT",
            "MIRE",
            "VOLCANIC",
        };
        public List<BiomeTileStyleGroup> biomeStyleGroups = new();

        [Header("Prop Randomization")]
        [Range(0f, 1f)] public float propChance = 0.25f;
        public List<string> randomPropPool = new()
        {
            "Tree",
            "Rock",
            "Shrub",
            "Skull",
            "Crystal",
            "Totem",
        };

        public int EffectiveTargetTileCount => Mathf.Max(targetTileCount, spineMinLength + 2);

        private void OnValidate()
        {
            spineMinLength = Mathf.Max(8, spineMinLength);
            spineMaxLength = Mathf.Max(spineMinLength, spineMaxLength);
            minBossDistance = Mathf.Clamp(minBossDistance, 2, spineMaxLength);
            bossArenaRadius = Mathf.Max(1, bossArenaRadius);
            targetTileCount = Mathf.Max(targetTileCount, spineMinLength + 2);
            pocketMinSize = Mathf.Max(1, pocketMinSize);
            pocketMaxSize = Mathf.Max(pocketMinSize, pocketMaxSize);
            biomePatchSize = Mathf.Max(1, biomePatchSize);
        }
    }
}
