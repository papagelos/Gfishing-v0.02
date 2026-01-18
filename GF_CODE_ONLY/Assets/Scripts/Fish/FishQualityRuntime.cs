using System;
using System.Reflection;
using UnityEngine;

namespace GalacticFishing
{
    /// <summary>
    /// Runtime quality roller. Attach to fish prefabs to compute a quality score on spawn.
    /// Prefers global QualitySettings but can fall back to manual parameters.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Galactic Fishing/Fish Quality Runtime")]
    public sealed class FishQualityRuntime : MonoBehaviour
    {
        [Header("When to roll")]
        public bool sampleOnAwake = true;

        [Header("Source")]
        [Tooltip("Use global QualitySettings asset (recommended). If false, uses manual fields below.")]
        public bool useQualitySettings = true;
        [Tooltip("If true and a FishIdentity->FishMeta is present, use its quality value as the mean.")]
        public bool preferMeanFromFishMeta = false;

        [Header("Manual values (used only when QualitySettings is disabled)")]
        [Range(0, 100)] public int manualMean = 50;
        [Range(0.1f, 50f)] public float manualSigma = 12f;
        public int min = 0;
        public int max = 100;
        [Range(0f, 1f)] public float upwardBonus = 0f;
        [Range(0f, 1f)] public float tightenBonus = 0f;

        [Header("Runtime value (read-only)")]
        [SerializeField] int _value;
        [SerializeField] bool _hasValue;
        public int Value => _value;
        public bool HasValue => _hasValue;

        void Awake()
        {
            if (sampleOnAwake)
                Resample();
        }

        [ContextMenu("Resample Now")]
        public void Resample()
        {
            _value = SampleInternal();
            _hasValue = true;
        }

        int SampleInternal()
        {
            int mean = manualMean;
            float sigma = manualSigma;
            float meanBias = QualitySampling.MeanBiasFromBonus(upwardBonus);
            float sigmaMul = QualitySampling.SigmaMulFromBonus(tightenBonus);

            var qs = useQualitySettings ? QualitySettings.Active : null;
            if (qs)
            {
                mean = qs.baseMean;
                sigma = qs.sigma;
                min = qs.min;
                max = qs.max;
                meanBias = qs.MeanBias;
                sigmaMul = qs.SigmaMul;
            }

            if (preferMeanFromFishMeta && TryGetQualityFromMeta(out var metaMean))
                mean = Mathf.Clamp(metaMean, min, max);

            return QualitySampling.Sample(mean, sigma, min, max, meanBias, sigmaMul, null);
        }

        [ContextMenu("Clear Value")]
        public void ClearValue()
        {
            _value = 0;
            _hasValue = false;
        }

        bool TryGetQualityFromMeta(out int quality)
        {
            quality = 0;
            var comps = GetComponents<Component>();
            foreach (var comp in comps)
            {
                if (!comp) continue;
                var type = comp.GetType();
                if (!type.Name.Contains("FishIdentity")) continue;

                object metaObj = null;
                var fi = type.GetField("meta", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                      ?? type.GetField("Meta", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (fi != null)
                    metaObj = fi.GetValue(comp);
                if (metaObj == null)
                {
                    var pi = type.GetProperty("meta", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                           ?? type.GetProperty("Meta", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (pi != null && pi.CanRead)
                        metaObj = pi.GetValue(comp);
                }
                if (metaObj is not ScriptableObject so) continue;
                var metaType = so.GetType();
                if (!metaType.Name.Contains("FishMeta")) continue;

                var qf = metaType.GetField("quality", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                      ?? metaType.GetField("Quality", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (qf != null)
                {
                    quality = Mathf.RoundToInt(Convert.ToSingle(qf.GetValue(so)));
                    return true;
                }
                var qp = metaType.GetProperty("quality", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                       ?? metaType.GetProperty("Quality", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (qp != null && qp.CanRead)
                {
                    quality = Mathf.RoundToInt(Convert.ToSingle(qp.GetValue(so)));
                    return true;
                }
            }
            return false;
        }
    }
}
