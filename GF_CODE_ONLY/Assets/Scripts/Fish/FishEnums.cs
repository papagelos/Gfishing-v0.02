// FishEnums.cs
using System;

public enum FishTier
{
    Tier1, Tier2, Tier3, Tier4, Boss
}

public enum FishRarity
{
    Common, Uncommon, Rare, Epic, Legendary, UberLegendary, OneOfAKind
}

[Flags]
public enum FishFlags
{
    None        = 0,
    Freshwater  = 1 << 0,
    Saltwater   = 1 << 1,
    Day         = 1 << 2,
    Night       = 1 << 3,
    Shallow     = 1 << 4,
    Deep        = 1 << 5,
    Aggressive  = 1 << 6,
    Skittish    = 1 << 7,
    Event       = 1 << 8
}
