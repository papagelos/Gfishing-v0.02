using System.Collections.Generic;
using System.Text;
using GalacticFishing.Minigames.HexWorld;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GalacticFishing.Minigames.Dungeon3D
{
    [DisallowMultipleComponent]
    public sealed class DungeonRunInventory : MonoBehaviour
    {
        [SerializeField] private bool verboseLogging;

        private readonly Dictionary<HexWorldResourceId, int> _loot = new();
        private static readonly Dictionary<HexWorldResourceId, int> s_pendingTransfer = new();
        private static bool s_waitingForWarehouseFlush;
        public event System.Action OnChanged;

        public IReadOnlyDictionary<HexWorldResourceId, int> Loot => _loot;

        public void AddLoot(HexWorldResourceId id, int amount)
        {
            if (id == HexWorldResourceId.None || amount <= 0)
                return;

            _loot.TryGetValue(id, out int current);
            _loot[id] = current + amount;
            OnChanged?.Invoke();

            if (verboseLogging)
                Debug.Log($"[DungeonRunInventory] +{amount} {id} (total: {_loot[id]})", this);
        }

        public int GetAmount(HexWorldResourceId id)
        {
            if (id == HexWorldResourceId.None)
                return 0;

            return _loot.TryGetValue(id, out int amount) ? amount : 0;
        }

        public void ClearLoot()
        {
            _loot.Clear();
        }

        private void OnDisable()
        {
            // Safety net: if this inventory is torn down during scene unload, preserve any unpushed loot.
            if (_loot.Count == 0)
                return;

            QueueInstanceLootForWarehouseTransfer(this);
        }

        /// <summary>
        /// Transfers this run's loot into the warehouse as weightless stacks and clears the run inventory.
        /// </summary>
        public void PushToWarehouse(HexWorldWarehouseInventory warehouse)
        {
            if (warehouse == null || _loot.Count == 0)
                return;

            foreach (var kv in _loot)
            {
                if (kv.Key == HexWorldResourceId.None || kv.Value <= 0)
                    continue;

                warehouse.TryAddWeightless(kv.Key, kv.Value);
            }

            _loot.Clear();
            OnChanged?.Invoke();

            if (verboseLogging)
                Debug.Log("[DungeonRunInventory] Loot pushed to warehouse (weightless).", this);
        }

        /// <summary>
        /// Captures loot from the active dungeon run and schedules delivery to a warehouse after scene load.
        /// Call this right before leaving the dungeon scene.
        /// </summary>
        public static bool QueueActiveRunLootForWarehouseTransfer()
        {
            DungeonRunInventory runInventory = Object.FindAnyObjectByType<DungeonRunInventory>(FindObjectsInactive.Include);
            if (runInventory == null || runInventory._loot.Count == 0)
                return false;

            // Fast path: if a warehouse already exists, push immediately.
            HexWorldWarehouseInventory existingWarehouse = Object.FindAnyObjectByType<HexWorldWarehouseInventory>(FindObjectsInactive.Include);
            if (existingWarehouse != null)
            {
                runInventory.PushToWarehouse(existingWarehouse);
                return true;
            }

            QueueInstanceLootForWarehouseTransfer(runInventory);

            if (!s_waitingForWarehouseFlush)
            {
                SceneManager.sceneLoaded += OnSceneLoadedTryFlushPending;
                s_waitingForWarehouseFlush = true;
            }

            // In case a warehouse already exists in the active scene.
            TryFlushPendingToAnyWarehouse();
            return true;
        }

        public static bool TryFlushPendingToAnyWarehouse()
        {
            HexWorldWarehouseInventory warehouse = Object.FindAnyObjectByType<HexWorldWarehouseInventory>(FindObjectsInactive.Include);
            return TryFlushPendingToWarehouse(warehouse);
        }

        public static bool TryFlushPendingToWarehouse(HexWorldWarehouseInventory warehouse)
        {
            if (warehouse == null || s_pendingTransfer.Count == 0)
                return false;

            foreach (var kv in s_pendingTransfer)
            {
                if (kv.Key == HexWorldResourceId.None || kv.Value <= 0)
                    continue;

                warehouse.TryAddWeightless(kv.Key, kv.Value);
            }

            s_pendingTransfer.Clear();
            StopWaitingForFlush();

            Debug.Log("[DungeonRunInventory] Pending loot delivered to warehouse.");
            return true;
        }

        public string ToDebugString()
        {
            if (_loot.Count == 0)
                return "Dungeon Loot: (empty)";

            var sb = new StringBuilder(128);
            sb.Append("Dungeon Loot: ");

            bool first = true;
            foreach (var kv in _loot)
            {
                if (!first)
                    sb.Append(" | ");

                sb.Append(kv.Key).Append(": ").Append(kv.Value);
                first = false;
            }

            return sb.ToString();
        }

        private static void OnSceneLoadedTryFlushPending(Scene scene, LoadSceneMode mode)
        {
            if (s_pendingTransfer.Count == 0)
            {
                StopWaitingForFlush();
                return;
            }

            TryFlushPendingToAnyWarehouse();
        }

        private static void StopWaitingForFlush()
        {
            if (!s_waitingForWarehouseFlush)
                return;

            SceneManager.sceneLoaded -= OnSceneLoadedTryFlushPending;
            s_waitingForWarehouseFlush = false;
        }

        private static void QueueInstanceLootForWarehouseTransfer(DungeonRunInventory inventory)
        {
            if (inventory == null || inventory._loot.Count == 0)
                return;

            foreach (var kv in inventory._loot)
            {
                if (kv.Key == HexWorldResourceId.None || kv.Value <= 0)
                    continue;

                s_pendingTransfer.TryGetValue(kv.Key, out int current);
                s_pendingTransfer[kv.Key] = current + kv.Value;
            }

            inventory._loot.Clear();
            inventory.OnChanged?.Invoke();

            if (!s_waitingForWarehouseFlush)
            {
                SceneManager.sceneLoaded += OnSceneLoadedTryFlushPending;
                s_waitingForWarehouseFlush = true;
            }
        }
    }
}
