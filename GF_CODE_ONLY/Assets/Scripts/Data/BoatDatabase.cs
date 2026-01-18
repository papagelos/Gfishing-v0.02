// Assets/Scripts/Data/BoatDatabase.cs
using System.Collections.Generic;
using UnityEngine;

namespace GalacticFishing.Data
{
    [CreateAssetMenu(
        menuName = "GalacticFishing/Gear/Boat Database",
        fileName = "BoatDatabase")]
    public class BoatDatabase : ScriptableObject
    {
        public List<BoatDefinition> boats = new();

        public BoatDefinition GetById(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            for (int i = 0; i < boats.Count; i++)
            {
                var b = boats[i];
                if (b != null && b.id == id)
                    return b;
            }

            return null;
        }
    }
}
