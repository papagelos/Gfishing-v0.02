// Assets/Minigames/HexWorld3D/Scripts/Village/Minigames/ForestryMinigameController.cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace GalacticFishing.Minigames.HexWorld
{
    /// <summary>
    /// Represents a single forest plot that can grow trees.
    /// </summary>
    [Serializable]
    public class ForestPlot
    {
        [Tooltip("Identifier for the tree species planted (0 = default/generic tree).")]
        public int treeSpeciesId;

        [Tooltip("Current growth progress from 0.0 (just planted) to 1.0 (ready to harvest).")]
        [Range(0f, 1f)]
        public float growthProgress;

        [Tooltip("Whether this plot currently has a tree planted.")]
        public bool isOccupied;

        public ForestPlot()
        {
            treeSpeciesId = 0;
            growthProgress = 0f;
            isOccupied = false;
        }

        public ForestPlot(int speciesId, float progress, bool occupied)
        {
            treeSpeciesId = speciesId;
            growthProgress = Mathf.Clamp01(progress);
            isOccupied = occupied;
        }
    }

    /// <summary>
    /// Forestry Station "Plot Manager" minigame controller.
    /// Manages forest plots that grow trees over time and produce Wood and Fiber.
    ///
    /// Features:
    /// - Manages 3–6 plots (default 3, upgradable to max 6)
    /// - Growth progress increments each production tick
    /// - Adjacent Forest-tagged tiles provide growth speed bonus
    /// - Mature trees (growthProgress >= 1.0) are auto-harvested for Wood + Fiber
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ForestryMinigameController : MonoBehaviour, IHexWorldBuildingStateProvider
    {
        [Header("Plot Configuration")]
        [Tooltip("List of forest plots managed by this station.")]
        [SerializeField] private List<ForestPlot> plots = new();

        [Tooltip("Number of plots unlocked at start (default 3).")]
        [SerializeField, Range(1, 6)] private int startingPlots = 3;

        [Tooltip("Maximum number of plots that can be unlocked.")]
        [SerializeField, Range(1, 6)] private int maxPlots = 6;

        [Header("Growth Settings")]
        [Tooltip("Base growth progress per production tick (0.1 = 10 ticks to mature).")]
        [SerializeField, Range(0.01f, 0.5f)] private float baseGrowthPerTick = 0.1f;

        [Tooltip("Additional growth progress per adjacent Forest-tagged tile.")]
        [SerializeField, Range(0f, 0.1f)] private float forestAdjacencyBonus = 0.02f;

        [Tooltip("Gameplay tag used to identify forest tiles for adjacency bonus.")]
        [SerializeField] private string forestTileTag = "forest";

        [Header("Harvest Outputs")]
        [Tooltip("Passive amount of Wood produced every production tick, even when no tree is harvested.")]
        [SerializeField, Min(0)] private int passiveWoodPerTick = 1;

        [Tooltip("Amount of Wood produced when harvesting a mature tree.")]
        [SerializeField, Min(1)] private int woodPerHarvest = 3;

        [Tooltip("Amount of Fiber produced when harvesting a mature tree.")]
        [SerializeField, Min(0)] private int fiberPerHarvest = 1;

        [Tooltip("If true, plots auto-replant after harvest. If false, plots become empty.")]
        [SerializeField] private bool autoReplant = true;

        [Header("References")]
        [SerializeField] private HexWorldBuildingProductionProfile productionProfile;

        // Events
        public event Action<int, ForestPlot> PlotGrowthChanged;  // plotIndex, plot
        public event Action<int> PlotHarvested;                  // plotIndex
        public event Action<int> PlotCountChanged;               // currentPlotCount

        // Properties
        public int PlotCount => plots.Count;
        public int MaxPlots => maxPlots;
        public IReadOnlyList<ForestPlot> Plots => plots;

        // Cached controller reference for adjacency checks
        private HexWorld3DController _controller;
        private HexWorldBuildingInstance _buildingInstance;

        // Serializable state for save/load
        [Serializable]
        private struct ForestryState
        {
            public List<ForestPlotSaveData> plotData;
            public int unlockedPlots;
        }

        [Serializable]
        private struct ForestPlotSaveData
        {
            public int speciesId;
            public float progress;
            public bool occupied;
        }

        private void Awake()
        {
            if (!productionProfile)
                productionProfile = GetComponent<HexWorldBuildingProductionProfile>();

            _buildingInstance = GetComponent<HexWorldBuildingInstance>();

            // Initialize plots if empty
            if (plots.Count == 0)
            {
                InitializePlots(startingPlots);
            }
        }

        private void Start()
        {
            // Cache controller reference
            _controller = UnityEngine.Object.FindObjectOfType<HexWorld3DController>(true);

            // Subscribe to production tick
            var ticker = UnityEngine.Object.FindObjectOfType<HexWorldProductionTicker>(true);
            if (ticker != null)
            {
                ticker.TickCompleted += OnProductionTick;
            }
        }

        private void OnDestroy()
        {
            var ticker = UnityEngine.Object.FindObjectOfType<HexWorldProductionTicker>(true);
            if (ticker != null)
            {
                ticker.TickCompleted -= OnProductionTick;
            }
        }

        /// <summary>
        /// Initializes the plot list with the specified number of plots.
        /// All plots start occupied with a new tree (growthProgress = 0).
        /// </summary>
        private void InitializePlots(int count)
        {
            plots.Clear();
            int plotCount = Mathf.Clamp(count, 1, maxPlots);

            for (int i = 0; i < plotCount; i++)
            {
                plots.Add(new ForestPlot(0, 0f, true));
            }

            PlotCountChanged?.Invoke(plots.Count);
        }

        /// <summary>
        /// Called each production tick. Advances growth on all occupied plots
        /// and harvests mature trees.
        /// </summary>
        private void OnProductionTick()
        {
            if (_buildingInstance != null && !_buildingInstance.IsActive)
                return; // Skip if building is dormant
            if (_buildingInstance != null && _buildingInstance.GetRelocationCooldown() > 0f)
                return; // Skip while relocation cooldown is active

            float growthAmount = CalculateGrowthAmount();
            int totalWood = 0;
            int totalFiber = 0;

            for (int i = 0; i < plots.Count; i++)
            {
                var plot = plots[i];
                if (!plot.isOccupied) continue;

                // Advance growth
                plot.growthProgress = Mathf.Clamp01(plot.growthProgress + growthAmount);
                PlotGrowthChanged?.Invoke(i, plot);

                // Check for harvest
                if (plot.growthProgress >= 1f)
                {
                    totalWood += woodPerHarvest;
                    totalFiber += fiberPerHarvest;

                    // Reset or clear the plot
                    if (autoReplant)
                    {
                        plot.growthProgress = 0f;
                        // Keep isOccupied = true for auto-replant
                    }
                    else
                    {
                        plot.growthProgress = 0f;
                        plot.isOccupied = false;
                    }

                    PlotHarvested?.Invoke(i);
                    Debug.Log($"[ForestryMinigame] Plot {i} harvested! Wood: {woodPerHarvest}, Fiber: {fiberPerHarvest}");
                }
            }

            // Update production profile with harvest outputs
            UpdateProductionOutput(totalWood, totalFiber);
        }

        /// <summary>
        /// Calculates the growth amount per tick, including forest adjacency bonus.
        /// </summary>
        private float CalculateGrowthAmount()
        {
            float growth = baseGrowthPerTick;

            // Check adjacent tiles for forest bonus
            if (_controller != null && _buildingInstance != null && !string.IsNullOrEmpty(forestTileTag))
            {
                int forestCount = _controller.CountAdjacentTilesWithTag(_buildingInstance.Coord, forestTileTag);
                growth += forestCount * forestAdjacencyBonus;

                if (forestCount > 0)
                {
                    Debug.Log($"[ForestryMinigame] Growth bonus from {forestCount} adjacent forest tiles: +{forestCount * forestAdjacencyBonus:P0}");
                }
            }

            return growth;
        }

        /// <summary>
        /// Updates the production profile's baseOutputPerTick with the harvested resources.
        /// This integrates with the normal production ticker system.
        /// </summary>
        private void UpdateProductionOutput(int wood, int fiber)
        {
            if (!productionProfile) return;

            int totalWood = Mathf.Max(0, passiveWoodPerTick) + Mathf.Max(0, wood);
            int totalFiber = Mathf.Max(0, fiber);

            // Clear existing outputs
            productionProfile.baseOutputPerTick.Clear();

            // Always emit passive wood baseline, plus any harvest burst on top.
            if (totalWood > 0)
                productionProfile.baseOutputPerTick.Add(new HexWorldResourceStack(HexWorldResourceId.Wood, totalWood));

            if (totalFiber > 0)
                productionProfile.baseOutputPerTick.Add(new HexWorldResourceStack(HexWorldResourceId.Fiber, totalFiber));
        }

        // ─────────────────────────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Unlocks an additional plot (up to maxPlots).
        /// Returns true if successful.
        /// </summary>
        public bool UnlockPlot()
        {
            if (plots.Count >= maxPlots)
            {
                Debug.Log($"[ForestryMinigame] Cannot unlock more plots. Already at max ({maxPlots}).");
                return false;
            }

            // Add a new occupied plot
            plots.Add(new ForestPlot(0, 0f, true));
            PlotCountChanged?.Invoke(plots.Count);
            Debug.Log($"[ForestryMinigame] Unlocked plot {plots.Count}/{maxPlots}");
            return true;
        }

        /// <summary>
        /// Plants a tree in an empty plot.
        /// Returns true if successful.
        /// </summary>
        public bool PlantTree(int plotIndex, int speciesId = 0)
        {
            if (plotIndex < 0 || plotIndex >= plots.Count)
            {
                Debug.LogWarning($"[ForestryMinigame] Invalid plot index: {plotIndex}");
                return false;
            }

            var plot = plots[plotIndex];
            if (plot.isOccupied)
            {
                Debug.Log($"[ForestryMinigame] Plot {plotIndex} is already occupied.");
                return false;
            }

            plot.treeSpeciesId = speciesId;
            plot.growthProgress = 0f;
            plot.isOccupied = true;

            PlotGrowthChanged?.Invoke(plotIndex, plot);
            Debug.Log($"[ForestryMinigame] Planted tree (species {speciesId}) in plot {plotIndex}");
            return true;
        }

        /// <summary>
        /// Gets the growth percentage of a specific plot.
        /// </summary>
        public float GetPlotGrowthPercent(int plotIndex)
        {
            if (plotIndex < 0 || plotIndex >= plots.Count)
                return 0f;
            return plots[plotIndex].growthProgress;
        }

        /// <summary>
        /// Gets a summary of the current forestry state.
        /// </summary>
        public string GetStatusSummary()
        {
            int occupied = 0;
            int mature = 0;

            for (int i = 0; i < plots.Count; i++)
            {
                if (plots[i].isOccupied)
                {
                    occupied++;
                    if (plots[i].growthProgress >= 1f)
                        mature++;
                }
            }

            return $"{occupied}/{plots.Count} plots active, {mature} ready";
        }

        /// <summary>
        /// Gets the total growth bonus from adjacent forest tiles.
        /// </summary>
        public float GetForestAdjacencyBonusPercent()
        {
            if (_controller == null || _buildingInstance == null)
                return 0f;

            int forestCount = _controller.CountAdjacentTilesWithTag(_buildingInstance.Coord, forestTileTag);
            return forestCount * forestAdjacencyBonus;
        }

        /// <summary>
        /// Gets the number of adjacent forest tiles.
        /// </summary>
        public int GetAdjacentForestCount()
        {
            if (_controller == null || _buildingInstance == null)
                return 0;

            return _controller.CountAdjacentTilesWithTag(_buildingInstance.Coord, forestTileTag);
        }

        /// <summary>
        /// Gets the base growth rate per tick.
        /// </summary>
        public float BaseGrowthPerTick => baseGrowthPerTick;

        /// <summary>
        /// Gets the bonus growth per adjacent forest tile.
        /// </summary>
        public float ForestAdjacencyBonusPerTile => forestAdjacencyBonus;

        /// <summary>
        /// Gets the wood produced per harvest.
        /// </summary>
        public int WoodPerHarvest => woodPerHarvest;

        /// <summary>
        /// Gets the fiber produced per harvest.
        /// </summary>
        public int FiberPerHarvest => fiberPerHarvest;

        /// <summary>
        /// Returns true if any plot is ready for harvest (growthProgress >= 1.0).
        /// </summary>
        public bool HasReadyPlots()
        {
            for (int i = 0; i < plots.Count; i++)
            {
                if (plots[i].isOccupied && plots[i].growthProgress >= 1f)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Returns the count of plots ready for harvest.
        /// </summary>
        public int GetReadyPlotCount()
        {
            int count = 0;
            for (int i = 0; i < plots.Count; i++)
            {
                if (plots[i].isOccupied && plots[i].growthProgress >= 1f)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// Returns true if any plot is empty (not occupied).
        /// </summary>
        public bool HasEmptyPlots()
        {
            for (int i = 0; i < plots.Count; i++)
            {
                if (!plots[i].isOccupied)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Manually harvests all ready plots and adds resources to the warehouse.
        /// Returns the total resources harvested as (wood, fiber).
        /// </summary>
        public (int wood, int fiber) HarvestAllReady()
        {
            int totalWood = 0;
            int totalFiber = 0;

            for (int i = 0; i < plots.Count; i++)
            {
                var plot = plots[i];
                if (!plot.isOccupied || plot.growthProgress < 1f)
                    continue;

                totalWood += woodPerHarvest;
                totalFiber += fiberPerHarvest;

                // Reset the plot
                if (autoReplant)
                {
                    plot.growthProgress = 0f;
                }
                else
                {
                    plot.growthProgress = 0f;
                    plot.isOccupied = false;
                }

                PlotHarvested?.Invoke(i);
                PlotGrowthChanged?.Invoke(i, plot);
            }

            // Add to warehouse
            if (totalWood > 0 || totalFiber > 0)
            {
                var warehouse = UnityEngine.Object.FindObjectOfType<HexWorldWarehouseInventory>(true);
                if (warehouse != null)
                {
                    if (totalWood > 0) warehouse.TryAdd(HexWorldResourceId.Wood, totalWood);
                    if (totalFiber > 0) warehouse.TryAdd(HexWorldResourceId.Fiber, totalFiber);
                }

                Debug.Log($"[ForestryMinigame] Harvested all ready plots! Wood: {totalWood}, Fiber: {totalFiber}");
            }

            return (totalWood, totalFiber);
        }

        /// <summary>
        /// Plants trees in all empty plots.
        /// Returns the number of plots planted.
        /// </summary>
        public int PlantAllEmpty(int speciesId = 0)
        {
            int planted = 0;

            for (int i = 0; i < plots.Count; i++)
            {
                var plot = plots[i];
                if (plot.isOccupied)
                    continue;

                plot.treeSpeciesId = speciesId;
                plot.growthProgress = 0f;
                plot.isOccupied = true;
                planted++;

                PlotGrowthChanged?.Invoke(i, plot);
            }

            if (planted > 0)
            {
                Debug.Log($"[ForestryMinigame] Planted {planted} trees in empty plots.");
            }

            return planted;
        }

        // ─────────────────────────────────────────────────────────────────
        // IHexWorldBuildingStateProvider Implementation
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Serializes the forestry state to a JSON string for saving.
        /// </summary>
        public string GetSerializedState()
        {
            var plotData = new List<ForestPlotSaveData>();
            for (int i = 0; i < plots.Count; i++)
            {
                var p = plots[i];
                plotData.Add(new ForestPlotSaveData
                {
                    speciesId = p.treeSpeciesId,
                    progress = p.growthProgress,
                    occupied = p.isOccupied
                });
            }

            var state = new ForestryState
            {
                plotData = plotData,
                unlockedPlots = plots.Count
            };

            return JsonUtility.ToJson(state);
        }

        /// <summary>
        /// Restores the forestry state from a JSON string during load.
        /// </summary>
        public void LoadSerializedState(string state)
        {
            if (string.IsNullOrEmpty(state))
                return;

            try
            {
                var loaded = JsonUtility.FromJson<ForestryState>(state);

                // Reconstruct plots from saved data
                plots.Clear();

                if (loaded.plotData != null)
                {
                    for (int i = 0; i < loaded.plotData.Count; i++)
                    {
                        var pd = loaded.plotData[i];
                        plots.Add(new ForestPlot(pd.speciesId, pd.progress, pd.occupied));
                    }
                }

                // Ensure we have at least startingPlots
                while (plots.Count < startingPlots)
                {
                    plots.Add(new ForestPlot(0, 0f, true));
                }

                Debug.Log($"[ForestryMinigame] State loaded: {plots.Count} plots");
                PlotCountChanged?.Invoke(plots.Count);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ForestryMinigame] Failed to load state: {e.Message}");
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // Debug / Testing
        // ─────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
        [ContextMenu("Debug: Force Tick")]
        private void DebugForceTick()
        {
            OnProductionTick();
        }

        [ContextMenu("Debug: Unlock Plot")]
        private void DebugUnlockPlot()
        {
            UnlockPlot();
        }

        [ContextMenu("Debug: Set All Plots to 90% Growth")]
        private void DebugSetGrowth90()
        {
            for (int i = 0; i < plots.Count; i++)
            {
                if (plots[i].isOccupied)
                {
                    plots[i].growthProgress = 0.9f;
                    PlotGrowthChanged?.Invoke(i, plots[i]);
                }
            }
        }
#endif
    }
}
