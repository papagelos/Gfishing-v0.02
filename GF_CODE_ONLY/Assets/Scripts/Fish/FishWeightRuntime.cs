using UnityEngine;

namespace GalacticFishing
{
    /// <summary>
    /// Holds the spawned fish's weight (in kg) derived from the rolled size,
    /// using a per-instance density coefficient (densityK) provided by the spawner.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Galactic Fishing/Fish Weight Runtime")]
    public sealed class FishWeightRuntime : MonoBehaviour
    {
        [Header("Runtime value (read-only)")]
        [SerializeField] float _valueKg;
        [SerializeField] bool  _hasValue;

        // Per-instance density coefficient used for this fish
        [SerializeField] float _densityK;

        /// <summary>Current weight in kg (only valid if HasValue is true).</summary>
        public float ValueKg => _valueKg;

        /// <summary>True once a value has been assigned via SetFromMeters.</summary>
        public bool HasValue => _hasValue;

        /// <summary>Density coefficient actually used for this instance.</summary>
        public float DensityK => _densityK;

        [Header("Clamp")]
        public float minKg = 0.05f;
        public float maxKg = 1000000f;

        /// <summary>
        /// Set from size in meters and a rolled density coefficient.
        /// The spawner is responsible for rolling densityK (e.g. via a log-normal draw).
        /// </summary>
        public void SetFromMeters(float meters, float densityK)
        {
            // Store the runtime density used for this individual
            _densityK = Mathf.Max(0.0001f, densityK);

            // Simple mass ~ densityK * L^3
            float kg = _densityK * meters * meters * meters;

            _valueKg = Mathf.Clamp(kg, minKg, maxKg);
            _hasValue = true;
        }

        [ContextMenu("Clear Value")]
        public void ClearValue()
        {
            _valueKg = 0f;
            _hasValue = false;
            _densityK = 0f;
        }
    }
}
