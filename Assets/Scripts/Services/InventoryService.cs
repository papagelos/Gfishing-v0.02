// Assets/Scripts/Services/InventoryService.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using GalacticFishing; // Fish, FishRegistry

/// <summary>
/// Simple, scene-agnostic inventory for fish counts.
/// ID = index in FishRegistry.fishes (stable order from your ScriptableObject).
/// </summary>
public static class InventoryService
{
    public static event Action OnChanged;

    static FishRegistry _registry;
    static int[] _counts;
    static bool _initialized;

    /// <summary>Call once (e.g., from InventoryGridController or boot strap) to wire the registry.</summary>
    public static void Initialize(FishRegistry registry)
    {
        if (registry == null)
        {
            Debug.LogError("[InventoryService] Initialize() called with null registry.");
            return;
        }
        if (_initialized && ReferenceEquals(registry, _registry)) return;

        _registry = registry;
        _counts = new int[_registry.fishes.Count];
        _initialized = true;

        OnChanged?.Invoke();
    }

    public static bool IsInitialized => _initialized && _registry != null;
    public static int FishTypeCount => _counts?.Length ?? 0;

    public static Fish GetFish(int fishId)
    {
        if (!IsInitialized || fishId < 0 || fishId >= _registry.fishes.Count) return null;
        return _registry.fishes[fishId];
    }

    public static int GetId(Fish fish)
    {
        if (!IsInitialized || fish == null) return -1;
        return _registry.fishes.IndexOf(fish);
    }

    public static int GetCount(int fishId)
    {
        if (!IsInitialized || fishId < 0 || fishId >= _counts.Length) return 0;
        return _counts[fishId];
    }

    public static void Set(int fishId, int amount)
    {
        if (!IsInitialized || fishId < 0 || fishId >= _counts.Length) return;
        _counts[fishId] = Mathf.Max(0, amount);
        OnChanged?.Invoke();
    }

    public static void Add(int fishId, int amount)
    {
        if (!IsInitialized || fishId < 0 || fishId >= _counts.Length) return;
        long newVal = (long)_counts[fishId] + amount;
        _counts[fishId] = (int)Mathf.Clamp(newVal, 0, int.MaxValue);
        OnChanged?.Invoke();
    }

    // Convenience overloads:
    public static void Add(Fish fish, int amount)
    {
        int id = GetId(fish);
        if (id >= 0) Add(id, amount);
    }

    public static IEnumerable<(int id, Fish fish, int count)> All()
    {
        if (!IsInitialized) yield break;
        for (int i = 0; i < _registry.fishes.Count; i++)
            yield return (i, _registry.fishes[i], _counts[i]);
    }

    public static void ClearAll()
    {
        if (!IsInitialized) return;
        Array.Clear(_counts, 0, _counts.Length);
        OnChanged?.Invoke();
    }
}
