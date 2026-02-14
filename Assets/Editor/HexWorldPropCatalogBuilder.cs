#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using GalacticFishing.Minigames.HexWorld;

public sealed class HexWorldPropCatalogBuilder : EditorWindow
{
    private const string SpritesFolder = "Assets/Sprites/Props";
    private const string PropPrefabsFolder = "Assets/Minigames/HexWorld3D/Prefabs/Props";
    private const string PropDefinitionsFolder = "Assets/Minigames/HexWorld3D/Definitions/Props";
    private const string PropRegistryAssetPath = "Assets/Minigames/HexWorld3D/Definitions/PropRegistry_Main.asset";
    private const string VillageControllerPrefabPath = "Assets/Minigames/Prefabs/Prefab_HexWorld3D_Village.prefab";
    private const string ShadowMaterialPath = "Assets/Minigames/HexWorld3D/Materials/Props/Mat_ShadowSilhouette.mat";
    private const string PropScalePrefKey = "HexWorldPropCatalogBuilder.PropScale";
    private const byte AlphaThreshold = 10;
    private float _propScale = 0.1f;
    private string _utilityPath = "Assets/Sprites/Buildings";

    [MenuItem("Galactic Fishing/Catalogs/HexWorld Props")]
    public static void OpenWindow()
    {
        GetWindow<HexWorldPropCatalogBuilder>("Prop Catalog Builder");
    }

    private void OnEnable()
    {
        _propScale = EditorPrefs.GetFloat(PropScalePrefKey, _propScale);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("HexWorld Prop Catalog Builder", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Processes PNGs in Assets/Sprites/Props, creates/updates prop prefabs and definitions, then rewires propCatalog on the village controller prefab.",
            MessageType.Info);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Sprites Folder", SpritesFolder);
        EditorGUILayout.LabelField("Prefabs Folder", PropPrefabsFolder);
        EditorGUILayout.LabelField("Definitions Folder", PropDefinitionsFolder);
        EditorGUILayout.LabelField("Registry Asset", PropRegistryAssetPath);
        EditorGUILayout.LabelField("Controller Prefab", VillageControllerPrefabPath);
        EditorGUILayout.LabelField("Shadow Material", ShadowMaterialPath);

        EditorGUILayout.Space();
        _propScale = Mathf.Max(0.001f, EditorGUILayout.FloatField("PROP SCALE", _propScale));
        EditorPrefs.SetFloat(PropScalePrefKey, _propScale);

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
        {
            if (GUILayout.Button("Rebuild Prop Catalog", GUILayout.Height(34f)))
            {
                RebuildCatalog(_propScale);
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
        RebuildCatalog(EditorPrefs.GetFloat(PropScalePrefKey, 0.1f));
    }

    private static void RebuildCatalog(float propScale)
    {
        if (!AssetDatabase.IsValidFolder(SpritesFolder))
        {
            Debug.LogError($"[HexWorldPropCatalogBuilder] Missing folder: {SpritesFolder}");
            return;
        }

        EnsureFolder(PropPrefabsFolder);
        EnsureFolder(PropDefinitionsFolder);

        string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { SpritesFolder });
        var texturePaths = textureGuids
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
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

                string safeName = MakeSafeAssetName(rawName);
                string displayName = rawName.ToUpperInvariant();

                Sprite sprite = ConfigureAndLoadSprite(texturePath, rawName, ref texturesProcessed);
                if (sprite == null)
                {
                    Debug.LogWarning($"[HexWorldPropCatalogBuilder] Could not load sprite from '{texturePath}', skipped.");
                    continue;
                }

                GameObject propPrefab = CreateOrUpdatePropPrefab(safeName, sprite, shadowMaterial, ref prefabsCreated, ref prefabsUpdated);
                if (propPrefab == null)
                {
                    Debug.LogWarning($"[HexWorldPropCatalogBuilder] Could not create prefab for '{texturePath}', skipped.");
                    continue;
                }

                CreateOrUpdateDefinition(safeName, displayName, sprite, propPrefab, propScale, ref defsCreated, ref defsUpdated);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[HexWorldPropCatalogBuilder] Failed processing '{texturePath}': {ex}");
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var defs = LoadPropDefinitions();
        var registry = GetOrCreatePropRegistry();
        int wiredRegistryCount = WireRegistry(registry, defs);
        int wiredPrefabCount = WireCatalogToVillageControllerPrefab(defs);
        int wiredSceneCount = WireCatalogToSceneInstance(defs);

        Debug.Log(
            $"[HexWorldPropCatalogBuilder] Done. PNGs: {texturePaths.Count}, processed: {texturesProcessed}, " +
            $"prefabs created/updated: {prefabsCreated}/{prefabsUpdated}, definitions created/updated: {defsCreated}/{defsUpdated}, " +
            $"registry/prefab/scene wired: {wiredRegistryCount}/{wiredPrefabCount}/{wiredSceneCount}.");
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

    private static GameObject CreateOrUpdatePropPrefab(string safeName, Sprite sprite, Material shadowMaterial, ref int created, ref int updated)
    {
        if (string.IsNullOrWhiteSpace(safeName))
            return null;
        if (sprite == null)
            return null;

        string prefabPath = $"{PropPrefabsFolder}/{safeName}.prefab";
        GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

        if (existingPrefab == null)
        {
            var go = new GameObject($"Prop_{safeName}");
            try
            {
                if (go == null)
                    return null;

                if (!ConfigurePropPrefabHierarchy(go, sprite, shadowMaterial))
                    return null;

                EditorUtility.SetDirty(go);
                var savedPrefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                if (savedPrefab == null)
                    return null;
                if (savedPrefab != null)
                    EditorUtility.SetDirty(savedPrefab);
                created++;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }

        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        if (root == null)
            return null;

        try
        {
            if (!ConfigurePropPrefabHierarchy(root, sprite, shadowMaterial))
                return null;
            EditorUtility.SetDirty(root);

            var savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            if (savedPrefab == null)
                return null;
            if (savedPrefab != null)
                EditorUtility.SetDirty(savedPrefab);
            updated++;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
    }

    private static bool ConfigurePropPrefabHierarchy(GameObject root, Sprite sprite, Material shadowMaterial)
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

        var billboard = EnsureComponent<BillboardToCamera>(visual.gameObject);
        if (billboard == null)
            return false;

        if (rootBillboard != null && rootBillboard != billboard)
        {
            CopyBillboardSettings(rootBillboard, billboard);
            UnityEngine.Object.DestroyImmediate(rootBillboard);
        }

        billboard.yAxisOnly = true;
        billboard.spriteRenderer = visualSr;

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
        shadowCast.useYawOverride = true;
        shadowCast.yawDegrees = -45f;
        shadowCast.castDistanceInHeights = 0f;
        shadowCast.groundTiltX = 90f;
        shadowCast.groundLift = 0.03f;
        shadowCast.lengthScale = 2.0f;
        shadowCast.alpha = 0.65f;

        // Root should be a container only.
        RemoveComponentIfExists<SpriteRenderer>(root);
        RemoveComponentIfExists<BillboardToCamera>(root);
        RemoveComponentIfExists<GroundCastShadow2D>(root);
        RemoveObsoleteRootShadowScripts(root);

        EditorUtility.SetDirty(visualSr);
        EditorUtility.SetDirty(billboard);
        EditorUtility.SetDirty(shadowSr);
        EditorUtility.SetDirty(shadowCast);
        return true;
    }

    private static void CreateOrUpdateDefinition(
        string safeName,
        string displayName,
        Sprite sprite,
        GameObject propPrefab,
        float propScale,
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

        // Keep IDs aligned with generated asset filenames for deterministic registry lookup.
        def.id = safeName;
        def.displayName = displayName;
        def.thumbnail = sprite;
        def.prefab = propPrefab;
        def.scale = propScale;
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
                if (!importer.isReadable)
                {
                    importer.isReadable = true;
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

                bool pivotChanged = settings.spriteAlignment != (int)SpriteAlignment.Custom ||
                                    !Mathf.Approximately(settings.spritePivot.x, newPivot.x) ||
                                    !Mathf.Approximately(settings.spritePivot.y, newPivot.y);
                if (!pivotChanged)
                    continue;

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

    private static void CopyBillboardSettings(BillboardToCamera source, BillboardToCamera destination)
    {
        if (source == null || destination == null)
            return;

        destination.targetCamera = source.targetCamera;
        destination.yAxisOnly = source.yAxisOnly;
        destination.yawOffsetDegrees = source.yawOffsetDegrees;
        destination.invertFacing = source.invertFacing;
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
