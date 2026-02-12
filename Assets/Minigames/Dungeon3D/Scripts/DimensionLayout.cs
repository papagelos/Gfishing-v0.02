using System;
using System.Collections.Generic;
using GalacticFishing.Minigames.HexWorld;
using UnityEngine;

namespace GalacticFishing.Minigames.Dungeon3D
{
    public enum DimensionTileKind
    {
        Spine = 0,
        Pocket = 1,
        Filler = 2,
    }

    [Serializable]
    public struct DimensionTileData
    {
        public HexCoord coord;
        public string biomeGroup;
        public bool hasProp;
        public string propId;
        public DimensionTileKind kind;
    }

    [Serializable]
    public sealed class DimensionLayout
    {
        public int seedUsed;
        public HexCoord startCoord;
        public HexCoord bossCoord;
        public bool bossReachable;

        public List<HexCoord> spineCoords = new();
        public List<HexCoord> pocketCoords = new();
        public List<DimensionTileData> tiles = new();

        public int WalkableCount => tiles?.Count ?? 0;

        public void Clear()
        {
            seedUsed = 0;
            startCoord = new HexCoord(0, 0);
            bossCoord = new HexCoord(0, 0);
            bossReachable = false;
            spineCoords.Clear();
            pocketCoords.Clear();
            tiles.Clear();
        }

        public HashSet<HexCoord> BuildWalkableSet()
        {
            var set = new HashSet<HexCoord>();
            for (int i = 0; i < tiles.Count; i++)
                set.Add(tiles[i].coord);
            return set;
        }
    }
}
