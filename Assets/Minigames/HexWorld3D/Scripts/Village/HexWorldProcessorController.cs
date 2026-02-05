// Assets/Minigames/HexWorld3D/Scripts/Village/HexWorldProcessorController.cs
using System;
using UnityEngine;
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
    public sealed class HexWorldProcessorController : MonoBehaviour
    {
        [Header("Recipe")]
        [Tooltip("The recipe this processor uses to convert resources.")]
        [SerializeField] private ProcessorRecipeDefinition recipe;

        [Header("Tool Slot")]
        [Tooltip("The type of tool this processor requires (e.g., Tool_Saw for Sawmill).")]
        [SerializeField] private HexWorldResourceId toolSlotType = HexWorldResourceId.Tool_Saw;

        [Tooltip("Quality level of the installed tool (affects output quality).")]
        [SerializeField] private float installedToolQuality = 10f;

        [Header("Debug")]
        [SerializeField] private bool logProcessing;

        // Cached references
        private HexWorldProductionTicker _ticker;
        private HexWorldWarehouseInventory _warehouse;
        private HexWorldBuildingInstance _buildingInstance;
        private HexWorldBuildingActiveState _activeState;

        /// <summary>
        /// The recipe this processor uses.
        /// </summary>
        public ProcessorRecipeDefinition Recipe => recipe;

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

            // Get input material quality (use the first/primary input for quality calculation)
            var primaryInput = allInputs[0];
            string inputMaterialId = primaryInput.id.ToString();
            int inputQuality = PlayerProgressManager.Instance?.GetMaterialQuality(inputMaterialId) ?? 0;

            // Calculate output quality using the spec formula
            int outputQuality = CalculateOutputQuality(inputQuality, installedToolQuality, recipe.gainFactor, recipe.lossFactor);

            // Calculate output amount (base amount, could be modified by synergies later)
            int outputAmount = Mathf.RoundToInt(recipe.baseOutputAmount);
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
            int outputQuality;

            if (toolQuality >= inputQuality)
            {
                // Tool is good enough - quality improves
                float diff = toolQuality - inputQuality;
                int bonus = Mathf.CeilToInt(diff * gainFactor);
                outputQuality = inputQuality + bonus;
            }
            else
            {
                // Tool is lower quality than input - quality degrades
                float diff = inputQuality - toolQuality;
                int penalty = Mathf.CeilToInt(diff * lossFactor);
                outputQuality = inputQuality - penalty;
            }

            // Ensure quality doesn't go negative
            return Mathf.Max(0, outputQuality);
        }

        /// <summary>
        /// Gets a preview of what the output quality would be for the current state.
        /// Useful for UI display. Uses primary input for quality calculation.
        /// </summary>
        public int GetPreviewOutputQuality()
        {
            if (recipe == null) return 0;

            var allInputs = recipe.GetAllInputs();
            if (allInputs.Length == 0) return 0;

            string inputMaterialId = allInputs[0].id.ToString();
            int inputQuality = PlayerProgressManager.Instance?.GetMaterialQuality(inputMaterialId) ?? 0;

            return CalculateOutputQuality(inputQuality, installedToolQuality, recipe.gainFactor, recipe.lossFactor);
        }

        /// <summary>
        /// Checks if the processor can currently convert (has enough input resources).
        /// Supports multi-input recipes (TICKET 23).
        /// </summary>
        public bool CanConvert()
        {
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
        /// Gets a status summary for UI display.
        /// Supports multi-input recipes (TICKET 23).
        /// </summary>
        public string GetStatusSummary()
        {
            if (recipe == null) return "No recipe";

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
            var primaryInput = allInputs[0];
            string inputMaterialId = primaryInput.id.ToString();
            int inputQuality = PlayerProgressManager.Instance?.GetMaterialQuality(inputMaterialId) ?? 0;
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

            string inputMaterialId = allInputs[0].id.ToString();
            int inputQuality = PlayerProgressManager.Instance?.GetMaterialQuality(inputMaterialId) ?? 0;
            int outputQuality = GetPreviewOutputQuality();

            string inputsStr = string.Join(" + ", System.Array.ConvertAll(allInputs, i => $"{i.amount} {i.id}"));
            Debug.Log($"[Processor Debug] {inputsStr} Q{inputQuality} + Tool Q{installedToolQuality} -> " +
                      $"{recipe.outputId} Q{outputQuality} (gain={recipe.gainFactor}, loss={recipe.lossFactor})");
        }
#endif
    }
}
