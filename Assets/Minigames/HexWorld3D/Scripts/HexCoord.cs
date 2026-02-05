using System;
using UnityEngine;

namespace GalacticFishing.Minigames.HexWorld
{
    /// <summary>Axial hex coordinates (q, r). Cube s = -q-r.</summary>
    [Serializable]
    public readonly struct HexCoord : IEquatable<HexCoord>
    {
        public readonly int q;
        public readonly int r;

        public HexCoord(int q, int r)
        {
            this.q = q;
            this.r = r;
        }

        public int s => -q - r;

        public static readonly HexCoord[] NeighborDirs =
        {
            new HexCoord(+1,  0),
            new HexCoord(+1, -1),
            new HexCoord( 0, -1),
            new HexCoord(-1,  0),
            new HexCoord(-1, +1),
            new HexCoord( 0, +1),
        };

        public HexCoord Neighbor(int i)
        {
            var d = NeighborDirs[i];
            return new HexCoord(q + d.q, r + d.r);
        }

        public int DistanceTo(HexCoord other)
        {
            int dq = Mathf.Abs(q - other.q);
            int dr = Mathf.Abs(r - other.r);
            int ds = Mathf.Abs(s - other.s);
            return (dq + dr + ds) / 2;
        }

        public bool Equals(HexCoord other) => q == other.q && r == other.r;
        public override bool Equals(object obj) => obj is HexCoord other && Equals(other);
        public override int GetHashCode() => unchecked((q * 397) ^ r);
        public static bool operator ==(HexCoord a, HexCoord b) => a.Equals(b);
        public static bool operator !=(HexCoord a, HexCoord b) => !a.Equals(b);
        public override string ToString() => $"({q},{r})";
    }
}
