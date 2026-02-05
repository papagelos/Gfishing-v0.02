// Assets/Minigames/HexWorld3D/Scripts/Village/HexWorldProductionTicker.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using GalacticFishing.UI;

namespace GalacticFishing.Minigames.HexWorld
{
    internal struct ProductionPopup
    {
        public HexWorldResourceId id;
        public int amount;
        public Vector3 pos;
    }

    /// <summary>
    /// Runs the global village production tick (default every 60 seconds),
    /// gathers output from all Active buildings, and pushes resources into the Warehouse.
    ///
    /// Policy: clamped. If the warehouse cannot fit the whole batch, it fills remaining space
    /// and discards the overflow (waste).
    ///
    /// TICKET 9: Now evaluates SynergyRules for each building to calculate bonuses.
    /// </summary>
    public sealed class HexWorldProductionTicker : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private HexWorldWarehouseInventory warehouse;

        [Header("Tick")]
        [SerializeField, Min(1f)] private float tickSeconds = 60f;

        [Header("Synergy Caps")]
        [Tooltip("Maximum total synergy bonus (2.0 = +200%)")]
        [SerializeField] private float maxSynergyBonus = 2.0f;

        [Header("Debug")]
        [SerializeField] private bool logEachTick;
        [SerializeField] private bool logSynergyDetails;

        public event Action TickCompleted;
        public event Action ProductionBlocked;

        private float _t;

        public float SecondsUntilTick => Mathf.Max(0f, tickSeconds - _t);

        /// <summary>
        /// Calculates the total synergy bonus for a building at the given coordinate.
        /// Returns a value between 0 and maxSynergyBonus (e.g., 0.15 = +15%).
        /// </summary>
        public float CalculateSynergyBonus(HexCoord coord, HexWorldBuildingDefinition def)
        {
            var controller = FindObjectOfType<HexWorld3DController>(true);
            float bonus = EvaluateSynergyRules(controller, coord, def);
            return Mathf.Clamp(bonus, 0f, maxSynergyBonus);
        }

        /// <summary>
        /// Result of evaluating a single synergy rule.
        /// </summary>
        public struct SynergyRuleResult
        {
            public SynergyRule rule;
            public bool isSatisfied;
            public int matchCount;
            public float bonusValue;
        }

        /// <summary>
        /// Evaluates all synergy rules for a building and returns detailed results for each rule.
        /// Used by the context menu to display the synergy checklist.
        /// </summary>
        public List<SynergyRuleResult> EvaluateSynergyRulesDetailed(HexCoord coord, HexWorldBuildingDefinition def)
        {
            var results = new List<SynergyRuleResult>();
            if (def == null || def.synergyRules == null) return results;

            var ctrl = FindObjectOfType<HexWorld3DController>(true);
            if (ctrl == null) return results;

            for (int i = 0; i < def.synergyRules.Count; i++)
            {
                var rule = def.synergyRules[i];
                if (rule == null) continue;

                int matchCount = EvaluateRuleMatchCount(ctrl, coord, rule);
                float bonus = CalculateRuleBonus(rule, matchCount);

                results.Add(new SynergyRuleResult
                {
                    rule = rule,
                    isSatisfied = matchCount > 0,
                    matchCount = matchCount,
                    bonusValue = bonus
                });
            }

            return results;
        }

        /// <summary>
        /// Gets the match count for a single synergy rule without calculating the bonus.
        /// </summary>
        private int EvaluateRuleMatchCount(HexWorld3DController controller, HexCoord coord, SynergyRule rule)
        {
            switch (rule.type)
            {
                case SynergyType.RoadAdjacent:
                    return controller.IsAdjacentToRoad(coord) ? 1 : 0;

                case SynergyType.RoadConnectedToTownHall:
                    return controller.IsConnectedToTownHall(coord) ? 1 : 0;

                case SynergyType.AdjacentTileTag:
                    return controller.CountAdjacentTilesWithTag(coord, rule.targetTagOrId);

                case SynergyType.WithinRadiusBuildingType:
                    return controller.CountBuildingsWithinRadius(coord, rule.targetTagOrId, rule.radius);

                default:
                    return 0;
            }
        }

        /// <summary>
        /// Calculates the bonus value for a rule given a match count.
        /// </summary>
        private float CalculateRuleBonus(SynergyRule rule, int matchCount)
        {
            if (matchCount <= 0) return 0f;

            if (rule.stacking == SynergyStacking.Binary)
            {
                return rule.amountPct;
            }
            else // PerCount
            {
                int effectiveCount = matchCount;
                if (rule.maxStacks > 0)
                    effectiveCount = Mathf.Min(effectiveCount, rule.maxStacks);
                return rule.amountPct * effectiveCount;
            }
        }

        /// <summary>
        /// Calculates the effective production per tick for a building.
        /// Returns the base output multiplied by (1 + synergyBonus).
        /// If the building is dormant, returns empty list.
        /// </summary>
        public List<HexWorldResourceStack> CalculateEffectiveProduction(HexWorldBuildingInstance instance)
        {
            var result = new List<HexWorldResourceStack>();
            if (instance == null || !instance.IsActive) return result;

            var prod = instance.GetComponent<HexWorldBuildingProductionProfile>();
            if (prod == null || prod.baseOutputPerTick == null) return result;

            var controller = FindObjectOfType<HexWorld3DController>(true);
            var def = controller != null ? controller.ResolveBuildingByName(instance.buildingName) : null;

            float synergyBonus = EvaluateSynergyRules(controller, instance.Coord, def);
            synergyBonus = Mathf.Clamp(synergyBonus, 0f, maxSynergyBonus);
            float multiplier = 1.0f + synergyBonus;

            for (int i = 0; i < prod.baseOutputPerTick.Count; i++)
            {
                var outp = prod.baseOutputPerTick[i];
                if (outp.id == HexWorldResourceId.None) continue;
                if (outp.amount <= 0) continue;

                int finalAmount = Mathf.RoundToInt(outp.amount * multiplier);
                if (finalAmount > 0)
                    result.Add(new HexWorldResourceStack(outp.id, finalAmount));
            }

            return result;
        }

        private void Awake()
        {
            if (!warehouse) warehouse = FindObjectOfType<HexWorldWarehouseInventory>(true);
        }

        private void Update()
        {
            if (!warehouse) return;

            _t += Time.deltaTime;
            if (_t < tickSeconds) return;
            _t -= tickSeconds;

            DoTick();
        }

        private void DoTick()
        {
            var popups = new List<ProductionPopup>(64);
            var batch = BuildBatchFromActiveBuildings(popups);

            if (batch.Count == 0)
            {
                if (logEachTick) Debug.Log("[HexWorldProduction] Tick: no active producers.");
                TickCompleted?.Invoke();
                return;
            }

            bool anyAdded = warehouse.TryAddClamped(batch, out int accepted, out int wasted);

            if (!anyAdded)
            {
                if (logEachTick)
                    Debug.LogWarning($"[HexWorldProduction] Tick blocked (warehouse full). Stored={warehouse.TotalStored}/{warehouse.Capacity}");
                ProductionBlocked?.Invoke();
                return;
            }

            // Spawn floating text popups for produced resources
            if (FloatingTextManager.Instance != null)
            {
                for (int i = 0; i < popups.Count; i++)
                {
                    var p = popups[i];
                    FloatingTextManager.Instance.SpawnWorld($"+{p.amount} {p.id}", p.pos);
                }
            }

            if (logEachTick)
            {
                int producedTotal = 0;
                for (int i = 0; i < batch.Count; i++)
                {
                    var s = batch[i];
                    if (s.id == HexWorldResourceId.None) continue;
                    if (s.amount <= 0) continue;
                    producedTotal += s.amount;
                }

                if (wasted > 0)
                    Debug.LogWarning($"[HexWorldProduction] Tick produced={producedTotal}, accepted={accepted}, wasted={wasted}. Stored={warehouse.TotalStored}/{warehouse.Capacity}");
                else
                    Debug.Log($"[HexWorldProduction] Tick produced={producedTotal}, accepted={accepted}. Stored={warehouse.TotalStored}/{warehouse.Capacity}");
            }

            TickCompleted?.Invoke();
        }

        private List<HexWorldResourceStack> BuildBatchFromActiveBuildings(List<ProductionPopup> popups)
        {
            var totals = new Dictionary<HexWorldResourceId, int>();

            // Find the controller to access owned tiles and synergy helpers
            var controller = FindObjectOfType<HexWorld3DController>(true);

            var states = FindObjectsOfType<HexWorldBuildingActiveState>(true);
            for (int i = 0; i < states.Length; i++)
            {
                var s = states[i];
                if (!s || !s.IsActive) continue;

                var prod = s.GetComponent<HexWorldBuildingProductionProfile>();
                if (prod == null || prod.baseOutputPerTick == null) continue;

                // Get building instance to find its coordinate and definition
                var buildingInst = s.GetComponent<HexWorldBuildingInstance>();
                if (buildingInst == null) continue;

                // Find building definition to get synergy rules
                var buildingDef = controller != null ? controller.ResolveBuildingByName(buildingInst.buildingName) : null;

                // Calculate total synergy bonus from all rules
                float synergyBonus = EvaluateSynergyRules(controller, buildingInst.Coord, buildingDef);

                // Clamp total bonus to maxSynergyBonus (default +200%)
                synergyBonus = Mathf.Clamp(synergyBonus, 0f, maxSynergyBonus);

                // Apply synergy multiplier (1.0 + bonus)
                float multiplier = 1.0f + synergyBonus;

                if (logSynergyDetails && synergyBonus > 0f)
                {
                    Debug.Log($"[Synergy] Building '{buildingInst.buildingName}' at {buildingInst.Coord}: +{synergyBonus * 100:F1}% bonus (multiplier={multiplier:F2})");
                }

                for (int j = 0; j < prod.baseOutputPerTick.Count; j++)
                {
                    var outp = prod.baseOutputPerTick[j];
                    if (outp.id == HexWorldResourceId.None) continue;
                    if (outp.amount <= 0) continue;

                    // Apply multiplier and round to int
                    int finalAmount = Mathf.RoundToInt(outp.amount * multiplier);
                    if (finalAmount <= 0) continue;

                    totals.TryGetValue(outp.id, out int have);
                    totals[outp.id] = have + finalAmount;

                    // Record popup for this building's production
                    popups.Add(new ProductionPopup
                    {
                        id = outp.id,
                        amount = finalAmount,
                        pos = s.transform.position + Vector3.up * 1.25f
                    });
                }
            }

            var batch = new List<HexWorldResourceStack>(totals.Count);
            foreach (var kv in totals)
                batch.Add(new HexWorldResourceStack(kv.Key, kv.Value));

            batch.Sort((a, b) => ((int)a.id).CompareTo((int)b.id));
            return batch;
        }

        /// <summary>
        /// Evaluates all synergy rules for a building and returns the total bonus percentage.
        /// </summary>
        private float EvaluateSynergyRules(HexWorld3DController controller, HexCoord coord, HexWorldBuildingDefinition def)
        {
            if (controller == null || def == null) return 0f;
            if (def.synergyRules == null || def.synergyRules.Count == 0) return 0f;

            float totalBonus = 0f;

            for (int i = 0; i < def.synergyRules.Count; i++)
            {
                var rule = def.synergyRules[i];
                if (rule == null) continue;

                float ruleBonus = EvaluateSingleRule(controller, coord, rule);
                totalBonus += ruleBonus;

                if (logSynergyDetails && ruleBonus > 0f)
                {
                    Debug.Log($"  [Synergy Rule] '{rule.label}': +{ruleBonus * 100:F1}%");
                }
            }

            return totalBonus;
        }

        /// <summary>
        /// Evaluates a single synergy rule and returns the bonus percentage.
        /// </summary>
        private float EvaluateSingleRule(HexWorld3DController controller, HexCoord coord, SynergyRule rule)
        {
            int matchCount = 0;

            switch (rule.type)
            {
                case SynergyType.RoadAdjacent:
                    // Check if building is adjacent to any road tile
                    matchCount = controller.IsAdjacentToRoad(coord) ? 1 : 0;
                    break;

                case SynergyType.RoadConnectedToTownHall:
                    // Check if building is connected to Town Hall via roads
                    matchCount = controller.IsConnectedToTownHall(coord) ? 1 : 0;
                    break;

                case SynergyType.AdjacentTileTag:
                    // Count adjacent tiles with the specified tag
                    matchCount = controller.CountAdjacentTilesWithTag(coord, rule.targetTagOrId);
                    break;

                case SynergyType.WithinRadiusBuildingType:
                    // Count buildings of specified type within radius
                    matchCount = controller.CountBuildingsWithinRadius(coord, rule.targetTagOrId, rule.radius);
                    break;

                default:
                    return 0f;
            }

            if (matchCount <= 0)
                return 0f;

            // Apply stacking rules
            if (rule.stacking == SynergyStacking.Binary)
            {
                // Binary: bonus applies once if any match exists
                return rule.amountPct;
            }
            else // PerCount
            {
                // PerCount: bonus stacks per matching count
                int effectiveCount = matchCount;

                // Apply max stacks cap if configured
                if (rule.maxStacks > 0)
                    effectiveCount = Mathf.Min(effectiveCount, rule.maxStacks);

                return rule.amountPct * effectiveCount;
            }
        }
    }
}
