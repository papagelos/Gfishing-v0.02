using System.Collections.Generic;
using UnityEngine;

namespace GalacticFishing
{
    [CreateAssetMenu(menuName = "GalacticFishing/Fish Registry", fileName = "FishRegistry")]
    public sealed class FishRegistry : ScriptableObject
    {
        public List<Fish> fishes = new();
    }
}
