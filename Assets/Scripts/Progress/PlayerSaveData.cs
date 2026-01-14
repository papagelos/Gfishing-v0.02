// Assets/Scripts/Progress/PlayerSaveData.cs
using System;

namespace GalacticFishing.Progress
{
    /// <summary>
    /// Root of everything we save for the player.
    /// Extend this class with new fields as systems are added.
    /// </summary>
    [Serializable]
    public class PlayerSaveData
    {
        // What gear the player owns / has equipped.
        public PlayerGearData gear = new PlayerGearData();

        // Lifetime stats (casts, catches, kg, etc).
        public PlayerStatsData stats = new PlayerStatsData();

                // NEW: Currency data
        public PlayerCurrencyData currency = new PlayerCurrencyData();


        // Later:
        // public InventorySaveData inventory;
        // public UpgradeDeckSaveData decks;
        // public string currentWorldId;
    }
}
