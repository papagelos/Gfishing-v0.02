using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using GalacticFishing;

public static class GF_BuildFishMetaIndex
{
    const string DefaultAssetPath = "Assets/Data/FishMetaIndex.asset";

    [MenuItem("GF/Build/Fish Meta Index")]
    public static void Build()
    {
        var metaGuids = AssetDatabase.FindAssets("t:FishMeta");
        var metas = metaGuids.Select(g => AssetDatabase.LoadAssetAtPath<FishMeta>(AssetDatabase.GUIDToAssetPath(g)))
                             .Where(m => m != null).ToList();
        var fishGuids = AssetDatabase.FindAssets("t:Fish");
        var fishes = fishGuids.Select(g => AssetDatabase.LoadAssetAtPath<Fish>(AssetDatabase.GUIDToAssetPath(g)))
                              .Where(f => f != null).ToList();

        var index = AssetDatabase.LoadAssetAtPath<FishMetaIndex>(DefaultAssetPath);
        if (!index)
        {
            var dir = System.IO.Path.GetDirectoryName(DefaultAssetPath);
            if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
            index = ScriptableObject.CreateInstance<FishMetaIndex>();
            AssetDatabase.CreateAsset(index, DefaultAssetPath);
        }

        string Slug(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            var builder = new StringBuilder(s.Length);
            foreach (var ch in s)
            {
                if (char.IsLetterOrDigit(ch)) builder.Append(char.ToLowerInvariant(ch));
            }
            var result = builder.ToString();
            if (result.StartsWith("fish")) result = result.Substring(4);
            return result;
        }
        string DisplayOrName(UnityEngine.Object o, string fallback = null)
        {
            var dn = TryGetDisplayName(o);
            if (!string.IsNullOrWhiteSpace(dn)) return dn;
            return !string.IsNullOrWhiteSpace(fallback) ? fallback : o.name;
        }

        var metaBySlug = metas
            .GroupBy(m => Slug(DisplayOrName(m)))
            .ToDictionary(g => g.Key, g => g.First());

        index.entries.Clear();
        int paired = 0;
        foreach (var fish in fishes)
        {
            var fishDisplay = DisplayOrName(fish, fish.name);
            var fishSlug = Slug(fishDisplay);
            if (metaBySlug.TryGetValue(fishSlug, out var meta))
            {
                index.entries.Add(new FishMetaIndex.Entry
                {
                    fish = fish,
                    meta = meta,
                    key = DisplayOrName(meta)
                });
                paired++;
            }
        }

        var pairedMetas = new HashSet<FishMeta>(index.entries.Where(e => e.meta).Select(e => e.meta));
        foreach (var meta in metas)
        {
            if (pairedMetas.Contains(meta)) continue;
            index.entries.Add(new FishMetaIndex.Entry
            {
                fish = null,
                meta = meta,
                key = DisplayOrName(meta)
            });
        }

        EditorUtility.SetDirty(index);
        AssetDatabase.SaveAssets();
        index.BuildMap();
        Debug.Log($"[GF] Fish Meta Index built at {DefaultAssetPath} with {index.entries.Count} entries (paired {paired} Fish→Meta).");
        Selection.activeObject = index;
    }

    // -------- helpers: tolerant reflection (field/property/nested) --------
    static string TryGetDisplayName(object obj)
    {
        if (obj == null) return null;
        // direct: DisplayName / displayName
        var s = TryGetStringCI(obj, "DisplayName") ?? TryGetStringCI(obj, "displayName");
        if (!string.IsNullOrWhiteSpace(s)) return s;
        // nested containers commonly used by your assets
        foreach (var c in new[] { "Identity", "identity", "meta", "def", "definition", "data", "info" })
        {
            var container = TryGetMemberValueCI(obj, c);
            s = TryGetStringCI(container, "DisplayName") ?? TryGetStringCI(container, "displayName");
            if (!string.IsNullOrWhiteSpace(s)) return s;
        }
        return null;
    }
    static string TryGetStringCI(object obj, string name)
    {
        var v = TryGetMemberValueCI(obj, name);
        return v as string;
    }
    static object TryGetMemberValueCI(object obj, string name)
    {
        if (obj == null || string.IsNullOrEmpty(name)) return null;
        var t = obj.GetType();
        const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase;
        var p = t.GetProperty(name, BF);
        if (p != null && p.CanRead) return p.GetValue(obj, null);
        var f = t.GetField(name, BF);
        if (f != null) return f.GetValue(obj);
        return null;
    }
}
