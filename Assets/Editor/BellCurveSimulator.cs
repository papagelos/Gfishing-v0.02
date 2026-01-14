#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;

namespace GalacticFishing.EditorTools
{
    public sealed class BellCurveSimulator : EditorWindow
    {
        int mean = 50, min = 0, max = 100, samples = 100_000;
        float sigma = 12f, upBonus = 0f, tighten = 0f;

        [MenuItem("Galactic Fishing/Quality/Bell Curve Simulator")]
        public static void Open() => GetWindow<BellCurveSimulator>("Bell Curve Simulator");

        void OnEnable() => SyncFromSettings();

        void OnGUI()
        {
            GUILayout.Label("Quality Parameters", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Create/Open Quality Settings", GUILayout.Height(22)))
                {
                    var s = CreateOrFindSettings();
                    Selection.activeObject = s;
                    SyncFromSettings();
                }
                if (GUILayout.Button("Sync From Settings", GUILayout.Width(160)))
                    SyncFromSettings();
            }

            EditorGUILayout.Space();
            mean    = EditorGUILayout.IntSlider("Mean", mean, min, max);
            sigma   = EditorGUILayout.Slider("Sigma", sigma, 0.1f, 50f);
            upBonus = EditorGUILayout.Slider("Upward Bonus (0..1)", upBonus, 0f, 1f);
            tighten = EditorGUILayout.Slider("Tighten Bonus (0..1)", tighten, 0f, 1f);
            samples = EditorGUILayout.IntField("Samples", samples);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Run & Save CSV", GUILayout.Height(24))) Run();
                if (GUILayout.Button("Apply To Settings", GUILayout.Width(160))) ApplyToSettings();
            }
            EditorGUILayout.HelpBox("CSV saved to Assets/Temp/quality_histogram.csv", MessageType.Info);
        }

        void SyncFromSettings()
        {
            var s = GalacticFishing.QualitySettings.Active;
            if (s)
            {
                mean = s.baseMean; sigma = s.sigma; min = s.min; max = s.max;
                upBonus = s.upwardBonus; tighten = s.tightenBonus;
            }
        }

        void ApplyToSettings()
        {
            var s = CreateOrFindSettings();
            s.baseMean = mean; s.sigma = sigma; s.min = min; s.max = max;
            s.upwardBonus = upBonus; s.tightenBonus = tighten;
            EditorUtility.SetDirty(s); AssetDatabase.SaveAssets();
        }

        static GalacticFishing.QualitySettings CreateOrFindSettings()
        {
            var s = GalacticFishing.QualitySettings.Active;
            if (s) return s;
            Directory.CreateDirectory("Assets/Resources");
            s = ScriptableObject.CreateInstance<GalacticFishing.QualitySettings>();
            AssetDatabase.CreateAsset(s, "Assets/Resources/QualitySettings.asset");
            AssetDatabase.SaveAssets();
            GalacticFishing.QualitySettings.Active = s;
            return s;
        }

        void Run()
        {
            float bias = GalacticFishing.QualitySampling.MeanBiasFromBonus(upBonus);
            float mul  = GalacticFishing.QualitySampling.SigmaMulFromBonus(tighten);
            int range = (max - min) + 1;
            var counts = new int[range];
            var rng = new System.Random(12345);
            for (int i = 0; i < samples; i++)
            {
                int q = GalacticFishing.QualitySampling.Sample(mean, sigma, min, max, bias, mul, rng);
                counts[q - min]++;
            }
            Directory.CreateDirectory("Assets/Temp");
            string path = "Assets/Temp/quality_histogram.csv";
            using (var sw = new StreamWriter(path))
            {
                sw.WriteLine("value,count");
                for (int v = min; v <= max; v++) sw.WriteLine($"{v},{counts[v - min]}");
            }
            AssetDatabase.Refresh();
            Debug.Log($"Saved histogram: {path}");
        }
    }
}
#endif
