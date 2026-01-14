using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using GalacticFishing; // Fish, FishRegistry

/// <summary>
/// Persists InventoryService's counts between sessions (JSON).
/// Attach once in the scene (use your "Systems" object).
/// </summary>
public class InventoryCountsPersistence : MonoBehaviour
{
    [Header("Links")]
    [SerializeField] private FishRegistry registry;

    [Header("Options")]
    [SerializeField] private bool saveOnEveryChange = true;
    [SerializeField] private bool saveOnQuit = true;
    [SerializeField] private bool logs = false;

    static string SavePath => Path.Combine(Application.persistentDataPath, "inventory_counts_v1.json");

    [Serializable] class Entry { public string name; public string displayName; public int count; }
    [Serializable] class Blob { public int version = 1; public List<Entry> entries = new(); }

    bool _loading;

    void Awake()
    {
        // Make sure InventoryService is ready (same registry you use in your grid)
        if (registry && !InventoryService.IsInitialized)
            InventoryService.Initialize(registry);

        LoadCounts(); // safe if file missing
        InventoryService.OnChanged += OnInventoryChanged;
    }

    void OnDestroy()
    {
        InventoryService.OnChanged -= OnInventoryChanged;
    }

    void OnApplicationQuit()
    {
        if (saveOnQuit) SaveCounts();
    }

    void OnInventoryChanged()
    {
        if (_loading) return; // don't save while applying a load
        if (saveOnEveryChange) SaveCounts();
    }

    public void SaveCounts()
    {
        try
        {
            var blob = new Blob();
            foreach (var (id, fish, count) in InventoryService.All())
            {
                blob.entries.Add(new Entry {
                    name        = fish ? fish.name : "",
                    displayName = fish ? (string.IsNullOrEmpty(fish.displayName) ? "" : fish.displayName) : "",
                    count       = count
                });
            }
            var json = JsonUtility.ToJson(blob, true);
            Directory.CreateDirectory(Path.GetDirectoryName(SavePath));
            File.WriteAllText(SavePath, json);
            if (logs) Debug.Log($"[InventoryCountsPersistence] Saved {blob.entries.Count} entries → {SavePath}");
        }
        catch (Exception ex)
        {
            Debug.LogError("[InventoryCountsPersistence] Save failed: " + ex);
        }
    }

    public void LoadCounts()
    {
        _loading = true;
        try
        {
            if (!File.Exists(SavePath))
            {
                if (logs) Debug.Log("[InventoryCountsPersistence] No save file yet.");
                return;
            }

            var blob = JsonUtility.FromJson<Blob>(File.ReadAllText(SavePath));
            if (blob == null || blob.entries == null) return;

            // Build lookup by multiple keys (robust to renames)
            var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in blob.entries)
            {
                if (!string.IsNullOrWhiteSpace(e.name))        dict[Norm(e.name)] = e.count;
                if (!string.IsNullOrWhiteSpace(e.displayName)) dict[Norm(e.displayName)] = e.count;
            }

            // Apply to current registry order
            int applied = 0;
            for (int i = 0; i < InventoryService.FishTypeCount; i++)
            {
                var fish = InventoryService.GetFish(i);
                if (!fish) continue;

                // Try displayName first, then asset name
                int count = 0;
                bool hit = false;

                if (!string.IsNullOrWhiteSpace(fish.displayName))
                    hit = dict.TryGetValue(Norm(fish.displayName), out count);

                if (!hit)
                    hit = dict.TryGetValue(Norm(fish.name), out count);

                if (hit)
                {
                    InventoryService.Set(i, count);
                    applied++;
                }
                else
                {
                    // no saved value → keep whatever InventoryService already has (usually 0)
                }
            }

            if (logs) Debug.Log($"[InventoryCountsPersistence] Loaded {applied}/{InventoryService.FishTypeCount} entries from save.");
        }
        catch (Exception ex)
        {
            Debug.LogError("[InventoryCountsPersistence] Load failed: " + ex);
        }
        finally
        {
            _loading = false;
        }
    }

    static string Norm(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (char c in s)
            if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
        return sb.ToString();
    }
}
