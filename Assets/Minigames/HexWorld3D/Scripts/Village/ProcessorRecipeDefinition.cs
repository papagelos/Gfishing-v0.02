// Assets/Minigames/HexWorld3D/Scripts/Village/ProcessorRecipeDefinition.cs
using UnityEngine;

namespace GalacticFishing.Minigames.HexWorld
{
    /// <summary>
    /// Defines a recipe for converting raw resources into refined goods.
    /// Used by Processor buildings (e.g., Sawmill converts Wood to Planks).
    /// Supports multiple inputs for crafting recipes (TICKET 23).
    /// </summary>
    [CreateAssetMenu(menuName = "Galactic Fishing/Village/Processor Recipe", fileName = "Recipe_")]
    public sealed class ProcessorRecipeDefinition : ScriptableObject
    {
        [Header("Input (Legacy - single input)")]
        [Tooltip("The raw resource consumed by this recipe (e.g., Wood x5). Use 'inputs' array for multi-input recipes.")]
        public HexWorldResourceStack input;

        [Header("Inputs (Multi-input recipes)")]
        [Tooltip("Multiple input resources for crafting recipes (e.g., MetalIngots x2 + Planks x1). If empty, falls back to single 'input' field.")]
        public HexWorldResourceStack[] inputs;

        [Header("Output")]
        [Tooltip("The refined resource produced by this recipe.")]
        public HexWorldResourceId outputId = HexWorldResourceId.Planks;

        [Tooltip("Base amount of output produced per conversion cycle.")]
        [Min(0.1f)]
        public float baseOutputAmount = 1f;

        [Header("Quality Modifiers")]
        [Tooltip("Multiplier for bonus output when using high-quality tools (0.5 = +50% max).")]
        [Range(0f, 2f)]
        public float gainFactor = 0.5f;

        [Tooltip("Multiplier for output loss when using low-quality tools (0.33 = -33% max).")]
        [Range(0f, 1f)]
        public float lossFactor = 0.33f;

        /// <summary>
        /// Gets all input resources for this recipe.
        /// Returns the 'inputs' array if populated, otherwise returns single 'input' for backward compatibility.
        /// </summary>
        public HexWorldResourceStack[] GetAllInputs()
        {
            if (inputs != null && inputs.Length > 0)
                return inputs;

            // Backward compatibility: wrap single input in array
            if (input.id != HexWorldResourceId.None && input.amount > 0)
                return new[] { input };

            return System.Array.Empty<HexWorldResourceStack>();
        }

        /// <summary>
        /// Calculates the actual output amount based on tool quality.
        /// Quality ranges from 0 (worst) to 1 (best), with 0.5 being baseline.
        /// </summary>
        /// <param name="toolQuality">Tool quality factor (0-1).</param>
        /// <returns>Adjusted output amount.</returns>
        public float CalculateOutput(float toolQuality)
        {
            // Quality 0.5 = baseline (no modifier)
            // Quality > 0.5 = bonus (up to +gainFactor at quality 1.0)
            // Quality < 0.5 = penalty (up to -lossFactor at quality 0.0)
            float modifier;
            if (toolQuality >= 0.5f)
            {
                // Scale from 0% bonus at 0.5 to gainFactor bonus at 1.0
                float t = (toolQuality - 0.5f) * 2f; // 0 to 1
                modifier = 1f + (gainFactor * t);
            }
            else
            {
                // Scale from 0% penalty at 0.5 to lossFactor penalty at 0.0
                float t = (0.5f - toolQuality) * 2f; // 0 to 1
                modifier = 1f - (lossFactor * t);
            }

            return baseOutputAmount * modifier;
        }
    }
}
