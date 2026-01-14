using UnityEngine;

namespace GalacticFishing
{
    /// Holds the spawned fish's length (in cm) rolled at runtime.
    [DisallowMultipleComponent]
    [AddComponentMenu("Galactic Fishing/Fish Length Runtime")]
    public sealed class FishLengthRuntime : MonoBehaviour
    {
        [Header("Runtime value (read-only)")]
        [SerializeField] float _valueCm;
        [SerializeField] bool _hasValue;
        public float ValueCm => _valueCm;
        public bool HasValue => _hasValue;

        [Header("Clamp")]
        public float minCm = 5f;
        public float maxCm = 1000f;

        /// Set from size in meters (the value your spawner already rolled).
        public void SetFromMeters(float meters)
        {
            _valueCm = Mathf.Clamp(meters * 100f, minCm, maxCm);
            _hasValue = true;
        }

        [ContextMenu("Clear Value")]
        public void ClearValue() { _valueCm = 0f; _hasValue = false; }
    }
}
