// Assets/Minigames/HexWorld3D/Scripts/HexWorldBuildingInstance.cs
using UnityEngine;

namespace GalacticFishing.Minigames.HexWorld
{
    public sealed class HexWorldBuildingInstance : MonoBehaviour
    {
        [Header("Runtime (set by controller)")]
        public int Q;
        public int R;
        public string buildingName;

        public HexCoord Coord => new HexCoord(Q, R);

        public void Set(HexCoord c, string defName)
        {
            Q = c.q;
            R = c.r;
            buildingName = defName;
        }
    }
}
