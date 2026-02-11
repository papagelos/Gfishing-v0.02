using UnityEngine;

namespace GalacticFishing
{
    [CreateAssetMenu(fileName = "QualitySettings", menuName = "Galactic Fishing/Data/Quality Settings")]
    public sealed class QualitySettings : ScriptableObject
    {
        [Header("Bell Curve")]
        [Range(0, 100)] public int baseMean = 50;
        [Range(0.1f, 50f)] public float sigma = 12f;
        public int min = 0;
        public int max = 100;

        [Header("Bonuses (0..1)")]
        [Range(0f, 1f)] public float upwardBonus = 0f;   // shifts mean upward
        [Range(0f, 1f)] public float tightenBonus = 0f;  // tightens spread

        public float MeanBias => QualitySampling.MeanBiasFromBonus(upwardBonus);
        public float SigmaMul => QualitySampling.SigmaMulFromBonus(tightenBonus);

        public int Sample(System.Random rng = null) =>
            QualitySampling.Sample(baseMean, sigma, min, max, MeanBias, SigmaMul, rng);

        // Runtime access (drop the asset under Assets/Resources/QualitySettings.asset).
        static QualitySettings _active;
        public static QualitySettings Active
        {
            get
            {
                if (_active) return _active;
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<QualitySettings>("Assets/Resources/QualitySettings.asset");
                    if (asset) return _active = asset;
                }
#endif
                return _active = Resources.Load<QualitySettings>("QualitySettings");
            }
            set { _active = value; }
        }
    }
}
