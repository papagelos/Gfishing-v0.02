// Assets/Minigames/HexWorld3D/Scripts/Village/HexWorldRuntimeBuildingConfigurator.cs
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

        private static void ApplyMvpOutputs(HexWorldBuildingInstance inst, HexWorldBuildingProductionProfile prod)
        {
            // If already configured (has outputs), do nothing.
            if (prod.baseOutputPerTick != null && prod.baseOutputPerTick.Count > 0)
                return;

            string n = inst.name.ToLowerInvariant();

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

            // MVP producer mapping by name
            // Lumberyard: +6 Wood
            if (n.Contains("lumber") || n.Contains("wood"))
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

            // Unknown building => default non-producing, consumes slot if activated (you can change later)
            prod.consumesActiveSlot = true;
        }
    }
}
