#if UNITY_EDITOR
using UnityEngine;

namespace GalacticFishing
{
    public sealed class FishCatalogSettings : ScriptableObject
    {
        [Header("Folders")]
        public string spritesFolder  = "Assets/Sprites/Fish";  // source sprites
        public string dataFolder     = "Assets/Data/Fish";     // Fish_*.asset
        public string prefabsFolder  = "Assets/Prefabs/Fish";  // per-species prefabs
        public string registryPath   = "Assets/Data/FishRegistry.asset";

        [Header("Prefab mode")]
        public bool      useSharedPrefab = true;               // shared BaseFish route
        public GameObject baseFishPrefab;                      // assign if useSharedPrefab

        [Header("Defaults for new Fish")]
        public FishRarity defaultRarity     = FishRarity.Common;
        public float      baselineMeters    = 0.5f;
        public float      sigmaLogSize      = 0.25f;
        public float      nativeScaleMult   = 1f;

        [Tooltip("Median density coefficient used when auto-creating Fish assets (kg ≈ densityK * L^3).")]
        public float      baselineDensityK  = 8.0f;

        [Tooltip("Log-space sigma for density variation when auto-creating Fish assets.")]
        public float      sigmaLogDensity   = 0.1f;

        [Header("Automation")]
        public bool autoBuildOnImport = true;                  // watch sprites folder
    }
}
#endif
