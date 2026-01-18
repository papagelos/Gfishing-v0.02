using UnityEngine;

namespace GalacticFishing
{
    public static class FishSizing
    {
        /// <summary>
        /// Draws a log-normal size around baselineMeters with sigmaLog controlling spread.
        /// sigmaLog ~ 0.12 (narrow), 0.2 (medium), 0.3+ (wide).
        /// </summary>
        public static float DrawLogNormalSizeMeters(float baselineMeters, float sigmaLog)
        {
            // Convert baseline (median) to mu for log-normal
            // median = exp(mu) → mu = ln(median)
            float mu = Mathf.Log(Mathf.Max(0.0001f, baselineMeters));
            float sigma = Mathf.Max(0.0001f, sigmaLog);

            // Box–Muller
            float u1 = Mathf.Clamp01(Random.value);
            float u2 = Mathf.Clamp01(Random.value);
            float z = Mathf.Sqrt(-2f * Mathf.Log(u1 + 1e-7f)) * Mathf.Cos(2f * Mathf.PI * u2);

            // log-space sample
            float ln = mu + sigma * z;
            float meters = Mathf.Exp(ln);
            return Mathf.Max(0.0001f, meters);
        }

        /// <summary>
        /// Draws a log-normal density coefficient around baselineDensityK with sigmaLog controlling spread.
        /// baselineDensityK is the median of the distribution (e.g. 8.0f), sigmaLog the log-space sigma (e.g. 0.1f).
        /// </summary>
        public static float DrawLogNormalDensityK(float baselineDensityK, float sigmaLog)
        {
            // Convert baseline (median) to mu for log-normal
            float mu = Mathf.Log(Mathf.Max(0.0001f, baselineDensityK));
            float sigma = Mathf.Max(0.0001f, sigmaLog);

            // Box–Muller (same structure as DrawLogNormalSizeMeters)
            float u1 = Mathf.Clamp01(Random.value);
            float u2 = Mathf.Clamp01(Random.value);
            float z = Mathf.Sqrt(-2f * Mathf.Log(u1 + 1e-7f)) * Mathf.Cos(2f * Mathf.PI * u2);

            // log-space sample
            float ln = mu + sigma * z;
            float densityK = Mathf.Exp(ln);
            return Mathf.Max(0.0001f, densityK);
        }

        /// <summary>
        /// Compute a uniform localScale so that the fish sprite's visible length matches the desired meters.
        /// We use the sprite's width in world units at scale=1 as the art baseline.
        /// </summary>
        public static float ComputeScaleFromSizeMeters(Fish fishDef, SpriteRenderer sr, float desiredMeters)
        {
            var settings = FishSizingSettings.LoadOrDefault();

            // If no sprite yet, assume 1 meter width at scale=1 as a neutral fallback.
            if (!sr || !sr.sprite)
            {
                float fallbackMetersAtScaleOne = 1f * settings.metersPerUnit; // 1 Unity unit = metersPerUnit meters
                float sFallback = desiredMeters / Mathf.Max(0.0001f, fallbackMetersAtScaleOne);
                return Mathf.Max(0.0001f, sFallback) * settings.globalVisualScale;
            }

            // Sprite width in Unity units at scale = 1
            // units = pixels / PPU; if 1 unit = metersPerUnit meters, widthMetersAtScaleOne = units * metersPerUnit
            float widthUnitsAtScaleOne = sr.sprite.rect.width / sr.sprite.pixelsPerUnit;
            float widthMetersAtScaleOne = Mathf.Max(0.0001f, widthUnitsAtScaleOne * settings.metersPerUnit);

            float uniformScale = desiredMeters / widthMetersAtScaleOne;

            // One global visual multiplier so things aren’t tiny. This is our only “extra” factor.
            uniformScale *= settings.globalVisualScale;

            return Mathf.Max(0.0001f, uniformScale);
        }

        /// <summary>
        /// Applies a uniform scale while preserving sign on X (if already flipped).
        /// </summary>
        public static void ApplyTransformScale(Transform t, float uniformScale)
        {
            var ls = t.localScale;
            float signX = Mathf.Sign(ls.x) == 0 ? 1f : Mathf.Sign(ls.x);
            t.localScale = new Vector3(Mathf.Abs(uniformScale) * signX, Mathf.Abs(uniformScale), 1f);
        }
    }
}
