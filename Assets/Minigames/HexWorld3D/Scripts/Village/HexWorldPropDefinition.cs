using UnityEngine;

namespace GalacticFishing.Minigames.HexWorld
{
    [CreateAssetMenu(menuName = "Galactic Fishing/Village/Prop Definition", fileName = "Prop_")]
    public sealed class HexWorldPropDefinition : ScriptableObject
    {
        public string id = "Prop_Id";
        public string displayName = "New Prop";
        public string biomeGroup = "ALL";
        public Sprite thumbnail;
        public GameObject prefab;
        [Min(1)] public int minPerTile = 1;
        [Min(1)] public int maxPerTile = 1;
        [Min(0f)] public float jitterRadius = 0.4f;
        [Min(0.001f)] public float masterScale = 1.0f;
        [Min(0.001f)] public float scale = 1.0f;
        public Vector2 randomScaleRange = new Vector2(-10f, 10f);
    }
}
