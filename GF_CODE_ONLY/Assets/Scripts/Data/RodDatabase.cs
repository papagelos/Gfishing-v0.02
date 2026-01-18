// Assets/Scripts/Data/RodDatabase.cs
using System.Collections.Generic;
using UnityEngine;

namespace GalacticFishing.Data
{
    [CreateAssetMenu(
        menuName = "GalacticFishing/Gear/Rod Database",
        fileName = "RodDatabase")]
    public class RodDatabase : ScriptableObject
    {
        public List<RodDefinition> rods = new();

        public RodDefinition GetById(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            for (int i = 0; i < rods.Count; i++)
            {
                var r = rods[i];
                if (r != null && r.id == id)
                    return r;
            }

            return null;
        }
    }
}
