using System.Collections.Generic;
using GalacticFishing.Minigames.HexWorld;
using GalacticFishing.Progress;
using GalacticFishing.Story;
using UnityEngine;

namespace GalacticFishing
{
    /// <summary>
    /// Monitors catch rewards and triggers lake-clear milestones once all unique species
    /// in the active lake pool have been caught at least once.
    /// </summary>
    public sealed class AIStoryLakeMonitor : MonoBehaviour
    {
        private const string Lake1ClearStoryId = "Museum_Enrollment_Lake1";
        private const string Lake1SealGrantToken = "Milestone_MuseumEnrollmentSeal";
        private const string Lake1SealPendingToken = "Pending_MuseumEnrollmentSeal";

        [Header("Wiring")]
        [SerializeField] private WorldManager worldManager;

        [Header("Debug")]
        [SerializeField] private bool debugLogs = false;

        private void Awake()
        {
            if (!worldManager)
                worldManager = FindFirstObjectByType<WorldManager>();
        }

        private void OnEnable()
        {
            CatchToInventory.OnCoinsAwarded += HandleCatchAwarded;
        }

        private void OnDisable()
        {
            CatchToInventory.OnCoinsAwarded -= HandleCatchAwarded;
        }

        private void HandleCatchAwarded(Fish species, int registryId, float rawPrice, int credits)
        {
            // OnCoinsAwarded is fired after InventoryStatsService.RecordCatch in CatchToInventory,
            // so completion checks here always see the just-caught fish as recorded.
            EvaluateLakeCompletion();
        }

        private void EvaluateLakeCompletion()
        {
            if (!worldManager)
            {
                if (debugLogs)
                    Debug.LogWarning("[AIStoryLakeMonitor] WorldManager missing. Cannot evaluate lake completion.");
                return;
            }

            var pool = worldManager.GetActivePool();
            if (pool == null || pool.Count == 0)
                return;

            var uniqueSpecies = new HashSet<Fish>();

            for (int i = 0; i < pool.Count; i++)
            {
                var fish = pool[i].fish as Fish;
                if (!fish)
                    continue;

                if (!uniqueSpecies.Add(fish))
                    continue;

                if (!HasCaughtSpecies(fish))
                    return;
            }

            if (uniqueSpecies.Count == 0)
                return;

            // Spec trigger target: Lake 1 (index 0).
            if (worldManager.lakeIndex == 0)
                GrantLake1MilestoneIfNeeded();
        }

        private static bool HasCaughtSpecies(Fish fish)
        {
            if (!fish)
                return false;

            int fishId = InventoryService.GetId(fish);
            if (fishId >= 0)
            {
                if (InventoryStatsService.TryGetWeightRecord(fishId, out _)) return true;
                if (InventoryStatsService.TryGetLengthRecord(fishId, out _)) return true;
                if (InventoryStatsService.TryGetQualityRecord(fishId, out _)) return true;
            }

            if (InventoryLookup.TryGetCaughtTotal(fish.name, out int count))
                return count > 0;

            return false;
        }

        private void GrantLake1MilestoneIfNeeded()
        {
            var ppm = PlayerProgressManager.Instance;
            if (ppm == null || ppm.Data == null)
            {
                if (debugLogs)
                    Debug.LogWarning("[AIStoryLakeMonitor] PlayerProgressManager not available. Cannot grant milestone.");
                return;
            }

            if (ppm.Data.gear == null)
                ppm.Data.gear = new PlayerGearData();

            var granted = ppm.Data.gear.unlockedBlueprintIds;
            if (granted == null)
                granted = ppm.Data.gear.unlockedBlueprintIds = new List<string>();

            if (granted.Contains(Lake1SealGrantToken))
                return;

            granted.Add(Lake1SealGrantToken);

            bool addedToWarehouse = false;

            // If a HexWorld warehouse exists in the active scene, grant the milestone resource immediately.
            var warehouses = UnityEngine.Object.FindObjectsByType<HexWorldWarehouseInventory>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (warehouses != null && warehouses.Length > 0 && warehouses[0] != null)
                addedToWarehouse = warehouses[0].TryAdd(HexWorldResourceId.MuseumEnrollmentSeal, 1);

            if (!addedToWarehouse && !granted.Contains(Lake1SealPendingToken))
                granted.Add(Lake1SealPendingToken);

            StoryEvents.Raise(Lake1ClearStoryId);
            ppm.Save();

            Debug.Log("[Milestone] Lake 1 Clear! Museum Enrollment Seal granted.");
        }

        [ContextMenu("Debug: Reset Lake 1 Milestone")]
        private void DebugResetLake1()
        {
            var ppm = PlayerProgressManager.Instance;
            if (ppm?.Data?.gear?.unlockedBlueprintIds != null)
            {
                ppm.Data.gear.unlockedBlueprintIds.Remove(Lake1SealGrantToken);
            }

            PlayerPrefs.DeleteKey("AIStory_Seen_Museum_Enrollment_Lake1");
            ppm?.Save();

            Debug.Log("[Milestone Debug] Lake 1 state reset. Next catch will trigger enrollment.");
        }
    }
}
