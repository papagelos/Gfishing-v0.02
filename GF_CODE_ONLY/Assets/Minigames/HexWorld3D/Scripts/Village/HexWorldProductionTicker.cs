// Assets/Minigames/HexWorld3D/Scripts/Village/HexWorldProductionTicker.cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace GalacticFishing.Minigames.HexWorld
{
    /// <summary>
    /// Runs the global village production tick (default every 60 seconds),
    /// gathers output from all Active buildings, and pushes resources into the Warehouse.
    ///
    /// Policy: all-or-nothing. If the warehouse cannot fit the whole batch, nothing is added.
    /// </summary>
    public sealed class HexWorldProductionTicker : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private HexWorldWarehouseInventory warehouse;

        [Header("Tick")]
        [SerializeField, Min(1f)] private float tickSeconds = 60f;

        [Header("Debug")]
        [SerializeField] private bool logEachTick;

        public event Action TickCompleted;
        public event Action ProductionBlocked;

        private float _t;

        public float SecondsUntilTick => Mathf.Max(0f, tickSeconds - _t);

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
            var batch = BuildBatchFromActiveBuildings();

            if (batch.Count == 0)
            {
                if (logEachTick) Debug.Log("[HexWorldProduction] Tick: no active producers.");
                TickCompleted?.Invoke();
                return;
            }

            bool ok = warehouse.TryAddAllOrNothing(batch);
            if (!ok)
            {
                if (logEachTick) Debug.LogWarning($"[HexWorldProduction] Tick blocked (warehouse full). Stored={warehouse.TotalStored}/{warehouse.Capacity}");
                ProductionBlocked?.Invoke();
                return;
            }

            if (logEachTick)
                Debug.Log($"[HexWorldProduction] Tick produced: {string.Join(", ", batch)}. Stored={warehouse.TotalStored}/{warehouse.Capacity}");

            TickCompleted?.Invoke();
        }

        private static List<HexWorldResourceStack> BuildBatchFromActiveBuildings()
        {
            var totals = new Dictionary<HexWorldResourceId, int>();

            // Find the controller to access owned tiles
            var controller = FindObjectOfType<HexWorld3DController>(true);
            var ownedTiles = controller != null ? controller.OwnedTiles : null;

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

                // Find building definition to get terrain type
                var buildingDef = controller != null ? controller.ResolveBuildingByName(buildingInst.buildingName) : null;

                // Calculate district bonus
                float districtBonus = 0f;
                if (buildingDef != null && ownedTiles != null)
                {
                    districtBonus = HexWorldDistrictBonusService.CalculateDistrictBonus(
                        buildingInst.Coord,
                        buildingDef.preferredTerrainType,
                        ownedTiles);
                }

                // Apply district bonus multiplier (1.0 + bonus)
                float multiplier = 1.0f + districtBonus;

                for (int j = 0; j < prod.baseOutputPerTick.Count; j++)
                {
                    var outp = prod.baseOutputPerTick[j];
                    if (outp.id == HexWorldResourceId.None) continue;
                    if (outp.amount <= 0) continue;

                    // Apply multiplier and round to int
                    int finalAmount = Mathf.RoundToInt(outp.amount * multiplier);

                    totals.TryGetValue(outp.id, out int have);
                    totals[outp.id] = have + finalAmount;
                }
            }

            var batch = new List<HexWorldResourceStack>(totals.Count);
            foreach (var kv in totals)
                batch.Add(new HexWorldResourceStack(kv.Key, kv.Value));

            batch.Sort((a, b) => ((int)a.id).CompareTo((int)b.id));
            return batch;
        }
    }
}
