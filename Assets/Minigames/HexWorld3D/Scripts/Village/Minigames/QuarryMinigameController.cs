// Assets/Minigames/HexWorld3D/Scripts/Village/Minigames/QuarryMinigameController.cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace GalacticFishing.Minigames.HexWorld
{
    /// <summary>
    /// Quarry Rig "Strata Drill" minigame controller.
    /// Manages drill depth, energy, and unlocks resources based on strata thresholds.
    ///
    /// Strata Unlocks:
    /// - Depth 0-100: Stone + Coal
    /// - Depth 101-300: Stone + Coal + Copper
    /// - Depth 301-600: Stone + Coal + Copper + Iron
    /// - Depth 601+: Stone + Coal + Copper + Iron + Gold
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class QuarryMinigameController : MonoBehaviour, IHexWorldBuildingStateProvider
    {
        [Header("Drill State")]
        [Tooltip("Current depth the drill has reached (in meters).")]
        [SerializeField] private float drillDepth = 0f;

        [Tooltip("Current drill energy available.")]
        [SerializeField] private float drillEnergy = 100f;

        [Tooltip("Maximum drill energy capacity.")]
        [SerializeField] private float maxDrillEnergy = 100f;

        [Header("Drill Settings")]
        [Tooltip("Energy cost per meter of drilling.")]
        [SerializeField] private float energyPerMeter = 1f;

        [Tooltip("Depth gained per drill action.")]
        [SerializeField] private float depthPerDrill = 10f;

        [Tooltip("Energy regenerated per second.")]
        [SerializeField] private float energyRegenPerSecond = 0.5f;

        [Header("Strata Thresholds")]
        [Tooltip("Depth required to unlock Coal.")]
        [SerializeField] private float coalDepth = 0f;

        [Tooltip("Depth required to unlock Copper.")]
        [SerializeField] private float copperDepth = 100f;

        [Tooltip("Depth required to unlock Iron.")]
        [SerializeField] private float ironDepth = 300f;

        [Tooltip("Depth required to unlock Gold.")]
        [SerializeField] private float goldDepth = 600f;

        [Header("Production Amounts")]
        [Tooltip("Base Stone production per tick.")]
        [SerializeField] private int stonePerTick = 5;

        [Tooltip("Coal production per tick when unlocked.")]
        [SerializeField] private int coalPerTick = 2;

        [Tooltip("Copper production per tick when unlocked.")]
        [SerializeField] private int copperPerTick = 2;

        [Tooltip("Iron production per tick when unlocked.")]
        [SerializeField] private int ironPerTick = 1;

        [Tooltip("Gold production per tick when unlocked.")]
        [SerializeField] private int goldPerTick = 1;

        [Header("References")]
        [SerializeField] private HexWorldBuildingProductionProfile productionProfile;

        // Events
        public event Action<float> DrillDepthChanged;
        public event Action<float> DrillEnergyChanged;
        public event Action<string> StrataUnlocked;

        // Properties
        public float DrillDepth => drillDepth;
        public float DrillEnergy => drillEnergy;
        public float MaxDrillEnergy => maxDrillEnergy;
        public float EnergyPercent => maxDrillEnergy > 0 ? drillEnergy / maxDrillEnergy : 0f;

        public bool HasCoal => drillDepth >= coalDepth;
        public bool HasCopper => drillDepth >= copperDepth;
        public bool HasIron => drillDepth >= ironDepth;
        public bool HasGold => drillDepth >= goldDepth;

        // Serializable state for save/load
        [Serializable]
        private struct QuarryState
        {
            public float depth;
            public float energy;
        }

        private void Awake()
        {
            if (!productionProfile)
                productionProfile = GetComponent<HexWorldBuildingProductionProfile>();
        }

        private void Start()
        {
            // Initialize production based on current depth
            UpdateProductionOutput();
        }

        private void Update()
        {
            // Regenerate energy over time
            if (drillEnergy < maxDrillEnergy)
            {
                drillEnergy = Mathf.Min(maxDrillEnergy, drillEnergy + energyRegenPerSecond * Time.deltaTime);
                // Only fire event occasionally to avoid spam
            }
        }

        /// <summary>
        /// Attempts to push the drill deeper, consuming energy.
        /// Returns true if successful, false if not enough energy.
        /// </summary>
        public bool PushDrill()
        {
            float energyCost = depthPerDrill * energyPerMeter;

            if (drillEnergy < energyCost)
            {
                Debug.Log($"[QuarryMinigame] Not enough energy to drill. Need {energyCost}, have {drillEnergy}");
                return false;
            }

            // Track previous thresholds for unlock notifications
            bool hadCoal = HasCoal;
            bool hadCopper = HasCopper;
            bool hadIron = HasIron;
            bool hadGold = HasGold;

            // Consume energy and increase depth
            drillEnergy -= energyCost;
            drillDepth += depthPerDrill;

            Debug.Log($"[QuarryMinigame] Drilled! Depth: {drillDepth}m, Energy: {drillEnergy}/{maxDrillEnergy}");

            // Fire events
            DrillDepthChanged?.Invoke(drillDepth);
            DrillEnergyChanged?.Invoke(drillEnergy);

            // Check for new unlocks
            if (!hadCoal && HasCoal)
            {
                StrataUnlocked?.Invoke("Coal");
                Debug.Log("[QuarryMinigame] Coal stratum unlocked!");
            }
            if (!hadCopper && HasCopper)
            {
                StrataUnlocked?.Invoke("Copper");
                Debug.Log("[QuarryMinigame] Copper stratum unlocked!");
            }
            if (!hadIron && HasIron)
            {
                StrataUnlocked?.Invoke("Iron");
                Debug.Log("[QuarryMinigame] Iron stratum unlocked!");
            }
            if (!hadGold && HasGold)
            {
                StrataUnlocked?.Invoke("Gold");
                Debug.Log("[QuarryMinigame] Gold stratum unlocked!");
            }

            // Update production output
            UpdateProductionOutput();

            return true;
        }

        /// <summary>
        /// Attempts to drill multiple times in succession.
        /// Returns the number of successful drills.
        /// </summary>
        public int PushDrillMultiple(int count)
        {
            int successfulDrills = 0;
            for (int i = 0; i < count; i++)
            {
                if (!PushDrill())
                    break;
                successfulDrills++;
            }
            return successfulDrills;
        }

        /// <summary>
        /// Updates the production profile's baseOutputPerTick based on current depth.
        /// </summary>
        public void UpdateProductionOutput()
        {
            if (!productionProfile)
            {
                productionProfile = GetComponent<HexWorldBuildingProductionProfile>();
                if (!productionProfile) return;
            }

            // Clear existing outputs
            productionProfile.baseOutputPerTick.Clear();

            // Always produce stone
            if (stonePerTick > 0)
                productionProfile.baseOutputPerTick.Add(new HexWorldResourceStack(HexWorldResourceId.Stone, stonePerTick));

            // Add resources based on depth thresholds
            if (HasCoal && coalPerTick > 0)
                productionProfile.baseOutputPerTick.Add(new HexWorldResourceStack(HexWorldResourceId.Coal, coalPerTick));

            if (HasCopper && copperPerTick > 0)
                productionProfile.baseOutputPerTick.Add(new HexWorldResourceStack(HexWorldResourceId.Copper, copperPerTick));

            if (HasIron && ironPerTick > 0)
                productionProfile.baseOutputPerTick.Add(new HexWorldResourceStack(HexWorldResourceId.Iron, ironPerTick));

            if (HasGold && goldPerTick > 0)
                productionProfile.baseOutputPerTick.Add(new HexWorldResourceStack(HexWorldResourceId.Gold, goldPerTick));
        }

        /// <summary>
        /// Gets a summary of unlocked strata for UI display.
        /// </summary>
        public string GetStrataStatusSummary()
        {
            var parts = new List<string> { "Stone" };

            if (HasCoal) parts.Add("Coal");
            if (HasCopper) parts.Add("Copper");
            if (HasIron) parts.Add("Iron");
            if (HasGold) parts.Add("Gold");

            return string.Join(", ", parts);
        }

        /// <summary>
        /// Gets the next stratum name and depth required to unlock it.
        /// Returns null if all strata are unlocked.
        /// </summary>
        public (string name, float depth)? GetNextStratum()
        {
            if (!HasCoal) return ("Coal", coalDepth);
            if (!HasCopper) return ("Copper", copperDepth);
            if (!HasIron) return ("Iron", ironDepth);
            if (!HasGold) return ("Gold", goldDepth);
            return null;
        }

        /// <summary>
        /// Manually sets the drill depth (for testing/debugging).
        /// </summary>
        public void SetDrillDepth(float depth)
        {
            drillDepth = Mathf.Max(0f, depth);
            UpdateProductionOutput();
            DrillDepthChanged?.Invoke(drillDepth);
        }

        /// <summary>
        /// Refills drill energy to maximum (for testing/debugging).
        /// </summary>
        public void RefillEnergy()
        {
            drillEnergy = maxDrillEnergy;
            DrillEnergyChanged?.Invoke(drillEnergy);
        }

        // ─────────────────────────────────────────────────────────────────
        // IHexWorldBuildingStateProvider Implementation
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Serializes the quarry state to a JSON string for saving.
        /// </summary>
        public string GetSerializedState()
        {
            var state = new QuarryState
            {
                depth = drillDepth,
                energy = drillEnergy
            };
            return JsonUtility.ToJson(state);
        }

        /// <summary>
        /// Restores the quarry state from a JSON string during load.
        /// </summary>
        public void LoadSerializedState(string state)
        {
            if (string.IsNullOrEmpty(state))
                return;

            try
            {
                var loaded = JsonUtility.FromJson<QuarryState>(state);
                drillDepth = Mathf.Max(0f, loaded.depth);
                drillEnergy = Mathf.Clamp(loaded.energy, 0f, maxDrillEnergy);

                Debug.Log($"[QuarryMinigame] State loaded: Depth={drillDepth}m, Energy={drillEnergy}");

                // Update production based on loaded depth
                UpdateProductionOutput();

                // Fire events
                DrillDepthChanged?.Invoke(drillDepth);
                DrillEnergyChanged?.Invoke(drillEnergy);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[QuarryMinigame] Failed to load state: {e.Message}");
            }
        }
    }
}
