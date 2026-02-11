using System.Collections.Generic;
using UnityEngine;

namespace GalacticFishing
{
    [CreateAssetMenu(menuName = "Galactic Fishing/Data/Fish Registry", fileName = "FishRegistry")]
    public sealed class FishRegistry : ScriptableObject
    {
        public List<Fish> fishes = new();
    }
}
