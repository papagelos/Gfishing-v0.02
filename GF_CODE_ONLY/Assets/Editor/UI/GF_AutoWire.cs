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
//       "path": "UI_Root/Canvas/TileBar/BuildingBar/Btn_PageLeft_buildings" }
//   ]
// }
//
// Notes:
// - Paths are relative to the chosen root. If your JSON accidentally includes an extra top segment like
//   "Prefab_HexWorld3D_Core/...", this tool will try stripping leading segments until it finds a match.
// - JsonUtility cannot parse comments or trailing commas. Keep JSON strict.
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
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
        public string refComponentType;

        // remove_component
        public bool removeAll;

        // set_* values
        public int intValue;
        public float floatValue;
        public bool boolValue;
        public string stringValue;
    }

    // UI
    private UnityEngine.Object _target; // GameObject in scene OR prefab asset OR component
    private TextAsset _recipeJsonAsset;
    private string _recipeJsonRaw = "";
    private bool _useRawJson = false;
    private Vector2 _scroll;

    // Caches
    private static Dictionary<string, Type> _typeCache = new();

    [MenuItem("Tools/GF Auto Wire")]
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
        if (_target == null) return false;
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

            GameObject rootGo = GetRootGameObject(_target, out bool isPrefabAsset, out string prefabAssetPath);
            if (!rootGo)
            {
                Debug.LogError("GF_AutoWire: Could not resolve a root GameObject from the chosen target.");
                return;
            }

            if (isPrefabAsset)
            {
                var loadedRoot = PrefabUtility.LoadPrefabContents(prefabAssetPath);
                if (!loadedRoot)
                {
                    Debug.LogError($"GF_AutoWire: Failed to LoadPrefabContents: {prefabAssetPath}");
                    return;
                }

                int ok = 0, fail = 0, skip = 0;
                ApplyJsonToRoot(json, loadedRoot.transform, ref ok, ref fail, ref skip);

                PrefabUtility.SaveAsPrefabAsset(loadedRoot, prefabAssetPath);
                PrefabUtility.UnloadPrefabContents(loadedRoot);

                Debug.Log($"GF_AutoWire: Applied JSON to Prefab asset. OK={ok}, SKIP={skip}, FAIL={fail}");
            }
            else
            {
                Undo.RegisterFullObjectHierarchyUndo(rootGo, "GF AutoWire Apply Recipe");

                int ok = 0, fail = 0, skip = 0;
                ApplyJsonToRoot(json, rootGo.transform, ref ok, ref fail, ref skip);

                Debug.Log($"GF_AutoWire: Applied JSON to Scene object. OK={ok}, SKIP={skip}, FAIL={fail}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"GF_AutoWire: Exception while applying recipe: {e}");
        }
    }

    private static void ApplyJsonToRoot(string json, Transform root, ref int ok, ref int fail, ref int skip)
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
            ApplyOpsToRoot(opsRecipe, root, ref ok, ref fail, ref skip);
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
    private static void ApplyOpsToRoot(OpsRecipe recipe, Transform root, ref int ok, ref int fail, ref int skip)
    {
        bool useUndo = root != null && root.gameObject.scene.IsValid() && root.gameObject.scene.isLoaded;

        for (int i = 0; i < recipe.ops.Length; i++)
        {
            var o = recipe.ops[i];
            if (o == null) { fail++; continue; }

            string opName = (o.op ?? "").Trim();
            if (string.IsNullOrEmpty(opName)) { fail++; continue; }

            try
            {
                switch (opName)
                {
                    case "ensure_tmp_button":
                    {
                        var parentTf = ResolveTransformPathSmart(root, o.parentPath, out _);
                        if (!parentTf)
                        {
                            Debug.LogWarning($"GF_AutoWire: Op[{i}] ensure_tmp_button parentPath not found: '{o.parentPath}'");
                            fail++;
                            break;
                        }

                        EnsureTMPButton(parentTf, o, useUndo);
                        ok++;
                        break;
                    }

                    case "ensure_component":
                    {
                        var targetTf = ResolveTransformPathSmart(root, o.path, out _);
                        if (!targetTf) { Debug.LogWarning($"GF_AutoWire: Op[{i}] ensure_component path not found: '{o.path}'"); fail++; break; }
                        if (string.IsNullOrWhiteSpace(o.componentType)) { Debug.LogWarning($"GF_AutoWire: Op[{i}] ensure_component missing componentType"); fail++; break; }

                        var comp = EnsureComponentByTypeName(targetTf.gameObject, o.componentType, useUndo);
                        if (comp != null) ok++; else fail++;
                        break;
                    }

                    case "remove_component":
                    {
                        var targetTf = ResolveTransformPathSmart(root, o.path, out _);
                        if (!targetTf) { Debug.LogWarning($"GF_AutoWire: Op[{i}] remove_component path not found: '{o.path}'"); fail++; break; }
                        if (string.IsNullOrWhiteSpace(o.componentType)) { Debug.LogWarning($"GF_AutoWire: Op[{i}] remove_component missing componentType"); fail++; break; }

                        int removed = RemoveComponentByTypeName(targetTf.gameObject, o.componentType, o.removeAll, useUndo);
                        if (removed > 0) ok++; else { skip++; }
                        break;
                    }

                    case "destroy_gameobject":
                    {
                        var targetTf = ResolveTransformPathSmart(root, o.path, out _);
                        if (!targetTf)
                        {
                            Debug.LogWarning($"GF_AutoWire: Op[{i}] destroy_gameobject path not found: '{o.path}'");
                            skip++;
                            break;
                        }
                        if (targetTf == root)
                        {
                            Debug.LogWarning($"GF_AutoWire: Op[{i}] destroy_gameobject refused to destroy the root object.");
                            fail++;
                            break;
                        }

                        DestroyGameObject(targetTf.gameObject, useUndo);
                        ok++;
                        break;
                    }

                    case "set_object_ref":
                    {
                        var targetTf = ResolveTransformPathSmart(root, o.path, out _);
                        if (!targetTf) { Debug.LogWarning($"GF_AutoWire: Op[{i}] target path not found: '{o.path}'"); fail++; break; }

                        var targetComp = GetComponentByTypeName(targetTf.gameObject, o.componentType);
                        if (!targetComp) { Debug.LogWarning($"GF_AutoWire: Op[{i}] component not found: '{o.componentType}' on '{targetTf.name}'"); fail++; break; }

                        var refTf = ResolveTransformPathSmart(root, o.refPath, out _);
                        if (!refTf) { Debug.LogWarning($"GF_AutoWire: Op[{i}] refPath not found: '{o.refPath}'"); fail++; break; }

                        object value;
                        if (!string.IsNullOrWhiteSpace(o.refComponentType))
                        {
                            var rc = GetComponentByTypeName(refTf.gameObject, o.refComponentType);
                            if (!rc) { Debug.LogWarning($"GF_AutoWire: Op[{i}] ref component '{o.refComponentType}' not found on '{refTf.name}'"); fail++; break; }
                            value = rc;
                        }
                        else value = refTf.gameObject;

                        bool assigned = TryAssign(targetComp, o.fieldName, value, true, out string why);
                        if (assigned) ok++; else { Debug.LogWarning($"GF_AutoWire: Op[{i}] set_object_ref failed: {why}"); fail++; }
                        break;
                    }

                    case "clear_object_ref":
                    {
                        var targetTf = ResolveTransformPathSmart(root, o.path, out _);
                        if (!targetTf) { Debug.LogWarning($"GF_AutoWire: Op[{i}] clear_object_ref target path not found: '{o.path}'"); fail++; break; }

                        var targetComp = GetComponentByTypeName(targetTf.gameObject, o.componentType);
                        if (!targetComp) { Debug.LogWarning($"GF_AutoWire: Op[{i}] clear_object_ref component not found: '{o.componentType}' on '{targetTf.name}'"); fail++; break; }

                        bool assigned = TryAssign(targetComp, o.fieldName, null, true, out string why);
                        if (assigned) ok++; else { Debug.LogWarning($"GF_AutoWire: Op[{i}] clear_object_ref failed: {why}"); fail++; }
                        break;
                    }

                    case "set_int":
                    {
                        var targetTf = ResolveTransformPathSmart(root, o.path, out _);
                        if (!targetTf) { Debug.LogWarning($"GF_AutoWire: Op[{i}] target path not found: '{o.path}'"); fail++; break; }

                        var targetComp = GetComponentByTypeName(targetTf.gameObject, o.componentType);
                        if (!targetComp) { Debug.LogWarning($"GF_AutoWire: Op[{i}] component not found: '{o.componentType}' on '{targetTf.name}'"); fail++; break; }

                        bool assigned = TryAssign(targetComp, o.fieldName, o.intValue, true, out string why);
                        if (assigned) ok++; else { Debug.LogWarning($"GF_AutoWire: Op[{i}] set_int failed: {why}"); fail++; }
                        break;
                    }

                    case "set_float":
                    {
                        var targetTf = ResolveTransformPathSmart(root, o.path, out _);
                        if (!targetTf) { Debug.LogWarning($"GF_AutoWire: Op[{i}] target path not found: '{o.path}'"); fail++; break; }

                        var targetComp = GetComponentByTypeName(targetTf.gameObject, o.componentType);
                        if (!targetComp) { Debug.LogWarning($"GF_AutoWire: Op[{i}] component not found: '{o.componentType}' on '{targetTf.name}'"); fail++; break; }

                        bool assigned = TryAssign(targetComp, o.fieldName, o.floatValue, true, out string why);
                        if (assigned) ok++; else { Debug.LogWarning($"GF_AutoWire: Op[{i}] set_float failed: {why}"); fail++; }
                        break;
                    }

                    case "set_bool":
                    {
                        var targetTf = ResolveTransformPathSmart(root, o.path, out _);
                        if (!targetTf) { Debug.LogWarning($"GF_AutoWire: Op[{i}] target path not found: '{o.path}'"); fail++; break; }

                        var targetComp = GetComponentByTypeName(targetTf.gameObject, o.componentType);
                        if (!targetComp) { Debug.LogWarning($"GF_AutoWire: Op[{i}] component not found: '{o.componentType}' on '{targetTf.name}'"); fail++; break; }

                        bool assigned = TryAssign(targetComp, o.fieldName, o.boolValue, true, out string why);
                        if (assigned) ok++; else { Debug.LogWarning($"GF_AutoWire: Op[{i}] set_bool failed: {why}"); fail++; }
                        break;
                    }

                    case "set_string":
                    {
                        var targetTf = ResolveTransformPathSmart(root, o.path, out _);
                        if (!targetTf) { Debug.LogWarning($"GF_AutoWire: Op[{i}] target path not found: '{o.path}'"); fail++; break; }

                        var targetComp = GetComponentByTypeName(targetTf.gameObject, o.componentType);
                        if (!targetComp) { Debug.LogWarning($"GF_AutoWire: Op[{i}] component not found: '{o.componentType}' on '{targetTf.name}'"); fail++; break; }

                        bool assigned = TryAssign(targetComp, o.fieldName, o.stringValue ?? "", true, out string why);
                        if (assigned) ok++; else { Debug.LogWarning($"GF_AutoWire: Op[{i}] set_string failed: {why}"); fail++; }
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
