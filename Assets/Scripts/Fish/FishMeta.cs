// FishMeta.cs
using System.Collections.Generic;
using UnityEngine;

namespace GalacticFishing
{
    [CreateAssetMenu(fileName = "FishMeta_", menuName = "GalacticFishing/Fish Meta", order = 10)]
    public partial class FishMeta : ScriptableObject
    {
        [Header("Identity")]
        public string displayName;

        [Header("Gameplay")]
        public FishTier tier = FishTier.Tier1;
        public FishRarity rarity = FishRarity.Common;
        [Range(0f, 1f)] public float catchDifficulty = 0.5f; // your overall slider if you want it

        [Header("Movement")]
        public Vector2 sizeRange = new Vector2(1f, 1f);
        public Vector2 speedRange = new Vector2(1f, 1f);

        [Header("Tags / Flags")]
        public FishFlags flags = FishFlags.None;

        [Header("Reaction Phase 2 overrides (optional)")]
        public Reaction2Settings reaction2 = new Reaction2Settings();

        [System.Serializable]
        public class CustomKV
        {
            public string key;
            public float value;
        }

        [Header("Custom numeric properties")]
        public List<CustomKV> custom = new List<CustomKV>();
    }
}
