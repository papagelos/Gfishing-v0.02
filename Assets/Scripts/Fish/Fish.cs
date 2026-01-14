using UnityEngine;

namespace GalacticFishing
{
    [CreateAssetMenu(menuName = "GalacticFishing/Fish", fileName = "Fish_")]
    public sealed class Fish : ScriptableObject
    {
        [Header("Identity")]
        public string displayName;
        public FishRarity rarity = FishRarity.Common;

        [Header("Art / Prefab")]
        public GameObject prefab;   // optional per-species prefab
        public Sprite sprite;       // used if prefab has a SpriteRenderer

        [Tooltip("All art in this project faces LEFT by convention.")]
        public bool artFacesLeft = true;

        [Header("Scale & Size Distribution")]
        public float baselineMeters = 0.5f;
        public float sigmaLogSize = 0.25f;
        public float nativeScaleMultiplier = 1f;

        [Header("Density Distribution")]
        [Tooltip("Median density coefficient used to convert length to weight (kg ≈ densityK * L^3).")]
        public float baselineDensityK = 8.0f;

        [Tooltip("Log-space sigma controlling how much density varies between individuals.")]
        public float sigmaLogDensity = 0.1f;

        [Header("Behaviour")]
        public bool allowFlipX = true;

        [Tooltip("Extra space above water surface the fish should avoid.")]
        public float surfacePadding = 0.05f;

        /// <summary>
        /// Helper: returns -1 if art faces left, +1 otherwise.
        /// Use anywhere you previously needed ArtFacing.ToSign().
        /// </summary>
        public int FacingSign => artFacesLeft ? -1 : 1;
    }
}
