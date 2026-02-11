#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace GalacticFishing.EditorTools
{
    public sealed partial class FishMetaBatchCustomWindow : EditorWindow
    {
        [MenuItem("Galactic Fishing/Data/Fish Meta Batch – Custom Fields", priority = 3003)]
        static void Open() => GetWindow<FishMetaBatchCustomWindow>("Fish Meta Batch (Custom)");

        private readonly List<GalacticFishing.FishMeta> _metas = new();
        private readonly List<bool> _sel = new();
        private Vector2 _left, _right;

        private string _fieldName = "quality";
        private string _status = "";

        // input cache
        private int _intVal;
        private float _floatVal;
        private bool _boolVal;
        private string _stringVal = "";

        private void OnEnable() => Rescan();

        void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Rescan", EditorStyles.toolbarButton, GUILayout.Width(70)))
                    Rescan();

                if (GUILayout.Button("Select All", EditorStyles.toolbarButton, GUILayout.Width(80)))
                    SetAll(true);

                if (GUILayout.Button("None", EditorStyles.toolbarButton, GUILayout.Width(60)))
                    SetAll(false);

                GUILayout.FlexibleSpace();
                GUILayout.Label($"{_metas.Count} FishMeta assets", EditorStyles.miniLabel);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawLeft();
                DrawRight();
            }

            if (!string.IsNullOrEmpty(_status))
                EditorGUILayout.HelpBox(_status, MessageType.Info);
        }

        void DrawLeft()
        {
            using (var s = new EditorGUILayout.ScrollViewScope(_left, GUILayout.Width(position.width * 0.4f)))
            {
                _left = s.scrollPosition;
                for (int i = 0; i < _metas.Count; i++)
                {
                    var fm = _metas[i];
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        _sel[i] = EditorGUILayout.Toggle(_sel[i], GUILayout.Width(18));
                        if (GUILayout.Button(fm ? fm.name : "(missing)", "Label"))
                            Selection.activeObject = fm;
                    }
                }
            }
        }

        void DrawRight()
        {
            using (var s = new EditorGUILayout.ScrollViewScope(_right))
            {
                _right = s.scrollPosition;

                EditorGUILayout.LabelField("Custom Field (from FishMeta partial/companion)", EditorStyles.boldLabel);
                _fieldName = EditorGUILayout.TextField("Field Name", _fieldName);

                // probe selected meta to infer type
                var probe = FirstSelected();
                FieldInfo fi = null;
                Type fType = null;
                if (probe != null && !string.IsNullOrWhiteSpace(_fieldName))
                {
                    fi = FindField(probe, _fieldName);
                    if (fi != null) fType = fi.FieldType;
                }

                if (fi == null)
                {
                    EditorGUILayout.HelpBox(
                        $"Type a serialized field name that exists on FishMeta (e.g., 'quality'). " +
                        $"Supported types: int, float, bool, string, enums.", MessageType.Warning);
                    return;
                }

                // value editor by type
                object newVal = null;
                if (fType == typeof(int))
                {
                    _intVal = EditorGUILayout.IntField("Set Value", _intVal);
                    newVal = _intVal;
                }
                else if (fType == typeof(float))
                {
                    _floatVal = EditorGUILayout.FloatField("Set Value", _floatVal);
                    newVal = _floatVal;
                }
                else if (fType == typeof(bool))
                {
                    _boolVal = EditorGUILayout.Toggle("Set Value", _boolVal);
                    newVal = _boolVal;
                }
                else if (fType == typeof(string))
                {
                    _stringVal = EditorGUILayout.TextField("Set Value", _stringVal);
                    newVal = _stringVal;
                }
                else if (fType.IsEnum)
                {
                    var boxed = Enum.ToObject(fType, 0);
                    boxed = EditorGUILayout.EnumPopup("Set Value", (Enum)boxed);
                    newVal = boxed;
                }
                else
                {
                    EditorGUILayout.HelpBox($"Field '{_fieldName}' is type '{fType.Name}', which this tool doesn't edit.", MessageType.Info);
                    return;
                }

                GUILayout.Space(6);
                if (GUILayout.Button("Apply to Selected", GUILayout.Height(28)))
                {
                    int changed = 0;
                    Undo.RecordObjects(_metas.ToArray(), "Batch set custom field");
                    for (int i = 0; i < _metas.Count; i++)
                    {
                        if (!_sel[i] || _metas[i] == null) continue;
                        var f = FindField(_metas[i], _fieldName);
                        if (f == null) continue;
                        f.SetValue(_metas[i], ConvertTo(newVal, f.FieldType));
                        EditorUtility.SetDirty(_metas[i]);
                        changed++;
                    }
                    AssetDatabase.SaveAssets();
                    _status = $"Applied to {changed} meta(s).";
                }
            }
        }

        static FieldInfo FindField(GalacticFishing.FishMeta meta, string name)
        {
            const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            // SerializeField allows private fields; public fields are also fine.
            var t = meta.GetType();
            var fi = t.GetField(name, BF);
            if (fi != null) return fi;

            // also consider backing fields with leading _
            fi = t.GetField($"_{name}", BF);
            return fi;
        }

        static object ConvertTo(object src, Type dst)
        {
            if (src == null) return null;
            if (dst.IsEnum) return Enum.ToObject(dst, System.Convert.ToInt32(src));
            return System.Convert.ChangeType(src, dst);
        }

        void Rescan()
        {
            _metas.Clear(); _sel.Clear();

            // Try direct t:FishMeta scan
            var guids = AssetDatabase.FindAssets("t:FishMeta", new[] { "Assets" });
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var fm = AssetDatabase.LoadAssetAtPath<GalacticFishing.FishMeta>(path);
                if (fm == null) continue;
                _metas.Add(fm); _sel.Add(false);
            }

            // Fallback — broad SO scan + filter
            if (_metas.Count == 0)
            {
                var broad = AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets" });
                foreach (var g in broad)
                {
                    var path = AssetDatabase.GUIDToAssetPath(g);
                    var so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                    if (so is GalacticFishing.FishMeta fm)
                    {
                        _metas.Add(fm); _sel.Add(false);
                    }
                }
            }

            Repaint();
        }

        void SetAll(bool v) { for (int i = 0; i < _sel.Count; i++) _sel[i] = v; }
        GalacticFishing.FishMeta FirstSelected()
        {
            for (int i = 0; i < _metas.Count; i++)
                if (_sel[i]) return _metas[i];
            return _metas.Count > 0 ? _metas[0] : null;
        }
    }
}
#endif
