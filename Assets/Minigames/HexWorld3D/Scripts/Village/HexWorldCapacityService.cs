using System;
using UnityEngine;

namespace GalacticFishing.Minigames.HexWorld
{
    /// <summary>
    /// Holds Town Hall level and exposes capacity values (Active Slots) from the design doc table.
    /// Includes a static helper GetActiveSlots(int) because some callers use it statically.
    /// </summary>
    public sealed class HexWorldCapacityService : MonoBehaviour
    {
        public event Action Changed;

        [Header("Town Hall")]
        [SerializeField, Range(1, 10)] private int townHallLevel = 1;

        /// <summary>Town Hall level (1..10)</summary>
        public int TownHallLevel
        {
            get => townHallLevel;
            set
            {
                int v = Mathf.Clamp(value, 1, 10);
                if (v == townHallLevel) return;
                townHallLevel = v;
                Changed?.Invoke();
            }
        }

        /// <summary>Active slots for the current Town Hall level.</summary>
        public int ActiveSlots => GetActiveSlots(townHallLevel);

        /// <summary>
        /// Design doc Active Slots table (Town Hall L1..L10).
        /// This is static so callers can do HexWorldCapacityService.GetActiveSlots(level).
        /// </summary>
        public static int GetActiveSlots(int level)
        {
            int l = Mathf.Clamp(level, 1, 10);

            // Doc: L1=2, L2=3, L3=4, L4=5, L5=6, L6=7, L7=8, L8=9, L9=10, L10=12
            switch (l)
            {
                case 1: return 2;
                case 2: return 3;
                case 3: return 4;
                case 4: return 5;
                case 5: return 6;
                case 6: return 7;
                case 7: return 8;
                case 8: return 9;
                case 9: return 10;
                case 10: return 12;
                default: return 2;
            }
        }

        /// <summary>
        /// Tile capacity granted by Town Hall level.
        /// L1=37, L2=61, L3=91, L4=127, L5=169, L6=217, L7=271, L8=331, L9=397, L10=469
        /// </summary>
        public static int GetTileCapacity(int level)
        {
            int l = Mathf.Clamp(level, 1, 10);

            switch (l)
            {
                case 1: return 37;
                case 2: return 61;
                case 3: return 91;
                case 4: return 127;
                case 5: return 169;
                case 6: return 217;
                case 7: return 271;
                case 8: return 331;
                case 9: return 397;
                case 10: return 469;
                default: return 37;
            }
        }
    }
}
