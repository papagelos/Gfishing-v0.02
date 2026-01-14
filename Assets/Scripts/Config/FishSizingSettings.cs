using UnityEngine;

namespace GalacticFishing
{
    /// <summary>
    /// Global visual calibration for fish size.
    /// Keep meters as the only semantic size; this turns meters into localScale.
    /// </summary>
    [CreateAssetMenu(fileName = "FishSizingSettings", menuName = "GalacticFishing/Fish Sizing Settings")]
    public sealed class FishSizingSettings : ScriptableObject
    {
        [Tooltip("Assume 1 Unity unit equals this many real-world meters (usually 1).")]
        public float metersPerUnit = 1f;

        [Tooltip("Global display multiplier applied after converting meters to localScale.\nUse this to make fish reasonably large on screen without touching prefabs or sprites.")]
        [Min(0.01f)] public float globalVisualScale = 4.0f; // tweak until a 30cm fish looks OK vs boat

        private static FishSizingSettings _cached;
        public static FishSizingSettings LoadOrDefault()
        {
            if (_cached) return _cached;
            // Try Resources first so you can place it under Assets/Resources/
            _cached = Resources.Load<FishSizingSettings>("FishSizingSettings");
            if (!_cached)
            {
                // Fallback default so things still work if no asset is created yet.
                _cached = ScriptableObject.CreateInstance<FishSizingSettings>();
                _cached.metersPerUnit = 1f;
                _cached.globalVisualScale = 4.0f;
            }
            return _cached;
        }
    }
}
