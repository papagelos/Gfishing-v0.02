#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using GalacticFishing.Minigames.HexWorld;
using GalacticFishing.Minigames.Dungeon3D;

public sealed class HexWorldTileCatalogBuilder : EditorWindow
{
    private const string WindowMenuPath = "Galactic Fishing/Catalogs/Tile Auto-Builder";
    private const string TargetShaderName = "Shader Graphs/SG_Tiletop_WorldUV";
    private const string VerifiedTexturePropertyName = "_Basemap";
    private const string MaterialFolder = "Assets/Minigames/HexWorld3D/Materials/Tiles/Dungeon";
    private const string DefinitionFolder = "Assets/Minigames/HexWorld3D/Definitions/Tiles/Dungeon";
    private const string OwnedTilePrefabPath = "Assets/Minigames/HexWorld3D/Prefabs/PF_HexTile3D_Owned.prefab";
    private const string FrontierTilePrefabPath = "Assets/Minigames/HexWorld3D/Prefabs/PF_HexTile3D_Frontier.prefab";
    private const float BaseHexSize = 0.5f;
    private const string PrefTargetHexSize = "GF.HexWorldTileCatalogBuilder.TargetHexSize";
    private const string PrefLastAppliedHexSize = "GF.HexWorldTileCatalogBuilder.LastAppliedHexSize";
    private const string PrefScaleMovementAndPan = "GF.HexWorldTileCatalogBuilder.ScaleMovementAndPan";

    private string _sourceFolder = "Assets/Sprites/Biomes";
    private bool _appendToSceneCatalog = true;
    private DimensionGenProfile _genProfile;
    private float _targetHexSize = BaseHexSize;
    private float _lastAppliedHexSize = BaseHexSize;
    private bool _scaleMovementAndPan = true;

    [MenuItem(WindowMenuPath)]
    public static void Open()
    {
        var window = GetWindow<HexWorldTileCatalogBuilder>("Tile Auto-Builder");
        window.minSize = new Vector2(560f, 260f);
        window.Show();
    }

    private void OnEnable()
    {
        _targetHexSize = Mathf.Max(0.05f, EditorPrefs.GetFloat(PrefTargetHexSize, BaseHexSize));
        _lastAppliedHexSize = Mathf.Max(0.05f, EditorPrefs.GetFloat(PrefLastAppliedHexSize, BaseHexSize));
        _scaleMovementAndPan = EditorPrefs.GetBool(PrefScaleMovementAndPan, true);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("HexWorld Tile Auto-Builder", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Batch-configures tile textures, creates Dungeon materials, creates TileStyle assets, and optionally appends them to the scene HexWorld3DController styleCatalog.",
            MessageType.Info);

        EditorGUILayout.Space();
        var sourceFolderLabel = new GUIContent(
            "Source PNG Folder",
            "Directory-driven mode: scans recursively for .png files under Assets/Sprites/Biomes/[GROUP]/Tiles and tags each generated TileStyle with [GROUP].");
        _sourceFolder = EditorGUILayout.TextField(sourceFolderLabel, _sourceFolder);

        var appendLabel = new GUIContent(
            "Append generated styles to current scene styleCatalog",
            "If enabled, the tool will automatically fill the Tile Bar in your current Village scene with the tiles found in the source folder.");
        _appendToSceneCatalog = EditorGUILayout.ToggleLeft(appendLabel, _appendToSceneCatalog);

        var profileLabel = new GUIContent(
            "Dungeon Gen Profile",
            "Assign your 'DimensionGenProfile' here. The tool will automatically sort your new tiles into the correct biome groups so the procedural dungeon renderer can find them.");
        _genProfile = (DimensionGenProfile)EditorGUILayout.ObjectField(profileLabel, _genProfile, typeof(DimensionGenProfile), false);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Material Shader", TargetShaderName);
        EditorGUILayout.LabelField("Material Path", MaterialFolder);
        EditorGUILayout.LabelField("Definition Path", DefinitionFolder);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Global Hex Scale Utility", EditorStyles.boldLabel);
        _targetHexSize = Mathf.Max(0.05f, EditorGUILayout.FloatField(
            new GUIContent("Global Hex Size", "Sets hexSize on Village/Dungeon controllers and scales core tile prefabs to match."),
            _targetHexSize));
        _scaleMovementAndPan = EditorGUILayout.ToggleLeft(
            new GUIContent("Scale player moveSpeed and camera panSpeed (scene instances)",
                "Optional: scales PlayerController3D.moveSpeed and HexCameraPanZoom3D.panSpeed by the same ratio so navigation keeps pace with world scale."),
            _scaleMovementAndPan);

        EditorPrefs.SetFloat(PrefTargetHexSize, _targetHexSize);
        EditorPrefs.SetBool(PrefScaleMovementAndPan, _scaleMovementAndPan);

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
        {
            if (GUILayout.Button("Build Tile Catalog", GUILayout.Height(34f)))
                BuildCatalog(destructive: false);

            var destructiveContent = new GUIContent(
                "Clear and Rebuild Catalog",
                "DESTRUCTIVE: Deletes ALL existing generated tile materials and assets, wipes the current scene's tile list, and resets the Dungeon Profile before rebuilding from the source folder.");
            if (GUILayout.Button(destructiveContent, GUILayout.Height(34f)))
                BuildCatalog(destructive: true);

            if (GUILayout.Button("Apply Global Hex Scale", GUILayout.Height(34f)))
                ApplyGlobalHexScale();
        }
    }

    private void ApplyGlobalHexScale()
    {
        float targetHexSize = Mathf.Max(0.05f, _targetHexSize);
        float baseRatio = targetHexSize / BaseHexSize;
        float deltaRatio = targetHexSize / Mathf.Max(0.05f, _lastAppliedHexSize);

        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Apply Global Hex Scale");

        int villageSceneControllers = SetSceneFloatProperty<HexWorld3DController>("hexSize", targetHexSize);
        int dungeonSceneRenderers = SetSceneFloatProperty<DimensionRenderer>("hexSize", targetHexSize);
        int villagePrefabControllers = SetPrefabFloatProperty<HexWorld3DController>("hexSize", targetHexSize);
        int dungeonPrefabRenderers = SetPrefabFloatProperty<DimensionRenderer>("hexSize", targetHexSize);
        int tilePrefabsScaled = SetTilePrefabScale(baseRatio);
        int propDefsUpdated = ScaleAllPropJitter(deltaRatio);

        int moveSpeedScaled = 0;
        int panSpeedScaled = 0;
        if (_scaleMovementAndPan)
        {
            moveSpeedScaled = ScaleSceneFloatProperty<PlayerController3D>("moveSpeed", deltaRatio, 0.1f);
            panSpeedScaled = ScaleSceneFloatProperty<HexCameraPanZoom3D>("panSpeed", deltaRatio, 0.01f);
        }

        _targetHexSize = targetHexSize;
        _lastAppliedHexSize = targetHexSize;
        EditorPrefs.SetFloat(PrefTargetHexSize, _targetHexSize);
        EditorPrefs.SetFloat(PrefLastAppliedHexSize, _lastAppliedHexSize);

        AssetDatabase.SaveAssets();
        Undo.CollapseUndoOperations(undoGroup);

        Debug.Log(
            $"[HexWorldTileCatalogBuilder] Global hex scale applied. target={targetHexSize:0.###}, baseRatio={baseRatio:0.###}, deltaRatio={deltaRatio:0.###}. " +
            $"Updated scene controllers/renderers: {villageSceneControllers}/{dungeonSceneRenderers}, " +
            $"prefab controllers/renderers: {villagePrefabControllers}/{dungeonPrefabRenderers}, " +
            $"tile prefabs: {tilePrefabsScaled}, prop jitter defs: {propDefsUpdated}, move/pan scaled: {moveSpeedScaled}/{panSpeedScaled}.");
    }

    private void BuildCatalog(bool destructive)
    {
        if (!AssetDatabase.IsValidFolder(_sourceFolder))
        {
            Debug.LogError($"[HexWorldTileCatalogBuilder] Invalid source folder: {_sourceFolder}");
            return;
        }

        EnsureFolder(MaterialFolder);
        EnsureFolder(DefinitionFolder);

        int deletedMaterials = 0;
        int deletedDefinitions = 0;
        int clearedSceneStyles = 0;
        int clearedProfileGroups = 0;

        if (destructive)
        {
            deletedMaterials = DeleteAllAssetsInFolder(MaterialFolder);
            deletedDefinitions = DeleteAllAssetsInFolder(DefinitionFolder);
            clearedSceneStyles = ClearSceneStyleCatalog();
            clearedProfileGroups = ResetGenProfileBiomeGroups(_genProfile);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { _sourceFolder });
        var texturePaths = textureGuids
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path =>
                path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) &&
                IsBiomeTilePath(path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (texturePaths.Count == 0)
        {
            Debug.LogWarning($"[HexWorldTileCatalogBuilder] No PNG files found in {_sourceFolder}");
            return;
        }

        int texturesProcessed = 0;
        int materialsCreated = 0;
        int materialsUpdated = 0;
        int stylesCreated = 0;
        int stylesUpdated = 0;

        var generatedStyles = new List<HexWorldTileStyle>(texturePaths.Count);

        for (int i = 0; i < texturePaths.Count; i++)
        {
            string texturePath = texturePaths[i];
            string rawName = Path.GetFileNameWithoutExtension(texturePath).Trim();
            if (string.IsNullOrEmpty(rawName))
                continue;

            if (!TryExtractBiomeGroupFromTilePath(texturePath, out string biomeGroup))
                continue;

            Texture2D texture = ConfigureTextureImporter(texturePath);
            if (texture == null)
            {
                Debug.LogWarning($"[HexWorldTileCatalogBuilder] Failed importing texture: {texturePath}");
                continue;
            }

            texturesProcessed++;

            string displayName = ParseDisplayName(rawName);

            Material material = CreateOrUpdateMaterial(rawName, texture, ref materialsCreated, ref materialsUpdated);
            if (material == null)
                continue;

            HexWorldTileStyle style = CreateOrUpdateTileStyle(
                rawName,
                displayName,
                biomeGroup,
                texture,
                material,
                ref stylesCreated,
                ref stylesUpdated);

            if (style != null)
                generatedStyles.Add(style);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        int appendedCount = 0;
        bool shouldAppendToScene = destructive || _appendToSceneCatalog;
        if (shouldAppendToScene && generatedStyles.Count > 0)
            appendedCount = AppendStylesToSceneCatalog(generatedStyles);

        int syncedToProfile = SyncStylesToGenProfile(_genProfile, generatedStyles, destructive);

        Debug.Log(
            $"[HexWorldTileCatalogBuilder] Done. PNGs: {texturePaths.Count}, processed: {texturesProcessed}, " +
            $"materials created/updated: {materialsCreated}/{materialsUpdated}, styles created/updated: {stylesCreated}/{stylesUpdated}, " +
            $"appended to scene styleCatalog: {appendedCount}, " +
            $"synced to dungeon profile: {syncedToProfile}" +
            (destructive
                ? $", deleted materials/definitions: {deletedMaterials}/{deletedDefinitions}, cleared scene styles: {clearedSceneStyles}, cleared profile groups: {clearedProfileGroups}"
                : "."));
    }

    // Phase 1: Texture Import Settings
    private static Texture2D ConfigureTextureImporter(string texturePath)
    {
        var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
        if (importer == null)
            return null;

        importer.textureType = TextureImporterType.Default;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.maxTextureSize = 1024;
        importer.mipmapEnabled = true;
        importer.anisoLevel = 16;
        importer.mipMapBias = -0.5f;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
    }

    // Phase 2: Naming & Parsing
    private static string ParseDisplayName(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
            return "Unknown";

        string[] parts = rawName.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
        string displayToken = parts.Length > 1
            ? string.Join(" ", parts.Skip(1))
            : rawName;
        return ToTitleCase(displayToken.Replace('-', ' ').Replace('.', ' '));
    }

    private static string ToTitleCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Unknown";

        var textInfo = CultureInfo.InvariantCulture.TextInfo;
        return textInfo.ToTitleCase(value.Trim().ToLowerInvariant());
    }

    // Phase 3: Asset Creation (Material)
    private static Material CreateOrUpdateMaterial(string rawName, Texture2D texture, ref int created, ref int updated)
    {
        string safeName = MakeSafeAssetName(rawName);
        string materialPath = $"{MaterialFolder}/mat_tiletop_{safeName}.mat";

        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        bool isNew = material == null;

        Shader shader = Shader.Find(TargetShaderName);
        if (shader == null)
        {
            Debug.LogError($"[HexWorldTileCatalogBuilder] Shader not found: {TargetShaderName}");
            return null;
        }

        if (isNew)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, materialPath);
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
        }

        if (texture != null)
        {
            // Assign to all common texture slots plus the verified shader-graph reference.
            SetTextureIfPresent(material, "_BaseMap", texture);
            SetTextureIfPresent(material, "_MainTex", texture);
            SetTextureIfPresent(material, VerifiedTexturePropertyName, texture);
        }

        // World-UV tiles must have a valid non-zero scale to render.
        if (material.HasProperty("_WorldScale"))
            material.SetFloat("_WorldScale", 0.3f);

        EditorUtility.SetDirty(material);
        if (isNew) created++;
        else updated++;

        return material;
    }

    // Phase 3/4: Asset Creation + Data Population (TileStyle)
    private static HexWorldTileStyle CreateOrUpdateTileStyle(
        string rawName,
        string displayName,
        string biomeGroup,
        Texture2D thumbnail,
        Material material,
        ref int created,
        ref int updated)
    {
        string safeName = MakeSafeAssetName(rawName);
        string stylePath = $"{DefinitionFolder}/TileStyle_{safeName}.asset";

        HexWorldTileStyle style = AssetDatabase.LoadAssetAtPath<HexWorldTileStyle>(stylePath);
        bool isNew = style == null;

        if (isNew)
        {
            style = ScriptableObject.CreateInstance<HexWorldTileStyle>();
            AssetDatabase.CreateAsset(style, stylePath);
        }

        style.displayName = displayName;
        style.biomeGroup = biomeGroup;
        style.thumbnail = thumbnail;
        style.materials = material != null ? new[] { material } : Array.Empty<Material>();
        style.category = TileCategory.Cosmetic;
        style.unlockTownTier = 1;

        EditorUtility.SetDirty(style);
        if (isNew) created++;
        else updated++;

        return style;
    }

    // Phase 5: Scene Wiring
    private static int AppendStylesToSceneCatalog(List<HexWorldTileStyle> generatedStyles)
    {
        var controller = UnityEngine.Object.FindAnyObjectByType<HexWorld3DController>(FindObjectsInactive.Include);
        if (controller == null)
        {
            Debug.LogWarning("[HexWorldTileCatalogBuilder] No HexWorld3DController found in current scene. Skipped styleCatalog append.");
            return 0;
        }

        var so = new SerializedObject(controller);
        var styleCatalog = so.FindProperty("styleCatalog");
        if (styleCatalog == null || !styleCatalog.isArray)
        {
            Debug.LogWarning("[HexWorldTileCatalogBuilder] styleCatalog array not found on HexWorld3DController.");
            return 0;
        }

        var merged = new List<HexWorldTileStyle>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        string Key(HexWorldTileStyle style)
        {
            if (style == null) return null;
            string path = AssetDatabase.GetAssetPath(style);
            if (string.IsNullOrEmpty(path))
                return style.GetInstanceID().ToString();

            string guid = AssetDatabase.AssetPathToGUID(path);
            return string.IsNullOrEmpty(guid) ? path : guid;
        }

        for (int i = 0; i < styleCatalog.arraySize; i++)
        {
            var existing = styleCatalog.GetArrayElementAtIndex(i).objectReferenceValue as HexWorldTileStyle;
            if (existing == null) continue;

            string key = Key(existing);
            if (string.IsNullOrEmpty(key) || !seen.Add(key)) continue;
            merged.Add(existing);
        }

        for (int i = 0; i < generatedStyles.Count; i++)
        {
            var style = generatedStyles[i];
            if (style == null) continue;

            string key = Key(style);
            if (string.IsNullOrEmpty(key) || !seen.Add(key)) continue;
            merged.Add(style);
        }

        styleCatalog.arraySize = merged.Count;
        for (int i = 0; i < merged.Count; i++)
            styleCatalog.GetArrayElementAtIndex(i).objectReferenceValue = merged[i];

        so.ApplyModifiedProperties();
        PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
        if (controller.gameObject.scene.IsValid())
            EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);

        EditorUtility.SetDirty(controller);
        return merged.Count;
    }

    private static int ClearSceneStyleCatalog()
    {
        var controller = UnityEngine.Object.FindAnyObjectByType<HexWorld3DController>(FindObjectsInactive.Include);
        if (controller == null)
            return 0;

        var so = new SerializedObject(controller);
        var styleCatalog = so.FindProperty("styleCatalog");
        if (styleCatalog == null || !styleCatalog.isArray)
            return 0;

        int previousCount = styleCatalog.arraySize;
        styleCatalog.arraySize = 0;
        so.ApplyModifiedProperties();

        PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
        if (controller.gameObject.scene.IsValid())
            EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
        EditorUtility.SetDirty(controller);
        return previousCount;
    }

    private static int ResetGenProfileBiomeGroups(DimensionGenProfile profile)
    {
        if (profile == null)
            return 0;

        if (profile.biomeStyleGroups == null)
            profile.biomeStyleGroups = new List<BiomeTileStyleGroup>();

        int count = profile.biomeStyleGroups.Count;
        profile.biomeStyleGroups.Clear();
        EditorUtility.SetDirty(profile);
        return count;
    }

    private static int SyncStylesToGenProfile(DimensionGenProfile profile, List<HexWorldTileStyle> styles, bool groupsAlreadyCleared)
    {
        if (profile == null || styles == null || styles.Count == 0)
            return 0;

        if (profile.biomeStyleGroups == null)
            profile.biomeStyleGroups = new List<BiomeTileStyleGroup>();

        if (!groupsAlreadyCleared && profile.biomeStyleGroups.Count == 0)
            profile.biomeStyleGroups = new List<BiomeTileStyleGroup>();

        var groupByBiome = new Dictionary<string, BiomeTileStyleGroup>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < profile.biomeStyleGroups.Count; i++)
        {
            var group = profile.biomeStyleGroups[i];
            if (group == null)
                continue;

            string key = NormalizeBiome(group.biomeGroup);
            if (string.IsNullOrEmpty(key))
                continue;

            group.biomeGroup = key;
            group.tileStyles ??= new List<HexWorldTileStyle>();
            groupByBiome[key] = group;
        }

        int addedCount = 0;
        for (int i = 0; i < styles.Count; i++)
        {
            HexWorldTileStyle style = styles[i];
            if (style == null)
                continue;

            string biome = NormalizeBiome(style.biomeGroup);
            if (!groupByBiome.TryGetValue(biome, out BiomeTileStyleGroup group))
            {
                group = new BiomeTileStyleGroup
                {
                    biomeGroup = biome,
                    tileStyles = new List<HexWorldTileStyle>()
                };
                profile.biomeStyleGroups.Add(group);
                groupByBiome[biome] = group;
            }

            if (!group.tileStyles.Contains(style))
            {
                group.tileStyles.Add(style);
                addedCount++;
            }
        }

        for (int i = 0; i < profile.biomeStyleGroups.Count; i++)
        {
            var group = profile.biomeStyleGroups[i];
            if (group?.tileStyles == null)
                continue;

            var unique = new List<HexWorldTileStyle>();
            var seen = new HashSet<HexWorldTileStyle>();
            for (int t = 0; t < group.tileStyles.Count; t++)
            {
                var style = group.tileStyles[t];
                if (style == null || !seen.Add(style))
                    continue;
                unique.Add(style);
            }

            group.tileStyles = unique
                .OrderBy(s => s.name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        profile.biomeStyleGroups = profile.biomeStyleGroups
            .Where(g => g != null)
            .OrderBy(g => g.biomeGroup, StringComparer.OrdinalIgnoreCase)
            .ToList();

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        return addedCount;
    }

    private static int DeleteAllAssetsInFolder(string folder)
    {
        if (!AssetDatabase.IsValidFolder(folder))
            return 0;

        string[] guids = AssetDatabase.FindAssets(string.Empty, new[] { folder });
        int deleted = 0;
        for (int i = 0; i < guids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (string.IsNullOrEmpty(assetPath) || AssetDatabase.IsValidFolder(assetPath))
                continue;

            if (AssetDatabase.DeleteAsset(assetPath))
                deleted++;
        }

        return deleted;
    }

    private static string NormalizeBiome(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "DEFAULT";

        return value.Trim().ToUpperInvariant();
    }

    private static bool IsBiomeTilePath(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return false;

        string normalized = assetPath.Replace("\\", "/");
        return normalized.StartsWith("Assets/Sprites/Biomes/", StringComparison.OrdinalIgnoreCase) &&
               normalized.IndexOf("/Tiles/", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool TryExtractBiomeGroupFromTilePath(string assetPath, out string biomeGroup)
    {
        biomeGroup = null;
        if (string.IsNullOrEmpty(assetPath))
            return false;

        string normalized = assetPath.Replace("\\", "/");
        string[] parts = normalized.Split('/');
        for (int i = 0; i < parts.Length - 2; i++)
        {
            if (!string.Equals(parts[i], "Biomes", StringComparison.OrdinalIgnoreCase))
                continue;

            string biome = parts[i + 1];
            string category = parts[i + 2];
            if (!string.Equals(category, "Tiles", StringComparison.OrdinalIgnoreCase))
                return false;

            biomeGroup = NormalizeBiome(biome);
            return true;
        }

        return false;
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

    private static void SetTextureIfPresent(Material material, string propertyName, Texture texture)
    {
        if (material == null || string.IsNullOrEmpty(propertyName) || texture == null)
            return;

        if (material.HasProperty(propertyName))
            material.SetTexture(propertyName, texture);
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

    private static int SetTilePrefabScale(float ratio)
    {
        int updated = 0;
        updated += SetPrefabRootScale(OwnedTilePrefabPath, ratio) ? 1 : 0;
        updated += SetPrefabRootScale(FrontierTilePrefabPath, ratio) ? 1 : 0;
        return updated;
    }

    private static bool SetPrefabRootScale(string prefabPath, float ratio)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"[HexWorldTileCatalogBuilder] Missing prefab at path: {prefabPath}");
            return false;
        }

        Vector3 targetScale = Vector3.one * ratio;
        Transform tr = prefab.transform;
        if ((tr.localScale - targetScale).sqrMagnitude <= 0.0000001f)
            return false;

        Undo.RecordObject(tr, "Apply Global Hex Scale");
        tr.localScale = targetScale;
        EditorUtility.SetDirty(tr);
        EditorUtility.SetDirty(prefab);
        return true;
    }

    private static int ScaleAllPropJitter(float deltaRatio)
    {
        if (Mathf.Approximately(deltaRatio, 1f))
            return 0;

        string[] guids = AssetDatabase.FindAssets("t:HexWorldPropDefinition");
        int updated = 0;
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            HexWorldPropDefinition def = AssetDatabase.LoadAssetAtPath<HexWorldPropDefinition>(path);
            if (def == null)
                continue;

            float next = Mathf.Max(0f, def.jitterRadius * deltaRatio);
            if (Mathf.Approximately(def.jitterRadius, next))
                continue;

            Undo.RecordObject(def, "Apply Global Hex Scale");
            def.jitterRadius = next;
            EditorUtility.SetDirty(def);
            updated++;
        }

        return updated;
    }

    private static int SetSceneFloatProperty<T>(string propertyName, float value) where T : MonoBehaviour
    {
        T[] instances = Resources.FindObjectsOfTypeAll<T>();
        int updated = 0;
        for (int i = 0; i < instances.Length; i++)
        {
            T instance = instances[i];
            if (instance == null)
                continue;

            if (EditorUtility.IsPersistent(instance))
                continue;

            if (!instance.gameObject.scene.IsValid())
                continue;

            SerializedObject so = new SerializedObject(instance);
            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop == null || prop.propertyType != SerializedPropertyType.Float)
                continue;

            if (Mathf.Approximately(prop.floatValue, value))
                continue;

            Undo.RecordObject(instance, "Apply Global Hex Scale");
            prop.floatValue = value;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(instance);
            EditorSceneManager.MarkSceneDirty(instance.gameObject.scene);
            updated++;
        }

        return updated;
    }

    private static int ScaleSceneFloatProperty<T>(string propertyName, float multiplier, float minValue) where T : MonoBehaviour
    {
        if (Mathf.Approximately(multiplier, 1f))
            return 0;

        T[] instances = Resources.FindObjectsOfTypeAll<T>();
        int updated = 0;
        for (int i = 0; i < instances.Length; i++)
        {
            T instance = instances[i];
            if (instance == null)
                continue;

            if (EditorUtility.IsPersistent(instance))
                continue;

            if (!instance.gameObject.scene.IsValid())
                continue;

            SerializedObject so = new SerializedObject(instance);
            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop == null || prop.propertyType != SerializedPropertyType.Float)
                continue;

            float next = Mathf.Max(minValue, prop.floatValue * multiplier);
            if (Mathf.Approximately(prop.floatValue, next))
                continue;

            Undo.RecordObject(instance, "Apply Global Hex Scale");
            prop.floatValue = next;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(instance);
            EditorSceneManager.MarkSceneDirty(instance.gameObject.scene);
            updated++;
        }

        return updated;
    }

    private static int SetPrefabFloatProperty<T>(string propertyName, float value) where T : MonoBehaviour
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        int updated = 0;
        for (int i = 0; i < prefabGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                continue;

            T component = prefab.GetComponentInChildren<T>(true);
            if (component == null)
                continue;

            SerializedObject so = new SerializedObject(component);
            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop == null || prop.propertyType != SerializedPropertyType.Float)
                continue;

            if (Mathf.Approximately(prop.floatValue, value))
                continue;

            Undo.RecordObject(component, "Apply Global Hex Scale");
            prop.floatValue = value;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(component);
            EditorUtility.SetDirty(prefab);
            updated++;
        }

        return updated;
    }
}
#endif
