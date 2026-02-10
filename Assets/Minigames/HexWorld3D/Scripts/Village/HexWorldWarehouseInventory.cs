// Assets/Minigames/HexWorld3D/Scripts/Village/HexWorldWarehouseInventory.cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace GalacticFishing.Minigames.HexWorld
{
    /// <summary>
    /// Simple warehouse inventory with a hard capacity.
    /// Capacity is base storage plus capacity unlocked by Warehouse level.
    /// Stores stacks of resources; total capacity is the sum of all stored amounts.
    /// </summary>
    public sealed class HexWorldWarehouseInventory : MonoBehaviour
    {
        // New preferred event name (matches label/UI scripts).
        public event Action InventoryChanged;

        // Back-compat event name (older scripts may subscribe to this).
        public event Action Changed;

        [Header("Warehouse")]
        [SerializeField, Min(0)] private int baseStorageCapacity = 50;
        [SerializeField, Range(0, 7)] private int warehouseLevel = 0;

        // Internal store
        private readonly Dictionary<HexWorldResourceId, int> _store = new();

        public int WarehouseLevel
        {
            get => warehouseLevel;
            set
            {
                int v = Mathf.Clamp(value, 0, 7);
                if (v == warehouseLevel) return;
                warehouseLevel = v;
                RaiseChanged();
            }
        }

        public int Capacity => GetCapacityForLevel(warehouseLevel);

        public int GetCapacityForLevel(int level)
        {
            return Mathf.Max(0, baseStorageCapacity) + GetCapacityForWarehouseLevel(level);
        }

        public int TotalStored
        {
            get
            {
                int sum = 0;
                foreach (var kv in _store)
                    sum += Mathf.Max(0, kv.Value);
                return sum;
            }
        }

        public int FreeSpace => Mathf.Max(0, Capacity - TotalStored);

        public bool IsFull => TotalStored >= Capacity;

        public int Get(HexWorldResourceId id)
        {
            if (id == HexWorldResourceId.None) return 0;
            return _store.TryGetValue(id, out int v) ? v : 0;
        }

        public int GetFreeSpace(HexWorldResourceId id)
        {
            if (id == HexWorldResourceId.None) return 0;
            return Mathf.Max(0, Capacity - Get(id));
        }

        /// <summary>
        /// Reflection-friendly lookup by enum name (e.g. "Wood", "BaitIngredients").
        /// </summary>
        public int Get(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return 0;

            if (Enum.TryParse(id.Trim(), ignoreCase: true, out HexWorldResourceId parsedId))
                return Get(parsedId);

            return 0;
        }

        public void ClearAll()
        {
            if (_store.Count == 0) return;
            _store.Clear();
            RaiseChanged();
        }

        public bool TryRemove(HexWorldResourceId id, int amount)
        {
            if (id == HexWorldResourceId.None) return false;
            if (amount <= 0) return true;

            int cur = Get(id);
            if (cur < amount) return false;

            int next = cur - amount;
            if (next <= 0) _store.Remove(id);
            else _store[id] = next;

            RaiseChanged();
            return true;
        }

        public bool TryAdd(HexWorldResourceId id, int amount)
        {
            if (id == HexWorldResourceId.None) return false;
            if (amount <= 0) return true;

            int cur = Get(id);
            int room = Capacity - cur;
            if (room <= 0)
                return false;

            int toAdd = Mathf.Min(amount, room);
            if (toAdd <= 0)
                return false;

            _store[id] = cur + toAdd;
            RaiseChanged();
            return true;
        }

        /// <summary>
        /// Adds all stacks if and only if they all fit (no partial adds).
        /// Returns false if the full batch doesn't fit.
        /// </summary>
        public bool TryAddAllOrNothing(IReadOnlyList<HexWorldResourceStack> stacks)
        {
            if (stacks == null || stacks.Count == 0) return true;

            var pendingByResource = new Dictionary<HexWorldResourceId, int>();
            for (int i = 0; i < stacks.Count; i++)
            {
                var s = stacks[i];
                if (s.id == HexWorldResourceId.None) continue;
                if (s.amount <= 0) continue;

                pendingByResource.TryGetValue(s.id, out int pending);
                pendingByResource[s.id] = pending + s.amount;
            }

            if (pendingByResource.Count == 0) return true;

            foreach (var kv in pendingByResource)
            {
                int nextAmount = Get(kv.Key) + kv.Value;
                if (nextAmount > Capacity)
                    return false;
            }

            // Commit
            foreach (var kv in pendingByResource)
            {
                int cur = Get(kv.Key);
                _store[kv.Key] = cur + kv.Value;
            }

            RaiseChanged();
            return true;
        }

        // Overloads for common call-site types.
        public bool TryAddAllOrNothing(List<HexWorldResourceStack> stacks) => TryAddAllOrNothing((IReadOnlyList<HexWorldResourceStack>)stacks);
        public bool TryAddAllOrNothing(HexWorldResourceStack[] stacks) => TryAddAllOrNothing((IReadOnlyList<HexWorldResourceStack>)stacks);

        /// <summary>
        /// CLAMPED deposit: fills remaining per-resource room, discards overflow.
        /// Returns true if ANY amount was added. Returns false if nothing could be added.
        /// Outputs accepted and wasted totals.
        /// </summary>
        public bool TryAddClamped(IReadOnlyList<HexWorldResourceStack> stacks, out int accepted, out int wasted)
        {
            accepted = 0;
            wasted = 0;

            if (stacks == null || stacks.Count == 0)
                return true; // no-op, not blocked

            bool changed = false;

            // Deterministic: process in provided order.
            for (int i = 0; i < stacks.Count; i++)
            {
                var s = stacks[i];
                if (s.id == HexWorldResourceId.None) continue;
                if (s.amount <= 0) continue;

                int cur = Get(s.id);
                int room = Mathf.Max(0, Capacity - cur);
                int add = Mathf.Min(s.amount, room);
                int overflow = s.amount - add;

                if (add > 0)
                {
                    _store[s.id] = cur + add;
                    accepted += add;
                    changed = true;
                }

                if (overflow > 0)
                    wasted += overflow;
            }

            if (changed)
                RaiseChanged();

            return accepted > 0;
        }

        public bool TryAddClamped(List<HexWorldResourceStack> stacks, out int accepted, out int wasted)
            => TryAddClamped((IReadOnlyList<HexWorldResourceStack>)stacks, out accepted, out wasted);

        public bool TryAddClamped(HexWorldResourceStack[] stacks, out int accepted, out int wasted)
            => TryAddClamped((IReadOnlyList<HexWorldResourceStack>)stacks, out accepted, out wasted);

        public List<HexWorldResourceStack> ToStacks()
        {
            var list = new List<HexWorldResourceStack>(_store.Count);
            foreach (var kv in _store)
            {
                if (kv.Key == HexWorldResourceId.None) continue;
                if (kv.Value <= 0) continue;
                list.Add(new HexWorldResourceStack(kv.Key, kv.Value));
            }
            return list;
        }

        /// <summary>
        /// Back-compat signature.
        /// </summary>
        public void LoadFromStacks(IEnumerable<HexWorldResourceStack> stacks)
        {
            LoadFromStacks(stacks, level: -1);
        }

        /// <summary>
        /// Load inventory from stacks; optionally set warehouse level.
        /// Parameter name MUST be 'level' because some call sites use named args: level: X
        /// </summary>
        public void LoadFromStacks(IEnumerable<HexWorldResourceStack> stacks, int level = -1)
        {
            if (level >= 0)
                WarehouseLevel = level;

            _store.Clear();
            if (stacks != null)
            {
                foreach (var s in stacks)
                {
                    if (s.id == HexWorldResourceId.None) continue;
                    if (s.amount <= 0) continue;
                    _store[s.id] = s.amount;
                }
            }

            RaiseChanged();
        }

        private void RaiseChanged()
        {
            InventoryChanged?.Invoke();
            Changed?.Invoke();
        }

        public static int GetCapacityForWarehouseLevel(int level)
        {
            // Design doc warehouse caps:
            // L1 200, L2 450, L3 800, L4 1400, L5 2300, L6 3600, L7 5400
            if (level <= 0)
                return 0;

            switch (Mathf.Clamp(level, 1, 7))
            {
                case 1: return 200;
                case 2: return 450;
                case 3: return 800;
                case 4: return 1400;
                case 5: return 2300;
                case 6: return 3600;
                case 7: return 5400;
                default: return 0;
            }
        }
    }
}
