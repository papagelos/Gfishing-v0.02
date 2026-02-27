#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using GalacticFishing.Minigames.HexWorld;
using GalacticFishing.Data;

public sealed class HexWorldPropCatalogBuilder : EditorWindow
{
    private const string SpritesFolder = "Assets/Sprites/Biomes";
    private const string GemsFolder = "Assets/Sprites/Gems";
    private const string GemSpritePrefix = "gem_";
    private const string PropPrefabsFolder = "Assets/Minigames/HexWorld3D/Prefabs/Props";
    private const string PropDefinitionsFolder = "Assets/Minigames/HexWorld3D/Definitions/Props";
    private const string GeneratedMeshesFolder = "Assets/Minigames/HexWorld3D/Prefabs/Props/GeneratedMeshes";
    private const string DungeonResourceFolder = "Assets/Minigames/Dungeon3D/Data/Resources";
    private const string GemIdScriptPath = "Assets/Scripts/Data/GemId.cs";
    private const string DungeonGemRegistryAssetPath = "Assets/Minigames/Dungeon3D/Definitions/DungeonGemRegistry_Main.asset";
    private const string DungeonScenePath = "Assets/Minigames/Dungeon3D/Scenes/Dungeon_Minigame.unity";
    private const string HexWorldResourceIdScriptPath = "Assets/Minigames/HexWorld3D/Scripts/Village/HexWorldResourceId.cs";
    private const string PropRegistryAssetPath = "Assets/Minigames/HexWorld3D/Definitions/PropRegistry_Main.asset";
    private const string VillageControllerPrefabPath = "Assets/Minigames/Prefabs/Prefab_HexWorld3D_Village.prefab";
    private const string ShadowMaterialPath = "Assets/Minigames/HexWorld3D/Materials/Props/Mat_ShadowSilhouette.mat";
    private const string TemplatePrefabPath = "Assets/Minigames/HexWorld3D/Prefabs/Props/PF_PropsDefault.prefab";
    private const string MiningDustPrefabPath = "Assets/Minigames/Dungeon3D/Prefabs/FX/PF_MiningDust.prefab";
    private const string MiningDustSpriteSheetPath = "Assets/Sprites/FX/SP_MiningParticles.png";
    private const string PropScaleManifestFileName = "PropScaleManifest.json";
    private const string SpriteRenderMaterialGuid = "9dfc825aed78fcd4ba02077103263b40";
    private const string PropScalePrefKey = "HexWorldPropCatalogBuilder.PropScale";
    private const string ShadowYawPrefKey = "HexWorldPropCatalogBuilder.ShadowYaw";
    private const string ShadowUseSunPrefKey = "HexWorldPropCatalogBuilder.ShadowUseSun";
    private const byte AlphaThreshold = 10;
    private static readonly string[] ResourceSuffixes = { "_Ore", "_Fruit", "_Licence", "_Material", "_Part" };
    private float _propScale = 0.1f;
    private float _shadowYaw = -45f;
    private bool _useSun = true;
    private string _utilityPath = "Assets/Sprites/Buildings";

    [Serializable]
    private sealed class ManifestEntry
    {
        public string propId;
        public string PropId;
        public string PropID;
        public string id;
        public string key;
        public string safeName;
        public string name;
        public float scale;
        public float Scale;
        public float value;
        public float Value;
        public float masterScale;
        public float MasterScale;
    }

    [Serializable]
    private sealed class ManifestRoot
    {
        public List<ManifestEntry> entries;
        public List<ManifestEntry> items;
        public List<ManifestEntry> scales;
    }

    [MenuItem("Galactic Fishing/Catalogs/HexWorld Props")]
    public static void OpenWindow()
    {
        GetWindow<HexWorldPropCatalogBuilder>("Prop Catalog Builder");
    }

    private void OnEnable()
    {
        _propScale = EditorPrefs.GetFloat(PropScalePrefKey, _propScale);
        _shadowYaw = EditorPrefs.GetFloat(ShadowYawPrefKey, _shadowYaw);
        _useSun = EditorPrefs.GetBool(ShadowUseSunPrefKey, _useSun);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("HexWorld Prop Catalog Builder", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Processes PNGs recursively in Assets/Sprites/Biomes/**/Props, creates/updates prop prefabs and definitions, then rewires propCatalog on the village controller prefab.",
            MessageType.Info);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Sprites Folder", SpritesFolder);
        EditorGUILayout.LabelField("Prefabs Folder", PropPrefabsFolder);
        EditorGUILayout.LabelField("Definitions Folder", PropDefinitionsFolder);
        EditorGUILayout.LabelField("Registry Asset", PropRegistryAssetPath);
        EditorGUILayout.LabelField("Controller Prefab", VillageControllerPrefabPath);
        EditorGUILayout.LabelField("Template Prefab", TemplatePrefabPath);
        EditorGUILayout.LabelField("Shadow Material", ShadowMaterialPath);

        EditorGUILayout.Space();
        _propScale = Mathf.Max(0.001f, EditorGUILayout.FloatField("PROP SCALE", _propScale));
        EditorPrefs.SetFloat(PropScalePrefKey, _propScale);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("GLOBAL SHADOW SETTINGS", EditorStyles.boldLabel);
        _shadowYaw = EditorGUILayout.FloatField("Shadow Yaw (Degrees)", _shadowYaw);
        _useSun = EditorGUILayout.Toggle("Use Sun If Available", _useSun);
        EditorPrefs.SetFloat(ShadowYawPrefKey, _shadowYaw);
        EditorPrefs.SetBool(ShadowUseSunPrefKey, _useSun);

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
        {
            if (GUILayout.Button("Rebuild Prop Catalog", GUILayout.Height(34f)))
            {
                RebuildCatalog(_propScale, _shadowYaw, _useSun);
            }

            if (GUILayout.Button("Make Prop Scales Permanent", GUILayout.Height(28f)))
            {
                MakePropScalesPermanentFromManifest();
            }

            if (GUILayout.Button(new GUIContent(
                    "Clear and Rebuild Catalog",
                    "DESTRUCTIVE: Deletes orphan generated prop prefabs/definitions and rebuilds from current PNG sources."),
                GUILayout.Height(30f)))
            {
                bool confirmed = EditorUtility.DisplayDialog(
                    "Clear and Rebuild Prop Catalog",
                    "This will delete orphan generated prop assets that no longer have source PNGs, clear the prop registry list, and rebuild the catalog. Continue?",
                    "Clear and Rebuild",
                    "Cancel");
                if (confirmed)
                    ClearAndRebuildCatalog(_propScale, _shadowYaw, _useSun);
            }
        }

        GUILayout.Space(20);
        EditorGUILayout.LabelField("SPRITE UTILITIES", EditorStyles.boldLabel);
        _utilityPath = EditorGUILayout.TextField("UTILITY PATH", _utilityPath);

        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
        {
            if (GUILayout.Button("Set Pivot on Sprites in Path"))
            {
                BatchProcessPivots(_utilityPath);
            }
        }
    }

    public static void RebuildCatalog()
    {
        RebuildCatalog(
            EditorPrefs.GetFloat(PropScalePrefKey, 0.1f),
            EditorPrefs.GetFloat(ShadowYawPrefKey, -45f),
            EditorPrefs.GetBool(ShadowUseSunPrefKey, true));
    }

    private static void RebuildCatalog(float propScale, float shadowYaw, bool useSun)
    {
        if (!AssetDatabase.IsValidFolder(SpritesFolder))
        {
            Debug.LogError($"[HexWorldPropCatalogBuilder] Missing folder: {SpritesFolder}");
            return;
        }

        EnsureFolder(PropPrefabsFolder);
        EnsureFolder(PropDefinitionsFolder);
        EnsureFolder(GeneratedMeshesFolder);

        string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { SpritesFolder });
        var texturePaths = textureGuids
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path =>
                path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) &&
                IsBiomePropPath(path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (texturePaths.Count == 0)
        {
            Debug.LogWarning($"[HexWorldPropCatalogBuilder] No PNG files found in {SpritesFolder}");
            return;
        }

        int texturesProcessed = 0;
        int prefabsCreated = 0;
        int prefabsUpdated = 0;
        int defsCreated = 0;
        int defsUpdated = 0;
        int gemIdsAdded = 0;
        int gemRegistryCount = 0;
        Material shadowMaterial = AssetDatabase.LoadAssetAtPath<Material>(ShadowMaterialPath);
        if (shadowMaterial == null)
            Debug.LogWarning($"[HexWorldPropCatalogBuilder] Shadow material not found: {ShadowMaterialPath}");

        foreach (string texturePath in texturePaths)
        {
            try
            {
                string rawName = Path.GetFileNameWithoutExtension(texturePath).Trim();
                if (string.IsNullOrEmpty(rawName))
                    continue;

                if (rawName.StartsWith("recource_", StringComparison.OrdinalIgnoreCase))
                {
                    Debug.LogWarning(
                        $"[HexWorldPropCatalogBuilder] '{rawName}' uses 'recource_' (typo). " +
                        "Use the standard 'resource_' prefix for automatic dungeon resource setup.");
                }

                string biomeGroup = ExtractBiomeGroupFromBiomePropPath(texturePath);
                if (string.IsNullOrEmpty(biomeGroup))
                    continue;

                bool isDungeonResourceFolder = IsBiomeDungeonResourcePath(texturePath);
                string safeName = MakeSafeAssetName(rawName);
                string displayName = rawName.ToUpperInvariant();

                if (isDungeonResourceFolder &&
                    !safeName.StartsWith("resource_", StringComparison.OrdinalIgnoreCase))
                {
                    Debug.LogWarning(
                        $"[HexWorldPropCatalogBuilder] '{texturePath}' is in /DungeonResources/ but does not use the 'resource_' prefix. " +
                        "Skipping to avoid adding it as a village prop.");
                    continue;
                }

                HexWorldPropDefinition existingDef =
                    AssetDatabase.LoadAssetAtPath<HexWorldPropDefinition>($"{PropDefinitionsFolder}/Prop_{safeName}.asset");
                float masterScale = ResolveDefinitionMasterScale(existingDef, propScale);

                Sprite sprite = ConfigureAndLoadSprite(texturePath, rawName, ref texturesProcessed);
                if (sprite == null)
                {
                    Debug.LogWarning($"[HexWorldPropCatalogBuilder] Could not load sprite from '{texturePath}', skipped.");
                    continue;
                }

                GameObject propPrefab = CreateOrUpdatePropPrefab(
                    safeName,
                    sprite,
                    masterScale,
                    shadowMaterial,
                    shadowYaw,
                    useSun,
                    ref prefabsCreated,
                    ref prefabsUpdated);
                if (propPrefab == null)
                {
                    Debug.LogWarning($"[HexWorldPropCatalogBuilder] Could not create prefab for '{texturePath}', skipped.");
                    continue;
                }

                CreateOrUpdateDefinition(safeName, displayName, biomeGroup, sprite, propPrefab, masterScale, ref defsCreated, ref defsUpdated);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[HexWorldPropCatalogBuilder] Failed processing '{texturePath}': {ex}");
            }
        }

        gemIdsAdded = SyncGemIdEnumFromGemsFolder();
        gemRegistryCount = SyncDungeonGemRegistryFromGemsFolder();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var defs = LoadPropDefinitions();
        var registry = GetOrCreatePropRegistry();
        int wiredRegistryCount = WireRegistry(registry, defs);
        int wiredPrefabCount = WireCatalogToVillageControllerPrefab(defs);
        int wiredSceneCount = WireCatalogToSceneInstance(defs);
        int wiredDungeonResourceCount = WireCatalogToDungeonGenerator();

        Debug.Log(
            $"[HexWorldPropCatalogBuilder] Done. PNGs: {texturePaths.Count}, processed: {texturesProcessed}, " +
            $"prefabs created/updated: {prefabsCreated}/{prefabsUpdated}, definitions created/updated: {defsCreated}/{defsUpdated}, " +
            $"registry/prefab/scene/dungeonResources wired: {wiredRegistryCount}/{wiredPrefabCount}/{wiredSceneCount}/{wiredDungeonResourceCount}, " +
            $"GemId added: {gemIdsAdded}, gems registered: {gemRegistryCount}.");
    }

    private static float ResolveDefinitionMasterScale(HexWorldPropDefinition def, float fallbackScale)
    {
        float fallback = Mathf.Max(0.001f, fallbackScale);
        if (def == null)
            return fallback;

        if (def.masterScale > 0f)
            return def.masterScale;

        if (def.scale > 0f)
            return def.scale;

        return fallback;
    }

    private static void MakePropScalesPermanentFromManifest()
    {
        if (!TryResolvePropScaleManifestPath(out string manifestPath, out List<string> checkedPaths))
        {
            Debug.LogWarning(
                "[HexWorldPropCatalogBuilder] Scale manifest not found. " +
                $"Checked paths:\n - {string.Join("\n - ", checkedPaths)}\n" +
                "Path separator mixing is not the issue; .NET handles '/' and '\\' on Windows. " +
                "Run the runtime prop-scaling workflow first (the part that writes PropScaleManifest.json), then try again.");
            return;
        }

        string json;
        try
        {
            json = File.ReadAllText(manifestPath);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[HexWorldPropCatalogBuilder] Failed reading scale manifest: {ex.Message}");
            return;
        }

        Dictionary<string, float> manifestScales = ParseManifestScales(json);
        if (manifestScales.Count == 0)
        {
            Debug.LogWarning(
                $"[HexWorldPropCatalogBuilder] Scale manifest parsed with zero entries: {manifestPath}");
            return;
        }

        List<HexWorldPropDefinition> defs = LoadPropDefinitions();
        var byId = new Dictionary<string, HexWorldPropDefinition>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < defs.Count; i++)
        {
            HexWorldPropDefinition def = defs[i];
            if (def == null)
                continue;

            if (!string.IsNullOrWhiteSpace(def.id))
                byId[MakeSafeAssetName(def.id)] = def;
            byId[MakeSafeAssetName(def.name.Replace("Prop_", string.Empty))] = def;
        }

        int updated = 0;
        int missing = 0;
        foreach (var kv in manifestScales)
        {
            string id = MakeSafeAssetName(kv.Key);
            if (string.IsNullOrWhiteSpace(id))
                continue;

            if (!byId.TryGetValue(id, out HexWorldPropDefinition def) || def == null)
            {
                missing++;
                continue;
            }

            float bakedScale = Mathf.Max(0.001f, kv.Value);
            def.masterScale = bakedScale;
            def.scale = bakedScale;
            EditorUtility.SetDirty(def);
            updated++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"[HexWorldPropCatalogBuilder] Make Prop Scales Permanent complete. " +
            $"Updated: {updated}, Missing definitions: {missing}, Manifest entries: {manifestScales.Count}. " +
            $"Source: {manifestPath}");
    }

    private static string GetPropScaleManifestPath()
    {
        // Primary expected location (same root as village autosave).
        return Path.Combine(
            Application.persistentDataPath,
            "GalacticFishing",
            "HexWorldVillage",
            PropScaleManifestFileName);
    }

    private static bool TryResolvePropScaleManifestPath(out string resolvedPath, out List<string> checkedPaths)
    {
        checkedPaths = GetPropScaleManifestCandidatePaths();
        for (int i = 0; i < checkedPaths.Count; i++)
        {
            string path = checkedPaths[i];
            if (File.Exists(path))
            {
                resolvedPath = path;
                return true;
            }
        }

        resolvedPath = null;
        return false;
    }

    private static List<string> GetPropScaleManifestCandidatePaths()
    {
        string primary = GetPropScaleManifestPath();
        string saveRoot = Path.Combine(Application.persistentDataPath, "GalacticFishing", "HexWorldVillage");
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        var candidates = new List<string>
        {
            primary,
            Path.Combine(saveRoot, "presets", PropScaleManifestFileName),
            Path.Combine(Application.persistentDataPath, PropScaleManifestFileName),
            Path.Combine(projectRoot, PropScaleManifestFileName),
            Path.Combine(projectRoot, "Temp", PropScaleManifestFileName)
        };

        // De-duplicate while preserving order.
        var unique = new List<string>(candidates.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < candidates.Count; i++)
        {
            string full = NormalizeFullPath(candidates[i]);
            if (seen.Add(full))
                unique.Add(full);
        }

        return unique;
    }

    private static string NormalizeFullPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return path;
        }
    }

    private static Dictionary<string, float> ParseManifestScales(string json)
    {
        var result = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json))
            return result;

        string trimmed = json.Trim();
        string wrapped = trimmed.StartsWith("[", StringComparison.Ordinal)
            ? "{\"entries\":" + trimmed + "}"
            : trimmed;

        try
        {
            ManifestRoot root = JsonUtility.FromJson<ManifestRoot>(wrapped);
            AddManifestEntries(result, root != null ? root.entries : null);
            AddManifestEntries(result, root != null ? root.items : null);
            AddManifestEntries(result, root != null ? root.scales : null);
        }
        catch
        {
            // Fall through to a permissive key/value parser.
        }

        if (result.Count > 0)
            return result;

        // Fallback for dictionary style manifests: { "Oak_Tree": 0.2, ... }
        MatchCollection matches = Regex.Matches(
            trimmed,
            "\"(?<key>[^\"]+)\"\\s*:\\s*(?<value>-?\\d+(?:\\.\\d+)?)",
            RegexOptions.CultureInvariant);
        for (int i = 0; i < matches.Count; i++)
        {
            string key = matches[i].Groups["key"].Value;
            string valueText = matches[i].Groups["value"].Value;
            if (string.IsNullOrWhiteSpace(key))
                continue;
            if (string.Equals(key, "entries", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "items", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "scales", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!float.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
                continue;

            result[MakeSafeAssetName(key)] = parsed;
        }

        return result;
    }

    private static void AddManifestEntries(Dictionary<string, float> result, List<ManifestEntry> entries)
    {
        if (entries == null)
            return;

        for (int i = 0; i < entries.Count; i++)
        {
            ManifestEntry entry = entries[i];
            if (entry == null)
                continue;

            string id = !string.IsNullOrWhiteSpace(entry.propId) ? entry.propId :
                        !string.IsNullOrWhiteSpace(entry.PropId) ? entry.PropId :
                        !string.IsNullOrWhiteSpace(entry.PropID) ? entry.PropID :
                        !string.IsNullOrWhiteSpace(entry.id) ? entry.id :
                        !string.IsNullOrWhiteSpace(entry.safeName) ? entry.safeName :
                        !string.IsNullOrWhiteSpace(entry.key) ? entry.key :
                        entry.name;
            if (string.IsNullOrWhiteSpace(id))
                continue;

            float scale = entry.MasterScale != 0f ? entry.MasterScale :
                          entry.masterScale != 0f ? entry.masterScale :
                          entry.Scale != 0f ? entry.Scale :
                          entry.scale != 0f ? entry.scale :
                          entry.Value != 0f ? entry.Value :
                          entry.value;
            if (Mathf.Approximately(scale, 0f))
                continue;

            result[MakeSafeAssetName(id)] = scale;
        }
    }

    private static void ClearAndRebuildCatalog(float propScale, float shadowYaw, bool useSun)
    {
        if (!AssetDatabase.IsValidFolder(SpritesFolder))
        {
            Debug.LogError($"[HexWorldPropCatalogBuilder] Missing folder: {SpritesFolder}");
            return;
        }

        EnsureFolder(PropPrefabsFolder);
        EnsureFolder(PropDefinitionsFolder);
        EnsureFolder(GeneratedMeshesFolder);

        HashSet<string> sourceSafeNamesAll = CollectSourceSafeNames(includeDungeonResources: true);
        if (sourceSafeNamesAll.Count == 0)
        {
            Debug.LogWarning(
                $"[HexWorldPropCatalogBuilder] No source PNGs found under '{SpritesFolder}'. Destructive rebuild aborted for safety.");
            return;
        }

        HashSet<string> sourceSafeNamesPropsOnly = CollectSourceSafeNames(includeDungeonResources: false);

        int deletedPrefabs = DeleteOrphanPropPrefabs(sourceSafeNamesAll);
        int deletedDefs = DeleteOrphanPropDefinitions(sourceSafeNamesPropsOnly);
        int deletedMeshes = DeleteOrphanGeneratedMeshes(sourceSafeNamesAll);
        int clearedRegistry = ClearPropRegistryList();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"[HexWorldPropCatalogBuilder] Clear step complete. Deleted prefabs/definitions/meshes: " +
            $"{deletedPrefabs}/{deletedDefs}/{deletedMeshes}. Registry entries cleared: {clearedRegistry}.");

        RebuildCatalog(propScale, shadowYaw, useSun);
    }

    private static Sprite ConfigureAndLoadSprite(string texturePath, string rawName, ref int texturesProcessed)
    {
        try
        {
            var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer == null)
                return null;

            bool oldReadable = importer.isReadable;

            // Use TextureImporterSettings to avoid CS1061 property errors [1]
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);

            bool isTransparent = false;
            try
            {
                bool changed = false;

                if (importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    changed = true;
                }

                if (importer.spriteImportMode != SpriteImportMode.Single)
                {
                    importer.spriteImportMode = SpriteImportMode.Single;
                    changed = true;
                }

                if (importer.maxTextureSize != 512)
                {
                    importer.maxTextureSize = 512;
                    changed = true;
                }

                // Must be readable to scan pixels for the pivot [2, 3]
                if (!importer.isReadable)
                {
                    importer.isReadable = true;
                    changed = true;
                }

                if (changed)
                {
                    importer.SaveAndReimport();
                    AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);
                }

                // Perform Alpha-Based Pivot Calculation [1]
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                if (texture != null)
                {
                    if (!TryGetOpaqueBounds(texture, out int minX, out int maxX, out int minY, out _))
                    {
                        isTransparent = true;
                    }
                    else
                    {
                        float pivotX = ((minX + maxX) * 0.5f + 0.5f) / texture.width;
                        float pivotY = minY / (float)texture.height;
                        Vector2 newPivot = new Vector2(Mathf.Clamp01(pivotX), Mathf.Clamp01(pivotY));

                        // Apply settings via the settings object to bypass CS1061 [1]
                        settings.spriteMode = (int)SpriteImportMode.Single;
                        settings.spriteAlignment = (int)SpriteAlignment.Custom;
                        settings.spritePivot = newPivot;
                        importer.SetTextureSettings(settings);
                        importer.SaveAndReimport();
                        AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);
                    }
                }
            }
            finally
            {
                RestoreReadable(importer, oldReadable);
                AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);
            }

            if (isTransparent)
            {
                Debug.LogWarning($"[HexWorldPropCatalogBuilder] Image '{rawName}' is empty/transparent, skipped.");
                return null;
            }

            var assets = AssetDatabase.LoadAllAssetsAtPath(texturePath);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Sprite s)
                    return s;
            }

            // Direct fallback in case sub-asset enumeration lags behind import.
            Sprite direct = AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);
            if (direct != null)
                return direct;

            return null;
        }
        finally
        {
            texturesProcessed++;
        }
    }

    // Ported helper methods from notebooklm.txt [1, 3]
    private static void RestoreReadable(TextureImporter importer, bool oldReadable)
    {
        if (importer != null && importer.isReadable != oldReadable)
        {
            importer.isReadable = oldReadable;
            importer.SaveAndReimport();
        }
    }

    private static bool TryGetOpaqueBounds(Texture2D tex, out int minX, out int maxX, out int minY, out int maxY)
    {
        minX = minY = int.MaxValue;
        maxX = maxY = int.MinValue;
        Color32[] pixels = tex.GetPixels32();
        int w = tex.width;
        int h = tex.height;
        bool found = false;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (pixels[y * w + x].a > AlphaThreshold)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                    found = true;
                }
            }
        }
        return found;
    }

    private static GameObject CreateOrUpdatePropPrefab(
        string safeName,
        Sprite sprite,
        float masterScale,
        Material shadowMaterial,
        float shadowYaw,
        bool useSun,
        ref int created,
        ref int updated)
    {
        if (string.IsNullOrWhiteSpace(safeName))
            return null;
        if (sprite == null)
            return null;

        string prefabPath = $"{PropPrefabsFolder}/{safeName}.prefab";
        bool existed = File.Exists(prefabPath);
        GameObject instance;
        if (!existed)
        {
            GameObject templatePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TemplatePrefabPath);
            if (templatePrefab == null)
            {
                Debug.LogError($"[HexWorldPropCatalogBuilder] Missing template prefab: {TemplatePrefabPath}");
                return null;
            }

            // New props: clone template as a regular GameObject (no prefab link).
            instance = UnityEngine.Object.Instantiate(templatePrefab);
        }
        else
        {
            // Existing props: instantiate the existing prefab so custom overrides are preserved.
            GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existingPrefab == null)
            {
                Debug.LogError($"[HexWorldPropCatalogBuilder] Existing prefab missing at path: {prefabPath}");
                return null;
            }

            instance = PrefabUtility.InstantiatePrefab(existingPrefab) as GameObject;
        }

        if (instance == null)
            return null;

        try
        {
            instance.name = $"Prop_{safeName}";
            instance.transform.localScale = Vector3.one * Mathf.Max(0.001f, masterScale);
            bool isResource = safeName.StartsWith("resource_", StringComparison.OrdinalIgnoreCase);

            if (!ApplyTemplateSpriteData(instance, sprite, shadowMaterial, shadowYaw, useSun))
                return null;

            ConfigureResourceMiningComponents(instance, isResource, safeName);

            EditorUtility.SetDirty(instance);
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            if (savedPrefab == null)
                return null;

            EditorUtility.SetDirty(savedPrefab);
            if (isResource)
                CreateOrUpdateDungeonResourceDefinition(safeName, savedPrefab);
            if (existed) updated++;
            else created++;
            return savedPrefab;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    private static void ConfigureResourceMiningComponents(GameObject root, bool isResource, string safeName)
    {
        if (root == null)
            return;

        Transform thick = EnsureChild(root.transform, "Thick");
        Transform visual = EnsureChild(root.transform, "Visual");
        var thickOutline = thick != null ? thick.GetComponent<SpriteOutlineThickMesh>() : null;
        if (thickOutline != null)
        {
            thickOutline.Rebuild();
            EditorUtility.SetDirty(thickOutline);
        }

        if (isResource)
        {
            int propsLayer = LayerMask.NameToLayer("Props");
            root.layer = propsLayer >= 0 ? propsLayer : LayerMask.NameToLayer("Default");

            // Rigidbody bridge on root so child colliders route trigger/collision callbacks to root scripts.
            var bridgeBody = EnsureComponent<Rigidbody>(root);
            if (bridgeBody != null)
            {
                bridgeBody.isKinematic = true;
                bridgeBody.useGravity = false;
                EditorUtility.SetDirty(bridgeBody);
            }

            // Detection collider on root so DungeonMiningNode receives trigger events directly.
            var detectionCollider = EnsureComponent<BoxCollider>(root);
            if (detectionCollider != null)
            {
                detectionCollider.isTrigger = true;

                var visualRenderer = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
                if (visualRenderer != null)
                {
                    Bounds localBounds = visualRenderer.localBounds;
                    Vector3 scale = visual.localScale;
                    Vector3 scaledSize = Vector3.Scale(
                        localBounds.size,
                        new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
                    const float tightenFactor = 0.15f;
                    Vector3 size = new Vector3(scaledSize.x * tightenFactor, scaledSize.y * tightenFactor, 1f);

                    detectionCollider.center = localBounds.center;
                    detectionCollider.size = size;
                }
                else
                {
                    detectionCollider.center = Vector3.zero;
                    detectionCollider.size = new Vector3(1f, 1f, 2f);
                }

                EditorUtility.SetDirty(detectionCollider);
            }

            if (thick != null)
            {
                Transform colliderChild = EnsureChild(thick, "Collider");
                colliderChild.localPosition = Vector3.zero;
                colliderChild.localRotation = Quaternion.identity;
                colliderChild.localScale = Vector3.one;

                // Thick keeps the generated mesh visual only; physical collider lives on Thick/Collider.
                RemoveComponentIfExists<MeshCollider>(thick.gameObject);
                var meshCollider = EnsureComponent<MeshCollider>(colliderChild.gameObject);
                if (meshCollider != null)
                {
                    meshCollider.convex = true;
                    meshCollider.isTrigger = false;

                    var meshFilter = thick.GetComponent<MeshFilter>();
                    if (meshFilter != null)
                    {
                        Mesh generatedMesh = meshFilter.sharedMesh;
                        Mesh persistentMesh = EnsurePersistentGeneratedMesh(safeName, generatedMesh);
                        if (persistentMesh != null)
                        {
                            meshFilter.sharedMesh = persistentMesh;
                            meshCollider.sharedMesh = null;
                            meshCollider.sharedMesh = persistentMesh;
                            EditorUtility.SetDirty(meshFilter);
                        }
                    }

                    EditorUtility.SetDirty(meshCollider);
                }
            }

            var miningNode = EnsureComponent<GalacticFishing.Minigames.Dungeon3D.DungeonMiningNode>(root);
            if (miningNode != null)
            {
                ParticleSystem miningDust = EnsureMiningDustFxChild(root);
                AssignMiningParticles(miningNode, miningDust);
                EditorUtility.SetDirty(miningNode);
            }

            return;
        }

        RemoveComponentIfExists<Rigidbody>(root);
        RemoveComponentIfExists<BoxCollider>(root);
        Transform dustFx = root.transform.Find("FX_MiningDust");
        if (dustFx != null)
            UnityEngine.Object.DestroyImmediate(dustFx.gameObject);
        if (thick != null)
        {
            RemoveComponentIfExists<MeshCollider>(thick.gameObject);
            Transform colliderChild = thick.Find("Collider");
            if (colliderChild != null)
                RemoveComponentIfExists<MeshCollider>(colliderChild.gameObject);
        }
        RemoveComponentIfExists<GalacticFishing.Minigames.Dungeon3D.DungeonMiningNode>(root);
    }

    private static ParticleSystem EnsureMiningDustFxChild(GameObject root)
    {
        if (root == null)
            return null;

        Transform existing = root.transform.Find("FX_MiningDust");
        if (existing != null)
        {
            ParticleSystem existingPs = existing.GetComponent<ParticleSystem>();
            if (existingPs != null)
            {
                existingPs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                return existingPs;
            }

            UnityEngine.Object.DestroyImmediate(existing.gameObject);
        }

        GameObject dustPrefab = EnsureMiningDustPrefabAsset();
        if (dustPrefab == null)
        {
            Debug.LogWarning($"[HexWorldPropCatalogBuilder] Mining dust prefab not found: {MiningDustPrefabPath}");
            return null;
        }

        GameObject dustInstance = PrefabUtility.InstantiatePrefab(dustPrefab) as GameObject;
        if (dustInstance == null)
            dustInstance = UnityEngine.Object.Instantiate(dustPrefab);
        if (dustInstance == null)
            return null;

        dustInstance.name = "FX_MiningDust";
        dustInstance.transform.SetParent(root.transform, false);
        EditorUtility.SetDirty(dustInstance);

        ParticleSystem particles = dustInstance.GetComponent<ParticleSystem>();
        if (particles != null)
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return particles;
    }

    private static GameObject EnsureMiningDustPrefabAsset()
    {
        EnsureFolder(Path.GetDirectoryName(MiningDustPrefabPath)?.Replace("\\", "/"));

        bool exists = File.Exists(MiningDustPrefabPath);
        GameObject root = null;
        try
        {
            root = exists
                ? PrefabUtility.LoadPrefabContents(MiningDustPrefabPath)
                : new GameObject("PF_MiningDust");

            if (root == null)
                return null;

            root.name = "PF_MiningDust";
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            ParticleSystem ps = EnsureComponent<ParticleSystem>(root);
            ParticleSystemRenderer psr = EnsureComponent<ParticleSystemRenderer>(root);
            if (ps == null || psr == null)
                return null;

            ConfigureMiningDustParticleSystem(ps, psr);
            EditorUtility.SetDirty(ps);
            EditorUtility.SetDirty(psr);
            EditorUtility.SetDirty(root);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, MiningDustPrefabPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(MiningDustPrefabPath, ImportAssetOptions.ForceUpdate);
            return saved != null ? saved : AssetDatabase.LoadAssetAtPath<GameObject>(MiningDustPrefabPath);
        }
        finally
        {
            if (root != null)
            {
                if (exists)
                    PrefabUtility.UnloadPrefabContents(root);
                else
                    UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }

    private static void ConfigureMiningDustParticleSystem(ParticleSystem ps, ParticleSystemRenderer psr)
    {
        var main = ps.main;
        main.duration = 1f;
        main.loop = true;
        main.prewarm = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
        main.startSpeed = 0.5f;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 20f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(0.8f, 0.8f, 0.1f);
        shape.position = new Vector3(0f, 0.2f, 0f);

        var texAnim = ps.textureSheetAnimation;
        texAnim.enabled = true;
        texAnim.mode = ParticleSystemAnimationMode.Sprites;
        texAnim.animation = ParticleSystemAnimationType.WholeSheet;
        texAnim.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
        texAnim.numTilesX = 1;
        texAnim.numTilesY = 1;
        while (texAnim.spriteCount > 0)
            texAnim.RemoveSprite(texAnim.spriteCount - 1);
        List<Sprite> miningSprites = LoadMiningDustSprites();
        for (int i = 0; i < miningSprites.Count; i++)
            texAnim.AddSprite(miningSprites[i]);
        if (miningSprites.Count < 16)
        {
            Debug.LogWarning(
                $"[HexWorldPropCatalogBuilder] Expected 16 mining sprites, found {miningSprites.Count} at {MiningDustSpriteSheetPath}.");
        }

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.2f, 1f, 0f));

        psr.renderMode = ParticleSystemRenderMode.Billboard;
        string matPath = AssetDatabase.GUIDToAssetPath(SpriteRenderMaterialGuid);
        if (!string.IsNullOrWhiteSpace(matPath))
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat != null)
                psr.sharedMaterial = mat;
        }

        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private static List<Sprite> LoadMiningDustSprites()
    {
        var all = AssetDatabase.LoadAllAssetsAtPath(MiningDustSpriteSheetPath);
        if (all == null || all.Length == 0)
            return new List<Sprite>();

        return all
            .OfType<Sprite>()
            .OrderBy(s => ParseTrailingNumber(s != null ? s.name : string.Empty))
            .ThenBy(s => s != null ? s.name : string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int ParseTrailingNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return int.MaxValue;

        int end = value.Length - 1;
        while (end >= 0 && char.IsDigit(value[end]))
            end--;

        if (end == value.Length - 1)
            return int.MaxValue;

        string digits = value.Substring(end + 1);
        return int.TryParse(digits, out int parsed) ? parsed : int.MaxValue;
    }

    private static void AssignMiningParticles(GalacticFishing.Minigames.Dungeon3D.DungeonMiningNode miningNode, ParticleSystem particles)
    {
        if (miningNode == null)
            return;

        var so = new SerializedObject(miningNode);
        SerializedProperty prop = so.FindProperty("miningParticles");
        if (prop == null)
            return;

        prop.objectReferenceValue = particles;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CreateOrUpdateDungeonResourceDefinition(string safeName, GameObject prefabAsset)
    {
        if (string.IsNullOrWhiteSpace(safeName))
            return;

        EnsureFolder(DungeonResourceFolder);
        string defPath = $"{DungeonResourceFolder}/DungeonResource_{safeName}.asset";
        var def = AssetDatabase.LoadAssetAtPath<GalacticFishing.Minigames.Dungeon3D.DungeonResourceDefinition>(defPath);
        bool isNew = def == null;
        if (isNew)
        {
            def = ScriptableObject.CreateInstance<GalacticFishing.Minigames.Dungeon3D.DungeonResourceDefinition>();
            AssetDatabase.CreateAsset(def, defPath);
        }

        if (def == null)
            return;

        string normalizedResourceId = ExtractResourceEnumName(safeName);
        def.resourceId = string.IsNullOrWhiteSpace(normalizedResourceId) ? safeName : normalizedResourceId;
        def.prefab = prefabAsset;

        if (isNew || def.maxHp <= 0)
            def.maxHp = 15;

        def.lootId = ResolveResourceLootId(safeName);
        EditorUtility.SetDirty(def);
    }

    private static HexWorldResourceId ResolveResourceLootId(string safeName)
    {
        if (TryResolveResourceLootIdValue(safeName, out int rawId))
            return (HexWorldResourceId)rawId;

        Debug.LogWarning($"[HexWorldPropCatalogBuilder] Resource '{safeName}' has no enum mapping. Falling back to Stone.");
        return HexWorldResourceId.Stone;
    }

    private static bool TryResolveResourceLootIdValue(string safeName, out int lootId)
    {
        lootId = (int)HexWorldResourceId.Stone;
        string enumName = ExtractResourceEnumName(safeName);
        if (string.IsNullOrWhiteSpace(enumName))
            return false;

        // Fast path: already compiled into the enum.
        if (Enum.IsDefined(typeof(HexWorldResourceId), enumName) &&
            Enum.TryParse(enumName, true, out HexWorldResourceId parsed))
        {
            lootId = (int)parsed;
            return true;
        }

        // Super-automation: append a new mining resource ID if missing.
        if (TryEnsureMiningResourceEnumEntry(enumName, out int generatedId))
        {
            lootId = generatedId;
            return true;
        }

        // Fallback parse in case name exists with different casing.
        if (Enum.TryParse(enumName, true, out parsed))
        {
            lootId = (int)parsed;
            return true;
        }

        return false;
    }

    private static string ExtractResourceEnumName(string safeName)
    {
        if (string.IsNullOrWhiteSpace(safeName))
            return string.Empty;

        string value = safeName.Trim();
        if (value.StartsWith("resource_", StringComparison.OrdinalIgnoreCase))
            value = value.Substring("resource_".Length);

        for (int i = 0; i < ResourceSuffixes.Length; i++)
        {
            string suffix = ResourceSuffixes[i];
            if (string.IsNullOrEmpty(suffix))
                continue;

            if (!value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                continue;

            value = value.Substring(0, value.Length - suffix.Length);
            break;
        }

        value = MakeSafeAssetName(value).Trim('_');
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        string[] parts = value.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            string p = parts[i];
            if (string.IsNullOrEmpty(p))
                continue;
            parts[i] = p.Length == 1
                ? char.ToUpperInvariant(p[0]).ToString()
                : char.ToUpperInvariant(p[0]) + p.Substring(1);
        }

        return string.Join("_", parts);
    }

    private static bool TryEnsureMiningResourceEnumEntry(string enumName, out int assignedId)
    {
        assignedId = -1;
        if (string.IsNullOrWhiteSpace(enumName))
            return false;

        string projectRoot = Directory.GetCurrentDirectory();
        string fullPath = Path.Combine(projectRoot, HexWorldResourceIdScriptPath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            Debug.LogWarning($"[HexWorldPropCatalogBuilder] Enum source not found: {HexWorldResourceIdScriptPath}");
            return false;
        }

        string text = File.ReadAllText(fullPath);
        if (TryFindEnumValueInSource(text, enumName, out assignedId))
            return true;

        string newline = text.Contains("\r\n") ? "\r\n" : "\n";
        var lines = new List<string>(File.ReadAllLines(fullPath));

        int miningHeaderIndex = lines.FindIndex(l => l != null && l.IndexOf("// Mining resources", StringComparison.Ordinal) >= 0);
        if (miningHeaderIndex < 0)
        {
            Debug.LogWarning($"[HexWorldPropCatalogBuilder] Could not find 'Mining resources' block in {HexWorldResourceIdScriptPath}");
            return false;
        }

        int lastEntryIndex = -1;
        int maxAssigned = int.MinValue;
        string entryIndent = "        ";
        var entryRegex = new Regex(@"^(\s*)([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(\d+)\s*,\s*$");

        for (int i = miningHeaderIndex + 1; i < lines.Count; i++)
        {
            string line = lines[i];

            if (line.TrimStart().StartsWith("//", StringComparison.Ordinal))
                break;
            if (line.TrimStart().StartsWith("}", StringComparison.Ordinal))
                break;

            Match m = entryRegex.Match(line);
            if (!m.Success)
                continue;

            lastEntryIndex = i;
            entryIndent = m.Groups[1].Value;

            if (string.Equals(m.Groups[2].Value, enumName, StringComparison.OrdinalIgnoreCase))
            {
                assignedId = int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
                return true;
            }

            int parsed = int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
            if (parsed > maxAssigned)
                maxAssigned = parsed;
        }

        if (lastEntryIndex < 0)
        {
            Debug.LogWarning($"[HexWorldPropCatalogBuilder] No enum entries found in mining block of {HexWorldResourceIdScriptPath}");
            return false;
        }

        assignedId = maxAssigned + 1;
        lines.Insert(lastEntryIndex + 1, $"{entryIndent}{enumName} = {assignedId},");
        File.WriteAllText(fullPath, string.Join(newline, lines) + newline);

        // RebuildCatalog already refreshes the AssetDatabase at the end; keep the run alive and let compilation happen once.
        Debug.Log($"[HexWorldPropCatalogBuilder] Added HexWorldResourceId.{enumName} = {assignedId} to mining resources.");
        return true;
    }

    private static bool TryFindEnumValueInSource(string sourceText, string enumName, out int value)
    {
        value = -1;
        if (string.IsNullOrWhiteSpace(sourceText) || string.IsNullOrWhiteSpace(enumName))
            return false;

        Match match = Regex.Match(
            sourceText,
            $@"\b{Regex.Escape(enumName)}\b\s*=\s*(\d+)\s*,",
            RegexOptions.CultureInvariant);
        if (!match.Success)
            return false;

        return int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static int SyncGemIdEnumFromGemsFolder()
    {
        if (!AssetDatabase.IsValidFolder(GemsFolder))
            return 0;

        EnsureGemIdEnumFileExists();

        string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { GemsFolder });
        var gemEnumNames = textureGuids
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            .Select(Path.GetFileNameWithoutExtension)
            .Where(raw => !string.IsNullOrWhiteSpace(raw) && raw.StartsWith("gem_", StringComparison.OrdinalIgnoreCase))
            .Select(ExtractGemEnumName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        int added = 0;
        for (int i = 0; i < gemEnumNames.Count; i++)
        {
            if (TryEnsureGemIdEnumEntry(gemEnumNames[i], out _, out bool wasAdded) && wasAdded)
                added++;
        }

        return added;
    }

    private static string ExtractGemEnumName(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
            return string.Empty;

        string value = rawName.Trim();
        if (value.StartsWith("gem_", StringComparison.OrdinalIgnoreCase))
            value = value.Substring("gem_".Length);

        value = MakeSafeAssetName(value).Trim('_');
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        string[] parts = value.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            string p = parts[i];
            if (string.IsNullOrEmpty(p))
                continue;
            parts[i] = p.Length == 1
                ? char.ToUpperInvariant(p[0]).ToString()
                : char.ToUpperInvariant(p[0]) + p.Substring(1);
        }

        return string.Join("_", parts);
    }

    private static void EnsureGemIdEnumFileExists()
    {
        string folder = Path.GetDirectoryName(GemIdScriptPath)?.Replace("\\", "/");
        if (!string.IsNullOrEmpty(folder))
            EnsureFolder(folder);

        string projectRoot = Directory.GetCurrentDirectory();
        string fullPath = Path.Combine(projectRoot, GemIdScriptPath.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(fullPath))
            return;

        string contents =
            "using System;" + Environment.NewLine +
            Environment.NewLine +
            "namespace GalacticFishing.Data" + Environment.NewLine +
            "{" + Environment.NewLine +
            "    public enum GemId" + Environment.NewLine +
            "    {" + Environment.NewLine +
            "        None = 0," + Environment.NewLine +
            "    }" + Environment.NewLine +
            "}" + Environment.NewLine;

        File.WriteAllText(fullPath, contents);
    }

    private static bool TryEnsureGemIdEnumEntry(string enumName, out int assignedId, out bool wasAdded)
    {
        assignedId = -1;
        wasAdded = false;
        if (string.IsNullOrWhiteSpace(enumName))
            return false;

        string projectRoot = Directory.GetCurrentDirectory();
        string fullPath = Path.Combine(projectRoot, GemIdScriptPath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            Debug.LogWarning($"[HexWorldPropCatalogBuilder] Gem enum source not found: {GemIdScriptPath}");
            return false;
        }

        string text = File.ReadAllText(fullPath);
        if (TryFindEnumValueInSource(text, enumName, out assignedId))
            return true;

        string newline = text.Contains("\r\n") ? "\r\n" : "\n";
        var lines = new List<string>(File.ReadAllLines(fullPath));

        int enumIndex = lines.FindIndex(l => l != null && l.IndexOf("enum GemId", StringComparison.Ordinal) >= 0);
        if (enumIndex < 0)
        {
            Debug.LogWarning($"[HexWorldPropCatalogBuilder] Could not find 'enum GemId' in {GemIdScriptPath}");
            return false;
        }

        int openBraceIndex = -1;
        for (int i = enumIndex; i < lines.Count; i++)
        {
            if (lines[i].Contains("{"))
            {
                openBraceIndex = i;
                break;
            }
        }

        if (openBraceIndex < 0)
        {
            Debug.LogWarning($"[HexWorldPropCatalogBuilder] Could not find GemId enum body in {GemIdScriptPath}");
            return false;
        }

        int closeBraceIndex = -1;
        int depth = 0;
        for (int i = openBraceIndex; i < lines.Count; i++)
        {
            string line = lines[i];
            for (int c = 0; c < line.Length; c++)
            {
                if (line[c] == '{') depth++;
                else if (line[c] == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        closeBraceIndex = i;
                        break;
                    }
                }
            }

            if (closeBraceIndex >= 0)
                break;
        }

        if (closeBraceIndex < 0)
        {
            Debug.LogWarning($"[HexWorldPropCatalogBuilder] Could not find GemId enum closing brace in {GemIdScriptPath}");
            return false;
        }

        int maxAssigned = 0;
        string entryIndent = "        ";
        var entryRegex = new Regex(@"^(\s*)([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(\d+)\s*,\s*$");

        for (int i = openBraceIndex + 1; i < closeBraceIndex; i++)
        {
            Match m = entryRegex.Match(lines[i]);
            if (!m.Success)
                continue;

            entryIndent = m.Groups[1].Value;

            if (string.Equals(m.Groups[2].Value, enumName, StringComparison.OrdinalIgnoreCase))
            {
                assignedId = int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
                return true;
            }

            int parsed = int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
            if (parsed > maxAssigned)
                maxAssigned = parsed;
        }

        assignedId = maxAssigned + 1;
        lines.Insert(closeBraceIndex, $"{entryIndent}{enumName} = {assignedId},");
        File.WriteAllText(fullPath, string.Join(newline, lines) + newline);
        wasAdded = true;

        Debug.Log($"[HexWorldPropCatalogBuilder] Added GemId.{enumName} to gems list.");
        return true;
    }

    private static int SyncDungeonGemRegistryFromGemsFolder()
    {
        if (!AssetDatabase.IsValidFolder(GemsFolder))
            return 0;

        EnsureGemIdEnumFileExists();
        string registryFolder = Path.GetDirectoryName(DungeonGemRegistryAssetPath)?.Replace("\\", "/");
        if (!string.IsNullOrEmpty(registryFolder))
            EnsureFolder(registryFolder);

        DungeonGemRegistry registry = AssetDatabase.LoadAssetAtPath<DungeonGemRegistry>(DungeonGemRegistryAssetPath);
        if (registry == null)
        {
            registry = ScriptableObject.CreateInstance<DungeonGemRegistry>();
            AssetDatabase.CreateAsset(registry, DungeonGemRegistryAssetPath);
        }

        if (registry == null)
            return 0;

        var so = new SerializedObject(registry);
        var gemsProp = so.FindProperty("gems");
        if (gemsProp == null || !gemsProp.isArray)
            return 0;

        string[] spriteGuids = AssetDatabase.FindAssets("t:Sprite", new[] { GemsFolder });
        var discovered = spriteGuids
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(path => AssetDatabase.LoadAssetAtPath<Sprite>(path))
            .Where(sprite => sprite != null && sprite.name.StartsWith(GemSpritePrefix, StringComparison.OrdinalIgnoreCase))
            .Select(sprite => new { sprite, enumName = ExtractGemEnumName(sprite.name) })
            .Where(x => !string.IsNullOrWhiteSpace(x.enumName))
            .GroupBy(x => x.enumName, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderBy(x => x.sprite.name, StringComparer.OrdinalIgnoreCase).First())
            .OrderBy(x => x.enumName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        gemsProp.arraySize = discovered.Count;
        for (int i = 0; i < discovered.Count; i++)
        {
            var item = discovered[i];
            if (!TryEnsureGemIdEnumEntry(item.enumName, out int gemIdValue, out _))
                gemIdValue = 0;

            SerializedProperty row = gemsProp.GetArrayElementAtIndex(i);
            SerializedProperty gemIdProp = row.FindPropertyRelative("gemId");
            SerializedProperty iconProp = row.FindPropertyRelative("icon");
            SerializedProperty descProp = row.FindPropertyRelative("description");

            if (gemIdProp != null)
                gemIdProp.intValue = gemIdValue;
            if (iconProp != null)
                iconProp.objectReferenceValue = item.sprite;
            if (descProp != null && string.IsNullOrWhiteSpace(descProp.stringValue))
                descProp.stringValue = string.Empty;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(registry);
        AssetDatabase.SaveAssets();

        return discovered.Count;
    }

    private static Mesh EnsurePersistentGeneratedMesh(string safeName, Mesh generatedMesh)
    {
        if (generatedMesh == null || string.IsNullOrWhiteSpace(safeName))
            return null;

        EnsureFolder(GeneratedMeshesFolder);
        string meshPath = $"{GeneratedMeshesFolder}/{safeName}_Thick.asset";

        Mesh persistent = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (persistent == null)
        {
            persistent = UnityEngine.Object.Instantiate(generatedMesh);
            persistent.name = $"{safeName}_Thick";
            AssetDatabase.CreateAsset(persistent, meshPath);
            EditorUtility.SetDirty(persistent);
            return persistent;
        }

        EditorUtility.CopySerialized(generatedMesh, persistent);
        EditorUtility.SetDirty(persistent);
        return persistent;
    }

    private static bool ApplyTemplateSpriteData(
        GameObject root,
        Sprite sprite,
        Material shadowMaterial,
        float shadowYaw,
        bool useSun)
    {
        if (root == null || sprite == null)
            return false;

        Transform visual = EnsureChild(root.transform, "Visual");
        Transform thick = EnsureChild(root.transform, "Thick");
        Transform shadow = EnsureChild(root.transform, "Shadow");

        for (int i = root.transform.childCount - 1; i >= 0; i--)
        {
            var child = root.transform.GetChild(i);
            if (child == visual || child == thick || child == shadow)
                continue;

            UnityEngine.Object.DestroyImmediate(child.gameObject);
        }

        // Enforce expected child ordering to match template intent.
        visual.SetSiblingIndex(0);
        shadow.SetSiblingIndex(1);
        thick.SetSiblingIndex(2);

        var visualSr = visual.GetComponent<SpriteRenderer>();
        if (visualSr == null)
            visualSr = visual.gameObject.AddComponent<SpriteRenderer>();
        visualSr.sprite = sprite;
        EditorUtility.SetDirty(visualSr);

        var thickOutline = thick.GetComponent<SpriteOutlineThickMesh>();
        if (thickOutline != null)
        {
            thickOutline.sourceSprite = sprite;
            thickOutline.Rebuild();
            EditorUtility.SetDirty(thickOutline);
        }
        else
        {
            Debug.LogWarning("[HexWorldPropCatalogBuilder] Template child 'Thick' is missing SpriteOutlineThickMesh.");
        }

        var shadowSr = shadow.GetComponent<SpriteRenderer>();
        if (shadowSr != null)
        {
            shadowSr.sprite = sprite;
            EditorUtility.SetDirty(shadowSr);
        }
        else
        {
            Debug.LogWarning("[HexWorldPropCatalogBuilder] Template child 'Shadow' is missing SpriteRenderer.");
        }

        return true;
    }

    private static bool ConfigurePropPrefabHierarchy(
        GameObject root,
        Sprite sprite,
        Material shadowMaterial,
        float shadowYaw,
        bool useSun)
    {
        if (root == null || sprite == null)
            return false;

        var rootSpriteRenderer = root.GetComponent<SpriteRenderer>();
        var rootBillboard = root.GetComponent<BillboardToCamera>();

        // New structure: Root -> Visual + Shadow
        Transform visual = EnsureChild(root.transform, "Visual");
        Transform shadow = EnsureChild(root.transform, "Shadow");

        visual.localPosition = Vector3.zero;
        visual.localRotation = Quaternion.identity;
        visual.localScale = Vector3.one;

        shadow.localPosition = Vector3.zero;
        shadow.localRotation = Quaternion.identity;
        shadow.localScale = Vector3.one;

        var visualSr = EnsureComponent<SpriteRenderer>(visual.gameObject);
        if (visualSr == null)
            return false;

        if (rootSpriteRenderer != null && rootSpriteRenderer != visualSr)
        {
            CopySpriteRendererSettings(rootSpriteRenderer, visualSr);
            UnityEngine.Object.DestroyImmediate(rootSpriteRenderer);
        }
        visualSr.sprite = sprite;

        BillboardToCamera visualBillboard = visual.GetComponent<BillboardToCamera>();
        BillboardToCamera billboardSource = visualBillboard ? visualBillboard : rootBillboard;
        var tracker = EnsureComponent<DistanceToCameraTracker>(visual.gameObject);
        if (tracker == null)
            return false;
        tracker.spriteRenderer = visualSr;
        if (billboardSource != null)
            CopyBillboardSettingsToTracker(billboardSource, tracker);
        RemoveComponentIfExists<BillboardToCamera>(visual.gameObject);
        RemoveComponentIfExists<BillboardToCamera>(root);

        var shadowSr = EnsureComponent<SpriteRenderer>(shadow.gameObject);
        if (shadowSr == null)
            return false;
        shadowSr.sprite = sprite;
        if (shadowMaterial != null)
            shadowSr.sharedMaterial = shadowMaterial;

        var shadowCast = EnsureComponent<GroundCastShadow2D>(shadow.gameObject);
        if (shadowCast == null)
            return false;

        shadowCast.mainRenderer = visualSr;
        shadowCast.shadowRenderer = shadowSr;
        shadowCast.screenSpaceDirection = false;
        shadowCast.anchorMode = GroundCastShadow2D.AnchorMode.MainRendererPivot;
        shadowCast.useSunIfAvailable = useSun;
        shadowCast.useYawOverride = !useSun;
        shadowCast.yawDegrees = shadowYaw;
        shadowCast.castDistanceInHeights = 0f;
        shadowCast.groundTiltX = 90f;
        shadowCast.groundLift = 0.03f;
        shadowCast.lengthScale = 2.0f;
        shadowCast.alpha = 0.65f;

        // Root should be a container only.
        RemoveComponentIfExists<SpriteRenderer>(root);
        RemoveComponentIfExists<GroundCastShadow2D>(root);
        RemoveObsoleteRootShadowScripts(root);

        EditorUtility.SetDirty(visualSr);
        EditorUtility.SetDirty(tracker);
        EditorUtility.SetDirty(shadowSr);
        EditorUtility.SetDirty(shadowCast);
        return true;
    }

    private static void CreateOrUpdateDefinition(
        string safeName,
        string displayName,
        string biomeGroup,
        Sprite sprite,
        GameObject propPrefab,
        float masterScale,
        ref int created,
        ref int updated)
    {
        string defPath = $"{PropDefinitionsFolder}/Prop_{safeName}.asset";
        HexWorldPropDefinition def = AssetDatabase.LoadAssetAtPath<HexWorldPropDefinition>(defPath);
        bool isNew = def == null;

        if (isNew)
        {
            def = ScriptableObject.CreateInstance<HexWorldPropDefinition>();
            AssetDatabase.CreateAsset(def, defPath);
        }

        bool isResource = safeName.StartsWith("resource_", StringComparison.OrdinalIgnoreCase);
        string normalizedResourceId = isResource ? ExtractResourceEnumName(safeName) : null;

        // Keep IDs aligned with generator/resource naming for deterministic registry lookup.
        def.id = !string.IsNullOrWhiteSpace(normalizedResourceId) ? normalizedResourceId : safeName;
        def.displayName = displayName;
        def.biomeGroup = string.IsNullOrWhiteSpace(biomeGroup) ? "ALL" : biomeGroup.Trim().ToUpperInvariant();
        def.thumbnail = sprite;
        def.prefab = propPrefab;
        float bakedScale = Mathf.Max(0.001f, masterScale);
        def.masterScale = bakedScale;
        def.scale = bakedScale;
        EditorUtility.SetDirty(def);

        if (isNew) created++;
        else updated++;
    }

    private static List<HexWorldPropDefinition> LoadPropDefinitions()
    {
        string[] defGuids = AssetDatabase.FindAssets("t:HexWorldPropDefinition", new[] { PropDefinitionsFolder });
        var defs = new List<HexWorldPropDefinition>(defGuids.Length);
        for (int i = 0; i < defGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(defGuids[i]);
            var def = AssetDatabase.LoadAssetAtPath<HexWorldPropDefinition>(path);
            if (def != null)
                defs.Add(def);
        }

        return defs
            .OrderBy(d => d.name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static PropRegistry GetOrCreatePropRegistry()
    {
        var registry = AssetDatabase.LoadAssetAtPath<PropRegistry>(PropRegistryAssetPath);
        if (registry != null)
            return registry;

        string folder = Path.GetDirectoryName(PropRegistryAssetPath)?.Replace("\\", "/");
        if (!string.IsNullOrEmpty(folder))
            EnsureFolder(folder);

        registry = ScriptableObject.CreateInstance<PropRegistry>();
        AssetDatabase.CreateAsset(registry, PropRegistryAssetPath);
        EditorUtility.SetDirty(registry);
        AssetDatabase.SaveAssets();
        return registry;
    }

    private static int WireRegistry(PropRegistry registry, List<HexWorldPropDefinition> defs)
    {
        if (registry == null)
        {
            Debug.LogWarning("[HexWorldPropCatalogBuilder] PropRegistry asset missing; skipping registry wiring.");
            return 0;
        }

        if (defs == null)
            defs = new List<HexWorldPropDefinition>();

        registry.allProps.Clear();
        for (int i = 0; i < defs.Count; i++)
        {
            HexWorldPropDefinition def = defs[i];
            if (def != null && !registry.allProps.Contains(def))
                registry.allProps.Add(def);
        }

        EditorUtility.SetDirty(registry);
        AssetDatabase.SaveAssets();
        return registry.allProps.Count;
    }

    private static int WireCatalogToVillageControllerPrefab(List<HexWorldPropDefinition> defs)
    {
        if (!File.Exists(VillageControllerPrefabPath))
        {
            Debug.LogWarning($"[HexWorldPropCatalogBuilder] Controller prefab not found: {VillageControllerPrefabPath}");
            return 0;
        }

        if (defs == null)
            defs = new List<HexWorldPropDefinition>();

        defs = FilterVillageVisiblePropDefinitions(defs);

        // Force-inject dungeon marker props so they are always available in the Village prop catalog.
        var startMarker = AssetDatabase.LoadAssetAtPath<HexWorldPropDefinition>(
            "Assets/Minigames/HexWorld3D/Definitions/Props/Prop_Start_Marker.asset");
        var bossMarker = AssetDatabase.LoadAssetAtPath<HexWorldPropDefinition>(
            "Assets/Minigames/HexWorld3D/Definitions/Props/Prop_Boss_Marker.asset");

        if (startMarker != null && !defs.Contains(startMarker))
            defs.Add(startMarker);
        if (bossMarker != null && !defs.Contains(bossMarker))
            defs.Add(bossMarker);

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(VillageControllerPrefabPath);
        if (prefabRoot == null)
        {
            Debug.LogWarning($"[HexWorldPropCatalogBuilder] Failed to load prefab contents: {VillageControllerPrefabPath}");
            return 0;
        }

        try
        {
            var controller = prefabRoot.GetComponentInChildren<HexWorld3DController>(true);
            if (controller == null)
            {
                Debug.LogWarning($"[HexWorldPropCatalogBuilder] No HexWorld3DController found in prefab: {VillageControllerPrefabPath}");
                return 0;
            }

            var serializedController = new SerializedObject(controller);
            var propCatalog = serializedController.FindProperty("propCatalog");
            if (propCatalog == null || !propCatalog.isArray)
            {
                Debug.LogWarning("[HexWorldPropCatalogBuilder] 'propCatalog' field not found or is not an array on HexWorld3DController.");
                return 0;
            }

            propCatalog.arraySize = defs.Count;
            for (int i = 0; i < defs.Count; i++)
            {
                propCatalog.GetArrayElementAtIndex(i).objectReferenceValue = defs[i];
            }

            serializedController.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(prefabRoot);

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, VillageControllerPrefabPath);
            return defs.Count;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static int WireCatalogToSceneInstance(List<HexWorldPropDefinition> defs)
    {
        var controller = UnityEngine.Object.FindAnyObjectByType<HexWorld3DController>(FindObjectsInactive.Include);
        if (controller == null)
        {
            Debug.LogWarning("[HexWorldPropCatalogBuilder] No scene HexWorld3DController found to wire.");
            return 0;
        }

        if (defs == null)
            defs = new List<HexWorldPropDefinition>();

        defs = FilterVillageVisiblePropDefinitions(defs);

        var serializedController = new SerializedObject(controller);
        var propCatalog = serializedController.FindProperty("propCatalog");
        if (propCatalog == null || !propCatalog.isArray)
        {
            Debug.LogWarning("[HexWorldPropCatalogBuilder] Scene controller missing array property 'propCatalog'.");
            return 0;
        }

        propCatalog.arraySize = defs.Count;
        for (int i = 0; i < defs.Count; i++)
        {
            propCatalog.GetArrayElementAtIndex(i).objectReferenceValue = defs[i];
        }

        serializedController.ApplyModifiedProperties();
        PrefabUtility.RecordPrefabInstancePropertyModifications(controller);

        if (controller.gameObject.scene.IsValid())
            EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);

        EditorUtility.SetDirty(controller);
        return defs.Count;
    }

    private static List<HexWorldPropDefinition> FilterVillageVisiblePropDefinitions(List<HexWorldPropDefinition> defs)
    {
        if (defs == null || defs.Count == 0)
            return new List<HexWorldPropDefinition>();

        var result = new List<HexWorldPropDefinition>(defs.Count);
        for (int i = 0; i < defs.Count; i++)
        {
            HexWorldPropDefinition def = defs[i];
            if (def == null)
                continue;

            if (IsDungeonResourcePropDefinition(def))
                continue;

            if (!result.Contains(def))
                result.Add(def);
        }

        return result;
    }

    private static bool IsDungeonResourcePropDefinition(HexWorldPropDefinition def)
    {
        if (def == null)
            return false;

        // Prefer the source sprite path, which preserves the original folder category (Props vs DungeonResources).
        if (def.thumbnail != null)
        {
            string sourcePath = AssetDatabase.GetAssetPath(def.thumbnail)?.Replace("\\", "/");
            if (!string.IsNullOrEmpty(sourcePath) &&
                sourcePath.IndexOf("/DungeonResources/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        // Fallback for older assets missing thumbnails: detect legacy generated definition name.
        string defPath = AssetDatabase.GetAssetPath(def)?.Replace("\\", "/");
        string fileName = Path.GetFileNameWithoutExtension(defPath ?? string.Empty);
        return fileName.StartsWith("Prop_resource_", StringComparison.OrdinalIgnoreCase);
    }

    private static int WireCatalogToDungeonGenerator()
    {
        if (!File.Exists(DungeonScenePath))
        {
            Debug.LogWarning($"[HexWorldPropCatalogBuilder] Dungeon scene not found: {DungeonScenePath}");
            return 0;
        }

        if (!Directory.Exists(DungeonResourceFolder))
        {
            Debug.LogWarning($"[HexWorldPropCatalogBuilder] Dungeon resource folder not found: {DungeonResourceFolder}");
            return 0;
        }

        string projectRoot = Directory.GetCurrentDirectory().Replace("\\", "/");
        var resourcePaths = Directory.GetFiles(DungeonResourceFolder, "*.asset", SearchOption.TopDirectoryOnly)
            .Select(path => path.Replace("\\", "/"))
            .Select(path => path.StartsWith(projectRoot + "/", StringComparison.OrdinalIgnoreCase)
                ? path.Substring(projectRoot.Length + 1)
                : path);

        var resources = resourcePaths
            .Select(path => AssetDatabase.LoadAssetAtPath<GalacticFishing.Minigames.Dungeon3D.DungeonResourceDefinition>(path))
            .Where(r => r != null)
            .OrderBy(r => r.name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Scene dungeonScene = SceneManager.GetSceneByPath(DungeonScenePath);
        bool openedTemporarily = false;

        if (!dungeonScene.IsValid() || !dungeonScene.isLoaded)
        {
            dungeonScene = EditorSceneManager.OpenScene(DungeonScenePath, OpenSceneMode.Additive);
            openedTemporarily = dungeonScene.IsValid() && dungeonScene.isLoaded;
        }

        if (!dungeonScene.IsValid() || !dungeonScene.isLoaded)
        {
            Debug.LogWarning($"[HexWorldPropCatalogBuilder] Failed to open dungeon scene: {DungeonScenePath}");
            return 0;
        }

        try
        {
            GalacticFishing.Minigames.Dungeon3D.DimensionGenerator generator = null;
            var roots = dungeonScene.GetRootGameObjects();
            for (int i = 0; i < roots.Length && generator == null; i++)
                generator = roots[i].GetComponentInChildren<GalacticFishing.Minigames.Dungeon3D.DimensionGenerator>(true);

            if (generator == null)
            {
                Debug.LogWarning("[HexWorldPropCatalogBuilder] No DimensionGenerator found in dungeon scene.");
                return 0;
            }

            var so = new SerializedObject(generator);
            var resourceDefinitionsProp = so.FindProperty("resourceDefinitions");
            if (resourceDefinitionsProp == null || !resourceDefinitionsProp.isArray)
            {
                Debug.LogWarning("[HexWorldPropCatalogBuilder] DimensionGenerator missing array field 'resourceDefinitions'.");
                return 0;
            }

            resourceDefinitionsProp.arraySize = resources.Count;
            for (int i = 0; i < resources.Count; i++)
                resourceDefinitionsProp.GetArrayElementAtIndex(i).objectReferenceValue = resources[i];

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(generator);
            EditorSceneManager.MarkSceneDirty(dungeonScene);
            EditorSceneManager.SaveScene(dungeonScene);
            return resources.Count;
        }
        finally
        {
            if (openedTemporarily && dungeonScene.IsValid() && dungeonScene.isLoaded)
                EditorSceneManager.CloseScene(dungeonScene, true);
        }
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
            return;

        string[] parts = folder.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static void BatchProcessPivots(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !AssetDatabase.IsValidFolder(folder))
        {
            Debug.LogError($"[HexWorld Utility] Invalid folder: {folder}");
            return;
        }

        string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
        var texturePaths = textureGuids
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        int processed = 0;
        for (int i = 0; i < texturePaths.Count; i++)
        {
            string texturePath = texturePaths[i];
            string rawName = Path.GetFileNameWithoutExtension(texturePath).Trim();

            var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer == null)
                continue;

            bool oldReadable = importer.isReadable;
            try
            {
                bool changed = false;

                if (importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    changed = true;
                }

                if (importer.spriteImportMode != SpriteImportMode.Single)
                {
                    importer.spriteImportMode = SpriteImportMode.Single;
                    changed = true;
                }

                if (!importer.isReadable)
                {
                    importer.isReadable = true;
                    changed = true;
                }

                if (changed)
                {
                    importer.SaveAndReimport();
                    AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);
                }

                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                if (texture == null)
                    continue;

                if (!TryGetOpaqueBounds(texture, out int minX, out int maxX, out int minY, out _))
                {
                    Debug.LogWarning($"[HexWorld Utility] Image '{rawName}' is empty/transparent, skipped.");
                    continue;
                }

                float pivotX = ((minX + maxX) * 0.5f + 0.5f) / texture.width;
                float pivotY = minY / (float)texture.height;
                Vector2 newPivot = new Vector2(Mathf.Clamp01(pivotX), Mathf.Clamp01(pivotY));

                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);

                bool pivotChanged = settings.spriteMode != (int)SpriteImportMode.Single ||
                                    settings.spriteAlignment != (int)SpriteAlignment.Custom ||
                                    !Mathf.Approximately(settings.spritePivot.x, newPivot.x) ||
                                    !Mathf.Approximately(settings.spritePivot.y, newPivot.y);
                if (!pivotChanged)
                    continue;

                settings.spriteMode = (int)SpriteImportMode.Single;
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = newPivot;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
                AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);
                processed++;
            }
            finally
            {
                RestoreReadable(importer, oldReadable);
                AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);
            }
        }

        Debug.Log($"[HexWorld Utility] Processed pivots for {processed} sprites in {folder}.");
    }

    private static string MakeSafeAssetName(string raw)
    {
        string value = raw.Trim();
        foreach (char c in Path.GetInvalidFileNameChars())
            value = value.Replace(c, '_');

        value = value.Replace(' ', '_');
        value = value.Replace('.', '_');
        return value;
    }

    private static bool IsBiomePropPath(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return false;

        string normalized = assetPath.Replace("\\", "/");
        return normalized.StartsWith(SpritesFolder + "/", StringComparison.OrdinalIgnoreCase) &&
               (normalized.IndexOf("/Props/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalized.IndexOf("/DungeonResources/", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static bool IsBiomeDungeonResourcePath(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return false;

        string normalized = assetPath.Replace("\\", "/");
        return normalized.StartsWith(SpritesFolder + "/", StringComparison.OrdinalIgnoreCase) &&
               normalized.IndexOf("/DungeonResources/", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string ExtractBiomeGroupFromBiomePropPath(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return null;

        string normalized = assetPath.Replace("\\", "/");
        string[] parts = normalized.Split('/');
        for (int i = 0; i < parts.Length - 2; i++)
        {
            if (!string.Equals(parts[i], "Biomes", StringComparison.OrdinalIgnoreCase))
                continue;

            string biome = parts[i + 1];
            string category = parts[i + 2];
            bool isSupportedCategory =
                string.Equals(category, "Props", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(category, "DungeonResources", StringComparison.OrdinalIgnoreCase);
            if (!isSupportedCategory)
                return null;

            if (string.Equals(biome, "GLOBAL", StringComparison.OrdinalIgnoreCase))
                return "ALL";

            return biome.Trim().ToUpperInvariant();
        }

        return null;
    }

    private static HashSet<string> CollectSourceSafeNames(bool includeDungeonResources = true)
    {
        string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { SpritesFolder });
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < textureGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(textureGuids[i]);
            if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || !IsBiomePropPath(path))
                continue;

            if (!includeDungeonResources && IsBiomeDungeonResourcePath(path))
                continue;

            string rawName = Path.GetFileNameWithoutExtension(path).Trim();
            if (string.IsNullOrEmpty(rawName))
                continue;

            names.Add(MakeSafeAssetName(rawName));
        }

        return names;
    }

    private static int DeleteOrphanPropDefinitions(HashSet<string> validSafeNames)
    {
        string[] defGuids = AssetDatabase.FindAssets("t:HexWorldPropDefinition", new[] { PropDefinitionsFolder });
        int deleted = 0;
        for (int i = 0; i < defGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(defGuids[i]);
            string fileName = Path.GetFileNameWithoutExtension(path);
            if (!fileName.StartsWith("Prop_", StringComparison.OrdinalIgnoreCase))
                continue;

            string safeName = fileName.Substring("Prop_".Length);
            if (string.IsNullOrWhiteSpace(safeName))
                continue;

            if (validSafeNames.Contains(safeName))
                continue;

            if (AssetDatabase.DeleteAsset(path))
                deleted++;
        }

        return deleted;
    }

    private static int DeleteOrphanPropPrefabs(HashSet<string> validSafeNames)
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { PropPrefabsFolder });
        int deleted = 0;
        string templateName = Path.GetFileNameWithoutExtension(TemplatePrefabPath);
        for (int i = 0; i < prefabGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
            string fileName = Path.GetFileNameWithoutExtension(path);
            if (string.Equals(fileName, templateName, StringComparison.OrdinalIgnoreCase))
                continue;

            string safeName = fileName.StartsWith("Prop_", StringComparison.OrdinalIgnoreCase)
                ? fileName.Substring("Prop_".Length)
                : fileName;

            if (string.IsNullOrWhiteSpace(safeName))
                continue;
            if (!fileName.StartsWith("Prop_", StringComparison.OrdinalIgnoreCase) && !IsSanitizedGeneratedName(fileName))
                continue;

            // Safety: only delete prefabs that match this tool's managed naming conventions.
            bool looksManaged = fileName.StartsWith("Prop_", StringComparison.OrdinalIgnoreCase) ||
                                AssetDatabase.LoadAssetAtPath<HexWorldPropDefinition>($"{PropDefinitionsFolder}/Prop_{safeName}.asset") != null;
            if (!looksManaged)
                continue;

            if (validSafeNames.Contains(safeName))
                continue;

            if (AssetDatabase.DeleteAsset(path))
                deleted++;
        }

        return deleted;
    }

    private static int DeleteOrphanGeneratedMeshes(HashSet<string> validSafeNames)
    {
        string[] meshGuids = AssetDatabase.FindAssets("t:Mesh", new[] { GeneratedMeshesFolder });
        int deleted = 0;
        for (int i = 0; i < meshGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(meshGuids[i]);
            string fileName = Path.GetFileNameWithoutExtension(path);
            if (!fileName.EndsWith("_Thick", StringComparison.OrdinalIgnoreCase))
                continue;

            string safeName = fileName.Substring(0, fileName.Length - "_Thick".Length);
            if (string.IsNullOrWhiteSpace(safeName))
                continue;

            if (validSafeNames.Contains(safeName))
                continue;

            if (AssetDatabase.DeleteAsset(path))
                deleted++;
        }

        return deleted;
    }

    private static int ClearPropRegistryList()
    {
        var registry = GetOrCreatePropRegistry();
        if (registry == null || registry.allProps == null)
            return 0;

        int previousCount = registry.allProps.Count;
        registry.allProps.Clear();
        EditorUtility.SetDirty(registry);
        return previousCount;
    }

    private static bool IsSanitizedGeneratedName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (char.IsLetterOrDigit(c) || c == '_' || c == '-')
                continue;

            return false;
        }

        return true;
    }

    private static T EnsureComponent<T>(GameObject gameObject) where T : Component
    {
        if (gameObject == null)
            return null;

        var existing = gameObject.GetComponent<T>();
        if (existing != null)
            return existing;

        return gameObject.AddComponent<T>();
    }

    private static void CopySpriteRendererSettings(SpriteRenderer source, SpriteRenderer destination)
    {
        if (source == null || destination == null)
            return;

        destination.sortingLayerID = source.sortingLayerID;
        destination.sortingOrder = source.sortingOrder;
        destination.spriteSortPoint = source.spriteSortPoint;
        destination.color = source.color;
        destination.flipX = source.flipX;
        destination.flipY = source.flipY;
        destination.drawMode = source.drawMode;
        destination.size = source.size;
        destination.maskInteraction = source.maskInteraction;
        destination.sharedMaterial = source.sharedMaterial;
    }

    private static void CopyBillboardSettingsToTracker(BillboardToCamera source, DistanceToCameraTracker destination)
    {
        if (source == null || destination == null)
            return;

        destination.targetCamera = source.targetCamera;
        destination.flattenAxisToXZ = source.yAxisOnly;
        destination.forcePivotSortPoint = source.forcePivotSortPoint;
        destination.driveSortingOrder = source.driveSortingOrder;
        destination.sortingOrderScale = source.sortingOrderScale;
        destination.sortingOrderBias = source.sortingOrderBias;
        destination.depthOffset = source.depthOffset;
    }

    private static void RemoveComponentIfExists<T>(GameObject gameObject) where T : Component
    {
        if (gameObject == null)
            return;

        T[] components = gameObject.GetComponents<T>();
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] != null)
                UnityEngine.Object.DestroyImmediate(components[i]);
        }
    }

    private static void RemoveObsoleteRootShadowScripts(GameObject root)
    {
        if (root == null)
            return;

        MonoBehaviour[] components = root.GetComponents<MonoBehaviour>();
        for (int i = 0; i < components.Length; i++)
        {
            MonoBehaviour component = components[i];
            if (component == null)
                continue;

            string typeName = component.GetType().Name;
            if (string.Equals(typeName, "BillboardCastShadow", StringComparison.Ordinal))
                UnityEngine.Object.DestroyImmediate(component);
        }
    }

    private static Transform EnsureChild(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        if (child != null)
            return child;

        var go = new GameObject(childName);
        child = go.transform;
        child.SetParent(parent, false);
        return child;
    }
}
#endif
