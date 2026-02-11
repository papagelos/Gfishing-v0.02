using System;
using System.Collections.Generic;
using UnityEngine;

namespace GalacticFishing
{
    [CreateAssetMenu(menuName = "Galactic Fishing/Worlds/World Definition", fileName = "WorldDefinition")]
    public sealed class WorldDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string worldId = "world_01";
        public string displayName = "World 1";

        [Header("Backdrop")]
        public Sprite backdrop;

        [Header("Quality bonuses (0..1)")]
        [Range(0f, 1f)] public float upwardBonus = 0f;
        [Range(0f, 1f)] public float tightenBonus = 0f;

        [Header("Default Fish Pool")]
        public List<FishWeight> defaultPool = new();

        [Header("Lakes")]
        public List<Lake> lakes = new();
    }

    [Serializable]
    public struct FishWeight
    {
        [Tooltip("Reference to the core Fish ScriptableObject (Assets/Data/Fish).")]
        public ScriptableObject fish;
        [Min(0)] public int weight;
    }

    [Serializable]
    public sealed class Lake
    {
        [Header("Identity")]
        public string lakeId = "lake_01";
        public string displayName = "Lake 1";
        [Min(0)] public int unlockCost = 0;

        [Header("Population")]
        [Tooltip("Base cap for how many fish can be alive in this lake at once. " +
                 "0 = use spawner's maxAlive (fallback for old assets).")]
        [Min(0)] public int maxAlive = 0;

        [Header("Visuals")]
        public Sprite backdropOverride;

        [Header("Quality modifiers")]
        [Range(0f, 1f)] public float addUpwardBonus = 0f;
        [Range(0f, 1f)] public float addTightenBonus = 0f;
        public float quantityScale = 1f;

        [Header("Fish Pool")]
        [Tooltip("If true, use this pool instead of the world's default pool.")]
        public bool usePoolOverride = false;
        public List<FishWeight> poolOverride = new();

        [Header("Boss (optional)")]
        public bool isBossLake = false;
        public ScriptableObject bossFish;
    }
}
