using System.Collections.Generic;
using UnityEngine;

namespace GalacticFishing.Minigames.HexWorld
{
    [CreateAssetMenu(menuName = "Galactic Fishing/Hex World/Tile Catalog", fileName = "TileCatalog")]
    public class TileCatalog : ScriptableObject
    {
        public List<TileDefinition> tiles = new();

        public TileDefinition GetById(string id)
        {
            foreach (var t in tiles)
                if (t && t.id == id) return t;
            return null;
        }

        public TileDefinition GetRandomWeighted()
        {
            int total = 0;
            foreach (var t in tiles)
                if (t) total += Mathf.Max(1, t.weight);

            if (total <= 0) return null;

            int roll = Random.Range(0, total);
            foreach (var t in tiles)
            {
                if (!t) continue;
                roll -= Mathf.Max(1, t.weight);
                if (roll < 0) return t;
            }
            return null;
        }
    }
}
