using UnityEngine;
using GalacticFishing;

public class InventoryPlayBootstrap : MonoBehaviour
{
    [SerializeField] InventoryGridController grid;
    [SerializeField] FishRegistry registry;
    [SerializeField] bool seedAddOneOfEveryFish = true;
    [SerializeField] bool onlyOncePerSession = true;

    const string SeedKey = "GF_INV_SEEDED_SESSION";

    void Awake()
    {
        if (!Application.isPlaying) return;
        if (!grid) grid = GetComponent<InventoryGridController>();
        if (!registry) registry = grid && grid.FishRegistry ? grid.FishRegistry : FindFirstObjectByType<FishRegistry>();
        if (!registry) { Debug.LogWarning("[InventoryPlayBootstrap] No FishRegistry found in scene."); return; }

        if (!InventoryService.IsInitialized) InventoryService.Initialize(registry);

        if (seedAddOneOfEveryFish && (!onlyOncePerSession || !PlayerPrefs.HasKey(SeedKey)))
        {
            for (int i = 0; i < registry.fishes.Count; i++)
                InventoryService.Set(i, 1);
            if (onlyOncePerSession)
                PlayerPrefs.SetInt(SeedKey, 1);
        }

        if (grid)
        {
            grid.FishRegistry = registry;
            grid.Populate();
        }

        var fixer = GetComponent<InventoryGridForceIcons>() ?? FindFirstObjectByType<InventoryGridForceIcons>();
        if (fixer) fixer.ApplyForce();
    }
}
