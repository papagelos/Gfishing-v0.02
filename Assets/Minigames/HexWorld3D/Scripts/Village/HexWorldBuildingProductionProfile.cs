// Assets/Minigames/HexWorld3D/Scripts/Village/HexWorldBuildingProductionProfile.cs
using System.Collections.Generic;
using UnityEngine;

namespace GalacticFishing.Minigames.HexWorld
{
    /// <summary>
    /// Production configuration attached to a building instance.
    /// MVP only: base output per 60s tick. District/landmarks later.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HexWorldBuildingProductionProfile : MonoBehaviour
    {
        [Tooltip("If true, this building consumes one Town Hall Active Slot when Active.")]
        public bool consumesActiveSlot = true;

        [Tooltip("Base output granted each tick while Active.")]
        public List<HexWorldResourceStack> baseOutputPerTick = new();
    }
}
