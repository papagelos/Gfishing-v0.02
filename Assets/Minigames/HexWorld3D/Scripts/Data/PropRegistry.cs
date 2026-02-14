using System.Collections.Generic;
using UnityEngine;

namespace GalacticFishing.Minigames.HexWorld
{
    [CreateAssetMenu(
        fileName = "PropRegistry_Main",
        menuName = "Galactic Fishing/Village/Prop Registry",
        order = 120)]
    public sealed class PropRegistry : ScriptableObject
    {
        public List<HexWorldPropDefinition> allProps = new();
    }
}
