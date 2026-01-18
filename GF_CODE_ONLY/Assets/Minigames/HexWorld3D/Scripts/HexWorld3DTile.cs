using UnityEngine;

namespace GalacticFishing.Minigames.HexWorld
{
    public enum HexWorldTerrainType
    {
        None = 0,
        Forest = 1,
        Mountain = 2,
        Plains = 3,
        Water = 4
    }

    /// <summary>
    /// Attached to both Owned + Frontier tile prefabs.
    /// Stores axial coord and state so the controller can raycast-hit it.
    /// </summary>
    public sealed class HexWorld3DTile : MonoBehaviour
    {
        [Header("Runtime (set by controller)")]
        public int Q;
        public int R;
        public bool IsFrontier;

        [Header("Terrain Tier System")]
        [Tooltip("Tier 0 (weight 0), Tier 1 (weight 1), Tier 2 (weight 2)")]
        [Range(0, 2)]
        public int TerrainTier = 0;

        [Tooltip("Terrain type of this tile (Forest, Mountain, Plains, Water)")]
        public HexWorldTerrainType TerrainType = HexWorldTerrainType.None;

        public HexCoord Coord => new HexCoord(Q, R);

        /// <summary>
        /// Returns the weight for district bonus calculations.
        /// Tier 0 = 0, Tier 1 = 1, Tier 2 = 2
        /// </summary>
        public int GetTierWeight() => TerrainTier;

        public void Set(HexCoord c, bool isFrontier)
        {
            Q = c.q;
            R = c.r;
            IsFrontier = isFrontier;
        }

        public void SetTerrainType(HexWorldTerrainType type)
        {
            TerrainType = type;
        }

        public void SetTerrainTier(int tier)
        {
            TerrainTier = UnityEngine.Mathf.Clamp(tier, 0, 2);
        }
    }
}
