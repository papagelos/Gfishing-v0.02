using UnityEngine;

namespace GalacticFishing.Minigames.HexWorld
{
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

        public HexCoord Coord => new HexCoord(Q, R);

        public void Set(HexCoord c, bool isFrontier)
        {
            Q = c.q;
            R = c.r;
            IsFrontier = isFrontier;
        }
    }
}
