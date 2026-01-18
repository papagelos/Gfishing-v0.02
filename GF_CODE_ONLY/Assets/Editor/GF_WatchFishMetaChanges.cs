using UnityEditor;
using UnityEngine;
using System.Linq;
using System;

// Rebuild FishMetaIndex whenever FishMeta assets change (import/move/delete/rename).
// Plays nice with other builders; it's quick and throttled.
internal sealed class GF_WatchFishMetaChanges : AssetPostprocessor
{
    static double _nextAllowed;

    static void DebouncedBuild()
    {
        var now = EditorApplication.timeSinceStartup;
        if (now < _nextAllowed) return;
        _nextAllowed = now + 0.5; // throttle burst imports
        EditorApplication.delayCall += () =>
        {
            try { GF_BuildFishMetaIndex.Build(); } catch (Exception) { /* ignore compile-order hiccups */ }
        };
    }

    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        if (TouchesFishMeta(imported) || TouchesFishMeta(deleted) || TouchesFishMeta(moved) || TouchesFishMeta(movedFrom))
            DebouncedBuild();
    }

    static bool TouchesFishMeta(string[] paths)
    {
        if (paths == null || paths.Length == 0) return false;
        foreach (var path in paths)
        {
            var type = AssetDatabase.GetMainAssetTypeAtPath(path);
            if (type != null)
            {
                // Avoid hard referencing FishMeta type; compare by name
                var name = type.Name;
                var full = type.FullName;
                bool isMeta = string.Equals(name, "FishMeta", StringComparison.Ordinal) ||
                              (full != null && full.EndsWith(".FishMeta", StringComparison.Ordinal));
                bool isFish = string.Equals(name, "Fish", StringComparison.Ordinal) ||
                              (full != null && full.EndsWith(".Fish", StringComparison.Ordinal));
                if (isMeta || isFish)
                    return true;
            }
        }
        return false;
    }
}
