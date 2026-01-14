#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using GalacticFishing.Upgrades;

namespace GalacticFishing.EditorTools
{
    public sealed class UpgradeDashboardWindow : EditorWindow
    {
        private enum Tab { BrowseEdit, Batch, Tools }
        private Tab _tab = Tab.BrowseEdit;

        private sealed class ItemRef
        {
            public string catalogGuid;
            public ShopCatalog catalog;
            public string catalogPath;
            public int itemIndex;
            public string itemId;
            public string title;
            public string saveKey;
        }

        private sealed class CatalogRef
        {
            public string guid;
            public ShopCatalog catalog;
            public string path;
            public bool foldout = true;
        }

        private readonly List<CatalogRef> _catalogs = new();
        private readonly Dictionary<string, bool> _foldoutByGuid = new();

        private readonly HashSet<string> _selectedSaveKeys = new(StringComparer.Ordinal);
        private ItemRef _active;

        private string _search = "";
        private Vector2 _leftScroll;
        private Vector2 _rightScroll;

        // Batch state
        private bool _dryRun = true;
        private bool _makeBak = true;

        private bool _batchSetCost = true;
        private int _batchCost = 100;

        private bool _batchSetMaxLevel = true;
        private int _batchMaxLevel = 10;

        // Tools output
        private readonly List<string> _validation = new();
        private Vector2 _toolsScroll;

        // Optional effects DB
        private UpgradeEffectsDatabase _effectsDb;
        private SerializedObject _effectsSO;

        [MenuItem("Galactic Fishing/Upgrade Dashboard", priority = 3010)]
        public static void Open()
        {
            var w = GetWindow<UpgradeDashboardWindow>("Upgrade Dashboard");
            w.minSize = new Vector2(1100, 620);
            w.Refresh();
            w.Show();
        }

        private void OnEnable() => Refresh();

        private void OnDisable()
        {
            _effectsSO = null;
        }

        private void Refresh()
        {
            RebuildCatalogs();
            FindEffectsDatabaseIfAny();
            Repaint();
        }

        private void RebuildCatalogs()
        {
            _catalogs.Clear();

            var guids = AssetDatabase.FindAssets("t:ShopCatalog");
            foreach (var g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                var cat = AssetDatabase.LoadAssetAtPath<ShopCatalog>(path);
                if (!cat) continue;

                bool fold = true;
                if (_foldoutByGuid.TryGetValue(g, out var stored))
                    fold = stored;

                _catalogs.Add(new CatalogRef
                {
                    guid = g,
                    catalog = cat,
                    path = path,
                    foldout = fold
                });
            }

            _catalogs.Sort((a, b) =>
            {
                string ac = a.catalog ? (a.catalog.catalogId ?? "") : "";
                string bc = b.catalog ? (b.catalog.catalogId ?? "") : "";
                int c = string.Compare(ac, bc, StringComparison.OrdinalIgnoreCase);
                if (c != 0) return c;
                string an = a.catalog ? a.catalog.name : "";
                string bn = b.catalog ? b.catalog.name : "";
                return string.Compare(an, bn, StringComparison.OrdinalIgnoreCase);
            });

            // If active selection got invalid, clear it
            if (_active != null)
            {
                bool stillExists = _catalogs.Any(c => c.guid == _active.catalogGuid && c.catalog == _active.catalog);
                if (!stillExists) _active = null;
            }
        }

        private void FindEffectsDatabaseIfAny()
        {
            // Auto-pick first one, but user can override in UI.
            if (_effectsDb != null)
                return;

            var dbGuids = AssetDatabase.FindAssets("t:UpgradeEffectsDatabase");
            if (dbGuids != null && dbGuids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(dbGuids[0]);
                _effectsDb = AssetDatabase.LoadAssetAtPath<UpgradeEffectsDatabase>(path);
                _effectsSO = _effectsDb ? new SerializedObject(_effectsDb) : null;
            }
        }

        private void OnGUI()
        {
            DrawToolbar();

            EditorGUILayout.BeginHorizontal();
            DrawLeftPane();
            EditorGUILayout.Space(6);

            switch (_tab)
            {
                case Tab.BrowseEdit: DrawBrowseEdit(); break;
                case Tab.Batch: DrawBatch(); break;
                case Tab.Tools: DrawTools(); break;
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
                    _tab = newTab;
                    _rightScroll = Vector2.zero;
                }
            }
        }

        // ---------------- LEFT PANE ----------------
        private void DrawLeftPane()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(470));

            GUILayout.Label("Shop Catalogs", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                _search = EditorGUILayout.TextField(_search, (GUIStyle)"SearchTextField");
                if (GUILayout.Button(GUIContent.none, (GUIStyle)"SearchCancelButton"))
                    _search = "";
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Select All (Filtered)", GUILayout.Width(150)))
                {
                    foreach (var it in EnumerateFilteredItems())
                        _selectedSaveKeys.Add(it.saveKey);
                }

                if (GUILayout.Button("None", GUILayout.Width(60)))
                    _selectedSaveKeys.Clear();

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Create Catalog", GUILayout.Width(120)))
                    CreateCatalogAsset();

                SmallInfo("Catalogs are your categories: Fishing, Crafting, Minigames, etc.");
            }

            EditorGUILayout.Space(6);

            _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll);

            if (_catalogs.Count == 0)
            {
                EditorGUILayout.HelpBox("No ShopCatalog assets found. Create one with the button above or via CreateAssetMenu.", MessageType.Info);
            }

            foreach (var c in _catalogs)
            {
                if (!c.catalog) continue;

                bool anyMatches = CatalogOrAnyItemMatchesSearch(c.catalog);
                if (!anyMatches) continue;

                string fileLabel = Path.GetFileNameWithoutExtension(c.path);
string headerText = $"{Safe(c.catalog.title, c.catalog.name)}   ({Safe(c.catalog.catalogId, "catalogId EMPTY")})   [{fileLabel}]   —   {c.catalog.items?.Count ?? 0} item(s)";
var headerContent = new GUIContent(headerText, c.path); // tooltip = full path

                bool fold = c.foldout;

                using (new EditorGUILayout.HorizontalScope())
                {
                    fold = EditorGUILayout.Foldout(fold, headerContent, true);
                    if (GUILayout.Button("Open", GUILayout.Width(55)))
                        Selection.activeObject = c.catalog;

                    if (GUILayout.Button("Ping", GUILayout.Width(45)))
                        EditorGUIUtility.PingObject(c.catalog);

                  if (GUILayout.Button("+Item", GUILayout.Width(55)))
{
    int newIndex = AddNewItem(c.catalog);
    if (newIndex >= 0)
    {
        // ensure catalog is expanded
        c.foldout = true;
        _foldoutByGuid[c.guid] = true;

        var newItem = c.catalog.items[newIndex];
        string newKey = BuildSaveKey(c.catalog.catalogId, newItem.id);

        _active = new ItemRef
        {
            catalogGuid = c.guid,
            catalog = c.catalog,
            catalogPath = c.path,
            itemIndex = newIndex,
            itemId = newItem.id,
            title = newItem.title,
            saveKey = newKey
        };

        _selectedSaveKeys.Add(newKey);
        _rightScroll = Vector2.zero;
        GUI.FocusControl(null);

        EditorGUIUtility.PingObject(c.catalog);
        Repaint();
    }
}
      
                }

                if (fold != c.foldout)
                {
                    c.foldout = fold;
                    _foldoutByGuid[c.guid] = fold;
                }

                if (!fold) continue;

                // Items
                var items = c.catalog.items;
                if (items == null) continue;

                for (int i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    if (item == null) continue;

                    string saveKey = BuildSaveKey(c.catalog.catalogId, item.id);
                    if (!ItemMatchesSearch(c.catalog, item)) continue;

                    bool sel = _selectedSaveKeys.Contains(saveKey);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        bool newSel = GUILayout.Toggle(sel, GUIContent.none, GUILayout.Width(18));
                        if (newSel != sel)
                        {
                            if (newSel) _selectedSaveKeys.Add(saveKey);
                            else _selectedSaveKeys.Remove(saveKey);
                        }

                        string label = $"{Safe(item.id, "(id EMPTY)")}  —  {Safe(item.title, "(title)")}  [cost {Mathf.Max(0, item.cost)} | max {Mathf.Max(1, item.maxLevel)}]";
                        if (GUILayout.Button(label, EditorStyles.label))
                        {
                            _active = new ItemRef
                            {
                                catalogGuid = c.guid,
                                catalog = c.catalog,
                                catalogPath = c.path,
                                itemIndex = i,
                                itemId = item.id,
                                title = item.title,
                                saveKey = saveKey
                            };
                            _rightScroll = Vector2.zero;
                            GUI.FocusControl(null);
                        }

                        if (GUILayout.Button("Key", GUILayout.Width(38)))
                        {
                            EditorGUIUtility.systemCopyBuffer = saveKey;
                            ShowNotification(new GUIContent("Copied saveKey to clipboard"));
                        }
                    }
                }

                EditorGUILayout.Space(4);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawBrowseEdit()
        {
            EditorGUILayout.BeginVertical();

            DrawEffectsDbPicker();

            _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);

            if (_active == null || _active.catalog == null)
            {
                EditorGUILayout.HelpBox("Select an upgrade item from the left list to edit it here.", MessageType.Info);
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();
                return;
            }

            // Ensure active still valid
            var cat = _active.catalog;
            if (cat.items == null || _active.itemIndex < 0 || _active.itemIndex >= cat.items.Count || cat.items[_active.itemIndex] == null)
            {
                EditorGUILayout.HelpBox("Selected item no longer exists. Rescan or pick another item.", MessageType.Warning);
                if (GUILayout.Button("Rescan")) Refresh();
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();
                return;
            }

            var item = cat.items[_active.itemIndex];
            string saveKey = BuildSaveKey(cat.catalogId, item.id);

            GUILayout.Label("Upgrade Item", EditorStyles.boldLabel);

            // Show save key like ShopListUI uses
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Save Key", saveKey);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Copy Save Key", GUILayout.Width(120)))
                    {
                        EditorGUIUtility.systemCopyBuffer = saveKey;
                        ShowNotification(new GUIContent("Copied"));
                    }

                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("Select Catalog Asset", GUILayout.Width(140)))
                        Selection.activeObject = cat;
                }

                SmallInfo("This must remain stable. It is what PlayerProgressManager saves levels under.");
            }

            // Edit via SerializedObject so Unity handles proper serialization
            var so = new SerializedObject(cat);
            var itemsProp = so.FindProperty("items");
            var itemProp = itemsProp.GetArrayElementAtIndex(_active.itemIndex);

            so.Update();

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.PropertyField(so.FindProperty("catalogId"));
            EditorGUILayout.PropertyField(so.FindProperty("title"));
            EditorGUILayout.Space(8);

            EditorGUILayout.LabelField("Item Fields", EditorStyles.boldLabel);
            DrawItemField(itemProp, "id");
            DrawItemField(itemProp, "title");
            DrawItemField(itemProp, "description");
            DrawItemField(itemProp, "icon");

            // Safety rails enforced here too
            var costProp = itemProp.FindPropertyRelative("cost");
            var maxProp = itemProp.FindPropertyRelative("maxLevel");

            EditorGUILayout.PropertyField(costProp);
            EditorGUILayout.PropertyField(maxProp);

            // Force minimums (editor-side safety rail)
            if (costProp.intValue < 0) costProp.intValue = 0;
            if (maxProp.intValue < 1) maxProp.intValue = 1;

            bool changed = EditorGUI.EndChangeCheck();

            if (changed)
            {
                Undo.RecordObject(cat, "Edit Upgrade Item");
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(cat);
            }

            EditorGUILayout.Space(10);
            DrawSelectedItemActions(cat, _active.itemIndex);

            EditorGUILayout.Space(12);
            DrawEffectsEditor(saveKey, Mathf.Max(1, item.maxLevel));

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawSelectedItemActions(ShopCatalog cat, int index)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Duplicate Item", GUILayout.Width(120)))
                {
                    DuplicateItem(cat, index);
                    Refresh();
                }

                if (GUILayout.Button("Delete Item", GUILayout.Width(100)))
                {
                    if (EditorUtility.DisplayDialog("Delete upgrade item?",
                            "This will remove the item from the catalog asset.\nThis does NOT delete player save data keys.",
                            "Delete", "Cancel"))
                    {
                        DeleteItem(cat, index);
                        _active = null;
                        Refresh();
                    }
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Save Assets", GUILayout.Width(100)))
                    AssetDatabase.SaveAssets();
            }
        }

        private void DrawBatch()
        {
            EditorGUILayout.BeginVertical();

            DrawEffectsDbPicker();

            GUILayout.Label("Batch Edit", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Selected upgrades", _selectedSaveKeys.Count.ToString());
                EditorGUILayout.Space(4);

                _dryRun = EditorGUILayout.ToggleLeft("Dry Run (preview only)", _dryRun);
                _makeBak = EditorGUILayout.ToggleLeft("Create .bak file for modified catalogs", _makeBak);

                EditorGUILayout.Space(6);

                _batchSetCost = EditorGUILayout.ToggleLeft("Set Cost", _batchSetCost);
                using (new EditorGUI.DisabledScope(!_batchSetCost))
                    _batchCost = EditorGUILayout.IntField("New Cost", Mathf.Max(0, _batchCost));

                _batchSetMaxLevel = EditorGUILayout.ToggleLeft("Set Max Level", _batchSetMaxLevel);
                using (new EditorGUI.DisabledScope(!_batchSetMaxLevel))
                    _batchMaxLevel = EditorGUILayout.IntField("New Max Level", Mathf.Max(1, _batchMaxLevel));

                EditorGUILayout.Space(8);

                using (new EditorGUI.DisabledScope(_selectedSaveKeys.Count == 0 || (!_batchSetCost && !_batchSetMaxLevel)))
                {
                    if (GUILayout.Button(_dryRun ? "Preview Changes" : "Apply Changes", GUILayout.Height(28)))
                    {
                        if (_dryRun) PreviewBatch();
                        else ApplyBatch();
                    }
                }

                SmallInfo("Batch edits only touch ShopCatalogItem fields (cost/maxLevel). Effects are edited per-item in Browse/Edit.");
            }

            EditorGUILayout.EndVertical();
        }

        private void PreviewBatch()
        {
            _validation.Clear();
            var items = EnumerateSelectedItems().ToList();

            if (items.Count == 0)
            {
                _validation.Add("Nothing selected.");
                return;
            }

            _validation.Add($"Previewing {items.Count} item(s):");

            foreach (var it in items)
            {
                if (!it.catalog) continue;
                var item = it.catalog.items != null && it.itemIndex >= 0 && it.itemIndex < it.catalog.items.Count
                    ? it.catalog.items[it.itemIndex]
                    : null;

                if (item == null) continue;

                if (_batchSetCost)
                    _validation.Add($"- {it.saveKey}: cost {item.cost} -> {_batchCost}");

                if (_batchSetMaxLevel)
                    _validation.Add($"- {it.saveKey}: maxLevel {item.maxLevel} -> {_batchMaxLevel}");
            }
        }

        private void ApplyBatch()
        {
            _validation.Clear();
            var selected = EnumerateSelectedItems().ToList();

            if (selected.Count == 0)
            {
                _validation.Add("Nothing selected.");
                return;
            }

            // Backup unique catalogs if requested
            if (_makeBak)
            {
                foreach (var cat in selected.Select(s => s.catalog).Where(c => c != null).Distinct())
                {
                    string path = AssetDatabase.GetAssetPath(cat);
                    if (!string.IsNullOrEmpty(path))
                        TryMakeBak(path);
                }
            }

            // Apply edits grouped by catalog
            foreach (var grp in selected.GroupBy(s => s.catalog))
            {
                var cat = grp.Key;
                if (!cat || cat.items == null) continue;

                Undo.RecordObject(cat, "Batch Edit Upgrades");

                foreach (var it in grp)
                {
                    if (it.itemIndex < 0 || it.itemIndex >= cat.items.Count) continue;
                    var item = cat.items[it.itemIndex];
                    if (item == null) continue;

                    if (_batchSetCost) item.cost = Mathf.Max(0, _batchCost);
                    if (_batchSetMaxLevel) item.maxLevel = Mathf.Max(1, _batchMaxLevel);
                }

                EditorUtility.SetDirty(cat);
            }

            AssetDatabase.SaveAssets();
            Refresh();

            _validation.Add($"Applied batch changes to {selected.Count} item(s).");
        }

        private void DrawTools()
        {
            EditorGUILayout.BeginVertical();

            DrawEffectsDbPicker();

            GUILayout.Label("Tools", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope("box"))
            {
                _dryRun = EditorGUILayout.ToggleLeft("Dry Run (preview only)", _dryRun);
                _makeBak = EditorGUILayout.ToggleLeft("Create .bak file for modified catalogs", _makeBak);

                EditorGUILayout.Space(8);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Validate All", GUILayout.Width(120)))
                        RunValidation();

                    if (GUILayout.Button("Normalize IDs (trim)", GUILayout.Width(160)))
                        NormalizeIds();

                    if (GUILayout.Button("Create Missing Effect Entries", GUILayout.Width(220)))
                        CreateMissingEffectEntries();
                }

                SmallInfo("Validate checks: empty ids, duplicate save keys, duplicates inside a catalog, negative cost, maxLevel < 1.");
            }

            EditorGUILayout.Space(8);

            _toolsScroll = EditorGUILayout.BeginScrollView(_toolsScroll);
            if (_validation.Count == 0)
            {
                EditorGUILayout.HelpBox("No tool output yet. Run Validate or other actions above.", MessageType.Info);
            }
            else
            {
                foreach (var line in _validation)
                    EditorGUILayout.LabelField(line, EditorStyles.wordWrappedLabel);
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.EndVertical();
        }

        // ---------------- Effects DB UI ----------------

        private void DrawEffectsDbPicker()
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Effects Database (optional)", EditorStyles.boldLabel);

                var newDb = (UpgradeEffectsDatabase)EditorGUILayout.ObjectField("Database Asset", _effectsDb, typeof(UpgradeEffectsDatabase), false);
                if (newDb != _effectsDb)
                {
                    _effectsDb = newDb;
                    _effectsSO = _effectsDb ? new SerializedObject(_effectsDb) : null;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Find Existing", GUILayout.Width(100)))
                    {
                        _effectsDb = null;
                        _effectsSO = null;
                        FindEffectsDatabaseIfAny();
                    }

                    if (GUILayout.Button("Create New", GUILayout.Width(100)))
                        CreateEffectsDatabaseAsset();

                    GUILayout.FlexibleSpace();

                    if (_effectsDb && GUILayout.Button("Open", GUILayout.Width(60)))
                        Selection.activeObject = _effectsDb;
                }

                SmallInfo("This is where you store upgrade STAT VALUES + RANGES (your 'database-like' part).");
            }
        }

        private void DrawEffectsEditor(string saveKey, int itemMaxLevel)
        {
            if (!_effectsDb)
            {
                EditorGUILayout.HelpBox("No UpgradeEffectsDatabase assigned. If you want per-upgrade stat values + ranges, create/select one above.", MessageType.Info);
                return;
            }

            if (_effectsSO == null) _effectsSO = new SerializedObject(_effectsDb);
            _effectsSO.Update();

            var entriesProp = _effectsSO.FindProperty("entries");
            int entryIndex = FindEntryIndex(entriesProp, saveKey);

            GUILayout.Label("Upgrade Effects (optional)", EditorStyles.boldLabel);

            if (entryIndex < 0)
            {
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    EditorGUILayout.LabelField("No effects entry exists for this upgrade yet.");
                    EditorGUILayout.LabelField("Save Key", saveKey);

                    if (GUILayout.Button("Create Effects Entry"))
                    {
                        Undo.RecordObject(_effectsDb, "Create Upgrade Effects Entry");
                        int newIndex = entriesProp.arraySize;
                        entriesProp.InsertArrayElementAtIndex(newIndex);

                        var e = entriesProp.GetArrayElementAtIndex(newIndex);
                        e.FindPropertyRelative("saveKey").stringValue = saveKey;
                        e.FindPropertyRelative("notes").stringValue = "";

                        _effectsSO.ApplyModifiedProperties();
                        EditorUtility.SetDirty(_effectsDb);
                        AssetDatabase.SaveAssets();
                    }

                    SmallInfo("Effects formula: final = baseValue + perLevel * level (clamped if enabled).");
                }

                _effectsSO.ApplyModifiedProperties();
                return;
            }

            var entryProp = entriesProp.GetArrayElementAtIndex(entryIndex);
            var notesProp = entryProp.FindPropertyRelative("notes");
            var effectsProp = entryProp.FindPropertyRelative("effects");

            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Save Key", saveKey);

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(notesProp);
                bool changedNotes = EditorGUI.EndChangeCheck();
                if (changedNotes)
                {
                    Undo.RecordObject(_effectsDb, "Edit Effects Notes");
                    _effectsSO.ApplyModifiedProperties();
                    EditorUtility.SetDirty(_effectsDb);
                }

                EditorGUILayout.Space(6);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("+ Add Effect", GUILayout.Width(100)))
                    {
                        Undo.RecordObject(_effectsDb, "Add Upgrade Effect");
                        int n = effectsProp.arraySize;
                        effectsProp.InsertArrayElementAtIndex(n);
                        var fx = effectsProp.GetArrayElementAtIndex(n);
                        fx.FindPropertyRelative("statKey").stringValue = "";
                        fx.FindPropertyRelative("baseValue").floatValue = 0f;
                        fx.FindPropertyRelative("perLevel").floatValue = 0f;
                        fx.FindPropertyRelative("clamp").boolValue = false;
                        fx.FindPropertyRelative("min").floatValue = 0f;
                        fx.FindPropertyRelative("max").floatValue = 999999f;

                        _effectsSO.ApplyModifiedProperties();
                        EditorUtility.SetDirty(_effectsDb);
                    }

                    if (GUILayout.Button("Remove Entry", GUILayout.Width(110)))
                    {
                        if (EditorUtility.DisplayDialog("Remove effects entry?",
                                "This removes the effects definition only (not the shop item).",
                                "Remove", "Cancel"))
                        {
                            Undo.RecordObject(_effectsDb, "Remove Upgrade Effects Entry");
                            entriesProp.DeleteArrayElementAtIndex(entryIndex);
                            _effectsSO.ApplyModifiedProperties();
                            EditorUtility.SetDirty(_effectsDb);
                            AssetDatabase.SaveAssets();
                            return;
                        }
                    }

                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField($"MaxLevel (from item): {itemMaxLevel}", GUILayout.Width(150));
                }

                EditorGUILayout.Space(6);

                // List effects
                for (int i = 0; i < effectsProp.arraySize; i++)
                {
                    var fx = effectsProp.GetArrayElementAtIndex(i);
                    var keyProp = fx.FindPropertyRelative("statKey");
                    var baseProp = fx.FindPropertyRelative("baseValue");
                    var perProp = fx.FindPropertyRelative("perLevel");
                    var clampProp = fx.FindPropertyRelative("clamp");
                    var minProp = fx.FindPropertyRelative("min");
                    var maxProp = fx.FindPropertyRelative("max");

                    using (new EditorGUILayout.VerticalScope("box"))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField($"Effect #{i + 1}", EditorStyles.boldLabel);

                            GUILayout.FlexibleSpace();

                            if (GUILayout.Button("Up", GUILayout.Width(40)) && i > 0)
                            {
                                Undo.RecordObject(_effectsDb, "Move Effect Up");
                                effectsProp.MoveArrayElement(i, i - 1);
                                _effectsSO.ApplyModifiedProperties();
                                EditorUtility.SetDirty(_effectsDb);
                                break;
                            }

                            if (GUILayout.Button("Dn", GUILayout.Width(40)) && i < effectsProp.arraySize - 1)
                            {
                                Undo.RecordObject(_effectsDb, "Move Effect Down");
                                effectsProp.MoveArrayElement(i, i + 1);
                                _effectsSO.ApplyModifiedProperties();
                                EditorUtility.SetDirty(_effectsDb);
                                break;
                            }

                            if (GUILayout.Button("X", GUILayout.Width(28)))
                            {
                                Undo.RecordObject(_effectsDb, "Remove Upgrade Effect");
                                effectsProp.DeleteArrayElementAtIndex(i);
                                _effectsSO.ApplyModifiedProperties();
                                EditorUtility.SetDirty(_effectsDb);
                                break;
                            }
                        }

                        EditorGUI.BeginChangeCheck();

                        EditorGUILayout.PropertyField(keyProp, new GUIContent("statKey"));
                        EditorGUILayout.PropertyField(baseProp, new GUIContent("baseValue (lvl0)"));
                        EditorGUILayout.PropertyField(perProp, new GUIContent("perLevel"));

                        EditorGUILayout.PropertyField(clampProp, new GUIContent("Clamp"));
                        using (new EditorGUI.DisabledScope(!clampProp.boolValue))
                        {
                            EditorGUILayout.PropertyField(minProp);
                            EditorGUILayout.PropertyField(maxProp);

                            // keep min <= max
                            if (minProp.floatValue > maxProp.floatValue)
                                maxProp.floatValue = minProp.floatValue;
                        }

                        bool fxChanged = EditorGUI.EndChangeCheck();
                        if (fxChanged)
                        {
                            Undo.RecordObject(_effectsDb, "Edit Upgrade Effect");
                            _effectsSO.ApplyModifiedProperties();
                            EditorUtility.SetDirty(_effectsDb);
                        }

                        // Preview
                        float v0 = Eval(baseProp.floatValue, perProp.floatValue, clampProp.boolValue, minProp.floatValue, maxProp.floatValue, 0);
                        float v1 = Eval(baseProp.floatValue, perProp.floatValue, clampProp.boolValue, minProp.floatValue, maxProp.floatValue, 1);
                        float vMax = Eval(baseProp.floatValue, perProp.floatValue, clampProp.boolValue, minProp.floatValue, maxProp.floatValue, itemMaxLevel);

                        EditorGUILayout.LabelField("Preview (base + perLevel*level)", $"lvl0={v0}   lvl1={v1}   lvl{itemMaxLevel}={vMax}");
                    }
                }
            }

            _effectsSO.ApplyModifiedProperties();
        }

        // ---------------- Tools actions ----------------

        private void RunValidation()
        {
            _validation.Clear();

            var seenSaveKeys = new HashSet<string>(StringComparer.Ordinal);

            foreach (var c in _catalogs)
            {
                if (!c.catalog) continue;

                if (string.IsNullOrWhiteSpace(c.catalog.catalogId))
                    _validation.Add($"[WARN] Catalog '{c.catalog.name}' has empty catalogId (save keys may collide).");

                var seenItemIds = new HashSet<string>(StringComparer.Ordinal);

                var items = c.catalog.items;
                if (items == null) continue;

                for (int i = 0; i < items.Count; i++)
                {
                    var it = items[i];
                    if (it == null) continue;

                    if (string.IsNullOrWhiteSpace(it.id))
                    {
                        _validation.Add($"[ERROR] Catalog '{c.catalog.name}' item index {i} has EMPTY id.");
                        continue;
                    }

                    if (!seenItemIds.Add(it.id))
                        _validation.Add($"[ERROR] Duplicate item id '{it.id}' inside catalog '{c.catalog.name}'.");

                    string key = BuildSaveKey(c.catalog.catalogId, it.id);
                    if (!seenSaveKeys.Add(key))
                        _validation.Add($"[ERROR] Duplicate SAVE KEY '{key}' across catalogs/items (this WILL collide in saves).");

                    if (it.cost < 0)
                        _validation.Add($"[WARN] {key} has cost < 0 (will be clamped).");

                    if (it.maxLevel < 1)
                        _validation.Add($"[WARN] {key} has maxLevel < 1 (will be clamped).");
                }
            }

            if (_validation.Count == 0)
                _validation.Add("No problems found.");
        }

        private void NormalizeIds()
        {
            _validation.Clear();

            // Backup all catalogs if desired
            if (_makeBak)
            {
                foreach (var c in _catalogs)
                {
                    if (!c.catalog) continue;
                    string p = AssetDatabase.GetAssetPath(c.catalog);
                    if (!string.IsNullOrEmpty(p))
                        TryMakeBak(p);
                }
            }

            int changed = 0;

            foreach (var c in _catalogs)
            {
                if (!c.catalog || c.catalog.items == null) continue;

                Undo.RecordObject(c.catalog, "Normalize Catalog IDs");

                if (!string.IsNullOrWhiteSpace(c.catalog.catalogId))
                {
                    string trimmed = c.catalog.catalogId.Trim();
                    if (trimmed != c.catalog.catalogId)
                    {
                        c.catalog.catalogId = trimmed;
                        changed++;
                    }
                }

                foreach (var it in c.catalog.items)
                {
                    if (it == null) continue;
                    if (it.id == null) continue;

                    string trimmed = it.id.Trim();
                    if (trimmed != it.id)
                    {
                        it.id = trimmed;
                        changed++;
                    }
                }

                EditorUtility.SetDirty(c.catalog);
            }

            AssetDatabase.SaveAssets();
            Refresh();

            _validation.Add($"Normalized ids (trimmed whitespace). Changes: {changed}");
        }

        private void CreateMissingEffectEntries()
        {
            _validation.Clear();

            if (!_effectsDb)
            {
                _validation.Add("No UpgradeEffectsDatabase assigned. Create/select one first.");
                return;
            }

            if (_effectsSO == null) _effectsSO = new SerializedObject(_effectsDb);
            _effectsSO.Update();

            var entriesProp = _effectsSO.FindProperty("entries");

            int created = 0;

            foreach (var it in EnumerateAllItems())
            {
                int idx = FindEntryIndex(entriesProp, it.saveKey);
                if (idx >= 0) continue;

                if (_dryRun)
                {
                    _validation.Add($"[DRY] Would create effects entry for {it.saveKey}");
                    continue;
                }

                Undo.RecordObject(_effectsDb, "Create Missing Effects Entries");

                int newIndex = entriesProp.arraySize;
                entriesProp.InsertArrayElementAtIndex(newIndex);
                var e = entriesProp.GetArrayElementAtIndex(newIndex);
                e.FindPropertyRelative("saveKey").stringValue = it.saveKey;
                e.FindPropertyRelative("notes").stringValue = "";
                created++;
            }

            if (!_dryRun)
            {
                _effectsSO.ApplyModifiedProperties();
                EditorUtility.SetDirty(_effectsDb);
                AssetDatabase.SaveAssets();
            }

            _validation.Add(_dryRun
                ? "Dry Run complete."
                : $"Created {created} missing effect entries.");
        }

        // ---------------- Helpers ----------------

        private static void DrawItemField(SerializedProperty itemProp, string relativeName)
        {
            var p = itemProp.FindPropertyRelative(relativeName);
            if (p != null)
                EditorGUILayout.PropertyField(p);
        }

        private IEnumerable<ItemRef> EnumerateAllItems()
        {
            foreach (var c in _catalogs)
            {
                if (!c.catalog || c.catalog.items == null) continue;
                for (int i = 0; i < c.catalog.items.Count; i++)
                {
                    var item = c.catalog.items[i];
                    if (item == null) continue;

                    yield return new ItemRef
                    {
                        catalogGuid = c.guid,
                        catalog = c.catalog,
                        catalogPath = c.path,
                        itemIndex = i,
                        itemId = item.id,
                        title = item.title,
                        saveKey = BuildSaveKey(c.catalog.catalogId, item.id),
                    };
                }
            }
        }

        private IEnumerable<ItemRef> EnumerateFilteredItems()
        {
            foreach (var it in EnumerateAllItems())
            {
                if (ItemMatchesSearch(it.catalog, it.catalog.items[it.itemIndex]))
                    yield return it;
            }
        }

        private IEnumerable<ItemRef> EnumerateSelectedItems()
        {
            foreach (var it in EnumerateAllItems())
            {
                if (_selectedSaveKeys.Contains(it.saveKey))
                    yield return it;
            }
        }

        private bool CatalogOrAnyItemMatchesSearch(ShopCatalog cat)
        {
            if (cat == null) return false;
            if (string.IsNullOrWhiteSpace(_search)) return true;

            string s = _search.Trim();
            if (string.IsNullOrEmpty(s)) return true;

            if (Contains(cat.catalogId, s) || Contains(cat.title, s) || Contains(cat.name, s))
                return true;

            if (cat.items == null) return false;

            foreach (var it in cat.items)
            {
                if (it == null) continue;
                if (ItemMatchesSearch(cat, it)) return true;
            }

            return false;
        }

        private bool ItemMatchesSearch(ShopCatalog cat, ShopCatalogItem item)
        {
            if (item == null) return false;
            if (string.IsNullOrWhiteSpace(_search)) return true;

            string s = _search.Trim();
            if (string.IsNullOrEmpty(s)) return true;

            if (Contains(item.id, s) || Contains(item.title, s) || Contains(item.description, s))
                return true;

            if (cat != null)
            {
                if (Contains(cat.catalogId, s) || Contains(cat.title, s))
                    return true;
            }

            // also match saveKey
            string key = BuildSaveKey(cat != null ? cat.catalogId : "", item.id);
            if (Contains(key, s))
                return true;

            return false;
        }

        private static bool Contains(string hay, string needle)
        {
            if (string.IsNullOrEmpty(hay) || string.IsNullOrEmpty(needle)) return false;
            return hay.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string Safe(string s, string fallback)
            => string.IsNullOrWhiteSpace(s) ? fallback : s;

        private static string BuildSaveKey(string catalogId, string itemId)
        {
            catalogId = string.IsNullOrWhiteSpace(catalogId) ? "catalog" : catalogId.Trim();
            itemId = string.IsNullOrWhiteSpace(itemId) ? "item" : itemId.Trim();
            return $"shop:{catalogId}:{itemId}";
        }

        private static float Eval(float baseValue, float perLevel, bool clamp, float min, float max, int level)
        {
            level = Mathf.Max(0, level);
            float v = baseValue + perLevel * level;
            if (clamp) v = Mathf.Clamp(v, min, max);
            return v;
        }

        private static int FindEntryIndex(SerializedProperty entriesProp, string saveKey)
        {
            if (entriesProp == null || !entriesProp.isArray) return -1;

            for (int i = 0; i < entriesProp.arraySize; i++)
            {
                var e = entriesProp.GetArrayElementAtIndex(i);
                var k = e.FindPropertyRelative("saveKey");
                if (k != null && string.Equals(k.stringValue, saveKey, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        private static void SmallInfo(string text)
        {
            var old = EditorStyles.miniLabel.wordWrap;
            EditorStyles.miniLabel.wordWrap = true;
            GUILayout.Label(text, EditorStyles.miniLabel);
            EditorStyles.miniLabel.wordWrap = old;
        }

        private void TryMakeBak(string assetPath)
        {
            try
            {
                string full = Path.GetFullPath(assetPath);
                string bak = full + ".bak";

                File.Copy(full, bak, overwrite: true);
                _validation.Add($"[bak] {assetPath} -> {assetPath}.bak");
            }
            catch (Exception ex)
            {
                _validation.Add($"[bak ERROR] {assetPath}: {ex.Message}");
            }
        }

        private void CreateCatalogAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create ShopCatalog",
                "ShopCatalog_New",
                "asset",
                "Choose where to save the catalog asset.");

            if (string.IsNullOrEmpty(path))
                return;

            var cat = CreateInstance<ShopCatalog>();
            AssetDatabase.CreateAsset(cat, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = cat;
            EditorGUIUtility.PingObject(cat);
            Refresh();
        }

        private void CreateEffectsDatabaseAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create UpgradeEffectsDatabase",
                "UpgradeEffectsDatabase",
                "asset",
                "Tip: put it under Assets/Resources/ so UpgradeService can auto-load it.");

            if (string.IsNullOrEmpty(path))
                return;

            var db = CreateInstance<UpgradeEffectsDatabase>();
            AssetDatabase.CreateAsset(db, path);
            AssetDatabase.SaveAssets();

            _effectsDb = db;
            _effectsSO = new SerializedObject(_effectsDb);

            Selection.activeObject = db;
            EditorGUIUtility.PingObject(db);

            Refresh();
        }
private static int AddNewItem(ShopCatalog cat)
{
    if (!cat) return -1;
    if (cat.items == null) cat.items = new List<ShopCatalogItem>();

    Undo.RecordObject(cat, "Add Upgrade Item");

    string baseId = "New_Upgrade";
    string id = baseId;
    int n = 1;

    var existing = new HashSet<string>(StringComparer.Ordinal);
    foreach (var it in cat.items)
    {
        if (it == null) continue;
        if (string.IsNullOrWhiteSpace(it.id)) continue;
        existing.Add(it.id);
    }

    while (existing.Contains(id))
        id = $"{baseId}_{n++}";

    var item = new ShopCatalogItem
    {
        id = id,
        title = "New Upgrade",
        description = "",
        icon = null,
        cost = 100,
        maxLevel = 10
    };

    cat.items.Add(item);
    EditorUtility.SetDirty(cat);
    AssetDatabase.SaveAssets();

    return cat.items.Count - 1;
}

        private static void DuplicateItem(ShopCatalog cat, int index)
        {
            
            if (!cat || cat.items == null) return;
            if (index < 0 || index >= cat.items.Count) return;

            var src = cat.items[index];
            if (src == null) return;

            Undo.RecordObject(cat, "Duplicate Upgrade Item");

            var copy = new ShopCatalogItem
            {
                id = (src.id ?? "item") + "_copy",
                title = src.title,
                description = src.description,
                icon = src.icon,
                cost = Mathf.Max(0, src.cost),
                maxLevel = Mathf.Max(1, src.maxLevel),
            };

            cat.items.Insert(index + 1, copy);
            EditorUtility.SetDirty(cat);
            AssetDatabase.SaveAssets();
        }

        private static void DeleteItem(ShopCatalog cat, int index)
        {
            if (!cat || cat.items == null) return;
            if (index < 0 || index >= cat.items.Count) return;

            Undo.RecordObject(cat, "Delete Upgrade Item");
            cat.items.RemoveAt(index);
            EditorUtility.SetDirty(cat);
            AssetDatabase.SaveAssets();
        }
    }
}
#endif
