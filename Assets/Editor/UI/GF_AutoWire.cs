// Assets/Editor/GF_AutoWire.cs
// -----------------------------------------------------------------------------
// GF_AutoWire — Generic JSON-driven auto-wiring tool for ANY GameObject or Prefab
// Put this file anywhere under an "Editor" folder so Unity compiles it as editor-only.
// Menu: Tools > GF Auto Wire
//
// ✅ Supports TWO recipe formats:
//
// (A) "bindings" format (wiring-only):
// {
//   "recipeName": "Wire paging buttons",
//   "bindings": [
//     {
//       "targetPath": "UI_Root/Canvas/TileBar/SlotsContainer",
//       "targetComponent": "GalacticFishing.Minigames.HexWorld.HexWorldTileBarSlotsUI",
//       "targetField": "prevPageButton",
//       "overwrite": true,
//       "valueKind": "FindInChildrenByName",
//       "findNameContains": "Btn_PageLeft",
//       "findComponent": "UnityEngine.UI.Button"
//     }
//   ]
// }
//
// Supported valueKind (bindings):
// - ConstantInt / ConstantFloat / ConstantBool / ConstantString
// - ConstantNull              (clears ObjectReference fields)
// - FromObjectPathGameObject
// - FromObjectPathComponent
// - FindInChildrenByName
// - AssetByPath
// - AssetByGuid
//
// (B) "ops" format (create + wire + cleanup):
// {
//   "ops": [
//     { "op": "ensure_tmp_button", "parentPath": "UI_Root/Canvas/TileBar", "name":"Btn_PageLeft", "label":"<", ... },
//
//     { "op": "ensure_gameobject", "path": "panelRoot/StatsContainer" },
//
//     { "op": "set_rect", "path": "panelRoot/StatsContainer",
//       "anchorMinX": 0.5, "anchorMinY": 0.5, "anchorMaxX": 0.5, "anchorMaxY": 0.5,
//       "pivotX": 0.5, "pivotY": 0.5, "sizeDeltaX": 300, "sizeDeltaY": 400,
//       "anchoredPosX": 0, "anchoredPosY": 0 },
//
//     { "op": "add_vlg", "path": "panelRoot/StatsContainer",
//       "childAlignment": 0, "spacing": 6, "controlChildWidth": 1, "controlChildHeight": 0,
//       "forceExpandWidth": 1, "forceExpandHeight": 0 },
//
//     { "op": "set_float", "path": "panelRoot/Row/Text", "componentType": "TextMeshProUGUI",
//       "fieldName": "fontSize", "floatValue": 18 },
//
//     { "op": "set_string", "path": "panelRoot/Row/Text", "componentType": "TextMeshProUGUI",
//       "fieldName": "color", "stringValue": "#FFFFFF" },
//
//     { "op": "ensure_component",
//       "path": "UI_Root/Canvas/TileBar/PaletteTabs/PaletteTabsController",
//       "componentType": "GalacticFishing.Minigames.HexWorld.HexWorldSharedPagingButtonsRouter" },
//
//     { "op": "set_object_ref",
//       "path": "UI_Root/Canvas/TileBar/PaletteTabs/PaletteTabsController",
//       "componentType": "GalacticFishing.Minigames.HexWorld.HexWorldSharedPagingButtonsRouter",
//       "fieldName": "sharedPrevButton",
//       "refPath": "UI_Root/Canvas/TileBar/Btn_PageLeft_Tiles",
//       "refComponentType": "UnityEngine.UI.Button" },
//
//     { "op": "set_object_ref",  // refPath can be an asset path
//       "path": "UI_Root/Canvas/Panel",
//       "componentType": "MyComponent",
//       "fieldName": "statRowPrefab",
//       "refPath": "Assets/Prefabs/UI/StatRow.prefab" },
//
//     { "op": "set_object_ref",  // refChildPath allows referencing a component on a child within the prefab asset
//       "path": "UI_Root/Canvas/Panel",
//       "componentType": "MyComponent",
//       "fieldName": "labelText",
//       "refPath": "Assets/Prefabs/UI/StatRow.prefab",
//       "refChildPath": "Label",
//       "refComponentType": "TMPro.TextMeshProUGUI" },
//
//     { "op": "clear_object_ref",
//       "path": "UI_Root/Canvas/TileBar/SlotsContainer",
//       "componentType": "GalacticFishing.Minigames.HexWorld.HexWorldTileBarSlotsUI",
//       "fieldName": "prevPageButton" },
//
//     { "op": "remove_component",
//       "path": "UI_Root/Canvas/TileBar/BuildingBar",
//       "componentType": "GalacticFishing.Minigames.HexWorld.HexWorldTileBarSlotsUI",
//       "removeAll": false },
//
//     { "op": "destroy_gameobject",
//       "path": "UI_Root/Canvas/TileBar/BuildingBar/Btn_PageLeft_buildings" },
//
//     { "op": "add_button_listener",
//       "path": "UI_Root/Canvas/PaletteTabs/CenterButtons/Btn_DeleteMode",
//       "listenerPath": "HexWorld3D_Controller",
//       "listenerComponent": "GalacticFishing.Minigames.HexWorld.HexWorld3DController",
//       "listenerMethod": "SetPaletteModeDelete" },
//
//     // --- Prefab buffer mode ops ---
//     { "op": "ensure_folder", "folderPath": "Assets/Prefabs/UI" },
//
//     { "op": "create_prefab_asset", "prefabPath": "Assets/Prefabs/UI/StatRow.prefab" },
//     // After create_prefab_asset, all path-based ops use the prefab buffer as root
//     { "op": "ensure_gameobject", "path": "Label" },
//     { "op": "ensure_component", "path": "Label", "componentType": "TMPro.TextMeshProUGUI" },
//     { "op": "set_float", "path": "Label", "componentType": "TextMeshProUGUI", "fieldName": "fontSize", "floatValue": 14 },
//     { "op": "save_prefab_asset" }
//     // After save_prefab_asset, prefab mode ends and ops use the original root again
//   ]
// }
//
// Notes:
// - Paths are relative to the chosen root. If your JSON accidentally includes an extra top segment like
//   "Prefab_HexWorld3D_Core/...", this tool will try stripping leading segments until it finds a match.
// - JsonUtility cannot parse comments or trailing commas. Keep JSON strict.
//
// Example: Create a StatRow prefab with a TMP child, save it, and assign it to a scene object's field:
// {
//   "ops": [
//     { "op": "ensure_folder", "folderPath": "Assets/Prefabs/UI" },
//     { "op": "create_prefab_asset", "prefabPath": "Assets/Prefabs/UI/StatRow.prefab" },
//     { "op": "ensure_gameobject", "path": "Label" },
//     { "op": "ensure_component", "path": "", "componentType": "UnityEngine.RectTransform" },
//     { "op": "ensure_component", "path": "Label", "componentType": "TMPro.TextMeshProUGUI" },
//     { "op": "set_rect", "path": "", "sizeDeltaX": 200, "sizeDeltaY": 24 },
//     { "op": "set_rect", "path": "Label", "anchorMinX": 0, "anchorMinY": 0, "anchorMaxX": 1, "anchorMaxY": 1,
//       "sizeDeltaX": 0, "sizeDeltaY": 0, "anchoredPosX": 0, "anchoredPosY": 0 },
//     { "op": "set_float", "path": "Label", "componentType": "TextMeshProUGUI", "fieldName": "fontSize", "floatValue": 14 },
//     { "op": "set_string", "path": "Label", "componentType": "TextMeshProUGUI", "fieldName": "text", "stringValue": "Stat: Value" },
//     { "op": "save_prefab_asset" },
//     { "op": "set_object_ref", "path": "UI_Root/Canvas/ContextMenu",
//       "componentType": "GalacticFishing.Minigames.HexWorld.HexWorldBuildingContextMenu",
//       "fieldName": "statLabelPrefab", "refPath": "Assets/Prefabs/UI/StatRow.prefab" }
//   ]
// }
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public sealed class GF_AutoWire : EditorWindow
{
    // -------------------- "bindings" model --------------------
    [Serializable]
    private class Recipe
    {
        public string recipeName;
        public Binding[] bindings;
    }

    [Serializable]
    private class Binding
    {
        // Where to assign
        public string targetPath = "";          // relative to root; "" = root
        public string targetComponent = "";     // type name
        public string targetField = "";         // serialized prop path OR field/property name
        public bool overwrite = true;

        // What to assign
        public string valueKind = "";           // see header snippet

        // Constants
        public int intValue;
        public float floatValue;
        public bool boolValue;
        public string stringValue;

        // FromObjectPath*
        public string sourcePath = "";          // relative to root
        public string sourceComponent = "";     // for FromObjectPathComponent

        // FindInChildrenByName
        public string findNameContains = "";
        public string findComponent = "";       // optional filter: only if object has this component

        // Asset loading
        public string assetPath = "";
        public string assetGuid = "";
    }

    // -------------------- "ops" model --------------------
    [Serializable]
    private class OpsRecipe
    {
        public Op[] ops;
    }

    [Serializable]
    private class Op
    {
        public string op;

        // ensure_tmp_button
        public string parentPath;
        public string name;
        public string label;
        public float anchorMinX, anchorMinY;
        public float anchorMaxX, anchorMaxY;
        public float pivotX, pivotY;
        public float posX, posY;
        public float sizeX, sizeY;

        // set_object_ref / clear_object_ref / set_int / set_float / set_bool / set_string
        // ensure_component / remove_component / destroy_gameobject
        public string path;
        public string componentType;
        public string fieldName;

        // set_object_ref
        public string refPath;
        public string refChildPath;  // Optional: child path within a prefab asset (e.g. "Label" to get component from child named "Label")
        public string refComponentType;

        // remove_component
        public bool removeAll;

        // set_* values
        public int intValue;
        public float floatValue;
        public bool boolValue;
        public string stringValue;

        // set_rect (use NaN as sentinel for "not set")
        public float sizeDeltaX = float.NaN;
        public float sizeDeltaY = float.NaN;
        public float anchoredPosX = float.NaN;
        public float anchoredPosY = float.NaN;

        // add_vlg (VerticalLayoutGroup)
        public int childAlignment = -1;  // -1 = not set
        public float spacing = float.NaN;
        public int controlChildWidth = -1;  // -1 = not set, 0 = false, 1 = true
        public int controlChildHeight = -1;
        public int forceExpandWidth = -1;
        public int forceExpandHeight = -1;

        // ensure_folder / create_prefab_asset / save_prefab_asset
        public string folderPath;   // e.g. "Assets/Prefabs/UI"
        public string prefabPath;   // e.g. "Assets/Prefabs/UI/StatRow.prefab"

        // add_button_listener
        public string listenerPath;       // path to the GameObject with the target component
        public string listenerComponent;  // component type name (e.g. "HexWorld3DController")
        public string listenerMethod;     // method name (e.g. "SetPaletteModeDelete")
    }

    // -------------------- Prefab buffer state --------------------
    private static GameObject _currentBuffer;
    private static string _pendingPrefabPath;
    private static bool _isPrefabMode;

    // UI
    private UnityEngine.Object _target; // GameObject in scene OR prefab asset OR component
    private TextAsset _recipeJsonAsset;
    private string _recipeJsonRaw = "";
    private bool _useRawJson = false;
    private bool _safetyMode = true; // Blocks destructive ops (destroy_gameobject, remove_component)
    private Vector2 _scroll;

    // Caches
    private static Dictionary<string, Type> _typeCache = new();

    [MenuItem("Galactic Fishing/Utilities/Auto Wire")]
    public static void Open()
    {
        var w = GetWindow<GF_AutoWire>("GF Auto Wire");
        w.minSize = new Vector2(520, 520);
        w.Show();
    }

    private void OnEnable()
    {
        if (_target == null && Selection.activeObject != null)
            _target = Selection.activeObject;
    }

    private void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Drag a Scene GameObject OR a Prefab asset here.\n" +
            "If you drag a Component, we'll use its GameObject as the root.\n" +
            "For Prefab assets, we will apply changes inside PrefabContents and save the asset.",
            MessageType.Info);

        _target = EditorGUILayout.ObjectField("Root (GO or Prefab)", _target, typeof(UnityEngine.Object), true);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Recipe", EditorStyles.boldLabel);

        _useRawJson = EditorGUILayout.ToggleLeft("Use raw JSON text (instead of a TextAsset)", _useRawJson);

        if (!_useRawJson)
        {
            _recipeJsonAsset = (TextAsset)EditorGUILayout.ObjectField("Recipe JSON (TextAsset)", _recipeJsonAsset, typeof(TextAsset), false);
            if (_recipeJsonAsset)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Load into Raw Text", GUILayout.Width(150)))
                {
                    _recipeJsonRaw = _recipeJsonAsset.text;
                    _useRawJson = true;
                }
                if (GUILayout.Button("Ping Asset", GUILayout.Width(110)))
                    EditorGUIUtility.PingObject(_recipeJsonAsset);
                EditorGUILayout.EndHorizontal();
            }
        }
        else
        {
            EditorGUILayout.LabelField("Raw JSON", EditorStyles.miniBoldLabel);
            _recipeJsonRaw = EditorGUILayout.TextArea(_recipeJsonRaw, GUILayout.MinHeight(220));
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear", GUILayout.Width(80)))
                _recipeJsonRaw = "";
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Safety", EditorStyles.boldLabel);
        _safetyMode = EditorGUILayout.ToggleLeft("Safety Mode (blocks destructive ops)", _safetyMode);

        EditorGUILayout.Space(14);

        using (new EditorGUI.DisabledScope(!CanApply()))
        {
            if (GUILayout.Button("APPLY RECIPE", GUILayout.Height(42)))
            {
                ApplyRecipe();
            }
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Tips", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "• Paths are relative to the chosen root.\n" +
            "• This tool supports BOTH {bindings:[...]} and {ops:[...]} JSON.\n" +
            "• If a path doesn't resolve, we auto-try stripping leading path segments.\n" +
            "• If something doesn't wire, check Console logs for the exact binding/op that failed.",
            MessageType.None);

        EditorGUILayout.EndScrollView();
    }

    private bool CanApply()
    {
        // Allow target to be null (for prefab-buffer-only recipes that use create_prefab_asset)
        if (_useRawJson) return !string.IsNullOrWhiteSpace(_recipeJsonRaw);
        return _recipeJsonAsset != null && !string.IsNullOrWhiteSpace(_recipeJsonAsset.text);
    }

    private void ApplyRecipe()
    {
        try
        {
            string json = _useRawJson ? _recipeJsonRaw : _recipeJsonAsset.text;
            json = (json ?? "").Trim();
            if (json.Length > 0 && json[0] == '\ufeff') // BOM safety
                json = json.TrimStart('\ufeff');

            // Allow target to be null (for prefab-buffer-only recipes)
            GameObject rootGo = null;
            bool isPrefabAsset = false;
            string prefabAssetPath = "";

            if (_target != null)
            {
                rootGo = GetRootGameObject(_target, out isPrefabAsset, out prefabAssetPath);
            }

            // Only run the PrefabUtility logic if we have a prefab asset target
            if (isPrefabAsset && rootGo != null)
            {
                var loadedRoot = PrefabUtility.LoadPrefabContents(prefabAssetPath);
                if (!loadedRoot)
                {
                    Debug.LogError($"GF_AutoWire: Failed to LoadPrefabContents: {prefabAssetPath}");
                    return;
                }

                int ok = 0, fail = 0, skip = 0;
                ApplyJsonToRoot(json, loadedRoot.transform, _safetyMode, ref ok, ref fail, ref skip);

                PrefabUtility.SaveAsPrefabAsset(loadedRoot, prefabAssetPath);
                PrefabUtility.UnloadPrefabContents(loadedRoot);

                Debug.Log($"GF_AutoWire: Applied JSON to Prefab asset. OK={ok}, SKIP={skip}, FAIL={fail}");
            }
            else
            {
                // If target is null, rootGo is null. ApplyOpsToRoot will use GetEffectiveRoot (prefab buffer).
                if (rootGo != null)
                    Undo.RegisterFullObjectHierarchyUndo(rootGo, "GF AutoWire Apply Recipe");

                int ok = 0, fail = 0, skip = 0;
                ApplyJsonToRoot(json, rootGo != null ? rootGo.transform : null, _safetyMode, ref ok, ref fail, ref skip);

                Debug.Log($"GF_AutoWire: Recipe complete. OK={ok}, SKIP={skip}, FAIL={fail}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"GF_AutoWire: Exception while applying recipe: {e}");
        }
    }

    private static void ApplyJsonToRoot(string json, Transform root, bool safetyMode, ref int ok, ref int fail, ref int skip)
    {
        // 1) Try "bindings"
        Recipe recipe = null;
        try { recipe = JsonUtility.FromJson<Recipe>(json); } catch { /* ignore */ }

        if (recipe != null && recipe.bindings != null && recipe.bindings.Length > 0)
        {
            ApplyRecipeToRoot(recipe, root, ref ok, ref fail, ref skip);
            return;
        }

        // 2) Try "ops"
        OpsRecipe opsRecipe = null;
        try { opsRecipe = JsonUtility.FromJson<OpsRecipe>(json); } catch { /* ignore */ }

        if (opsRecipe != null && opsRecipe.ops != null && opsRecipe.ops.Length > 0)
        {
            ApplyOpsToRoot(opsRecipe, root, safetyMode, ref ok, ref fail, ref skip);
            return;
        }

        Debug.LogError("GF_AutoWire: recipe is empty or invalid JSON. It must match either {bindings:[...]} or {ops:[...]} format.");
        fail++;
    }

    // -------------------- Apply "bindings" --------------------
    private static void ApplyRecipeToRoot(Recipe recipe, Transform root, ref int ok, ref int fail, ref int skip)
    {
        for (int i = 0; i < recipe.bindings.Length; i++)
        {
            var b = recipe.bindings[i];
            if (b == null) { fail++; continue; }

            try
            {
                if (string.IsNullOrWhiteSpace(b.targetComponent) || string.IsNullOrWhiteSpace(b.targetField))
                {
                    Debug.LogWarning($"GF_AutoWire: Binding[{i}] missing targetComponent/targetField. Skipping.");
                    skip++;
                    continue;
                }

                var targetTf = ResolveTransformPathSmart(root, b.targetPath, out _);
                if (!targetTf)
                {
                    Debug.LogWarning($"GF_AutoWire: Binding[{i}] targetPath not found: '{b.targetPath}'");
                    fail++;
                    continue;
                }

                var targetComp = GetComponentByTypeName(targetTf.gameObject, b.targetComponent);
                if (!targetComp)
                {
                    Debug.LogWarning($"GF_AutoWire: Binding[{i}] component '{b.targetComponent}' not found on '{targetTf.name}'");
                    fail++;
                    continue;
                }

                object value = EvaluateValue(root, b, out string whyFail);
                if (whyFail != null)
                {
                    Debug.LogWarning($"GF_AutoWire: Binding[{i}] value failed: {whyFail}");
                    fail++;
                    continue;
                }

                bool assigned = TryAssign(targetComp, b.targetField, value, b.overwrite, out string whyNotAssigned);
                if (assigned) ok++;
                else
                {
                    if (whyNotAssigned == "SKIP") skip++;
                    else
                    {
                        Debug.LogWarning($"GF_AutoWire: Binding[{i}] assign failed: {whyNotAssigned}");
                        fail++;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"GF_AutoWire: Binding[{i}] exception: {e.Message}");
                fail++;
            }
        }
    }

    // -------------------- Apply "ops" --------------------
    private static void ApplyOpsToRoot(OpsRecipe recipe, Transform root, bool safetyMode, ref int ok, ref int fail, ref int skip)
    {
        bool useUndo = root != null && root.gameObject.scene.IsValid() && root.gameObject.scene.isLoaded;

        for (int i = 0; i < recipe.ops.Length; i++)
        {
            var o = recipe.ops[i];
            if (o == null) { fail++; continue; }

            string opName = (o.op ?? "").Trim();
            if (string.IsNullOrEmpty(opName)) { fail++; continue; }

            // Safety Mode: block destructive ops
            if (safetyMode && (opName == "destroy_gameobject" || opName == "remove_component"))
            {
                Debug.LogWarning($"GF_AutoWire: Safety Mode blocked op '{opName}' at path '{o.path}'");
                skip++;
                continue;
            }

            // Get effective root (prefab buffer if in prefab mode, otherwise the provided root)
            Transform effectiveRoot = GetEffectiveRoot(root);
            // In prefab mode, never use Undo (buffer is not in a scene)
            bool effectiveUndo = useUndo && !_isPrefabMode;

            try
            {
                switch (opName)
                {
                    case "ensure_tmp_button":
                    {
                        var parentTf = ResolveTransformPathSmart(effectiveRoot, o.parentPath, out _);
                        if (!parentTf)
                        {
                            Debug.LogWarning($"GF_AutoWire: Op[{i}] ensure_tmp_button parentPath not found: '{o.parentPath}'");
                            fail++;
                            break;
                        }

                        EnsureTMPButton(parentTf, o, effectiveUndo);
                        ok++;
                        break;
                    }

                    case "ensure_component":
                    {
                        var targetTf = ResolveTransformPathSmart(effectiveRoot, o.path, out _);
                        if (!targetTf) { Debug.LogWarning($"GF_AutoWire: Op[{i}] ensure_component path not found: '{o.path}'"); fail++; break; }
                        if (string.IsNullOrWhiteSpace(o.componentType)) { Debug.LogWarning($"GF_AutoWire: Op[{i}] ensure_component missing componentType"); fail++; break; }

                        var comp = EnsureComponentByTypeName(targetTf.gameObject, o.componentType, effectiveUndo);
                        if (comp != null) ok++; else fail++;
                        break;
                    }

                    case "remove_component":
                    {
                        var targetTf = ResolveTransformPathSmart(effectiveRoot, o.path, out _);
                        if (!targetTf) { Debug.LogWarning($"GF_AutoWire: Op[{i}] remove_component path not found: '{o.path}'"); fail++; break; }
                        if (string.IsNullOrWhiteSpace(o.componentType)) { Debug.LogWarning($"GF_AutoWire: Op[{i}] remove_component missing componentType"); fail++; break; }

                        int removed = RemoveComponentByTypeName(targetTf.gameObject, o.componentType, o.removeAll, effectiveUndo);
                        if (removed > 0) ok++; else { skip++; }
                        break;
                    }

                    case "destroy_gameobject":
                    {
                        var targetTf = ResolveTransformPathSmart(effectiveRoot, o.path, out _);
                        if (!targetTf)
                        {
                            Debug.LogWarning($"GF_AutoWire: Op[{i}] destroy_gameobject path not found: '{o.path}'");
                            skip++;
                            break;
                        }
                        if (targetTf == effectiveRoot)
                        {
                            Debug.LogWarning($"GF_AutoWire: Op[{i}] destroy_gameobject refused to destroy the root object.");
                            fail++;
                            break;
                        }

                        DestroyGameObject(targetTf.gameObject, effectiveUndo);
                        ok++;
                        break;
                    }

                    case "set_object_ref":
                    {
                        var targetTf = ResolveTransformPathSmart(effectiveRoot, o.path, out _);
                        if (!targetTf) { Debug.LogWarning($"GF_AutoWire: Op[{i}] target path not found: '{o.path}'"); fail++; break; }

                        var targetComp = GetComponentByTypeName(targetTf.gameObject, o.componentType);
                        if (!targetComp) { Debug.LogWarning($"GF_AutoWire: Op[{i}] component not found: '{o.componentType}' on '{targetTf.name}'"); fail++; break; }

                        object value;
                        string refPath = (o.refPath ?? "").Trim();

                        // Check if refPath is an asset path (starts with "Assets/")
                        if (refPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                        {
                            // Load asset from AssetDatabase
                            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(refPath);
                            if (!asset) { Debug.LogWarning($"GF_AutoWire: Op[{i}] asset not found at path: '{refPath}'"); fail++; break; }

                            // If refComponentType specified and asset is GameObject, get component from it
                            if (!string.IsNullOrWhiteSpace(o.refComponentType) && asset is GameObject go)
                            {
                                // If refChildPath is specified, find that child first
                                GameObject targetGo = go;
                                string childPath = (o.refChildPath ?? "").Trim();
                                if (!string.IsNullOrEmpty(childPath))
                                {
                                    var childTf = go.transform.Find(childPath);
                                    if (!childTf) { Debug.LogWarning($"GF_AutoWire: Op[{i}] child path '{childPath}' not found in asset '{refPath}'"); fail++; break; }
                                    targetGo = childTf.gameObject;
                                }

                                var comp = GetComponentByTypeName(targetGo, o.refComponentType);
                                if (!comp) { Debug.LogWarning($"GF_AutoWire: Op[{i}] component '{o.refComponentType}' not found on '{(string.IsNullOrEmpty(childPath) ? refPath : refPath + "/" + childPath)}'"); fail++; break; }
                                value = comp;
                            }
                            else
                            {
                                value = asset;
                            }
                        }
                        else
                        {
                            // Resolve as hierarchy path (use effectiveRoot for prefab buffer support)
                            var refTf = ResolveTransformPathSmart(effectiveRoot, refPath, out _);
                            if (!refTf) { Debug.LogWarning($"GF_AutoWire: Op[{i}] refPath not found: '{refPath}'"); fail++; break; }

                            if (!string.IsNullOrWhiteSpace(o.refComponentType))
                            {
                                var rc = GetComponentByTypeName(refTf.gameObject, o.refComponentType);
                                if (!rc) { Debug.LogWarning($"GF_AutoWire: Op[{i}] ref component '{o.refComponentType}' not found on '{refTf.name}'"); fail++; break; }
                                value = rc;
                            }
                            else value = refTf.gameObject;
                        }

                        bool assigned = TryAssign(targetComp, o.fieldName, value, true, out string why);
                        if (assigned) ok++; else { Debug.LogWarning($"GF_AutoWire: Op[{i}] set_object_ref failed: {why}"); fail++; }
                        break;
                    }

                    case "clear_object_ref":
                    {
                        var targetTf = ResolveTransformPathSmart(effectiveRoot, o.path, out _);
                        if (!targetTf) { Debug.LogWarning($"GF_AutoWire: Op[{i}] clear_object_ref target path not found: '{o.path}'"); fail++; break; }

                        var targetComp = GetComponentByTypeName(targetTf.gameObject, o.componentType);
                        if (!targetComp) { Debug.LogWarning($"GF_AutoWire: Op[{i}] clear_object_ref component not found: '{o.componentType}' on '{targetTf.name}'"); fail++; break; }

                        bool assigned = TryAssign(targetComp, o.fieldName, null, true, out string why);
                        if (assigned) ok++; else { Debug.LogWarning($"GF_AutoWire: Op[{i}] clear_object_ref failed: {why}"); fail++; }
                        break;
                    }

                    case "set_int":
                    {
                        var targetTf = ResolveTransformPathSmart(effectiveRoot, o.path, out _);
                        if (!targetTf) { Debug.LogWarning($"GF_AutoWire: Op[{i}] target path not found: '{o.path}'"); fail++; break; }

                        var targetComp = GetComponentByTypeName(targetTf.gameObject, o.componentType);
                        if (!targetComp) { Debug.LogWarning($"GF_AutoWire: Op[{i}] component not found: '{o.componentType}' on '{targetTf.name}'"); fail++; break; }

                        bool assigned = TryAssign(targetComp, o.fieldName, o.intValue, true, out string why);
                        if (assigned) ok++; else { Debug.LogWarning($"GF_AutoWire: Op[{i}] set_int failed: {why}"); fail++; }
                        break;
                    }

                    case "set_float":
                    {
                        var targetTf = ResolveTransformPathSmart(effectiveRoot, o.path, out _);
                        if (!targetTf) { Debug.LogWarning($"GF_AutoWire: Op[{i}] target path not found: '{o.path}'"); fail++; break; }

                        var targetComp = GetComponentByTypeName(targetTf.gameObject, o.componentType);
                        if (!targetComp) { Debug.LogWarning($"GF_AutoWire: Op[{i}] component not found: '{o.componentType}' on '{targetTf.name}'"); fail++; break; }

                        // TMP enhancement: handle fontSize directly
                        if (targetComp is TextMeshProUGUI tmp && o.fieldName == "fontSize")
                        {
                            tmp.fontSize = o.floatValue;
                            EditorUtility.SetDirty(tmp);
                            ok++;
                            break;
                        }

                        bool assigned = TryAssign(targetComp, o.fieldName, o.floatValue, true, out string why);
                        if (assigned) ok++; else { Debug.LogWarning($"GF_AutoWire: Op[{i}] set_float failed: {why}"); fail++; }
                        break;
                    }

                    case "set_bool":
                    {
                        var targetTf = ResolveTransformPathSmart(effectiveRoot, o.path, out _);
                        if (!targetTf) { Debug.LogWarning($"GF_AutoWire: Op[{i}] target path not found: '{o.path}'"); fail++; break; }

                        var targetComp = GetComponentByTypeName(targetTf.gameObject, o.componentType);
                        if (!targetComp) { Debug.LogWarning($"GF_AutoWire: Op[{i}] component not found: '{o.componentType}' on '{targetTf.name}'"); fail++; break; }

                        bool assigned = TryAssign(targetComp, o.fieldName, o.boolValue, true, out string why);
                        if (assigned) ok++; else { Debug.LogWarning($"GF_AutoWire: Op[{i}] set_bool failed: {why}"); fail++; }
                        break;
                    }

                    case "set_string":
                    {
                        var targetTf = ResolveTransformPathSmart(effectiveRoot, o.path, out _);
                        if (!targetTf) { Debug.LogWarning($"GF_AutoWire: Op[{i}] target path not found: '{o.path}'"); fail++; break; }

                        var targetComp = GetComponentByTypeName(targetTf.gameObject, o.componentType);
                        if (!targetComp) { Debug.LogWarning($"GF_AutoWire: Op[{i}] component not found: '{o.componentType}' on '{targetTf.name}'"); fail++; break; }

                        // TMP enhancement: handle text and color directly
                        if (targetComp is TextMeshProUGUI tmp)
                        {
                            string val = o.stringValue ?? "";
                            if (o.fieldName == "text")
                            {
                                tmp.text = val;
                                EditorUtility.SetDirty(tmp);
                                ok++;
                                break;
                            }
                            if (o.fieldName == "color" || val.StartsWith("#"))
                            {
                                string colorStr = o.fieldName == "color" ? val : val;
                                if (ColorUtility.TryParseHtmlString(colorStr, out Color c))
                                {
                                    tmp.color = c;
                                    EditorUtility.SetDirty(tmp);
                                    ok++;
                                    break;
                                }
                            }
                        }

                        bool assigned = TryAssign(targetComp, o.fieldName, o.stringValue ?? "", true, out string why);
                        if (assigned) ok++; else { Debug.LogWarning($"GF_AutoWire: Op[{i}] set_string failed: {why}"); fail++; }
                        break;
                    }

                    case "ensure_gameobject":
                    {
                        if (string.IsNullOrWhiteSpace(o.path))
                        {
                            Debug.LogWarning($"GF_AutoWire: Op[{i}] ensure_gameobject missing path");
                            fail++;
                            break;
                        }

                        EnsureGameObjectPath(effectiveRoot, o.path, effectiveUndo);
                        ok++;
                        break;
                    }

                    case "set_rect":
                    {
                        var targetTf = ResolveTransformPathSmart(effectiveRoot, o.path, out _);
                        if (!targetTf) { Debug.LogWarning($"GF_AutoWire: Op[{i}] set_rect path not found: '{o.path}'"); fail++; break; }

                        var rt = targetTf.GetComponent<RectTransform>();
                        if (!rt) { Debug.LogWarning($"GF_AutoWire: Op[{i}] set_rect: no RectTransform on '{o.path}'"); fail++; break; }

                        // Apply values only if not NaN (sentinel for "not set")
                        if (!float.IsNaN(o.anchorMinX) || !float.IsNaN(o.anchorMinY))
                            rt.anchorMin = new Vector2(float.IsNaN(o.anchorMinX) ? rt.anchorMin.x : o.anchorMinX,
                                                        float.IsNaN(o.anchorMinY) ? rt.anchorMin.y : o.anchorMinY);
                        if (!float.IsNaN(o.anchorMaxX) || !float.IsNaN(o.anchorMaxY))
                            rt.anchorMax = new Vector2(float.IsNaN(o.anchorMaxX) ? rt.anchorMax.x : o.anchorMaxX,
                                                        float.IsNaN(o.anchorMaxY) ? rt.anchorMax.y : o.anchorMaxY);
                        if (!float.IsNaN(o.pivotX) || !float.IsNaN(o.pivotY))
                            rt.pivot = new Vector2(float.IsNaN(o.pivotX) ? rt.pivot.x : o.pivotX,
                                                    float.IsNaN(o.pivotY) ? rt.pivot.y : o.pivotY);
                        if (!float.IsNaN(o.sizeDeltaX) || !float.IsNaN(o.sizeDeltaY))
                            rt.sizeDelta = new Vector2(float.IsNaN(o.sizeDeltaX) ? rt.sizeDelta.x : o.sizeDeltaX,
                                                        float.IsNaN(o.sizeDeltaY) ? rt.sizeDelta.y : o.sizeDeltaY);
                        if (!float.IsNaN(o.anchoredPosX) || !float.IsNaN(o.anchoredPosY))
                            rt.anchoredPosition = new Vector2(float.IsNaN(o.anchoredPosX) ? rt.anchoredPosition.x : o.anchoredPosX,
                                                               float.IsNaN(o.anchoredPosY) ? rt.anchoredPosition.y : o.anchoredPosY);

                        EditorUtility.SetDirty(rt);
                        ok++;
                        break;
                    }

                    case "add_vlg":
                    {
                        var targetTf = ResolveTransformPathSmart(effectiveRoot, o.path, out _);
                        if (!targetTf) { Debug.LogWarning($"GF_AutoWire: Op[{i}] add_vlg path not found: '{o.path}'"); fail++; break; }

                        var vlg = targetTf.GetComponent<VerticalLayoutGroup>();
                        if (!vlg)
                        {
                            if (effectiveUndo)
                                vlg = Undo.AddComponent<VerticalLayoutGroup>(targetTf.gameObject);
                            else
                                vlg = targetTf.gameObject.AddComponent<VerticalLayoutGroup>();
                        }

                        // Apply values only if not sentinel (-1 for ints, NaN for floats)
                        if (o.childAlignment >= 0)
                            vlg.childAlignment = (TextAnchor)o.childAlignment;
                        if (!float.IsNaN(o.spacing))
                            vlg.spacing = o.spacing;
                        if (o.controlChildWidth >= 0)
                            vlg.childControlWidth = o.controlChildWidth == 1;
                        if (o.controlChildHeight >= 0)
                            vlg.childControlHeight = o.controlChildHeight == 1;
                        if (o.forceExpandWidth >= 0)
                            vlg.childForceExpandWidth = o.forceExpandWidth == 1;
                        if (o.forceExpandHeight >= 0)
                            vlg.childForceExpandHeight = o.forceExpandHeight == 1;

                        EditorUtility.SetDirty(vlg);
                        ok++;
                        break;
                    }

                    case "ensure_folder":
                    {
                        if (string.IsNullOrWhiteSpace(o.folderPath))
                        {
                            Debug.LogWarning($"GF_AutoWire: Op[{i}] ensure_folder missing folderPath");
                            fail++;
                            break;
                        }

                        EnsureAssetFolder(o.folderPath);
                        ok++;
                        break;
                    }

                    case "create_prefab_asset":
                    {
                        if (string.IsNullOrWhiteSpace(o.prefabPath))
                        {
                            Debug.LogWarning($"GF_AutoWire: Op[{i}] create_prefab_asset missing prefabPath");
                            fail++;
                            break;
                        }

                        if (_isPrefabMode || _currentBuffer != null)
                        {
                            Debug.LogWarning($"GF_AutoWire: Op[{i}] create_prefab_asset called while already in prefab mode. Call save_prefab_asset first.");
                            fail++;
                            break;
                        }

                        // Ensure parent folder exists
                        string folderPath = System.IO.Path.GetDirectoryName(o.prefabPath).Replace("\\", "/");
                        if (!string.IsNullOrEmpty(folderPath))
                            EnsureAssetFolder(folderPath);

                        // Derive name from path
                        string prefabName = System.IO.Path.GetFileNameWithoutExtension(o.prefabPath);
                        if (string.IsNullOrWhiteSpace(prefabName)) prefabName = "NewPrefab";

                        // Create buffer GameObject (hidden from scene)
                        _currentBuffer = new GameObject(prefabName);
                        _currentBuffer.hideFlags = HideFlags.HideAndDontSave;
                        _pendingPrefabPath = o.prefabPath;
                        _isPrefabMode = true;

                        Debug.Log($"GF_AutoWire: Entered prefab mode for '{o.prefabPath}'");
                        ok++;
                        break;
                    }

                    case "save_prefab_asset":
                    {
                        if (!_isPrefabMode || _currentBuffer == null)
                        {
                            Debug.LogWarning($"GF_AutoWire: Op[{i}] save_prefab_asset called but not in prefab mode. Call create_prefab_asset first.");
                            fail++;
                            break;
                        }

                        // Allow override path if specified
                        string savePath = !string.IsNullOrWhiteSpace(o.prefabPath) ? o.prefabPath : _pendingPrefabPath;
                        if (string.IsNullOrWhiteSpace(savePath))
                        {
                            Debug.LogWarning($"GF_AutoWire: Op[{i}] save_prefab_asset no path available.");
                            fail++;
                            break;
                        }

                        // Ensure parent folder exists
                        string folderPath = System.IO.Path.GetDirectoryName(savePath).Replace("\\", "/");
                        if (!string.IsNullOrEmpty(folderPath))
                            EnsureAssetFolder(folderPath);

                        // Clear hide flags before saving
                        _currentBuffer.hideFlags = HideFlags.None;

                        // Save the prefab
                        PrefabUtility.SaveAsPrefabAsset(_currentBuffer, savePath);
                        Debug.Log($"GF_AutoWire: Saved prefab to '{savePath}'");

                        // Cleanup
                        UnityEngine.Object.DestroyImmediate(_currentBuffer);
                        _currentBuffer = null;
                        _pendingPrefabPath = null;
                        _isPrefabMode = false;

                        ok++;
                        break;
                    }

                    case "add_button_listener":
                    {
                        // Get the button
                        var buttonTf = ResolveTransformPathSmart(effectiveRoot, o.path, out _);
                        if (!buttonTf) { Debug.LogWarning($"GF_AutoWire: Op[{i}] add_button_listener button path not found: '{o.path}'"); fail++; break; }

                        var button = buttonTf.GetComponent<Button>();
                        if (!button) { Debug.LogWarning($"GF_AutoWire: Op[{i}] add_button_listener: no Button component on '{o.path}'"); fail++; break; }

                        // Get the listener target (use original root for scene references)
                        var listenerTf = ResolveTransformPathSmart(root, o.listenerPath, out _);
                        if (!listenerTf) { Debug.LogWarning($"GF_AutoWire: Op[{i}] add_button_listener listenerPath not found: '{o.listenerPath}'"); fail++; break; }

                        if (string.IsNullOrWhiteSpace(o.listenerComponent)) { Debug.LogWarning($"GF_AutoWire: Op[{i}] add_button_listener missing listenerComponent"); fail++; break; }
                        if (string.IsNullOrWhiteSpace(o.listenerMethod)) { Debug.LogWarning($"GF_AutoWire: Op[{i}] add_button_listener missing listenerMethod"); fail++; break; }

                        var targetComp = GetComponentByTypeName(listenerTf.gameObject, o.listenerComponent);
                        if (!targetComp) { Debug.LogWarning($"GF_AutoWire: Op[{i}] add_button_listener component '{o.listenerComponent}' not found on '{listenerTf.name}'"); fail++; break; }

                        // Find the method
                        var methodInfo = targetComp.GetType().GetMethod(o.listenerMethod, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (methodInfo == null) { Debug.LogWarning($"GF_AutoWire: Op[{i}] add_button_listener method '{o.listenerMethod}' not found on '{o.listenerComponent}'"); fail++; break; }

                        // Check method signature - must be void with no parameters for Button.onClick
                        var parameters = methodInfo.GetParameters();
                        if (parameters.Length > 0)
                        {
                            Debug.LogWarning($"GF_AutoWire: Op[{i}] add_button_listener method '{o.listenerMethod}' must have no parameters for Button.onClick");
                            fail++;
                            break;
                        }

                        // Create the UnityAction delegate and add as persistent listener
                        var action = (UnityAction)Delegate.CreateDelegate(typeof(UnityAction), targetComp, methodInfo);
                        UnityEventTools.AddPersistentListener(button.onClick, action);

                        EditorUtility.SetDirty(button);
                        Debug.Log($"GF_AutoWire: Added onClick listener: {o.listenerComponent}.{o.listenerMethod} to button '{buttonTf.name}'");
                        ok++;
                        break;
                    }

                    default:
                        Debug.LogWarning($"GF_AutoWire: Op[{i}] unsupported op '{opName}'");
                        skip++;
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"GF_AutoWire: Op[{i}] exception: {e.Message}");
                fail++;
            }
        }
    }

    /// <summary>
    /// Ensures all GameObjects along a root-relative path exist.
    /// Creates missing objects with RectTransform if under a Canvas hierarchy.
    /// </summary>
    private static void EnsureGameObjectPath(Transform root, string path, bool useUndo)
    {
        if (!root || string.IsNullOrWhiteSpace(path)) return;

        string[] parts = path.Trim().TrimStart('/').Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;

        Transform current = root;
        bool isUIHierarchy = root.GetComponentInParent<Canvas>() != null;

        foreach (var part in parts)
        {
            Transform child = current.Find(part);
            if (child)
            {
                current = child;
                continue;
            }

            // Create the missing object
            GameObject go;
            if (useUndo)
            {
                go = new GameObject(part);
                Undo.RegisterCreatedObjectUndo(go, $"GF_AutoWire ensure_gameobject '{part}'");
                go.transform.SetParent(current, false);

                // Add RectTransform if in UI hierarchy
                if (isUIHierarchy || current.GetComponent<RectTransform>() != null)
                {
                    var rt = go.AddComponent<RectTransform>();
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                }
            }
            else
            {
                if (isUIHierarchy || current.GetComponent<RectTransform>() != null)
                {
                    go = new GameObject(part, typeof(RectTransform));
                    go.transform.SetParent(current, false);
                    var rt = go.GetComponent<RectTransform>();
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                }
                else
                {
                    go = new GameObject(part);
                    go.transform.SetParent(current, false);
                }
            }

            current = go.transform;
        }
    }

    private static void EnsureTMPButton(Transform parent, Op o, bool useUndo)
    {
        if (!parent) return;
        if (string.IsNullOrWhiteSpace(o.name)) return;

        Transform existing = parent.Find(o.name);
        GameObject go;
        if (existing) go = existing.gameObject;
        else
        {
            if (useUndo)
            {
                go = new GameObject(o.name);
                Undo.RegisterCreatedObjectUndo(go, "GF_AutoWire ensure_tmp_button");
                go.AddComponent<RectTransform>();
                go.AddComponent<CanvasRenderer>();
                go.AddComponent<Image>();
                go.AddComponent<Button>();
                go.transform.SetParent(parent, false);
            }
            else
            {
                go = new GameObject(o.name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                go.transform.SetParent(parent, false);
            }

            var img = go.GetComponent<Image>();
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
        }

        // Rect
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(o.anchorMinX, o.anchorMinY);
        rt.anchorMax = new Vector2(o.anchorMaxX, o.anchorMaxY);
        rt.pivot = new Vector2(o.pivotX, o.pivotY);
        rt.anchoredPosition = new Vector2(o.posX, o.posY);
        rt.sizeDelta = new Vector2(o.sizeX, o.sizeY);

        // TMP child
        Transform tChild = go.transform.Find("Text (TMP)");
        if (!tChild)
        {
            GameObject tgo;
            if (useUndo)
            {
                tgo = new GameObject("Text (TMP)");
                Undo.RegisterCreatedObjectUndo(tgo, "GF_AutoWire ensure_tmp_button TMP");
                tgo.AddComponent<RectTransform>();
                tgo.AddComponent<TextMeshProUGUI>();
                tgo.transform.SetParent(go.transform, false);
            }
            else
            {
                tgo = new GameObject("Text (TMP)", typeof(RectTransform), typeof(TextMeshProUGUI));
                tgo.transform.SetParent(go.transform, false);
            }
            tChild = tgo.transform;
        }

        var tmp = tChild.GetComponent<TextMeshProUGUI>();
        tmp.text = o.label ?? "";
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;

        var trt = (RectTransform)tChild;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
    }

    private static Component EnsureComponentByTypeName(GameObject go, string typeName, bool useUndo)
    {
        if (!go) return null;

        var t = ResolveType(typeName);
        if (t == null || !typeof(Component).IsAssignableFrom(t))
        {
            Debug.LogWarning($"GF_AutoWire: ensure_component type '{typeName}' not found or is not a Component.");
            return null;
        }

        var existing = go.GetComponent(t);
        if (existing) return existing;

        if (useUndo)
            return Undo.AddComponent(go, t);

        return go.AddComponent(t);
    }

    private static int RemoveComponentByTypeName(GameObject go, string typeName, bool removeAll, bool useUndo)
    {
        if (!go) return 0;

        var t = ResolveType(typeName);
        if (t == null || !typeof(Component).IsAssignableFrom(t))
        {
            Debug.LogWarning($"GF_AutoWire: remove_component type '{typeName}' not found or is not a Component.");
            return 0;
        }

        var comps = go.GetComponents(t);
        if (comps == null || comps.Length == 0)
            return 0;

        int removed = 0;
        for (int i = 0; i < comps.Length; i++)
        {
            var c = comps[i] as Component;
            if (!c) continue;

            if (useUndo) Undo.DestroyObjectImmediate(c);
            else UnityEngine.Object.DestroyImmediate(c);

            removed++;
            if (!removeAll) break;
        }

        return removed;
    }

    private static void DestroyGameObject(GameObject go, bool useUndo)
    {
        if (!go) return;
        if (useUndo) Undo.DestroyObjectImmediate(go);
        else UnityEngine.Object.DestroyImmediate(go);
    }

    /// <summary>
    /// Ensures the given Assets folder path exists, creating subfolders as needed.
    /// </summary>
    private static void EnsureAssetFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath)) return;

        folderPath = folderPath.Trim().Replace("\\", "/").TrimEnd('/');
        if (!folderPath.StartsWith("Assets"))
        {
            Debug.LogWarning($"GF_AutoWire: ensure_folder path must start with 'Assets': '{folderPath}'");
            return;
        }

        if (AssetDatabase.IsValidFolder(folderPath)) return;

        string[] parts = folderPath.Split('/');
        string current = parts[0]; // "Assets"

        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }
            current = next;
        }
    }

    /// <summary>
    /// Returns the effective root for path resolution.
    /// If in prefab mode, returns the prefab buffer's transform; otherwise returns the provided root.
    /// </summary>
    private static Transform GetEffectiveRoot(Transform root)
    {
        if (_isPrefabMode && _currentBuffer != null)
            return _currentBuffer.transform;
        return root;
    }

    // -------------------- shared helpers --------------------
    private static GameObject GetRootGameObject(UnityEngine.Object any, out bool isPrefabAsset, out string prefabPath)
    {
        isPrefabAsset = false;
        prefabPath = "";

        if (any is Component c) any = c.gameObject;

        if (any is GameObject go)
        {
            if (AssetDatabase.Contains(go))
            {
                var path = AssetDatabase.GetAssetPath(go);
                if (!string.IsNullOrEmpty(path))
                {
                    isPrefabAsset = true;
                    prefabPath = path;
                    return go;
                }
            }
            return go;
        }

        if (AssetDatabase.Contains(any))
        {
            var path = AssetDatabase.GetAssetPath(any);
            var maybeGo = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (maybeGo)
            {
                isPrefabAsset = true;
                prefabPath = path;
                return maybeGo;
            }
        }

        return null;
    }

    private static Transform ResolveTransformPathSmart(Transform root, string relPath, out string usedPath)
    {
        usedPath = relPath ?? "";
        if (!root) return null;
        if (string.IsNullOrEmpty(relPath)) { usedPath = ""; return root; }

        string p = (relPath ?? "").Trim().TrimStart('/');

        // try exact
        var tf = root.Find(p);
        if (tf) { usedPath = p; return tf; }

        // strip root name prefix if present
        if (p.StartsWith(root.name + "/", StringComparison.OrdinalIgnoreCase))
        {
            var p2 = p.Substring(root.name.Length + 1);
            tf = root.Find(p2);
            if (tf) { usedPath = p2; return tf; }
        }

        // progressively strip leading segments (handles "Prefab_HexWorld3D_Core/..." mistakes)
        var parts = p.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 1; i < parts.Length; i++)
        {
            var sub = string.Join("/", parts.Skip(i));
            tf = root.Find(sub);
            if (tf) { usedPath = sub; return tf; }
        }

        return null;
    }

    private static Type ResolveType(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName)) return null;
        typeName = typeName.Trim();

        if (_typeCache.TryGetValue(typeName, out var cached))
            return cached;

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                var t = asm.GetType(typeName, false);
                if (t != null)
                {
                    _typeCache[typeName] = t;
                    return t;
                }
            }
            catch { }
        }

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types = null;
            try { types = asm.GetTypes(); } catch { continue; }
            foreach (var t in types)
            {
                if (t == null) continue;
                if (t.Name == typeName)
                {
                    _typeCache[typeName] = t;
                    return t;
                }
            }
        }

        _typeCache[typeName] = null;
        return null;
    }

    private static Component GetComponentByTypeName(GameObject go, string typeName)
    {
        if (!go) return null;

        var t = ResolveType(typeName);
        if (t == null || !typeof(Component).IsAssignableFrom(t))
            return null;

        return go.GetComponent(t);
    }

    private static object EvaluateValue(Transform root, Binding b, out string failReason)
    {
        failReason = null;
        string kind = (b.valueKind ?? "").Trim();

        switch (kind)
        {
            case "ConstantInt": return b.intValue;
            case "ConstantFloat": return b.floatValue;
            case "ConstantBool": return b.boolValue;
            case "ConstantString": return b.stringValue ?? "";
            case "ConstantNull": return null; // clears ObjectReference fields

            case "FromObjectPathGameObject":
            {
                var tf = ResolveTransformPathSmart(root, b.sourcePath, out _);
                if (!tf) { failReason = $"sourcePath not found: '{b.sourcePath}'"; return null; }
                return tf.gameObject;
            }

            case "FromObjectPathComponent":
            {
                var tf = ResolveTransformPathSmart(root, b.sourcePath, out _);
                if (!tf) { failReason = $"sourcePath not found: '{b.sourcePath}'"; return null; }
                if (string.IsNullOrWhiteSpace(b.sourceComponent))
                {
                    failReason = "sourceComponent is required for FromObjectPathComponent";
                    return null;
                }
                var comp = GetComponentByTypeName(tf.gameObject, b.sourceComponent);
                if (!comp) { failReason = $"component '{b.sourceComponent}' not found at '{b.sourcePath}'"; return null; }
                return comp;
            }

            case "FindInChildrenByName":
            {
                if (string.IsNullOrWhiteSpace(b.findNameContains))
                {
                    failReason = "findNameContains is required for FindInChildrenByName";
                    return null;
                }

                string needle = b.findNameContains.Trim();
                string filterComp = (b.findComponent ?? "").Trim();

                var all = root.GetComponentsInChildren<Transform>(true);
                foreach (var tf in all)
                {
                    if (!tf) continue;
                    if (tf.name.IndexOf(needle, StringComparison.OrdinalIgnoreCase) < 0) continue;

                    if (!string.IsNullOrEmpty(filterComp))
                    {
                        var c = GetComponentByTypeName(tf.gameObject, filterComp);
                        if (!c) continue;
                        return c;
                    }

                    return tf.gameObject;
                }

                failReason = $"No child found containing '{needle}' (filterComp='{filterComp}')";
                return null;
            }

            case "AssetByPath":
            {
                if (string.IsNullOrWhiteSpace(b.assetPath))
                {
                    failReason = "assetPath is required for AssetByPath";
                    return null;
                }
                var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(b.assetPath.Trim());
                if (!obj) failReason = $"Asset not found at path: '{b.assetPath}'";
                return obj;
            }

            case "AssetByGuid":
            {
                if (string.IsNullOrWhiteSpace(b.assetGuid))
                {
                    failReason = "assetGuid is required for AssetByGuid";
                    return null;
                }
                string path = AssetDatabase.GUIDToAssetPath(b.assetGuid.Trim());
                if (string.IsNullOrEmpty(path))
                {
                    failReason = $"GUID not found: '{b.assetGuid}'";
                    return null;
                }
                var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                if (!obj) failReason = $"Asset not found for GUID '{b.assetGuid}' at '{path}'";
                return obj;
            }

            default:
                failReason = $"Unsupported valueKind '{kind}'. Check the comment snippet at top of GF_AutoWire.cs";
                return null;
        }
    }

    private static bool TryAssign(Component targetComp, string fieldOrPropPath, object value, bool overwrite, out string whyNotAssigned)
    {
        whyNotAssigned = null;
        if (!targetComp) { whyNotAssigned = "targetComp is null"; return false; }
        if (string.IsNullOrWhiteSpace(fieldOrPropPath)) { whyNotAssigned = "targetField is empty"; return false; }

        var so = new SerializedObject(targetComp);
        var sp = so.FindProperty(fieldOrPropPath);

        if (sp != null)
        {
            if (!overwrite && !IsSerializedDefault(sp))
            {
                whyNotAssigned = "SKIP";
                return false;
            }

            if (!TrySetSerializedProperty(sp, value, out whyNotAssigned))
                return false;

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(targetComp);
            return true;
        }

        var t = targetComp.GetType();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        var fi = t.GetField(fieldOrPropPath, flags);
        if (fi != null)
        {
            object current = fi.GetValue(targetComp);
            if (!overwrite && !IsDefaultForType(current, fi.FieldType))
            {
                whyNotAssigned = "SKIP";
                return false;
            }

            if (!TryConvertValue(value, fi.FieldType, out var converted))
            {
                whyNotAssigned = $"Cannot convert value type '{value?.GetType().Name}' to '{fi.FieldType.Name}'";
                return false;
            }

            fi.SetValue(targetComp, converted);
            EditorUtility.SetDirty(targetComp);
            return true;
        }

        var pi = t.GetProperty(fieldOrPropPath, flags);
        if (pi != null && pi.CanWrite)
        {
            object current = pi.GetValue(targetComp);
            if (!overwrite && !IsDefaultForType(current, pi.PropertyType))
            {
                whyNotAssigned = "SKIP";
                return false;
            }

            if (!TryConvertValue(value, pi.PropertyType, out var converted))
            {
                whyNotAssigned = $"Cannot convert value type '{value?.GetType().Name}' to '{pi.PropertyType.Name}'";
                return false;
            }

            pi.SetValue(targetComp, converted);
            EditorUtility.SetDirty(targetComp);
            return true;
        }

        whyNotAssigned = $"Field/property/serialized prop '{fieldOrPropPath}' not found on component '{t.FullName}'";
        return false;
    }

    private static bool TrySetSerializedProperty(SerializedProperty sp, object value, out string fail)
    {
        fail = null;

        if (sp.propertyType == SerializedPropertyType.ObjectReference)
        {
            if (value == null) { sp.objectReferenceValue = null; return true; }

            if (value is UnityEngine.Object uo)
            {
                sp.objectReferenceValue = uo;
                return true;
            }

            fail = $"SerializedProperty expects ObjectReference but got '{value.GetType().Name}'";
            return false;
        }

        switch (sp.propertyType)
        {
            case SerializedPropertyType.Integer:
                if (value is int ii) { sp.intValue = ii; return true; }
                if (value is float ff) { sp.intValue = Mathf.RoundToInt(ff); return true; }
                fail = "Expected int/float for Integer property."; return false;

            case SerializedPropertyType.Float:
                if (value is float f) { sp.floatValue = f; return true; }
                if (value is int i) { sp.floatValue = i; return true; }
                fail = "Expected float/int for Float property."; return false;

            case SerializedPropertyType.Boolean:
                if (value is bool b) { sp.boolValue = b; return true; }
                fail = "Expected bool for Boolean property."; return false;

            case SerializedPropertyType.String:
                sp.stringValue = value?.ToString() ?? "";
                return true;

            default:
                fail = $"Unsupported SerializedPropertyType '{sp.propertyType}'.";
                return false;
        }
    }

    private static bool IsSerializedDefault(SerializedProperty sp)
    {
        switch (sp.propertyType)
        {
            case SerializedPropertyType.ObjectReference: return sp.objectReferenceValue == null;
            case SerializedPropertyType.Integer: return sp.intValue == 0;
            case SerializedPropertyType.Float: return Mathf.Approximately(sp.floatValue, 0f);
            case SerializedPropertyType.Boolean: return sp.boolValue == false;
            case SerializedPropertyType.String: return string.IsNullOrEmpty(sp.stringValue);
            default: return true;
        }
    }

    private static bool IsDefaultForType(object current, Type t)
    {
        if (t == null) return true;

        if (typeof(UnityEngine.Object).IsAssignableFrom(t))
            return current == null;

        if (t == typeof(string))
            return string.IsNullOrEmpty(current as string);

        if (t.IsValueType)
        {
            object def = Activator.CreateInstance(t);
            return Equals(current, def);
        }

        return current == null;
    }

    private static bool TryConvertValue(object value, Type expected, out object converted)
    {
        converted = null;
        if (expected == null) return false;

        if (value == null)
        {
            if (!expected.IsValueType || Nullable.GetUnderlyingType(expected) != null)
            {
                converted = null;
                return true;
            }
            return false;
        }

        if (expected.IsInstanceOfType(value))
        {
            converted = value;
            return true;
        }

        if (typeof(UnityEngine.Object).IsAssignableFrom(expected))
        {
            if (value is GameObject go)
            {
                if (expected == typeof(GameObject)) { converted = go; return true; }
                if (expected == typeof(Transform)) { converted = go.transform; return true; }
                if (typeof(Component).IsAssignableFrom(expected))
                {
                    var comp = go.GetComponent(expected);
                    if (comp != null) { converted = comp; return true; }
                    return false;
                }
            }
            if (value is Component comp2)
            {
                if (expected == typeof(GameObject)) { converted = comp2.gameObject; return true; }
                if (expected == typeof(Transform)) { converted = comp2.transform; return true; }
                if (typeof(Component).IsAssignableFrom(expected) && expected.IsInstanceOfType(comp2))
                {
                    converted = comp2; return true;
                }
            }
            if (value is UnityEngine.Object uo && expected.IsInstanceOfType(uo))
            {
                converted = uo;
                return true;
            }
        }

        try
        {
            if (expected == typeof(int))
            {
                if (value is float f) { converted = Mathf.RoundToInt(f); return true; }
                if (value is string s && int.TryParse(s, out var ii)) { converted = ii; return true; }
            }
            if (expected == typeof(float))
            {
                if (value is int i) { converted = (float)i; return true; }
                if (value is string s && float.TryParse(s, out var ff)) { converted = ff; return true; }
            }
            if (expected == typeof(bool))
            {
                if (value is string s && bool.TryParse(s, out var bb)) { converted = bb; return true; }
            }
            if (expected == typeof(string))
            {
                converted = value.ToString();
                return true;
            }
        }
        catch { }

        return false;
    }
}
