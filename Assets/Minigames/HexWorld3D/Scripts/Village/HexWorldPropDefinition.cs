using UnityEngine;

namespace GalacticFishing.Minigames.HexWorld
{
    [CreateAssetMenu(menuName = "Galactic Fishing/Hex World/Prop Definition", fileName = "Prop_")]
    public sealed class HexWorldPropDefinition : ScriptableObject
    {
        public string id = "Prop_Id";
        public string displayName = "New Prop";
        public Sprite thumbnail;
        public GameObject prefab;
    }
}
