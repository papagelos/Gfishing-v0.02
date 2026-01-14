// Assets/Scripts/Progress/PlayerStatsData.cs
using System;

namespace GalacticFishing.Progress
{
    [Serializable]
    public class PlayerStatsData
    {
        // Raw counters
        public long totalFishCaught;
        public float totalKgCaught;

        public long manualCasts;
        public long manualCatches;

        public long autoCasts;
        public long autoCatches;

        // You can extend later:
        // public long critHooks;
        // public long perfectCasts;
        // public long bossFishCaught;
        
        // Convenience (not serialized necessarily)
        public float ManualAccuracy =>
            manualCasts > 0 ? (float)manualCatches / manualCasts : 0f;

        public float AutoAccuracy =>
            autoCasts > 0 ? (float)autoCatches / autoCasts : 0f;
    }
}
