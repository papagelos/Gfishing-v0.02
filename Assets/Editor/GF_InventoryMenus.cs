#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Linq;
using GalacticFishing;

public static class GF_InventoryMenus
{
    const string MenuGF = "Galactic Fishing/Cheats/Inventory/";

    [MenuItem(MenuGF + "Show All Fish (dim zeros)")]
    public static void ShowAllFishDimZeros()
    {
        var reg = EnsureInit();
        if (!reg) { Debug.LogError("[GF] No FishRegistry found."); return; }
        var grid = Object.FindFirstObjectByType<InventoryGridController>();
        if (!grid)
        {
            Debug.LogError("[GF] No InventoryGridController in scene. Wire your grid first.");
            return;
        }

        grid.FishRegistry = reg;
        grid.HideZeroItems = false;
        grid.DimZeroInsteadOfHide = true;
        grid.Populate();
        EditorUtility.SetDirty(grid);
        TryForceIcons(false);
        NudgeUI(reg);

        Debug.Log($"[GF] Showing {reg.fishes.Count} fish (zero counts are dimmed).");
    }

    [MenuItem(MenuGF + "Add 1 of Every Fish")]
    public static void AddOneOfEveryFish()
    {
        var reg = EnsureInit();
        if (!reg) { Debug.LogError("[GF] No FishRegistry found."); return; }

        for (int i = 0; i < reg.fishes.Count; i++)
            InventoryService.Set(i, 1);

        var grid = Object.FindFirstObjectByType<InventoryGridController>();
        if (grid)
        {
            grid.FishRegistry = reg;
            grid.Populate();
            EditorUtility.SetDirty(grid);
        }
        TryForceIcons(true);
        NudgeUI(reg);
        Debug.Log($"[GF] Set count=1 for all {reg.fishes.Count} fish.");
    }

    [MenuItem(MenuGF + "Clear Inventory")]
    public static void ClearInventory()
    {
        var reg = EnsureInit();
        if (!reg) { Debug.LogError("[GF] No FishRegistry found."); return; }

        InventoryService.ClearAll();
        var grid = Object.FindFirstObjectByType<InventoryGridController>();
        if (grid)
        {
            grid.FishRegistry = reg;
            grid.Populate();
            EditorUtility.SetDirty(grid);
        }
        TryForceIcons(false);
        NudgeUI(reg);
        Debug.Log("[GF] Inventory cleared.");
    }

    static FishRegistry EnsureInit()
    {
        var reg = Object.FindFirstObjectByType<FishRegistry>();
        if (!reg)
        {
            var guid = AssetDatabase.FindAssets("t:FishRegistry").FirstOrDefault();
            if (!string.IsNullOrEmpty(guid))
                reg = AssetDatabase.LoadAssetAtPath<FishRegistry>(AssetDatabase.GUIDToAssetPath(guid));
        }
        if (reg && !InventoryService.IsInitialized)
            InventoryService.Initialize(reg);
        return reg;
    }

    static void TryForceIcons(bool force)
    {
        var fixer = Object.FindFirstObjectByType<InventoryGridForceIcons>();
        if (!fixer) return;
        if (force) fixer.ApplyForce(); else fixer.Apply();
    }

    static void NudgeUI(FishRegistry reg)
    {
        if (reg && reg.fishes.Count > 0)
            InventoryService.Add(0, 0); // no-op to trigger OnChanged event
    }
}
#endif
