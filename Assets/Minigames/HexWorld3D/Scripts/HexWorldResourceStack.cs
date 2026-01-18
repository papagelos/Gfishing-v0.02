// Assets/Minigames/HexWorld3D/Scripts/Village/HexWorldResourceStack.cs
using System;

namespace GalacticFishing.Minigames.HexWorld
{
    /// <summary>
    /// Serializable (resource, amount) pair.
    /// </summary>
    [Serializable]
    public struct HexWorldResourceStack
    {
        public HexWorldResourceId id;
        public int amount;

        public HexWorldResourceStack(HexWorldResourceId id, int amount)
        {
            this.id = id;
            this.amount = amount;
        }
    }
}
