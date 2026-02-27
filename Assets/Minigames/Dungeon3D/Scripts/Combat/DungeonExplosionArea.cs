using System.Collections.Generic;
using UnityEngine;

namespace GalacticFishing.Minigames.Dungeon3D
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class DungeonExplosionArea : MonoBehaviour
    {
        [SerializeField, Min(1)] private int debugDamage = 1;
        [SerializeField, Min(0f)] private float debugKnockback = 0f;
        [SerializeField, Min(0f)] private float stunDuration = 0.1f;

        private int _damage = 1;
        private float _knockback;
        private readonly HashSet<int> _hitEnemyIds = new();

        public void Setup(int damage, float knockback)
        {
            _damage = Mathf.Max(1, damage);
            _knockback = Mathf.Max(0f, knockback);
            debugDamage = _damage;
            debugKnockback = _knockback;
            _hitEnemyIds.Clear();
        }

        private void Awake()
        {
            EnsureTriggerPhysics();
            _damage = Mathf.Max(1, debugDamage);
            _knockback = Mathf.Max(0f, debugKnockback);
        }

        private void Reset()
        {
            EnsureTriggerPhysics();
        }

        private void OnValidate()
        {
            debugDamage = Mathf.Max(1, debugDamage);
            debugKnockback = Mathf.Max(0f, debugKnockback);
            stunDuration = Mathf.Max(0f, stunDuration);
        }

        private void OnEnable()
        {
            _hitEnemyIds.Clear();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other == null)
                return;

            DungeonEnemyHealth enemyHealth = other.GetComponentInParent<DungeonEnemyHealth>();
            if (enemyHealth == null)
                return;

            int enemyId = enemyHealth.GetInstanceID();
            if (!_hitEnemyIds.Add(enemyId))
                return; // one hit per enemy per explosion instance

            Vector3 dir = other.transform.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude <= 0.0001f)
            {
                dir = enemyHealth.transform.position - transform.position;
                dir.y = 0f;
            }
            if (dir.sqrMagnitude <= 0.0001f)
                dir = Vector3.forward;

            dir.Normalize();
            Vector3 knockback = _knockback > 0f ? dir * _knockback : Vector3.zero;
            enemyHealth.TakeDamage(Mathf.Max(1, _damage), knockback, Mathf.Max(0f, stunDuration));
        }

        private void EnsureTriggerPhysics()
        {
            Collider col = GetComponent<Collider>();
            if (col != null)
                col.isTrigger = true;

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb == null)
                rb = gameObject.AddComponent<Rigidbody>();

            rb.useGravity = false;
            rb.isKinematic = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }
    }
}
