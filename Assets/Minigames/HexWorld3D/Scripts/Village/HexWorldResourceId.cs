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

        // Fish/resources used for upgrade costs (add as needed)
        Anglerfish = 14,
        RawHide = 15,
        Feathers = 16,
        Herbs = 17,

        // Mining resources (Quarry Strata Drill)
        Coal = 20,
        Copper = 21,
        Iron = 22,
        Gold = 23,
        Clay = 24,

        // Refined/Processed resources (TICKET 20)
        Planks = 30,
        StoneBlocks = 31,
        Cloth = 32,
        Leather = 33,
        MetalIngots = 34,
        Tool_Handle = 35,
        MetalPlates = 36,
        Bricks = 37,
        Charcoal = 38,
        Rope = 39,
        FineLeather = 40,
        Rations = 41,
        HealingOintment = 42,

        // Tools (TICKET 23)
        Tool_Saw = 50,
        Tool_Chisel = 51,
        Tool_Hammer = 52,
        Tool_FurnaceCore = 53,
        Tool_Vat = 54,
        Tool_LoomFrame = 55,
        Tool_PrepSet = 56,

        // Town Hall milestone resources (TICKET 56)
        MuseumEnrollmentSeal = 60,
        DungeonSurveyBadge = 61,
        LakeBossTrophy1 = 62,
        HeistAccessPass = 63,
        DungeonRelicCore = 64,
        LakeBossTrophy2 = 65,
    }
}
