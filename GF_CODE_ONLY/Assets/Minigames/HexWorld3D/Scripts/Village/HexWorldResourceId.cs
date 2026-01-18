// Assets/Minigames/HexWorld3D/Scripts/Village/HexWorldResourceId.cs
using System;

namespace GalacticFishing.Minigames.HexWorld
{
    /// <summary>
    /// Resource identifiers for the HexWorld3D minigame.
    /// NOTE: Keep 'None' as 0 (some systems use it as a sentinel).
    /// </summary>
    public enum HexWorldResourceId
    {
        None = 0,

        // Currencies (even if not stored in the warehouse, having IDs is convenient)
        Credits = 1,
        Deeds = 2,
        DeedShard = 3,

        // Warehouse-stored resources (MVP)
        Wood = 10,
        Stone = 11,
        Fiber = 12,
        BaitIngredients = 13,
    }
}
