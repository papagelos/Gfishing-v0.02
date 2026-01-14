// Assets/Scripts/Fish/FishWorldRecords.cs
//
// Utility for computing *theoretical* world-record size/weight
// for a given Fish definition, based on its log-normal parameters.
// No wiring needed in Unity – just use FishWorldRecords.Instance.
//
// Uses the fact that if X ~ LogNormal(mu, sigma), then the p-quantile is:
//   Q(p) = exp(mu + sigma * z_p)
// where z_p is the standard normal quantile at probability p.
// For weight we treat: W = D * L^3 where both D and L are log-normal.

using UnityEngine;

namespace GalacticFishing
{
    /// <summary>
    /// Stateless helper that can estimate "world record" values
    /// (expected maximum length/weight) for a fish species.
    /// </summary>
    public sealed class FishWorldRecords
    {
        // --------------------------------------------------------
        // Singleton (no Unity wiring required)
        // --------------------------------------------------------
        private static FishWorldRecords _instance;
        public static FishWorldRecords Instance => _instance ?? (_instance = new FishWorldRecords());

        // Private ctor so nobody can 'new' it by accident.
        private FishWorldRecords() { }

        // --------------------------------------------------------
        // Public data type
        // --------------------------------------------------------

        public struct Record
        {
            /// <summary>Estimated maximum length in centimeters.</summary>
            public float maxLengthCm;

            /// <summary>Estimated maximum weight in kilograms.</summary>
            public float maxWeightKg;

            public bool IsValid => maxLengthCm > 0f || maxWeightKg > 0f;
        }

        // --------------------------------------------------------
        // Settings / constants
        // --------------------------------------------------------

        /// <summary>
        /// How many hypothetical catches we assume when computing
        /// the "world record" quantile. 200k = very extreme fish.
        /// </summary>
        private const int DefaultVirtualCatchCount = 200_000;

        private const float MinSigma = 0.0001f;
        private const float MinMeters = 0.0001f;

        // --------------------------------------------------------
        // Public API (several names for compatibility)
        // --------------------------------------------------------

        public bool TryGetWorldRecord(Fish fishDef, out Record record, int virtualCatchCount = DefaultVirtualCatchCount)
        {
            return TryGetRecord(fishDef, out record, virtualCatchCount);
        }

        public bool TryGetRecord(Fish fishDef, out Record record, int virtualCatchCount = DefaultVirtualCatchCount)
        {
            record = default;

            if (!fishDef)
                return false;

            if (virtualCatchCount < 2)
                virtualCatchCount = 2;

            // ---- Pull parameters from Fish definition ----
            float baselineMeters   = Mathf.Max(MinMeters, fishDef.baselineMeters);
            float sigmaSize        = Mathf.Max(MinSigma, fishDef.sigmaLogSize);
            float baselineDensityK = Mathf.Max(MinSigma, fishDef.baselineDensityK);
            float sigmaDensity     = Mathf.Max(MinSigma, fishDef.sigmaLogDensity);

            // ---- Choose an extreme quantile for N samples ----
            // For N draws, maximum is near quantile p ≈ 1 - 1/N
            float p = 1f - 1f / virtualCatchCount;
            float z = NormalQuantile(p);   // standard normal quantile

            // ---- Length distribution: L ~ LogNormal(mu_L, sigmaSize) ----
            float lengthMeters = baselineMeters * Mathf.Exp(sigmaSize * z);
            float lengthCm     = lengthMeters * 100f;

            // ---- Weight distribution: W = D * L^3 with independent log-normals ----
            // ln L ~ N( ln(baselineMeters),          sigmaSize^2 )
            // ln D ~ N( ln(baselineDensityK),        sigmaDensity^2 )
            // ln W = ln D + 3 ln L
            //      ~ N( ln(baselineDensityK * baselineMeters^3),
            //            sigmaDensity^2 + 9 sigmaSize^2 )
            float baselineWeight = baselineDensityK * baselineMeters * baselineMeters * baselineMeters;
            float sigmaWeight    = Mathf.Sqrt(sigmaDensity * sigmaDensity + 9f * sigmaSize * sigmaSize);
            float weightKg       = baselineWeight * Mathf.Exp(sigmaWeight * z);

            record = new Record
            {
                maxLengthCm = lengthCm,
                maxWeightKg = weightKg
            };
            return true;
        }

        public Record GetWorldRecord(Fish fishDef, int virtualCatchCount = DefaultVirtualCatchCount)
        {
            TryGetRecord(fishDef, out var r, virtualCatchCount);
            return r;
        }

        public Record GetRecord(Fish fishDef, int virtualCatchCount = DefaultVirtualCatchCount)
        {
            return GetWorldRecord(fishDef, virtualCatchCount);
        }

        // --------------------------------------------------------
        // Standard normal quantile approximation
        // (Peter J. Acklam's approximation, single-precision)
        // --------------------------------------------------------

        private static float NormalQuantile(float p)
        {
            // Clamp away from 0 and 1 to avoid infinities
            p = Mathf.Clamp(p, 1e-6f, 1f - 1e-6f);

            // For p < 0.5, use symmetry: Q(p) = -Q(1-p)
            if (p < 0.5f)
            {
                return -RationalApprox(Mathf.Sqrt(-2f * Mathf.Log(p)));
            }
            else
            {
                return RationalApprox(Mathf.Sqrt(-2f * Mathf.Log(1f - p)));
            }
        }

        private static float RationalApprox(float t)
        {
            // Coefficients (single precision)
            const float c0 = 2.515517f;
            const float c1 = 0.802853f;
            const float c2 = 0.010328f;

            const float d0 = 1.432788f;
            const float d1 = 0.189269f;
            const float d2 = 0.001308f;

            float num = (c2 * t + c1) * t + c0;
            float den = ((d2 * t + d1) * t + d0) * t + 1f;
            return t - num / den;
        }
    }
}
