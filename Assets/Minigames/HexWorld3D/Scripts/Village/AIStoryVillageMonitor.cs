// Assets/Minigames/HexWorld3D/Scripts/Village/AIStoryVillageMonitor.cs
using UnityEngine;
using GalacticFishing.Story;

namespace GalacticFishing.Minigames.HexWorld
{
    /// <summary>
    /// Specialized monitor for the Village system that listens to HexWorld3DController events
    /// and triggers AI Story entries for milestones.
    ///
    /// Trigger IDs:
    /// - Village_TH_Level_[X] - When Town Hall reaches level X
    /// - Village_Capacity_Full - When tile capacity is full (placed == max)
    /// - Village_Warehouse_Full - When warehouse becomes full
    /// </summary>
    public sealed class AIStoryVillageMonitor : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Reference to the HexWorld3DController. Auto-finds if not set.")]
        [SerializeField] private HexWorld3DController controller;

        [Tooltip("Reference to the warehouse inventory. Auto-finds if not set.")]
        [SerializeField] private HexWorldWarehouseInventory warehouse;

        [Header("Debug")]
        [SerializeField] private bool debugLogs = false;

        // Track previous states to detect transitions
        private int _lastTownHallLevel = -1;
        private bool _wasWarehouseFull = false;
        private bool _wasCapacityFull = false;

        private void Awake()
        {
            // Auto-find references if not set (immediate attempt)
            TryAutoFindReferences();
        }

        private void OnEnable()
        {
            SubscribeToEvents();
        }

        private void OnDisable()
        {
            UnsubscribeFromEvents();
        }

        private void Start()
        {
            // Fallback auto-find in Start (in case controller was created after Awake)
            if (!controller || !warehouse)
            {
                TryAutoFindReferences();

                // Re-subscribe if we found new references
                SubscribeToEvents();
            }

            // Initialize tracking state (don't trigger on initial load)
            if (controller)
            {
                _lastTownHallLevel = controller.TownHallLevel;
                _wasCapacityFull = false; // Don't trigger on load
            }

            if (warehouse)
            {
                _wasWarehouseFull = warehouse.IsFull;
            }

            if (debugLogs)
            {
                Debug.Log($"[AIStoryVillageMonitor] Initialized - Controller: {(controller ? controller.name : "null")}, Warehouse: {(warehouse ? warehouse.name : "null")}");
            }
        }

        private void TryAutoFindReferences()
        {
            if (!controller)
                controller = FindObjectOfType<HexWorld3DController>();

            if (!warehouse)
                warehouse = FindObjectOfType<HexWorldWarehouseInventory>();
        }

        private void SubscribeToEvents()
        {
            if (controller)
            {
                controller.TownHallLevelChanged += OnTownHallLevelChanged;
                controller.TilesPlacedChanged += OnTilesPlacedChanged;
                controller.CreditsChanged += OnCreditsChanged;
            }

            if (warehouse)
            {
                warehouse.InventoryChanged += OnWarehouseChanged;
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (controller)
            {
                controller.TownHallLevelChanged -= OnTownHallLevelChanged;
                controller.TilesPlacedChanged -= OnTilesPlacedChanged;
                controller.CreditsChanged -= OnCreditsChanged;
            }

            if (warehouse)
            {
                warehouse.InventoryChanged -= OnWarehouseChanged;
            }
        }

        private void OnTownHallLevelChanged(int newLevel)
        {
            // Only trigger if level actually increased (not on load or decrease)
            if (newLevel > _lastTownHallLevel && _lastTownHallLevel >= 1)
            {
                string triggerId = $"Village_TH_Level_{newLevel}";

                if (debugLogs)
                    Debug.Log($"[AIStoryVillageMonitor] Town Hall level up: {triggerId}");

                TriggerStory(triggerId);
            }

            _lastTownHallLevel = newLevel;
        }

        private void OnTilesPlacedChanged(int tilesPlaced, int maxCapacity)
        {
            // Check if we just hit full capacity
            bool isCapacityFull = (tilesPlaced >= maxCapacity && maxCapacity > 0);

            if (isCapacityFull && !_wasCapacityFull)
            {
                if (debugLogs)
                    Debug.Log($"[AIStoryVillageMonitor] Tile capacity full: {tilesPlaced}/{maxCapacity}");

                TriggerStory("Village_Capacity_Full");
            }

            _wasCapacityFull = isCapacityFull;
        }

        private void OnCreditsChanged(int newCredits)
        {
            // Credits milestones can be added here if needed
            // Example: Village_Credits_1000, Village_Credits_10000, etc.

            if (debugLogs)
                Debug.Log($"[AIStoryVillageMonitor] Credits changed: {newCredits}");

            // Milestone triggers (example - uncomment and customize as needed)
            // CheckCreditsMilestone(newCredits, 1000);
            // CheckCreditsMilestone(newCredits, 10000);
        }

        private void OnWarehouseChanged()
        {
            if (!warehouse) return;

            bool isFull = warehouse.IsFull;

            // Only trigger when transitioning from not-full to full
            if (isFull && !_wasWarehouseFull)
            {
                if (debugLogs)
                    Debug.Log($"[AIStoryVillageMonitor] Warehouse full: {warehouse.TotalStored}/{warehouse.Capacity}");

                TriggerStory("Village_Warehouse_Full");
            }

            _wasWarehouseFull = isFull;
        }

        private void TriggerStory(string triggerId)
        {
            if (string.IsNullOrWhiteSpace(triggerId))
                return;

            // Use StoryEvents bus (preferred decoupled approach)
            StoryEvents.Raise(triggerId);

            // Also try direct AIStoryDirector if available
            if (AIStoryDirector.Instance != null)
            {
                AIStoryDirector.Instance.Trigger(triggerId);
            }
        }

        // Helper for credit milestones (optional feature)
        // private HashSet<int> _creditMilestonesTriggered = new();
        // private void CheckCreditsMilestone(int credits, int milestone)
        // {
        //     if (credits >= milestone && !_creditMilestonesTriggered.Contains(milestone))
        //     {
        //         _creditMilestonesTriggered.Add(milestone);
        //         TriggerStory($"Village_Credits_{milestone}");
        //     }
        // }
    }
}
