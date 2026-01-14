#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GalacticFishing
{
    /// <summary>
    /// Unity 6.2-clean: builds Fish assets + prefabs and updates registry.
    /// Exposes a static API so the auto-builder can call it.
    /// </summary>
    public sealed class FishCatalogBuilder : EditorWindow
    {
        private FishCatalogSettings settings;

        [MenuItem("Tools/GalacticFishing/Fish Catalog Settings")]
        public static void CreateOrSelectSettings()
        {
            var path = "Assets/Editor/FishCatalogSettings.asset";
            var s = AssetDatabase.LoadAssetAtPath<FishCatalogSettings>(path);
            if (!s)
            {
                s = ScriptableObject.CreateInstance<FishCatalogSettings>();
                AssetDatabase.CreateAsset(s, path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            Selection.activeObject = s;
            EditorGUIUtility.PingObject(s);
        }

        [MenuItem("Tools/GalacticFishing/Build Fish Catalog")]
        public static void Open()
        {
            GetWindow<FishCatalogBuilder>("Fish Catalog");
        }

        private void OnEnable()
        {
            settings = FindSettings() ?? CreateDefaultSettingsAsset();
        }

        public static FishCatalogSettings FindSettings()
        {
            var guids = AssetDatabase.FindAssets("t:FishCatalogSettings");
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<FishCatalogSettings>(path);
            }
            return null;
        }

        private static FishCatalogSettings CreateDefaultSettingsAsset()
        {
            var s = ScriptableObject.CreateInstance<FishCatalogSettings>();
            const string path = "Assets/Editor/FishCatalogSettings.asset";
            var dir = Path.GetDirectoryName(path).Replace("\\", "/");
            if (!AssetDatabase.IsValidFolder(dir))
            {
                AssetDatabase.CreateFolder("Assets", "Editor");
            }
            AssetDatabase.CreateAsset(s, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return s;
        }

        void OnGUI()
        {
            if (!settings) settings = FindSettings() ?? CreateDefaultSettingsAsset();
            EditorGUILayout.HelpBox("Edit the settings asset or click Build. Auto-build uses these settings.", MessageType.Info);
            EditorGUILayout.ObjectField("Settings", settings, typeof(FishCatalogSettings), false);

            if (GUILayout.Button("Open Settings Asset")) CreateOrSelectSettings();

            GUILayout.Space(8);
            if (GUILayout.Button("Scan & Build Now", GUILayout.Height(32)))
            {
                BuildWithSettings(settings);
            }
        }

        // === Public static API (auto-builder calls this) ===
        public static void BuildWithSettings(FishCatalogSettings s)
        {
            if (s == null) { Debug.LogWarning("FishCatalog: No settings asset found."); return; }

            EnsureFolder(s.dataFolder);
            if (!s.useSharedPrefab) EnsureFolder(s.prefabsFolder);
            EnsureFolder(Path.GetDirectoryName(s.registryPath)?.Replace("\\", "/") ?? "Assets/Data");

            // Load or create registry
            var registry = AssetDatabase.LoadAssetAtPath<FishRegistry>(s.registryPath);
            if (!registry)
            {
                registry = ScriptableObject.CreateInstance<FishRegistry>();
                AssetDatabase.CreateAsset(registry, s.registryPath);
                EditorUtility.SetDirty(registry);
            }

            // Sprites
            var spriteGUIDs = AssetDatabase.FindAssets("t:Sprite", new[] { s.spritesFolder });
            int createdFish = 0, updatedFish = 0, createdPrefabs = 0;

            var allFish = new List<Fish>(registry.fishes.Where(f => f != null));

            for (int i = 0; i < spriteGUIDs.Length; i++)
            {
                string guid = spriteGUIDs[i];
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (!sprite) continue;

                string baseName = sprite.name.Trim();
                string fishAssetPath = $"{s.dataFolder}/Fish_{Sanitize(baseName)}.asset";

                // Create/update Fish asset
                var fish = AssetDatabase.LoadAssetAtPath<Fish>(fishAssetPath);
                bool isNew = false;
                if (!fish)
                {
                    fish = ScriptableObject.CreateInstance<Fish>();
                    fish.displayName = baseName;
                    fish.rarity = s.defaultRarity;
                    fish.baselineMeters = s.baselineMeters;
                    fish.sigmaLogSize = s.sigmaLogSize;
                    fish.nativeScaleMultiplier = s.nativeScaleMult;
                    AssetDatabase.CreateAsset(fish, fishAssetPath);
                    isNew = true;
                }

                fish.sprite = sprite;

                if (s.useSharedPrefab)
                {
                    fish.prefab = null; // spawner uses shared default
                }
                else
                {
                    string prefabPath = $"{s.prefabsFolder}/{Sanitize(baseName)}.prefab";
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

                    if (!prefab)
                    {
                        GameObject go;
                        if (s.baseFishPrefab)
                        {
                            go = (GameObject)PrefabUtility.InstantiatePrefab(s.baseFishPrefab);
                        }
                        else
                        {
                            go = new GameObject(baseName);
                            var sr = go.AddComponent<SpriteRenderer>();
                            sr.sortingLayerName = "Characters";
                            sr.sortingOrder = 5;
                            var rb = go.AddComponent<Rigidbody2D>();
                            rb.gravityScale = 0f;
                            go.AddComponent<FishController>();
                        }
                        prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                        Object.DestroyImmediate(go);
                        createdPrefabs++;
                    }

                    // If we're not using a shared prefab, lock the sprite into the per-species prefab
                    var srPrefab = prefab.GetComponentInChildren<SpriteRenderer>();
                    if (srPrefab != null && !s.useSharedPrefab)
                    {
                        srPrefab.sprite = sprite;
                        EditorUtility.SetDirty(prefab);
                    }

                    fish.prefab = prefab;
                }

                EditorUtility.SetDirty(fish);

                if (isNew) { createdFish++; allFish.Add(fish); }
                else       { updatedFish++; if (!allFish.Contains(fish)) allFish.Add(fish); }
            }

            allFish = allFish.Where(f => f != null).Distinct().OrderBy(f => f.displayName).ToList();
            registry.fishes = allFish;
            EditorUtility.SetDirty(registry);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Fish Catalog: sprites {spriteGUIDs.Length}, created {createdFish}, updated {updatedFish}, " +
                      (s.useSharedPrefab ? "shared BaseFish mode" : $"prefabs +{createdPrefabs}") +
                      $", registry: {s.registryPath}");
        }

        // Helpers
        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            var parts = folder.Split('/');
            string build = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = build + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(build, parts[i]);
                build = next;
            }
        }

        private static string Sanitize(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Replace(' ', '_');
        }
    }
}
#endif
