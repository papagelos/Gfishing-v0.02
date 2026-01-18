using System;
using UnityEngine;

namespace GalacticFishing
{
    /// <summary>
    /// Quality bell-curve sampling (truncated normal), with optional upward bias and sigma scaling.
    /// </summary>
    public static class QualitySampling
    {
        public static int Sample(
            int mean = 50,
            float sigma = 10f,
            int min = 0,
            int max = 100,
            float meanBias = 0f,      // -1..+1 shift along [min,max] (0 = no shift)
            float sigmaMul = 1f,      // 1 = unchanged, 0.6 = tighter, 1.3 = wider
            System.Random rng = null)
        {
            if (max <= min) return min;
            float range = max - min;
            float t = Mathf.InverseLerp(min, max, mean);
            t = Mathf.Clamp01(t + meanBias);
            float effMean = Mathf.Lerp(min, max, t);
            float effSigma = Mathf.Max(0.0001f, sigma * Mathf.Max(0.01f, sigmaMul));

            // Truncated normal via rejection (few tries is fine for gameplay).
            for (int i = 0; i < 8; i++)
            {
                float z = NextStandardNormal(rng);
                int v = Mathf.RoundToInt(effMean + z * effSigma);
                if (v >= min && v <= max) return v;
            }
            return Mathf.Clamp(Mathf.RoundToInt(effMean), min, max);
        }

        public static float MeanBiasFromBonus(float bonus01) =>
            Mathf.Lerp(0f, 0.30f, Mathf.Clamp01(bonus01));      // map 0..1 → 0..+0.30 shift

        public static float SigmaMulFromBonus(float tighten01) =>
            Mathf.Lerp(1f, 0.60f, Mathf.Clamp01(tighten01));    // map 0..1 → 1.0x..0.6x

        // N(0,1) via Box–Muller
        public static float NextStandardNormal(System.Random rng = null)
        {
            double u1 = 1.0 - (rng != null ? rng.NextDouble() : UnityEngine.Random.value);
            double u2 = 1.0 - (rng != null ? rng.NextDouble() : UnityEngine.Random.value);
            double r  = Math.Sqrt(-2.0 * Math.Log(u1));
            double th = 2.0 * Math.PI * u2;
            return (float)(r * Math.Cos(th));
        }
    }
}
