using System;

namespace GalacticFishing.Minigames.HexWorld
{
    public struct TileContribution
    {
        public double[] prodPerSec; // ResourceType indexed
        public double[] stats;      // StatType indexed

        public static int ResourceCount { get; } = Enum.GetValues(typeof(ResourceType)).Length;
        public static int StatCount { get; } = Enum.GetValues(typeof(StatType)).Length;

        public static TileContribution CreateEmpty()
        {
            return new TileContribution
            {
                prodPerSec = new double[ResourceCount],
                stats = new double[StatCount]
            };
        }

        public void Clear()
        {
            Array.Clear(prodPerSec, 0, prodPerSec.Length);
            Array.Clear(stats, 0, stats.Length);
        }
    }
}
