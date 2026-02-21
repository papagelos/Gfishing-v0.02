using GalacticFishing.Minigames.HexWorld;
using UnityEngine;

namespace GalacticFishing.Minigames.Dungeon3D
{
    [CreateAssetMenu(
        menuName = "Galactic Fishing/Dungeon/Resource Definition",
        fileName = "DungeonResource_")]
    public sealed class DungeonResourceDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string resourceId = "Copper_Ore";
        public GameObject prefab;

        [Header("Durability")]
        [Min(1)] public int maxHp = 15;

        [Header("Spawn Floors")]
        [Min(1)] public int minFloor = 1;
        [Tooltip("Optional last floor where this can appear. Set 0 for no upper bound.")]
        [Min(0)] public int maxFloor = 0;

        [Header("Spawn Quantity")]
        [Tooltip("Random vein quantity range per spawn (inclusive).")]
        public Vector2Int veinSizeRange = new Vector2Int(2, 8);

        [Header("Loot")]
        public HexWorldResourceId lootId = HexWorldResourceId.Copper;

        private void OnValidate()
        {
            maxHp = Mathf.Max(1, maxHp);
            minFloor = Mathf.Max(1, minFloor);

            if (maxFloor != 0)
                maxFloor = Mathf.Max(minFloor, maxFloor);

            veinSizeRange.x = Mathf.Max(1, veinSizeRange.x);
            veinSizeRange.y = Mathf.Max(veinSizeRange.x, veinSizeRange.y);
        }
    }
}
