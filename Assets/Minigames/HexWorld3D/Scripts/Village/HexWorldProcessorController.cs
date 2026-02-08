// Assets/Minigames/HexWorld3D/Scripts/Village/HexWorldProcessorController.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using GalacticFishing.Progress; 
using GalacticFishing.UI;

namespace GalacticFishing.Minigames.HexWorld
{
    /// <summary>
    /// Controller for Processor buildings (Sawmill, Smelter, etc.) that convert
    /// raw resources into refined goods with quality-based output.
    ///
    /// Quality Formula (TICKET 21):
    /// - If Q_tool >= Q_in: Q_out = Q_in + ceil((Q_tool - Q_in) × gainFactor)
    /// - If Q_tool < Q_in: Q_out = Q_in - ceil((Q_in - Q_tool) × lossFactor)
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HexWorldProcessorController : MonoBehaviour, IHexWorldBuildingStateProvider
    {
        [Header("Recipe")]
        [Tooltip("All recipes this processor can execute (index 0 is default).")]
        [SerializeField] private List<ProcessorRecipeDefinition> availableRecipes = new();

        [SerializeField, FormerlySerializedAs("recipe"), HideInInspector]
        private ProcessorRecipeDefinition legacyRecipe;

        [SerializeField, HideInInspector]
        private int activeRecipeIndex;

        [Header("Tool Slot")]
        [Tooltip("The type of tool this processor requires (e.g., Tool_Saw for Sawmill).")]
        [SerializeField] private HexWorldResourceId toolSlotType = HexWorldResourceId.Tool_Saw;

        [Tooltip("Quality level of the installed tool (affects output quality).")]
        [SerializeField] private float installedToolQuality = 10f;
        [Tooltip("Resource ID of the currently installed tool item, if any.")]
        [SerializeField] private HexWorldResourceId installedToolId = HexWorldResourceId.None;

        [Header("Debug")]
        [SerializeField] private bool logProcessing;

        // Cached references
        private HexWorldProductionTicker _ticker;
        private HexWorldWarehouseInventory _warehouse;
        private HexWorldBuildingInstance _buildingInstance;
        private HexWorldBuildingActiveState _activeState;

        /// <summary>
        /// The recipes assigned to this processor.
        /// </summary>
        public IReadOnlyList<ProcessorRecipeDefinition> AvailableRecipes => availableRecipes;

        /// <summary>
        /// Index of the currently active recipe.
        /// </summary>
        public int ActiveRecipeIndex
        {
            get
            {
                int max = (availableRecipes != null && availableRecipes.Count > 0) ? availableRecipes.Count - 1 : 0;
                return Mathf.Clamp(activeRecipeIndex, 0, max);
            }
        }

        /// <summary>
        /// The recipe currently selected for this processor (null if none).
        /// </summary>
        public ProcessorRecipeDefinition ActiveRecipe
        {
            get
            {
                if (availableRecipes == null || availableRecipes.Count == 0)
                    return null;

                int clamped = Mathf.Clamp(activeRecipeIndex, 0, availableRecipes.Count - 1);
                return availableRecipes[clamped];
            }
        }

        /// <summary>
        /// The type of tool this processor requires (e.g., Tool_Saw).
        /// </summary>
        public HexWorldResourceId ToolSlotType => toolSlotType;

        /// <summary>
        /// Current tool quality level installed in this processor.
        /// </summary>
        public float InstalledToolQuality
        {
            get => installedToolQuality;
            set
            {
                float newValue = Mathf.Max(0f, value);
                if (Mathf.Approximately(installedToolQuality, newValue)) return;
                installedToolQuality = newValue;
                ToolChanged?.Invoke(installedToolQuality);
            }
        }

        /// <summary>
        /// Resource ID for the tool currently installed (or None if the processor is using a baseline tool).
        /// </summary>
        public HexWorldResourceId InstalledToolId => installedToolId;

        /// <summary>
        /// Fired when the installed tool quality changes.
        /// </summary>
        public event Action<float> ToolChanged;

        /// <summary>
        /// Fired when a conversion cycle completes successfully.
        /// Parameters: (outputId, outputAmount, outputQuality)
        /// </summary>
        public event Action<HexWorldResourceId, int, int> ConversionCompleted;

        /// <summary>
        /// Fired when conversion fails due to missing input resources.
        /// </summary>
        public event Action ConversionBlocked;

        private void Awake()
        {
            _buildingInstance = GetComponent<HexWorldBuildingInstance>();
            _activeState = GetComponent<HexWorldBuildingActiveState>();
            ClampActiveRecipeIndex();
        }

        private void OnValidate()
        {
            if (availableRecipes == null)
                availableRecipes = new List<ProcessorRecipeDefinition>();

            if (legacyRecipe != null)
            {
                if (availableRecipes.Count == 0)
                {
                    availableRecipes.Add(legacyRecipe);
                }
                legacyRecipe = null;
            }

            ClampActiveRecipeIndex();
        }

        private void ClampActiveRecipeIndex()
        {
            if (availableRecipes == null)
                availableRecipes = new List<ProcessorRecipeDefinition>();

            if (availableRecipes.Count == 0)
            {
                activeRecipeIndex = 0;
            }
            else
            {
                activeRecipeIndex = Mathf.Clamp(activeRecipeIndex, 0, availableRecipes.Count - 1);
            }
        }

        private void OnEnable()
        {
            _ticker = FindObjectOfType<HexWorldProductionTicker>(true);
            _warehouse = FindObjectOfType<HexWorldWarehouseInventory>(true);

            if (_ticker != null)
            {
                _ticker.TickCompleted += OnProductionTick;
            }
        }

        private void OnDisable()
        {
            if (_ticker != null)
            {
                _ticker.TickCompleted -= OnProductionTick;
            }
        }

        private void OnProductionTick()
        {
            // Only process if building is active
            if (_activeState != null && !_activeState.IsActive)
                return;

            // Respect relocation cooldown so moved/placed processors do not produce immediately.
            if (_buildingInstance != null && _buildingInstance.GetRelocationCooldown() > 0f)
                return;

            var recipe = ActiveRecipe;
            if (recipe == null)
            {
                if (logProcessing)
                    Debug.LogWarning($"[Processor] {gameObject.name}: No recipe assigned.");
                return;
            }

            if (_warehouse == null)
            {
                _warehouse = FindObjectOfType<HexWorldWarehouseInventory>(true);
                if (_warehouse == null) return;
            }

            TryProcessConversion();
        }

        /// <summary>
        /// Attempts to run one conversion cycle.
        /// Supports multi-input recipes (TICKET 23).
        /// </summary>
        public void TryProcessConversion()
        {
            var recipe = ActiveRecipe;
            if (recipe == null || _warehouse == null) return;

            var allInputs = recipe.GetAllInputs();
            if (allInputs.Length == 0)
            {
                if (logProcessing)
                    Debug.LogWarning($"[Processor] {gameObject.name}: Recipe has no inputs defined.");
                return;
            }

            // Check if warehouse has enough of ALL inputs
            foreach (var input in allInputs)
            {
                int inputRequired = input.amount;
                int inputAvailable = _warehouse.Get(input.id);

                if (inputAvailable < inputRequired)
                {
                    if (logProcessing)
                        Debug.Log($"[Processor] {gameObject.name}: Not enough {input.id} ({inputAvailable}/{inputRequired})");
                    ConversionBlocked?.Invoke();
                    return;
                }
            }

            int inputQuality = CalculateInputQualityQin(allInputs);

            // Calculate output quality using the spec formula
            int outputQuality = CalculateOutputQuality(inputQuality, installedToolQuality, recipe.gainFactor, recipe.lossFactor);

            float synergyMultiplier = 0f;
            if (_ticker == null)
                _ticker = FindObjectOfType<HexWorldProductionTicker>(true);

            if (_ticker != null && _buildingInstance != null)
            {
                var buildingDef = ResolveCurrentBuildingDefinition();
                if (buildingDef != null)
                {
                    synergyMultiplier = _ticker.CalculateSynergyBonus(_buildingInstance.Coord, buildingDef);
                }
            }

            int outputAmount = Mathf.RoundToInt(recipe.baseOutputAmount * (1f + synergyMultiplier));
            if (outputAmount <= 0) outputAmount = 1;

            // Deduct ALL inputs from warehouse
            foreach (var input in allInputs)
            {
                if (!_warehouse.TryRemove(input.id, input.amount))
                {
                    if (logProcessing)
                        Debug.LogWarning($"[Processor] {gameObject.name}: Failed to remove {input.amount} {input.id}");
                    ConversionBlocked?.Invoke();
                    return;
                }
            }

            // Add output to warehouse
            _warehouse.TryAdd(recipe.outputId, outputAmount);

            // Update output material quality in PlayerProgressManager
            string outputMaterialId = recipe.outputId.ToString();
            PlayerProgressManager.Instance?.SetMaterialQuality(outputMaterialId, outputQuality, 0f);

            if (logProcessing)
            {
                string inputsStr = string.Join(" + ", System.Array.ConvertAll(allInputs, i => $"{i.amount} {i.id}"));
                Debug.Log($"[Processor] {gameObject.name}: Converted {inputsStr} (Q{inputQuality}) -> " +
                          $"{outputAmount} {recipe.outputId} (Q{outputQuality}) [Tool Q{installedToolQuality}]");
            }

            // Spawn floating text popup
            if (FloatingTextManager.Instance != null)
            {
                FloatingTextManager.Instance.SpawnWorld(
                    $"+{outputAmount} {recipe.outputId} Q{outputQuality}",
                    transform.position + Vector3.up * 1.25f
                );
            }

            ConversionCompleted?.Invoke(recipe.outputId, outputAmount, outputQuality);
        }

        /// <summary>
        /// Calculates output quality using the deterministic formula from Segment 05.
        ///
        /// If Q_tool >= Q_in: Q_out = Q_in + ceil((Q_tool - Q_in) × gainFactor)
        /// If Q_tool < Q_in:  Q_out = Q_in - ceil((Q_in - Q_tool) × lossFactor)
        /// </summary>
        /// <param name="inputQuality">Quality level of the input material.</param>
        /// <param name="toolQuality">Quality level of the installed tool.</param>
        /// <param name="gainFactor">Multiplier for quality gain (e.g., 0.5).</param>
        /// <param name="lossFactor">Multiplier for quality loss (e.g., 0.33).</param>
        /// <returns>The calculated output quality (clamped to minimum 0).</returns>
        public static int CalculateOutputQuality(int inputQuality, float toolQuality, float gainFactor, float lossFactor)
        {
            float resolvedGainFactor = (!float.IsNaN(gainFactor) && !float.IsInfinity(gainFactor) && gainFactor > 0f)
                ? gainFactor
                : 0.50f;
            float resolvedLossFactor = (!float.IsNaN(lossFactor) && !float.IsInfinity(lossFactor) && lossFactor > 0f)
                ? lossFactor
                : 0.33f;

            int outputQuality;

            if (toolQuality >= inputQuality)
            {
                // Tool is good enough - quality improves
                float diff = toolQuality - inputQuality;
                int bonus = Mathf.CeilToInt(diff * resolvedGainFactor);
                outputQuality = inputQuality + bonus;
            }
            else
            {
                // Tool is lower quality than input - quality degrades
                float diff = inputQuality - toolQuality;
                int penalty = Mathf.CeilToInt(diff * resolvedLossFactor);
                outputQuality = inputQuality - penalty;
            }

            // Ensure quality doesn't go negative
            return Mathf.Max(0, outputQuality);
        }

        /// <summary>
        /// Gets a preview of what the output quality would be for the current state.
        /// Useful for UI display.
        /// </summary>
        public int GetPreviewOutputQuality()
        {
            var recipe = ActiveRecipe;
            if (recipe == null) return 0;

            var allInputs = recipe.GetAllInputs();
            if (allInputs.Length == 0) return 0;

            int inputQuality = CalculateInputQualityQin(allInputs);

            return CalculateOutputQuality(inputQuality, installedToolQuality, recipe.gainFactor, recipe.lossFactor);
        }

        /// <summary>
        /// Checks if the processor can currently convert (has enough input resources).
        /// Supports multi-input recipes (TICKET 23).
        /// </summary>
        public bool CanConvert()
        {
            var recipe = ActiveRecipe;
            if (recipe == null || _warehouse == null) return false;

            var allInputs = recipe.GetAllInputs();
            foreach (var input in allInputs)
            {
                int inputAvailable = _warehouse.Get(input.id);
                if (inputAvailable < input.amount)
                    return false;
            }
            return allInputs.Length > 0;
        }

        /// <summary>
        /// Sets the active recipe index, clamping to the valid range.
        /// Switching recipes cancels any pending manual conversion interactions.
        /// </summary>
        public void SetActiveRecipe(int index)
        {
            if (availableRecipes == null || availableRecipes.Count == 0)
            {
                activeRecipeIndex = 0;
                return;
            }

            int clamped = Mathf.Clamp(index, 0, availableRecipes.Count - 1);
            if (clamped == activeRecipeIndex)
                return;

            activeRecipeIndex = clamped;
        }

        /// <summary>
        /// Installs the given tool into this processor while updating the cached quality value.
        /// </summary>
        public void InstallTool(HexWorldResourceId toolId, float toolQuality)
        {
            installedToolId = toolId;
            InstalledToolQuality = toolQuality;
        }

        public void ConfigureToolSlot(HexWorldResourceId slotType)
        {
            toolSlotType = slotType;
        }

        /// <summary>
        /// Gets a status summary for UI display.
        /// Supports multi-input recipes (TICKET 23).
        /// </summary>
        public string GetStatusSummary()
        {
            var recipe = ActiveRecipe;
            if (recipe == null) return "No recipe configured";

            if (_warehouse == null)
                _warehouse = FindObjectOfType<HexWorldWarehouseInventory>(true);

            var allInputs = recipe.GetAllInputs();
            if (allInputs.Length == 0) return "No inputs defined";

            // Check each input for availability
            var missingInputs = new System.Collections.Generic.List<string>();
            foreach (var input in allInputs)
            {
                int inputAvailable = _warehouse?.Get(input.id) ?? 0;
                if (inputAvailable < input.amount)
                {
                    missingInputs.Add($"{inputAvailable}/{input.amount} {input.id}");
                }
            }

            if (missingInputs.Count > 0)
            {
                return $"Waiting: {string.Join(", ", missingInputs)}";
            }

            // All inputs available - show ready status
            int inputQuality = CalculateInputQualityQin(allInputs);
            int previewQuality = GetPreviewOutputQuality();

            string inputsStr = string.Join(" + ", System.Array.ConvertAll(allInputs, i => $"{i.amount} {i.id}"));
            return $"Ready: {inputsStr} Q{inputQuality} -> {recipe.outputId} Q{previewQuality}";
        }

#if UNITY_EDITOR
        [ContextMenu("Debug: Force Conversion")]
        private void DebugForceConversion()
        {
            _warehouse = FindObjectOfType<HexWorldWarehouseInventory>(true);
            TryProcessConversion();
        }

        [ContextMenu("Debug: Preview Output Quality")]
        private void DebugPreviewQuality()
        {
            var recipe = ActiveRecipe;
            if (recipe == null)
            {
                Debug.Log("[Processor Debug] No recipe assigned.");
                return;
            }

            var allInputs = recipe.GetAllInputs();
            if (allInputs.Length == 0)
            {
                Debug.Log("[Processor Debug] Recipe has no inputs defined.");
                return;
            }

            int inputQuality = CalculateInputQualityQin(allInputs);
            int outputQuality = GetPreviewOutputQuality();

            string inputsStr = string.Join(" + ", System.Array.ConvertAll(allInputs, i => $"{i.amount} {i.id}"));
            Debug.Log($"[Processor Debug] {inputsStr} Q{inputQuality} + Tool Q{installedToolQuality} -> " +
                      $"{recipe.outputId} Q{outputQuality} (gain={recipe.gainFactor}, loss={recipe.lossFactor})");
        }
#endif

        private static int CalculateInputQualityQin(HexWorldResourceStack[] allInputs)
        {
            if (allInputs == null || allInputs.Length == 0)
                return 0;

            long weightedQualitySum = 0;
            int totalInputQuantity = 0;

            for (int i = 0; i < allInputs.Length; i++)
            {
                var input = allInputs[i];
                int qty = Mathf.Max(0, input.amount);
                if (qty <= 0) continue;

                int qi = PlayerProgressManager.Instance?.GetMaterialQuality(input.id.ToString()) ?? 0;
                weightedQualitySum += (long)qi * qty;
                totalInputQuantity += qty;
            }

            if (totalInputQuantity <= 0)
                return 0;

            return (int)(weightedQualitySum / totalInputQuantity);
        }

        private HexWorldBuildingDefinition ResolveCurrentBuildingDefinition()
        {
            if (_buildingInstance == null)
                _buildingInstance = GetComponent<HexWorldBuildingInstance>();

            if (_buildingInstance == null || string.IsNullOrWhiteSpace(_buildingInstance.buildingName))
                return null;

            var controller = FindObjectOfType<HexWorld3DController>(true);
            return controller != null ? controller.ResolveBuildingByName(_buildingInstance.buildingName) : null;
        }

        // ─────────────────────────────────────────────────────────────────
        // IHexWorldBuildingStateProvider
        // ─────────────────────────────────────────────────────────────────

        [Serializable]
        private struct ProcessorState
        {
            public float installedToolQuality;
            public HexWorldResourceId installedToolId;
            public int activeRecipeIndex;
        }

        public string GetSerializedState()
        {
            var state = new ProcessorState
            {
                installedToolQuality = installedToolQuality,
                installedToolId = installedToolId,
                activeRecipeIndex = ActiveRecipeIndex
            };

            return JsonUtility.ToJson(state);
        }

        public void LoadSerializedState(string state)
        {
            if (string.IsNullOrEmpty(state))
                return;

            try
            {
                var loaded = JsonUtility.FromJson<ProcessorState>(state);
                installedToolId = loaded.installedToolId;
                InstalledToolQuality = Mathf.Max(0f, loaded.installedToolQuality);

                int maxIndex = (availableRecipes != null && availableRecipes.Count > 0)
                    ? availableRecipes.Count - 1
                    : 0;
                activeRecipeIndex = Mathf.Clamp(loaded.activeRecipeIndex, 0, maxIndex);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HexWorldProcessorController] Failed to load state: {e.Message}");
            }
        }
    }
}
