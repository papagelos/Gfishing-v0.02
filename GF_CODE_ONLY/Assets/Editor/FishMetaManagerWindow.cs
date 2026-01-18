// Assets/Editor/FishMetaManagerWindow.cs
// UNIFIED Fish Meta Manager — replaces scattered tools with one window.
// Tabs: Browse/Edit, Batch, Tools (Catalog, Fields, Repair).
// Unity 6.x compatible. Uses SerializedObject/SerializedProperty.

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace GalacticFishing.EditorTools
{
    public sealed class FishMetaManagerWindow : EditorWindow
    {
        // ---- Configuration (adjust if your folders differ) ----
        private const string META_FOLDER        = "Assets/Data/FishMeta";
        private const string FISH_DATA_FOLDER   = "Assets/Data/Fish"; // raw fish/prefabs folder (for seeding)
        private const string COMPANION_FILENAME = "FishMeta.Companion.cs";

        private enum Tab { BrowseEdit, Batch, Tools }
        private Tab _tab = Tab.BrowseEdit;

        // Row representing one fish meta (+prefab linkage if present)
        private sealed class Row
        {
            public bool             selected;
            public string           name;        // displayName (or asset name)
            public ScriptableObject meta;        // FishMeta asset
            public string           metaPath;    // path of meta
            // Avoid compile-time dependency on FishIdentity: keep a generic Component reference if present
            public Component        idOnPrefab;  // optional identity on prefab
            public string           prefabPath;  // prefab path if matched
            public ScriptableObject fish;        // Fish asset (core data)
            public string           fishPath;    // path of Fish asset
        }

        private readonly List<Row> _rows = new();
        private Vector2 _leftScroll, _rightScroll;
        private string _search = string.Empty;
        private Editor _singleInspector;
        private Editor _fishInspector;
        private GUIStyle _badge;
        private int _companionCount;
        private int _pairedFishCount;

        // Batch: which properties are active to copy (by propertyPath), per asset kind
        private readonly Dictionary<string, bool> _batchToggleMeta  = new();
        private readonly Dictionary<string, bool> _batchToggleFish  = new();

        // Tools: options
        private bool _dryRun  = true;
        private bool _makeBak = true;

        // Fields helper state
        private string _newFieldName       = string.Empty;
        private int    _typeIndex          = 0;
        private static readonly string[] k_FieldTypes = { "int", "float", "bool", "string", "Vector2", "Vector2Int" };
        private int    _defInt;
        private float  _defFloat;
        private bool   _defBool;
        private string _defString = "";

        // -------------- Menu --------------
        [MenuItem("Galactic Fishing/Unified Fish Meta Manager", priority = 3000)]
        public static void Open()
        {
            var w = GetWindow<FishMetaManagerWindow>("Unified Fish Meta Manager");
            w.minSize = new Vector2(1024, 560);
            w.Refresh();
            w.Show();
        }

        private void OnEnable()  { Refresh(); }
        private void OnDisable()
        {
            if (_singleInspector) DestroyImmediate(_singleInspector);
            if (_fishInspector)   DestroyImmediate(_fishInspector);
        }
        private void OnDestroy()
        {
            if (_singleInspector) DestroyImmediate(_singleInspector);
            if (_fishInspector)   DestroyImmediate(_fishInspector);
        }

        private void Refresh()
        {
            EnsureFolders();
            RebuildRows();
            Repaint();
        }

        private void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder(META_FOLDER))
            {
                var parent = Path.GetDirectoryName(META_FOLDER).Replace('\\', '/');
                var leaf   = Path.GetFileName(META_FOLDER);
                if (!AssetDatabase.IsValidFolder(parent) && !string.IsNullOrEmpty(parent))
                    AssetDatabase.CreateFolder(Path.GetDirectoryName(parent).Replace('\\', '/'), Path.GetFileName(parent));
                AssetDatabase.CreateFolder(parent, leaf);
            }
        }

        private void OnGUI()
        {
            DrawToolbar();

            EditorGUILayout.BeginHorizontal();
            DrawLeftList();
            EditorGUILayout.Space(6);
            switch (_tab)
            {
                case Tab.BrowseEdit: DrawBrowseEdit(); break;
                case Tab.Batch:      DrawBatch();      break;
                case Tab.Tools:      DrawTools();      break;
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Rescan", EditorStyles.toolbarButton, GUILayout.Width(70)))
                    Refresh();

                GUILayout.FlexibleSpace();

                var newTab = (Tab)GUILayout.Toolbar(
                    (int)_tab,
                    new[] { "Browse/Edit", "Batch", "Tools" },
                    EditorStyles.toolbarButton,
                    GUILayout.Width(320));
                if (newTab != _tab)
                {
                    _tab        = newTab;
                    _rightScroll = Vector2.zero;
                }
            }
        }

        // ---------------- Left Pane ----------------
        private void DrawLeftList()
        {
            if (_badge == null)
            {
                _badge = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white }
                };
                _badge.normal.background = Texture2D.grayTexture;
            }

            EditorGUILayout.BeginVertical(GUILayout.Width(380));
            GUILayout.Label("Fish", EditorStyles.boldLabel);
            if (_companionCount > 0)
                EditorGUILayout.HelpBox(
                    $"{_companionCount} meta asset(s) are bound to 'FishMeta.Companion'. Run Repair from the Tools tab (start with Dry Run).",
                    MessageType.Warning);
            if (_pairedFishCount == 0)
                EditorGUILayout.HelpBox(
                    "No Fish assets paired yet (scanning Assets/Data/Fish). Update FISH_DATA_FOLDER if your fish live elsewhere.",
                    MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                _search = EditorGUILayout.TextField(_search, (GUIStyle)"SearchTextField");
                if (GUILayout.Button(GUIContent.none, (GUIStyle)"SearchCancelButton"))
                    _search = "";
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Select All", GUILayout.Width(90)))
                    foreach (var r in FilteredRows()) r.selected = true;
                if (GUILayout.Button("None", GUILayout.Width(60)))
                    foreach (var r in _rows) r.selected = false;
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Create Missing Meta", GUILayout.Width(160)))
                    CreateMissingMetaForProjectFish();
                SmallInfo("Scans raw fish/prefab folder and creates FishMeta assets for any fish that don’t have one yet.");
            }

            _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll);
            foreach (var r in FilteredRows())
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    r.selected = GUILayout.Toggle(r.selected, GUIContent.none, GUILayout.Width(18));
                    if (GUILayout.Button(r.name, EditorStyles.label)) r.selected = !r.selected;
                    GUILayout.FlexibleSpace();

                    if (r.fish)
                    {
                        GUILayout.Label("fish", _badge, GUILayout.Width(34));
                        if (GUILayout.Button("Open", GUILayout.Width(50)))
                            Selection.activeObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(r.fishPath);
                    }

                    if (!string.IsNullOrEmpty(r.prefabPath))
                    {
                        GUILayout.Label("prefab", _badge, GUILayout.Width(48));
                        if (GUILayout.Button("Open", GUILayout.Width(50)))
                            Selection.activeObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(r.prefabPath);
                    }

                    if (GUILayout.Button("Select", GUILayout.Width(60)))
                        Selection.activeObject = r.meta;
                }
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private IEnumerable<Row> FilteredRows()
        {
            string q = string.IsNullOrWhiteSpace(_search) ? null : _search.ToLowerInvariant();
            return string.IsNullOrEmpty(q)
                ? _rows
                : _rows.Where(r => r.name != null && r.name.ToLowerInvariant().Contains(q));
        }

        // ---------------- Right Pane: Browse/Edit ----------------
        private void DrawBrowseEdit()
        {
            EditorGUILayout.BeginVertical();
            GUILayout.Label("Inspector", EditorStyles.boldLabel);

            var sel = _rows.Where(r => r.selected).ToList();
            if (sel.Count == 0)
            {
                EditorGUILayout.HelpBox("Select a fish on the left.", MessageType.Info);
                EditorGUILayout.EndVertical();
                if (_singleInspector) { DestroyImmediate(_singleInspector); _singleInspector = null; }
                if (_fishInspector)   { DestroyImmediate(_fishInspector);   _fishInspector   = null; }
                return;
            }

            // Single selection: show full inspector
            if (sel.Count == 1)
            {
                var r = sel[0];
                _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);
                if (_singleInspector) DestroyImmediate(_singleInspector);
                _singleInspector = Editor.CreateEditor(r.meta);
                if (_singleInspector != null)
                {
                    GUILayout.Label("FishMeta", EditorStyles.boldLabel);
                    _singleInspector.OnInspectorGUI();
                    GUILayout.Space(8);
                }
                if (r.fish)
                {
                    if (_fishInspector) DestroyImmediate(_fishInspector);
                    _fishInspector = Editor.CreateEditor(r.fish);
                    if (_fishInspector != null)
                    {
                        GUILayout.Label("Fish (Core Data)", EditorStyles.boldLabel);
                        _fishInspector.OnInspectorGUI();
                    }
                }
                else
                {
                    if (_fishInspector) { DestroyImmediate(_fishInspector); _fishInspector = null; }
                    EditorGUILayout.HelpBox(
                        "No Fish asset paired. Drag a Fish ScriptableObject here to link it for this session.",
                        MessageType.Info);
                    var picked = EditorGUILayout.ObjectField(null, typeof(ScriptableObject), false) as ScriptableObject;
                    if (picked != null)
                    {
                        r.fish     = picked;
                        r.fishPath = AssetDatabase.GetAssetPath(picked);
                        _pairedFishCount++;
                        Repaint();
                    }
                }

                // ---- PERMANENT DELETE BUTTON ----
                GUILayout.Space(12);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    Color prev = GUI.backgroundColor;
                    GUI.backgroundColor = Color.red;
                    if (GUILayout.Button("!!! PERMANENTLY REMOVE FISH !!!", GUILayout.Height(32)))
                    {
                        if (EditorUtility.DisplayDialog(
                            "Confirm Permanent Removal",
                            $"This will permanently delete FishMeta, Fish asset and sprite for:\n\n'{r.name}'\n\n" +
                            "This cannot be undone (except via version control/backups).",
                            "Yes, delete it", "Cancel"))
                        {
                            PermanentlyRemoveFish(r);
                            EditorGUILayout.EndScrollView();
                            GUI.backgroundColor = prev;
                            EditorGUILayout.EndVertical();
                            return; // rows changed; bail out of this GUI pass
                        }
                    }
                    GUI.backgroundColor = prev;
                }
                // ---------------------------------

                EditorGUILayout.EndScrollView();
            }
            else
            {
                EditorGUILayout.HelpBox(
                    $"{sel.Count} selected – switch to the Batch tab to apply fields to many at once.",
                    MessageType.Info);
                if (_fishInspector) { DestroyImmediate(_fishInspector); _fishInspector = null; }
            }

            EditorGUILayout.EndVertical();
        }

        // ---------------- Right Pane: Batch ----------------
        private void DrawBatch()
        {
            EditorGUILayout.BeginVertical();
            GUILayout.Label("Batch Apply Properties", EditorStyles.boldLabel);

            var sel = _rows.Where(r => r.selected && r.meta != null).ToList();
            if (sel.Count == 0)
            {
                EditorGUILayout.HelpBox("Select 2+ fish on the left.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            var first = sel[0];
            var srcSO = new SerializedObject(first.meta);
            srcSO.Update();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Check All (Meta)", GUILayout.Width(130)))
                    ToggleAllBatch(srcSO, _batchToggleMeta, true);
                if (GUILayout.Button("Uncheck All (Meta)", GUILayout.Width(150)))
                    ToggleAllBatch(srcSO, _batchToggleMeta, false);
                GUILayout.FlexibleSpace();
                _dryRun  = GUILayout.Toggle(_dryRun,  "Dry Run",    GUILayout.Width(90));
                _makeBak = GUILayout.Toggle(_makeBak, "Create .bak", GUILayout.Width(110));
            }

            _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);
            // --- FishMeta fields ---
            GUILayout.Label("FishMeta fields", EditorStyles.boldLabel);
            var it = srcSO.GetIterator();
            bool enterChildren = true;
            while (it.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (it.propertyPath == "m_Script") continue;

                bool on = _batchToggleMeta.TryGetValue(it.propertyPath, out var v) ? v : false;
                using (new EditorGUILayout.HorizontalScope())
                {
                    on = EditorGUILayout.Toggle(on, GUILayout.Width(18));
                    EditorGUILayout.LabelField(new GUIContent(it.displayName, it.propertyPath));
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField(it.propertyType.ToString(), GUILayout.Width(120));
                }
                _batchToggleMeta[it.propertyPath] = on;
            }

            // --- Fish fields (if source row has a Fish) ---
            if (first.fish)
            {
                GUILayout.Space(8);
                var srcFish = new SerializedObject(first.fish);
                srcFish.Update();
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Check All (Fish)", GUILayout.Width(130)))
                        ToggleAllBatch(srcFish, _batchToggleFish, true);
                    if (GUILayout.Button("Uncheck All (Fish)", GUILayout.Width(150)))
                        ToggleAllBatch(srcFish, _batchToggleFish, false);
                }
                GUILayout.Label("Fish (Core Data) fields", EditorStyles.boldLabel);
                var it2   = srcFish.GetIterator();
                bool enter2 = true;
                while (it2.NextVisible(enter2))
                {
                    enter2 = false;
                    if (it2.propertyPath == "m_Script") continue;
                    bool on = _batchToggleFish.TryGetValue(it2.propertyPath, out var v2) ? v2 : false;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        on = EditorGUILayout.Toggle(on, GUILayout.Width(18));
                        EditorGUILayout.LabelField(new GUIContent(it2.displayName, it2.propertyPath));
                        GUILayout.FlexibleSpace();
                        EditorGUILayout.LabelField(it2.propertyType.ToString(), GUILayout.Width(120));
                    }
                    _batchToggleFish[it2.propertyPath] = on;
                }
            }
            EditorGUILayout.EndScrollView();

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button($"Apply to {sel.Count - 1} other fish", GUILayout.Height(26)))
                    ApplyBatchBoth(first, sel.Skip(1).ToList());
            }

            EditorGUILayout.EndVertical();
        }

        private void ToggleAllBatch(SerializedObject srcSO, Dictionary<string, bool> map, bool on)
        {
            var it = srcSO.GetIterator();
            bool enterChildren = true;
            while (it.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (it.propertyPath == "m_Script") continue;
                map[it.propertyPath] = on;
            }
        }

        private void ApplyBatchBoth(Row source, List<Row> targets)
        {
            if (targets == null || targets.Count == 0) return;

            var srcSO = new SerializedObject(source.meta);
            srcSO.Update();

            int changed = 0;
            foreach (var t in targets)
            {
                if (t.meta == null || t.meta == source.meta) continue;

                // optional backup
                if (_makeBak) SafeCopyAssetFile(t.metaPath, t.metaPath + ".bak");

                var dstSO = new SerializedObject(t.meta);
                dstSO.Update();

                var srcIt = srcSO.GetIterator();
                bool enter = true;
                while (srcIt.NextVisible(enter))
                {
                    enter = false;
                    if (srcIt.propertyPath == "m_Script") continue;
                    if (_batchToggleMeta.TryGetValue(srcIt.propertyPath, out var on) && on)
                    {
                        var dstProp = dstSO.FindProperty(srcIt.propertyPath);
                        if (dstProp != null)
                        {
                            if (_dryRun == false) CopySerializedValue(srcIt, dstProp);
                        }
                    }
                }

                if (!_dryRun)
                {
                    Undo.RecordObject(t.meta, "Batch Apply FishMeta");
                    dstSO.ApplyModifiedProperties();
                    EditorUtility.SetDirty(t.meta);
                    changed++;
                }

                // ---- Fish asset (if present on source & target) ----
                if (source.fish && t.fish && _batchToggleFish.Any(kv => kv.Value))
                {
                    if (_makeBak && !string.IsNullOrEmpty(t.fishPath))
                        SafeCopyAssetFile(t.fishPath, t.fishPath + ".bak");
                    var srcFish = new SerializedObject(source.fish);
                    srcFish.Update();
                    var dstFish = new SerializedObject(t.fish);
                    dstFish.Update();
                    var it2   = srcFish.GetIterator();
                    bool enter2 = true;
                    while (it2.NextVisible(enter2))
                    {
                        enter2 = false;
                        if (it2.propertyPath == "m_Script") continue;
                        if (_batchToggleFish.TryGetValue(it2.propertyPath, out var on2) && on2)
                        {
                            var dp = dstFish.FindProperty(it2.propertyPath);
                            if (dp != null && !_dryRun) CopySerializedValue(it2, dp);
                        }
                    }
                    if (!_dryRun)
                    {
                        Undo.RecordObject(t.fish, "Batch Apply Fish");
                        dstFish.ApplyModifiedProperties();
                        EditorUtility.SetDirty(t.fish);
                    }
                }
            }

            if (!_dryRun) AssetDatabase.SaveAssets();
            ShowNotification(new GUIContent(_dryRun
                ? "Dry run complete (no writes)"
                : $"Applied to {changed} fish"));
        }

        // ---------------- Right Pane: Tools ----------------
        private void DrawTools()
        {
            _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);

            GUILayout.Label("Catalog", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope("box"))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool auto    = GetAutoBuildOnImport();
                    bool newAuto = EditorGUILayout.ToggleLeft("Auto-Build on Import", auto, GUILayout.Width(200));
                    SmallInfo("When enabled, the project rebuilds the Fish Catalog automatically after relevant assets import/move.");
                    GUILayout.FlexibleSpace();
                    if (newAuto != auto) SetAutoBuildOnImport(newAuto);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Build Fish Catalog", GUILayout.Height(22))) BuildFishCatalogNow();
                    SmallInfo("Runs FishCatalogBuilder.BuildWithSettings(s).");
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Open Catalog Settings", GUILayout.Width(180)))
                        InvokeCatalogCreateOrSelectSettings();
                }
            }

            EditorGUILayout.Space(8);
            GUILayout.Label("Fields (Partial Companion)", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope("box"))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Make FishMeta partial")) MakeFishMetaPartial();
                    if (GUILayout.Button("Generate Companion"))   GenerateCompanion();
                    if (GUILayout.Button("Open Companion"))       OpenCompanion();
                }

                _newFieldName = EditorGUILayout.TextField("Field Name", _newFieldName);
                _typeIndex    = EditorGUILayout.Popup("Type", _typeIndex, k_FieldTypes);
                switch (_typeIndex)
                {
                    case 0: _defInt    = EditorGUILayout.IntField("Default", _defInt);          break;
                    case 1: _defFloat  = EditorGUILayout.FloatField("Default", _defFloat);      break;
                    case 2: _defBool   = EditorGUILayout.Toggle("Default", _defBool);           break;
                    case 3: _defString = EditorGUILayout.TextField("Default", _defString);      break;
                    default: break; // vectors have no inline default
                }

                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_newFieldName)))
                {
                    if (GUILayout.Button("Add Field to Companion"))
                        AddFieldToCompanion(_newFieldName, k_FieldTypes[_typeIndex]);
                }

                EditorGUILayout.HelpBox(
                    "CreateAssetMenu must exist ONLY on the primary FishMeta file, never in the companion. This tool ensures generated companion has no CreateAssetMenu.",
                    MessageType.None);
            }

            EditorGUILayout.Space(8);
            GUILayout.Label("Repair", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope("box"))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    _dryRun  = GUILayout.Toggle(_dryRun,  "Dry Run",    GUILayout.Width(90));
                    _makeBak = GUILayout.Toggle(_makeBak, "Create .bak", GUILayout.Width(110));
                }

                if (GUILayout.Button("Detect & Repair assets pointing to 'FishMeta.Companion'"))
                    RepairCompanionAssets();
                SmallInfo("Scans FishMeta assets and replaces their script reference with the primary FishMeta script when necessary.");
            }

            EditorGUILayout.EndScrollView();
        }

        private void SmallInfo(string tip)
        {
            var c  = EditorGUIUtility.IconContent("_Help");
            var gc = new GUIContent(c.image, tip);
            GUILayout.Label(gc, GUILayout.Width(18), GUILayout.Height(18));
        }

        // --------- Scan/Build Rows ----------
        private void RebuildRows()
        {
            _rows.Clear();
            _companionCount  = 0;
            _pairedFishCount = 0;

            // Load FishMeta assets from META_FOLDER (type-agnostic load)
            var metaGuids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { META_FOLDER });
            foreach (var g in metaGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var obj  = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (!obj) continue;

                // Keep only assets whose type name matches FishMeta
                if (!obj.GetType().Name.Equals("FishMeta", StringComparison.Ordinal)) continue;

                var so   = new SerializedObject(obj);
                var disp = so.FindProperty("displayName");
                var n    = disp != null && !string.IsNullOrWhiteSpace(disp.stringValue)
                    ? disp.stringValue
                    : obj.name;

                _rows.Add(new Row { selected = false, name = n, meta = obj, metaPath = path });
            }

            // Attach prefab refs + fish data by matching assets
            var idLookup   = BuildIdentityLookup();
            var fishLookup = BuildFishAssetLookup();
            foreach (var r in _rows)
            {
                var trimmedName = r.name?.Trim();
                if (!string.IsNullOrEmpty(trimmedName) && idLookup.TryGetValue(trimmedName, out var tuple))
                {
                    r.idOnPrefab = tuple.id;
                    r.prefabPath = tuple.path;
                }

                // Try direct match, displayName match, and normalized names without "Fish_" prefix
                var keys = new[]
                {
                    trimmedName,
                    r.meta ? r.meta.name?.Trim()           : null,
                    r.meta ? RemoveFishPrefix(r.meta.name) : null,
                    RemoveFishPrefix(trimmedName)
                };

                foreach (var key in keys)
                {
                    if (string.IsNullOrEmpty(key)) continue;
                    if (fishLookup.TryGetValue(key, out var fish) && fish.fish)
                    {
                        r.fish     = fish.fish;
                        r.fishPath = fish.path;
                        _pairedFishCount++;
                        break;
                    }
                }
            }

            _rows.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
        }

        private static string RemoveFishPrefix(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.StartsWith("Fish_", StringComparison.OrdinalIgnoreCase) ? s.Substring(5) : s;
        }

        private Dictionary<string, (Component id, string path)> BuildIdentityLookup()
        {
            var dict = new Dictionary<string, (Component, string)>(StringComparer.OrdinalIgnoreCase);

            // Try to locate FishIdentity type by name, but don't require it to compile.
            var identityType =
                FindTypeByName("GalacticFishing.FishIdentity") ??
                FindTypeByName("FishIdentity");
            if (identityType == null)
                return dict; // Type not present in this project; skip prefab linking gracefully.

            var prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { FISH_DATA_FOLDER });
            foreach (var g in prefabGuids)
            {
                var p  = AssetDatabase.GUIDToAssetPath(g);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (!go) continue;
                var comp = go.GetComponentInChildren(identityType, true) as Component;
                if (!comp) continue;

                // Read displayName (SerializedProperty first, then reflection)
                string displayName = null;
                var so = new SerializedObject(comp);
                var sp = so.FindProperty("displayName");
                if (sp != null) displayName = sp.stringValue;
                if (string.IsNullOrEmpty(displayName))
                {
                    var fld = identityType.GetField("displayName",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (fld != null && fld.FieldType == typeof(string))
                        displayName = fld.GetValue(comp) as string;
                    else
                    {
                        var pi = identityType.GetProperty("displayName",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (pi != null && pi.PropertyType == typeof(string))
                            displayName = pi.GetValue(comp) as string;
                    }
                }
                if (string.IsNullOrWhiteSpace(displayName)) continue;

                var dn = displayName.Trim();
                if (!dict.ContainsKey(dn)) dict.Add(dn, (comp, p));
            }
            return dict;
        }

        private Dictionary<string, (ScriptableObject fish, string path)> BuildFishAssetLookup()
        {
            var dict = new Dictionary<string, (ScriptableObject, string)>(StringComparer.OrdinalIgnoreCase);

            var guids = AssetDatabase.FindAssets(string.Empty, new[] { FISH_DATA_FOLDER });
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var obj  = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                if (!obj) continue;
                if (obj is not ScriptableObject soObj) continue;

                string dn = null;
                var so = new SerializedObject(soObj);
                var sp = so.FindProperty("displayName");
                if (sp != null) dn = sp.stringValue;
                if (string.IsNullOrWhiteSpace(dn)) dn = soObj.name;

                void AddKey(string key)
                {
                    if (!string.IsNullOrWhiteSpace(key) && !dict.ContainsKey(key))
                        dict.Add(key, (soObj, path));
                }

                var fileName = Path.GetFileNameWithoutExtension(path);
                AddKey(dn);
                AddKey("Fish_" + dn);
                AddKey(RemoveFishPrefix(dn));
                AddKey(fileName);
                AddKey(RemoveFishPrefix(fileName));
            }
            return dict;
        }

        private void CreateMissingMetaForProjectFish()
        {
            var created = 0;
            var seen    = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in _rows)
                if (!string.IsNullOrEmpty(r.name)) seen.Add(r.name);

            foreach (var g in AssetDatabase.FindAssets("", new[] { FISH_DATA_FOLDER }))
            {
                var p  = AssetDatabase.GUIDToAssetPath(g);
                var nm = Path.GetFileNameWithoutExtension(p);
                if (!nm.StartsWith("Fish_", StringComparison.Ordinal)) continue;

                if (!seen.Contains(nm))
                {
                    var so = CreateMeta(nm);
                    if (so != null) { created++; seen.Add(nm); }
                }
            }

            AssetDatabase.SaveAssets();
            Refresh();
            ShowNotification(new GUIContent(
                created == 0 ? "No missing meta." : $"Created {created} FishMeta assets"));
        }

        private ScriptableObject CreateMeta(string fishName)
        {
            var type =
                FindTypeByName("GalacticFishing.FishMeta") ??
                FindTypeByName("FishMeta");
            if (type == null)
            {
                EditorUtility.DisplayDialog(
                    "FishMeta type not found",
                    "I couldn't find a type named 'FishMeta'. Make sure your FishMeta ScriptableObject class exists.",
                    "OK");
                return null;
            }

            var meta = ScriptableObject.CreateInstance(type) as ScriptableObject;
            if (!meta) return null;

            var sobj = new SerializedObject(meta);
            var dn   = sobj.FindProperty("displayName");
            if (dn != null) dn.stringValue = fishName;
            sobj.ApplyModifiedPropertiesWithoutUndo();

            var safe = fishName.Replace(' ', '_');
            var path = AssetDatabase.GenerateUniqueAssetPath($"{META_FOLDER}/{safe}.asset");
            AssetDatabase.CreateAsset(meta, path);
            return meta;
        }

        // -------- Catalog reflection helpers --------
        private static Type FindTypeByName(string fullOrShort)
        {
            if (string.IsNullOrEmpty(fullOrShort)) return null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var t = asm.GetType(fullOrShort, throwOnError: false);
                    if (t != null) return t;
                    t = asm.GetTypes().FirstOrDefault(x => x.Name == fullOrShort);
                    if (t != null) return t;
                }
                catch
                {
                    // ignore dynamic assemblies that throw
                }
            }
            return null;
        }

        private static object InvokeCatalogStatic(string methodName, params object[] args)
        {
            var bType =
                FindTypeByName("GalacticFishing.FishCatalogBuilder") ??
                FindTypeByName("FishCatalogBuilder");
            if (bType == null) { Debug.LogWarning("FishCatalogBuilder not found."); return null; }
            var mi = bType.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (mi == null) { Debug.LogWarning($"Method not found: FishCatalogBuilder.{methodName}"); return null; }
            return mi.Invoke(null, args);
        }

        // Find settings without side effects (no selection/ping/creation).
        private UnityEngine.Object FindCatalogSettings()
        {
            return InvokeCatalogStatic("FindSettings") as UnityEngine.Object;
        }

        // Create (which may select/ping), then return.
        private UnityEngine.Object CreateOrSelectCatalogSettings()
        {
            InvokeCatalogStatic("CreateOrSelectSettings");
            return InvokeCatalogStatic("FindSettings") as UnityEngine.Object;
        }

        private bool GetAutoBuildOnImport()
        {
            var s = FindCatalogSettings();
            if (!s) return false;
            var so = new SerializedObject(s);
            var p  = so.FindProperty("autoBuildOnImport");
            return p != null && p.boolValue;
        }

        private void SetAutoBuildOnImport(bool on)
        {
            // Creating here is okay because user explicitly toggled the value.
            var s = FindCatalogSettings() ?? CreateOrSelectCatalogSettings();
            if (!s) return;
            var so = new SerializedObject(s);
            var p  = so.FindProperty("autoBuildOnImport");
            if (p == null)
            {
                Debug.LogWarning("Settings missing 'autoBuildOnImport'");
                return;
            }
            so.Update();
            p.boolValue = on;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(s);
            AssetDatabase.SaveAssets();
        }

        private void BuildFishCatalogNow()
        {
            var s = FindCatalogSettings();
            if (!s)
            {
                if (EditorUtility.DisplayDialog(
                    "Catalog Settings missing",
                    "Fish Catalog Settings asset was not found. Create it now?",
                    "Create", "Cancel"))
                {
                    s = CreateOrSelectCatalogSettings();
                }
            }
            if (!s) return;
            InvokeCatalogStatic("BuildWithSettings", s);
        }

        private void InvokeCatalogCreateOrSelectSettings()
        {
            CreateOrSelectCatalogSettings(); // user explicitly clicked "Open Catalog Settings"
        }

        // -------- Repair Companion assets --------
        private void RepairCompanionAssets()
        {
            var guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { META_FOLDER });
            int fixedCount = 0, scanned = 0, missingCount = 0, companionCount = 0;

            // Resolve the correct script for main FishMeta
            var fishMetaType =
                FindTypeByName("GalacticFishing.FishMeta") ??
                FindTypeByName("FishMeta");
            if (fishMetaType == null)
            {
                EditorUtility.DisplayDialog("FishMeta not found", "Cannot locate FishMeta type.", "OK");
                return;
            }
            var tmp = ScriptableObject.CreateInstance(fishMetaType) as ScriptableObject;
            var mainScript = MonoScript.FromScriptableObject(tmp);
            DestroyImmediate(tmp);

            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var obj  = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                if (!obj) continue;

                var so   = new SerializedObject(obj);
                var mScr = so.FindProperty("m_Script");
                if (mScr == null) continue;

                scanned++;

                var current      = mScr.objectReferenceValue as MonoScript;
                var currentClass = current ? current.GetClass() : null;
                var cname        = currentClass != null ? currentClass.FullName : "(null)";

                bool isMissing = mScr.objectReferenceValue == null;
                bool looksCompanion =
                    (!string.IsNullOrEmpty(cname) && cname.Contains("Companion")) ||
                    (currentClass != null && currentClass.Name == "Companion");

                if (!isMissing && !looksCompanion) continue;
                if (isMissing) missingCount++; else companionCount++;

                if (_makeBak) SafeCopyAssetFile(path, path + ".bak");

                if (!_dryRun)
                {
                    Undo.RecordObject(obj, "Repair FishMeta script ref");
                    so.Update();
                    mScr.objectReferenceValue = mainScript;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(obj);
                    fixedCount++;
                }
            }

            if (!_dryRun) AssetDatabase.SaveAssets();
            ShowNotification(new GUIContent(_dryRun
                ? $"Scan complete. {scanned} scanned. Missing:{missingCount} Companion:{companionCount} (Dry run)"
                : $"Repaired {fixedCount} asset(s). Missing:{missingCount} Companion:{companionCount}"));
        }

        private static void SafeCopyAssetFile(string src, string dst)
        {
            try
            {
                var absSrc = Path.GetFullPath(src);
                var absDst = Path.GetFullPath(dst);
                Directory.CreateDirectory(Path.GetDirectoryName(absDst));
                File.Copy(absSrc, absDst, overwrite: true);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Backup failed: {ex.Message}");
            }
        }

        // ------------- Copy Serialized Values -------------
        private static readonly MethodInfo k_CopyFromSerializedProperty =
            typeof(SerializedProperty).GetMethod(
                "CopyFromSerializedProperty",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(SerializedProperty) },
                null);

        private static void CopySerializedValue(SerializedProperty src, SerializedProperty dst)
        {
            if (src == null || dst == null) return;

            if (k_CopyFromSerializedProperty != null)
            {
                // Native API available
                k_CopyFromSerializedProperty.Invoke(dst, new object[] { src });
                return;
            }

            // Fallback for older/editor variants
            switch (src.propertyType)
            {
                case SerializedPropertyType.Integer:         dst.intValue           = src.intValue;           break;
                case SerializedPropertyType.Boolean:         dst.boolValue          = src.boolValue;          break;
                case SerializedPropertyType.Float:           dst.floatValue         = src.floatValue;         break;
                case SerializedPropertyType.String:          dst.stringValue        = src.stringValue;        break;
                case SerializedPropertyType.Color:           dst.colorValue         = src.colorValue;         break;
                case SerializedPropertyType.ObjectReference: dst.objectReferenceValue = src.objectReferenceValue; break;
                case SerializedPropertyType.LayerMask:       dst.intValue           = src.intValue;           break;
                case SerializedPropertyType.Enum:            dst.enumValueIndex     = src.enumValueIndex;     break;
                case SerializedPropertyType.Vector2:         dst.vector2Value       = src.vector2Value;       break;
                case SerializedPropertyType.Vector3:         dst.vector3Value       = src.vector3Value;       break;
                case SerializedPropertyType.Vector4:         dst.vector4Value       = src.vector4Value;       break;
                case SerializedPropertyType.Rect:            dst.rectValue          = src.rectValue;          break;
                case SerializedPropertyType.AnimationCurve:  dst.animationCurveValue = src.animationCurveValue; break;
                case SerializedPropertyType.Bounds:          dst.boundsValue        = src.boundsValue;        break;
                case SerializedPropertyType.Quaternion:      dst.quaternionValue    = src.quaternionValue;    break;
#if UNITY_2019_1_OR_NEWER
                case SerializedPropertyType.Vector2Int:      dst.vector2IntValue    = src.vector2IntValue;    break;
                case SerializedPropertyType.Vector3Int:      dst.vector3IntValue    = src.vector3IntValue;    break;
                case SerializedPropertyType.RectInt:         dst.rectIntValue       = src.rectIntValue;       break;
                case SerializedPropertyType.BoundsInt:       dst.boundsIntValue     = src.boundsIntValue;     break;
#endif
                case SerializedPropertyType.Generic:
                    CopyGenericChildren(src, dst);
                    break;
                default:
                    Debug.LogWarning($"Unsupported property type: {src.propertyType} ({src.propertyPath})");
                    break;
            }
        }

        private static void CopyGenericChildren(SerializedProperty src, SerializedProperty dst)
        {
            var srcEnd  = src.GetEndProperty();
            var srcCopy = src.Copy();
            bool enterChildren = true;
            while (srcCopy.Next(enterChildren) && !SerializedProperty.EqualContents(srcCopy, srcEnd))
            {
                enterChildren = false;
                var rel = GetRelativePath(src, srcCopy);
                if (rel == null) continue;
                var dstChild = dst.FindPropertyRelative(rel);
                if (dstChild != null) CopySerializedValue(srcCopy, dstChild);
            }
        }

        private static string GetRelativePath(SerializedProperty parent, SerializedProperty child)
        {
            if (parent == null || child == null) return null;
            string p = parent.propertyPath;
            string c = child.propertyPath;
            if (string.IsNullOrEmpty(p) || string.IsNullOrEmpty(c)) return null;
            if (!c.StartsWith(p, StringComparison.Ordinal)) return null;
            if (c.Length == p.Length) return null;
            int start = p.Length + 1;
            if (start >= c.Length) return null;
            return c.Substring(start);
        }

        // ------------- Fields Helper (partial/companion) -------------
        private static string FindFishMetaScriptPath()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Script FishMeta"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
                if (!path.Contains("/Editor/")) return path;
            }
            var all = AssetDatabase.FindAssets("t:Script FishMeta");
            return all.Length > 0 ? AssetDatabase.GUIDToAssetPath(all[0]) : null;
        }

        private void MakeFishMetaPartial()
        {
            var path = FindFishMetaScriptPath();
            if (path == null)
            {
                EditorUtility.DisplayDialog(
                    "FishMeta not found",
                    "Create FishMeta.cs (ScriptableObject) first.",
                    "OK");
                return;
            }
            var code = File.ReadAllText(path);
            if (code.Contains("partial class FishMeta"))
            {
                ShowNotification(new GUIContent("FishMeta is already partial."));
                return;
            }
            code = code.Replace("class FishMeta", "partial class FishMeta");
            File.WriteAllText(path, code);
            AssetDatabase.Refresh();
            ShowNotification(new GUIContent("Made FishMeta partial"));
        }

        private static string CompanionPath()
        {
            var basePath = FindFishMetaScriptPath();
            if (basePath == null) return null;
            var dir = Path.GetDirectoryName(basePath);
            return Path.Combine(string.IsNullOrEmpty(dir) ? "Assets" : dir, COMPANION_FILENAME).Replace('\\', '/');
        }

        private void GenerateCompanion()
        {
            var path = CompanionPath();
            if (path == null)
            {
                EditorUtility.DisplayDialog(
                    "FishMeta.cs not found",
                    "Open your FishMeta ScriptableObject and choose 'Edit Script' so I can locate the runtime file.",
                    "OK");
                return;
            }

            // If an old companion lives under an Editor folder, move it beside FishMeta.cs
            var wrong = AssetDatabase.FindAssets("t:Script FishMeta.Companion")
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => !string.IsNullOrEmpty(p) &&
                                     p.Replace('\\', '/').Contains("/Editor/"));
            if (!string.IsNullOrEmpty(wrong) && wrong != path && !File.Exists(path))
            {
                AssetDatabase.MoveAsset(wrong, path);
                AssetDatabase.Refresh();
            }

            if (!File.Exists(path))
            {
                var code =
@"// Auto-generated companion partial for FishMeta.
// Add/remove your custom fields here. Safe to edit.
// NOTE: Do NOT add [CreateAssetMenu] here; only the primary FishMeta file should have it.
using UnityEngine;

namespace GalacticFishing
{
    public partial class FishMeta : ScriptableObject
    {
        // add fields below
    }
}
";
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, code);
            }
            AssetDatabase.Refresh();
            OpenCompanion();
        }

        private void OpenCompanion()
        {
            var path = CompanionPath();
            if (path == null) return;
            if (!File.Exists(path)) GenerateCompanion();
            var ms = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            if (ms) AssetDatabase.OpenAsset(ms);
        }

        private void AddFieldToCompanion(string name, string type)
        {
            var path = CompanionPath();
            if (path == null) return;
            if (!File.Exists(path)) GenerateCompanion();

            string code = File.ReadAllText(path);
            if (code.Contains($" {name};"))
            {
                EditorUtility.DisplayDialog(
                    "Field Exists",
                    $"'{name}' already exists in the companion.",
                    "OK");
                return;
            }

            var sb = new System.Text.StringBuilder();
            sb.Append("        public ").Append(type).Append(' ').Append(name);
            switch (type)
            {
                case "int":
                    sb.Append(" = ").Append(_defInt).Append(';');
                    break;
                case "float":
                    sb.Append(" = ")
                      .Append(_defFloat.ToString(System.Globalization.CultureInfo.InvariantCulture))
                      .Append('f')
                      .Append(';');
                    break;
                case "bool":
                    sb.Append(" = ").Append(_defBool ? "true" : "false").Append(';');
                    break;
                case "string":
                    sb.Append(" = \"")
                      .Append(_defString.Replace("\"", "\\\""))
                      .Append("\";");
                    break;
                default:
                    sb.Append(';'); // vectors: no inline default
                    break;
            }
            sb.Append('\n');

            code = code.Replace("// add fields below", "// add fields below\n" + sb);
            File.WriteAllText(path, code);
            AssetDatabase.Refresh();
            ShowNotification(new GUIContent($"Added '{name}'"));
        }

        // ---------- Helpers for permanent delete ----------

        /// <summary>
        /// Reads spritesFolder from the FishCatalogSettings via SerializedObject.
        /// Returns a project-relative folder path (e.g. "Assets/Art/Fish") or null.
        /// </summary>
        private string GetSpritesFolderFromSettings()
        {
            var settings = FindCatalogSettings();
            if (!settings) return null;

            try
            {
                var so = new SerializedObject(settings);
                var p  = so.FindProperty("spritesFolder");
                if (p != null && p.propertyType == SerializedPropertyType.String)
                {
                    var v = p.stringValue;
                    if (!string.IsNullOrWhiteSpace(v))
                        return v.Trim();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FishMetaManager] Failed to read spritesFolder from settings: {e.Message}");
            }

            return null;
        }

        /// <summary>
        /// Permanently removes all assets that belong to the given row:
        /// FishMeta asset, Fish asset, and its sprite (if found).
        /// Also rebuilds the Fish Catalog and refreshes this window.
        /// </summary>
        private void PermanentlyRemoveFish(Row row)
        {
            if (row == null) return;

            string labelName = row.name ?? row.metaPath ?? row.fishPath ?? "(unknown fish)";
            int deletedCount = 0;

            // 1) Delete FishMeta asset
            if (!string.IsNullOrEmpty(row.metaPath))
            {
                if (AssetDatabase.DeleteAsset(row.metaPath))
                {
                    deletedCount++;
                    Debug.Log($"[FishMetaManager] Deleted FishMeta asset: {row.metaPath}");
                }
            }

            // 2) Delete Fish asset (core data ScriptableObject)
            if (!string.IsNullOrEmpty(row.fishPath))
            {
                if (AssetDatabase.DeleteAsset(row.fishPath))
                {
                    deletedCount++;
                    Debug.Log($"[FishMetaManager] Deleted Fish asset: {row.fishPath}");
                }
            }

            // 3) Delete sprite (if FishCatalogSettings tells us where they live)
            string spritesFolder = GetSpritesFolderFromSettings();
            if (!string.IsNullOrEmpty(spritesFolder) && !string.IsNullOrEmpty(row.name))
            {
                try
                {
                    // Look for sprites whose filename (no extension) exactly matches row.name
                    string[] spriteGuids = AssetDatabase.FindAssets(row.name, new[] { spritesFolder });
                    foreach (var guid in spriteGuids)
                    {
                        var path = AssetDatabase.GUIDToAssetPath(guid);
                        if (string.IsNullOrEmpty(path)) continue;

                        var fileName = Path.GetFileNameWithoutExtension(path);
                        if (!string.Equals(fileName, row.name, StringComparison.OrdinalIgnoreCase))
                            continue;

                        if (AssetDatabase.DeleteAsset(path))
                        {
                            deletedCount++;
                            Debug.Log($"[FishMetaManager] Deleted sprite asset: {path}");
                        }

                        // Only delete the first exact match to avoid nuking similarly named things.
                        break;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[FishMetaManager] Error while trying to delete sprite for '{row.name}': {e.Message}");
                }
            }

            if (deletedCount > 0)
            {
                AssetDatabase.SaveAssets();

                // Clear inspectors so they don't point at deleted assets.
                if (_singleInspector) { DestroyImmediate(_singleInspector); _singleInspector = null; }
                if (_fishInspector)   { DestroyImmediate(_fishInspector);   _fishInspector   = null; }

                // Rebuild catalog & refresh rows so IDs remain consistent.
                BuildFishCatalogNow();
                Refresh();

                ShowNotification(new GUIContent($"Removed '{labelName}' ({deletedCount} asset(s))"));
            }
            else
            {
                ShowNotification(new GUIContent($"No assets deleted for '{labelName}'"));
            }
        }
    }
}
#endif
