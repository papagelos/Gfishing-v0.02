using System.Collections.Generic;
using System.Text;
using GalacticFishing.Minigames.HexWorld;
using UnityEngine;

namespace GalacticFishing.Minigames.Dungeon3D
{
    [DisallowMultipleComponent]
    public sealed class DungeonRunInventory : MonoBehaviour
    {
        [SerializeField] private bool verboseLogging;

        private readonly Dictionary<HexWorldResourceId, int> _loot = new();
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
    }
}
