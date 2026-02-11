using UnityEngine;

namespace GalacticFishing.Minigames.HexWorld
{
    [CreateAssetMenu(menuName = "Galactic Fishing/Village/Prop Definition", fileName = "Prop_")]
    public sealed class HexWorldPropDefinition : ScriptableObject
    {
        public string id = "Prop_Id";
        public string displayName = "New Prop";
        public Sprite thumbnail;
        public GameObject prefab;
        [Min(0.001f)] public float scale = 1.0f;
    }
}
