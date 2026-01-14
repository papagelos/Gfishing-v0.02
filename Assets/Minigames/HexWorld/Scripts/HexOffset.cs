using UnityEngine;

namespace GalacticFishing.Minigames.HexWorld
{
    /// <summary>
    /// Unity hex tilemaps use OFFSET coordinates (col,row). We convert to Axial for neighbors/distance.
    /// For flat-top hexes, the common layouts are Odd-R or Even-R (row offset).
    /// For pointy-top hexes, the common layouts are Odd-Q or Even-Q (column offset).
    /// </summary>
    public static class HexOffset
    {
        // Keep existing values first so your current serialized inspector choice doesn't get scrambled.
        public enum OffsetMode
        {
            OddQ = 0,
            EvenQ = 1,
            OddR = 2,
            EvenR = 3
        }

        public static HexCoord OffsetToAxial(int col, int row, OffsetMode mode)
        {
            switch (mode)
            {
                // Column-offset (often used with pointy-top)
                case OffsetMode.OddQ:
                {
                    int q = col;
                    int r = row - ((col - (col & 1)) / 2);
                    return new HexCoord(q, r);
                }
                case OffsetMode.EvenQ:
                {
                    int q = col;
                    int r = row - ((col + (col & 1)) / 2);
                    return new HexCoord(q, r);
                }

                // Row-offset (often used with flat-top)
                case OffsetMode.OddR:
                {
                    int r = row;
                    int q = col - ((row - (row & 1)) / 2);
                    return new HexCoord(q, r);
                }
                case OffsetMode.EvenR:
                default:
                {
                    int r = row;
                    int q = col - ((row + (row & 1)) / 2);
                    return new HexCoord(q, r);
                }
            }
        }

        public static Vector2Int AxialToOffset(HexCoord a, OffsetMode mode)
        {
            switch (mode)
            {
                // Column-offset (often used with pointy-top)
                case OffsetMode.OddQ:
                {
                    int col = a.q;
                    int row = a.r + ((a.q - (a.q & 1)) / 2);
                    return new Vector2Int(col, row);
                }
                case OffsetMode.EvenQ:
                {
                    int col = a.q;
                    int row = a.r + ((a.q + (a.q & 1)) / 2);
                    return new Vector2Int(col, row);
                }

                // Row-offset (often used with flat-top)
                case OffsetMode.OddR:
                {
                    int row = a.r;
                    int col = a.q + ((a.r - (a.r & 1)) / 2);
                    return new Vector2Int(col, row);
                }
                case OffsetMode.EvenR:
                default:
                {
                    int row = a.r;
                    int col = a.q + ((a.r + (a.r & 1)) / 2);
                    return new Vector2Int(col, row);
                }
            }
        }
    }
}
