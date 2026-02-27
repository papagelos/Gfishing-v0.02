using UnityEngine;

namespace GalacticFishing.Data
{
    [CreateAssetMenu(menuName = "Galactic Fishing/Dungeon/Skill Definition", fileName = "Skill_New")]
    public sealed class DungeonSkillDefinition : ScriptableObject
    {
        [Min(0f)] public float speed = 10f;
        [Min(0)] public int damage = 1;
        public Vector2Int damageRange = new Vector2Int(1, 1);
        [Min(0f)] public float maxRange = 0f;
        [Min(0f)] public float knockbackForce = 0f;
        [Min(0f)] public float stunDuration = 0f;
        [Min(0f)] public float cooldown = 0f;
        public GameObject projectilePrefab;
        public GameObject hitPrefab;
        [Min(0f)] public float explosionRadius = 0f;
    }
}
