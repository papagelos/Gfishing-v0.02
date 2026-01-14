#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine; // <-- needed for Debug.Log

namespace GalacticFishing
{
    /// <summary>
    /// Watches the fish sprites folder and rebuilds the catalog after imports/moves.
    /// Works with a FishCatalogBuilder that exposes:
    ///   - static FishCatalogSettings FindSettings();
    ///   - static void CreateOrSelectSettings();
    ///   - static void BuildWithSettings(FishCatalogSettings s);
    /// </summary>
    public sealed class FishCatalogAutoBuilder : AssetPostprocessor
    {
        static readonly string[] kExts = { ".png", ".psd", ".psb" };

        [MenuItem("Tools/GalacticFishing/Auto-Build On Import/Enable")]
        public static void EnableAuto()
        {
            // Ensure settings asset exists
            FishCatalogBuilder.CreateOrSelectSettings();
            var s = FishCatalogBuilder.FindSettings();
            if (s == null)
            {
                Debug.LogError("[Fish Catalog] Could not locate settings after CreateOrSelectSettings().");
                return;
            }

            s.autoBuildOnImport = true;
            EditorUtility.SetDirty(s);
            AssetDatabase.SaveAssets();
            Debug.Log("[Fish Catalog] Auto-build ENABLED.");
        }

        [MenuItem("Tools/GalacticFishing/Auto-Build On Import/Disable")]
        public static void DisableAuto()
        {
            var s = FishCatalogBuilder.FindSettings();
            if (s == null)
            {
                Debug.LogWarning("[Fish Catalog] Settings not found; nothing to disable.");
                return;
            }

            s.autoBuildOnImport = false;
            EditorUtility.SetDirty(s);
            AssetDatabase.SaveAssets();
            Debug.Log("[Fish Catalog] Auto-build DISABLED.");
        }

        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] movedTo, string[] movedFrom)
        {
            var s = FishCatalogBuilder.FindSettings();
            if (s == null || !s.autoBuildOnImport) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            // Normalize path + make StartsWith case-insensitive
            string spritesRoot = (string.IsNullOrEmpty(s.spritesFolder) ? "Assets/Sprites/Fish" : s.spritesFolder)
                                 .Replace('\\', '/');

            bool touchesSprites =
                imported.Concat(movedTo).Any(path =>
                {
                    if (string.IsNullOrEmpty(path)) return false;
                    string p = path.Replace('\\', '/');
                    string ext = Path.GetExtension(p).ToLowerInvariant();
                    return p.StartsWith(spritesRoot, StringComparison.OrdinalIgnoreCase)
                           && kExts.Contains(ext);
                });

            if (!touchesSprites) return;

            // Delay so multiple imports collapse into one rebuild
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;

                try
                {
                    FishCatalogBuilder.BuildWithSettings(s);
                    Debug.Log("[Fish Catalog] Auto-built after import.");
                }
                catch (Exception ex)
                {
                    Debug.LogError("[Fish Catalog] Auto-build failed: " + ex.Message);
                }
            };
        }
    }
}
#endif
