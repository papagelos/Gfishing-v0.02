// Assets/Minigames/HexWorld3D/Scripts/Village/HexWorldRuntimeBuildingConfigurator.cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace GalacticFishing.Minigames.HexWorld
{
    /// <summary>
    /// MVP helper: ensures placed building instances have
    /// - HexWorldBuildingActiveState
    /// - HexWorldBuildingProductionProfile (with doc MVP outputs)
    ///
    /// This runs periodically and configures any new buildings it hasn't seen.
    /// </summary>
    public sealed class HexWorldRuntimeBuildingConfigurator : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float scanIntervalSeconds = 0.75f;

        // Track configured instances to avoid reapplying outputs every scan.
        private readonly HashSet<int> _configured = new();
        private float _t;

        private void Update()
        {
            _t += Time.deltaTime;
            if (_t < scanIntervalSeconds) return;
            _t = 0f;

            ScanAndConfigure();
        }

        private void ScanAndConfigure()
        {
            var instances = FindObjectsOfType<HexWorldBuildingInstance>(true);
            for (int i = 0; i < instances.Length; i++)
            {
                var inst = instances[i];
                if (!inst) continue;

                int id = inst.GetInstanceID();
                if (_configured.Contains(id)) continue;

                // Ensure components exist
                var state = inst.GetComponent<HexWorldBuildingActiveState>();
                if (!state) state = inst.gameObject.AddComponent<HexWorldBuildingActiveState>();

                var prod = inst.GetComponent<HexWorldBuildingProductionProfile>();
                if (!prod) prod = inst.gameObject.AddComponent<HexWorldBuildingProductionProfile>();

                ApplyMvpOutputs(inst, prod);

                _configured.Add(id);
            }
        }

        private static readonly string[] LumberyardNames = { "Building_Lumberyard", "ForestryStation", "Lumberyard" };

        private static void ApplyMvpOutputs(HexWorldBuildingInstance inst, HexWorldBuildingProductionProfile prod)
        {
            // If already configured (has outputs), do nothing.
            if (prod.baseOutputPerTick != null && prod.baseOutputPerTick.Count > 0)
                return;

            string n = inst.name.ToLowerInvariant();
            string definitionName = inst.buildingName ?? string.Empty;
            var def = ResolveDefinition(inst);
            bool isProcessor = def && def.kind == HexWorldBuildingDefinition.BuildingKind.Processor;

            // Non-producers (do not consume active slots)
            if (n.Contains("town") && n.Contains("hall"))
            {
                prod.consumesActiveSlot = false;
                return;
            }
            if (n.Contains("warehouse"))
            {
                prod.consumesActiveSlot = false;
                return;
            }

            if (isProcessor)
            {
                prod.consumesActiveSlot = true;
                ConfigureProcessor(inst, n, definitionName);
                return;
            }

            // MVP producer mapping by name
            // Lumberyard: +6 Wood
            bool matchesForestryInternal = false;
            for (int i = 0; i < LumberyardNames.Length; i++)
            {
                if (string.Equals(definitionName, LumberyardNames[i], System.StringComparison.OrdinalIgnoreCase))
                {
                    matchesForestryInternal = true;
                    break;
                }
            }

            if (matchesForestryInternal || n.Contains("lumber") || n.Contains("wood"))
            {
                prod.consumesActiveSlot = true;
                prod.baseOutputPerTick = new List<HexWorldResourceStack>
                {
                    new HexWorldResourceStack(HexWorldResourceId.Wood, 6)
                };
                return;
            }

            // Quarry: +4 Stone
            if (n.Contains("quarry") || n.Contains("stone"))
            {
                prod.consumesActiveSlot = true;
                prod.baseOutputPerTick = new List<HexWorldResourceStack>
                {
                    new HexWorldResourceStack(HexWorldResourceId.Stone, 4)
                };
                return;
            }

            // Forager Meadow: +3 Fiber
            if (n.Contains("forage") || n.Contains("fiber") || n.Contains("meadow"))
            {
                prod.consumesActiveSlot = true;
                prod.baseOutputPerTick = new List<HexWorldResourceStack>
                {
                    new HexWorldResourceStack(HexWorldResourceId.Fiber, 3)
                };
                return;
            }

            // Bog Fishery: +3 BaitIngredients
            if (n.Contains("bog") || n.Contains("bait") || n.Contains("fishery"))
            {
                prod.consumesActiveSlot = true;
                prod.baseOutputPerTick = new List<HexWorldResourceStack>
                {
                    new HexWorldResourceStack(HexWorldResourceId.BaitIngredients, 3)
                };
                return;
            }

            if (n.Contains("herbalist") || n.Contains("greenhouse"))
            {
                prod.consumesActiveSlot = true;
                EnsureHerbalistController(inst);
                return;
            }

            // Unknown non-processor => default non-producing, consumes slot if activated.
            prod.consumesActiveSlot = true;
        }

        private static void ConfigureProcessor(HexWorldBuildingInstance inst, string instanceNameLower, string definitionName)
        {
            if (definitionName.Equals("Building_Kitchen", StringComparison.OrdinalIgnoreCase))
            {
                EnsureProcessorController(inst, HexWorldResourceId.Tool_PrepSet);
                return;
            }

            if (definitionName.Equals("Building_AlchemyBench", StringComparison.OrdinalIgnoreCase))
            {
                EnsureProcessorController(inst, HexWorldResourceId.None);
                return;
            }

            if (definitionName.Equals("Building_Workbench", StringComparison.OrdinalIgnoreCase) ||
                definitionName.Equals("Workbench", StringComparison.OrdinalIgnoreCase) ||
                definitionName.Equals("Workshop", StringComparison.OrdinalIgnoreCase))
            {
                // Keep Workbench as a processor with no hard tool requirement.
                // Recipes (including Recipe_PrepSet) are assigned manually in Unity.
                EnsureProcessorController(inst, HexWorldResourceId.None);
                return;
            }

            if (instanceNameLower.Contains("tannery"))
            {
                EnsureProcessorController(inst, HexWorldResourceId.Tool_Vat);
                return;
            }

            if (definitionName.Equals("Building_Loom", StringComparison.OrdinalIgnoreCase))
            {
                EnsureProcessorController(inst, HexWorldResourceId.Tool_LoomFrame);
                return;
            }

            if (instanceNameLower.Contains("smelt"))
            {
                EnsureProcessorController(inst, HexWorldResourceId.Tool_FurnaceCore);
                return;
            }

            if (definitionName.IndexOf("Blacksmith", StringComparison.OrdinalIgnoreCase) >= 0 ||
                definitionName.IndexOf("Forge", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                EnsureProcessorController(inst, HexWorldResourceId.Tool_Hammer);
                return;
            }

            if (definitionName.IndexOf("Carpenter", StringComparison.OrdinalIgnoreCase) >= 0 ||
                definitionName.IndexOf("Carpentry", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                EnsureProcessorController(inst, HexWorldResourceId.None);
                return;
            }

            if (definitionName.Equals("Building_Stoneworks", StringComparison.OrdinalIgnoreCase) ||
                definitionName.Equals("Building_Stonecutter", StringComparison.OrdinalIgnoreCase))
            {
                EnsureProcessorController(inst, HexWorldResourceId.Tool_Chisel);
                return;
            }

            if (definitionName.IndexOf("Charcoal", StringComparison.OrdinalIgnoreCase) >= 0 ||
                definitionName.IndexOf("Kiln", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                EnsureProcessorController(inst, HexWorldResourceId.None);
                return;
            }

            EnsureProcessorController(inst, HexWorldResourceId.None);
        }

        private static void EnsureProcessorController(HexWorldBuildingInstance inst, HexWorldResourceId toolSlot)
        {
            var controller = inst.GetComponent<HexWorldProcessorController>();
            if (!controller)
            {
                controller = inst.gameObject.AddComponent<HexWorldProcessorController>();
            }

            controller.ConfigureToolSlot(toolSlot);
        }

        private static void EnsureHerbalistController(HexWorldBuildingInstance inst)
        {
            if (!inst.GetComponent<HerbalistMinigameController>())
            {
                inst.gameObject.AddComponent<HerbalistMinigameController>();
            }
        }

        private static HexWorldBuildingDefinition ResolveDefinition(HexWorldBuildingInstance inst)
        {
            if (inst == null) return null;
            var controller = UnityEngine.Object.FindObjectOfType<HexWorld3DController>(true);
            if (controller == null) return null;
            return controller.ResolveBuildingByName(inst.buildingName);
        }
    }
}
