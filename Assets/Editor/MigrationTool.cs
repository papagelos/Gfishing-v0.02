#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GalacticFishing.Minigames.Dungeon3D;
using UnityEditor;
using UnityEngine;

public static class MigrationTool
{
    private const string BiomesRoot = "Assets/Sprites/Biomes";
    private const string LegacyDungeonTilesFolder = "Assets/Sprites/Tiles/Dungeon";

    [MenuItem("Galactic Fishing/Utilities/Initialize Biome Folders")]
    public static void InitializeBiomeFolders()
    {
        DimensionGenProfile profile = FindPreferredProfile();
        if (profile == null)
        {
            Debug.LogError("[MigrationTool] No DimensionGenProfile asset found. Create/assign one first.");
            return;
        }

        EnsureFolder(BiomesRoot);

        var biomeSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (profile.biomeGroups != null)
        {
            for (int i = 0; i < profile.biomeGroups.Count; i++)
            {
                string biome = NormalizeBiome(profile.biomeGroups[i]);
                if (string.IsNullOrEmpty(biome) || !biomeSet.Add(biome))
                    continue;

                EnsureFolder($"{BiomesRoot}/{biome}/Tiles");
                EnsureFolder($"{BiomesRoot}/{biome}/Props");
            }
        }

        EnsureFolder($"{BiomesRoot}/GLOBAL/Props");

        int moved = 0;
        int skipped = 0;
        if (AssetDatabase.IsValidFolder(LegacyDungeonTilesFolder))
        {
            string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { LegacyDungeonTilesFolder });
            var texturePaths = textureGuids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();

            for (int i = 0; i < texturePaths.Count; i++)
            {
                string sourcePath = texturePaths[i];
                string fileName = Path.GetFileName(sourcePath);
                string raw = Path.GetFileNameWithoutExtension(sourcePath);
                string prefix = ExtractPrefix(raw);
                if (string.IsNullOrEmpty(prefix) || !biomeSet.Contains(prefix))
                {
                    skipped++;
                    continue;
                }

                string targetDir = $"{BiomesRoot}/{prefix}/Tiles";
                EnsureFolder(targetDir);
                string targetPath = $"{targetDir}/{fileName}";
                if (string.Equals(sourcePath, targetPath, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (File.Exists(targetPath))
                {
                    Debug.LogWarning($"[MigrationTool] Skip move, target already exists: {targetPath}");
                    skipped++;
                    continue;
                }

                string moveError = AssetDatabase.MoveAsset(sourcePath, targetPath);
                if (!string.IsNullOrEmpty(moveError))
                {
                    Debug.LogWarning($"[MigrationTool] Move failed '{sourcePath}' -> '{targetPath}': {moveError}");
                    skipped++;
                    continue;
                }

                moved++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[MigrationTool] Biome folders initialized from profile '{profile.name}'. Moved legacy tiles: {moved}, skipped: {skipped}.");
    }

    private static DimensionGenProfile FindPreferredProfile()
    {
        string[] guids = AssetDatabase.FindAssets("t:DimensionGenProfile");
        if (guids == null || guids.Length == 0)
            return null;

        var profiles = new List<DimensionGenProfile>();
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var profile = AssetDatabase.LoadAssetAtPath<DimensionGenProfile>(path);
            if (profile != null)
                profiles.Add(profile);
        }

        if (profiles.Count == 0)
            return null;

        for (int i = 0; i < profiles.Count; i++)
        {
            string name = profiles[i].name;
            if (name.IndexOf("survey", StringComparison.OrdinalIgnoreCase) >= 0)
                return profiles[i];
        }

        return profiles[0];
    }

    private static string ExtractPrefix(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
            return string.Empty;

        int underscore = rawName.IndexOf('_');
        string prefix = underscore > 0 ? rawName.Substring(0, underscore) : rawName;
        return NormalizeBiome(prefix);
    }

    private static string NormalizeBiome(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value.Trim().ToUpperInvariant();
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
}
#endif
