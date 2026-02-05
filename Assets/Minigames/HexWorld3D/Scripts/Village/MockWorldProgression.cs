// Assets/Minigames/HexWorld3D/Scripts/Village/MockWorldProgression.cs
using UnityEngine;

namespace GalacticFishing.Minigames.HexWorld
{
    /// <summary>
    /// Mock implementation of IWorldProgression for testing.
    /// Returns a configurable test value for HighestUnlockedWorldNumber.
    /// Will be replaced with real world progression system later.
    /// </summary>
    public sealed class MockWorldProgression : MonoBehaviour, GalacticFishing.IWorldProgression
    {
        [Header("Mock Settings")]
        [Tooltip("Simulated highest unlocked world number. Used for testing Town Hall upgrade gating.")]
        [SerializeField] private int mockHighestUnlockedWorld = 1;

        public int HighestUnlockedWorldNumber => mockHighestUnlockedWorld;

        [ContextMenu("Debug: Set Unlocked World to 10")]
        public void DebugSetUnlockedWorldTo10()
        {
            mockHighestUnlockedWorld = 10;
            Debug.Log("MockWorldProgression: Set HighestUnlockedWorldNumber to 10");
        }

        [ContextMenu("Debug: Set Unlocked World to 5")]
        public void DebugSetUnlockedWorldTo5()
        {
            mockHighestUnlockedWorld = 5;
            Debug.Log("MockWorldProgression: Set HighestUnlockedWorldNumber to 5");
        }

        [ContextMenu("Debug: Set Unlocked World to 1")]
        public void DebugSetUnlockedWorldTo1()
        {
            mockHighestUnlockedWorld = 1;
            Debug.Log("MockWorldProgression: Set HighestUnlockedWorldNumber to 1");
        }
    }
}
