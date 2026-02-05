// Assets/Minigames/HexWorld3D/Scripts/Village/IWorldProgression.cs
namespace GalacticFishing
{
    /// <summary>
    /// Interface for accessing world progression data.
    /// Used to gate Town Hall upgrades based on player's highest unlocked world.
    /// </summary>
    public interface IWorldProgression
    {
        /// <summary>
        /// Returns the highest world number the player has unlocked.
        /// For example, if player has completed World 3, this returns 3.
        /// </summary>
        int HighestUnlockedWorldNumber { get; }
    }
}
