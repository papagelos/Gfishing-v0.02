using UnityEngine;
using UnityEngine.Rendering;

namespace GalacticFishing.Minigames.Dungeon3D
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class DungeonProjectile : MonoBehaviour
    {
        private const int HitEffectFallbackSortingOrder = 1000;
        private const string HitEffectFallbackSortingLayer = "Effects";

        [Header("Fallbacks")]
        [SerializeField, Min(0.1f)] private float defaultSpeed = 8f;
        [SerializeField, Min(1)] private int defaultDamage = 1;

        private Vector3 _target;
        private float _speed;
        private int _damage;
        private float _knockbackForce;
        private float _stunDuration;
        private GameObject _hitPrefab;
        private bool _initialized;
        private bool _hasImpacted;

        private struct HitEffectSortingData
        {
            public bool hasTargetSorting;
            public string sortingLayerName;
            public int sortingOrder;
        }

        public void Initialize(Vector3 target, int damage, float speed)
        {
            Initialize(target, damage, speed, null);
        }

        public void Initialize(Vector3 target, int damage, float speed, GameObject hitPrefab)
        {
            Initialize(target, damage, speed, hitPrefab, 0f);
        }

        public void Initialize(Vector3 target, int damage, float speed, GameObject hitPrefab, float knockbackForce)
        {
            Initialize(target, damage, speed, hitPrefab, knockbackForce, 0f);
        }

        public void Initialize(Vector3 target, int damage, float speed, GameObject hitPrefab, float knockbackForce, float stunDuration)
        {
            _target = target;
            _damage = Mathf.Max(1, damage);
            _speed = Mathf.Max(0.1f, speed);
            _knockbackForce = Mathf.Max(0f, knockbackForce);
            _stunDuration = Mathf.Max(0f, stunDuration);
            _hitPrefab = hitPrefab;
            ApplyInitialOrientation();
            _initialized = true;
        }

        private void Awake()
        {
            var col = GetComponent<Collider>();
            if (col != null)
                col.isTrigger = true;

            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }
        }

        private void OnEnable()
        {
            if (!_initialized)
            {
                _target = transform.position;
                _damage = Mathf.Max(1, defaultDamage);
                _speed = Mathf.Max(0.1f, defaultSpeed);
            }
        }

        private void ApplyInitialOrientation()
        {
            Vector3 direction = _target - transform.position;
            if (direction.sqrMagnitude <= 0.000001f)
                return;

            transform.forward = direction.normalized;
            // Project-wide art convention: projectile sprites are authored head-to-the-left.
            // Nudge local Y so the left side visually leads along the forward travel vector.
            transform.Rotate(0f, 90f, 0f, Space.Self);
        }

        private void Update()
        {
            if (_hasImpacted)
                return;

            Vector3 next = Vector3.MoveTowards(transform.position, _target, _speed * Time.deltaTime);
            transform.position = next;

            if ((next - _target).sqrMagnitude <= 0.000001f)
            {
                TriggerImpactAtRangeLimit();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_hasImpacted)
                return;

            if (other == null)
                return;

            if (!TryResolveEnemyHitTarget(other, out DungeonEnemyHealth enemyHealth, out DungeonChaserAI enemy))
                return;

            // Read-only target collider usage: MeshCollider/BoxCollider/etc. are inspected for hit position/sorting only.
            // The projectile never mutates enemy colliders, so it won't fight the enemy auto-sizer's runtime mesh updates.
            Vector3 impactPos = ResolveImpactPosition(other);
            if (enemyHealth == null && enemy == null)
                return;

            HitEffectSortingData hitSorting = ResolveHitEffectSorting(other);
            ApplyEnemyHitEffects(other);
            Object logContext = (Object)enemyHealth != null ? enemyHealth : enemy;
            string hitName = enemyHealth != null ? enemyHealth.name : enemy.name;
            Debug.Log($"[DungeonProjectile] Hit {hitName} ({other.GetType().Name}) for {Mathf.Max(1, _damage)} damage.", logContext);
            TriggerImpact(impactPos, hitSorting);
        }

        private void SpawnHitEffect(Vector3 worldPos, HitEffectSortingData sortingData)
        {
            if (_hitPrefab == null)
                return;

            GameObject hitGo = Instantiate(_hitPrefab, worldPos, Quaternion.identity);
            DungeonExplosionArea explosion = hitGo.GetComponent<DungeonExplosionArea>();
            if (explosion == null)
                explosion = hitGo.AddComponent<DungeonExplosionArea>();
            explosion.Setup(_damage, _knockbackForce);
            ApplyHitEffectSorting(hitGo, sortingData);
        }

        private void TriggerImpact(Vector3 hitPos)
        {
            TriggerImpact(hitPos, ResolveHitEffectSorting(null));
        }

        private void TriggerImpactAtRangeLimit()
        {
            // Max-range misses are projectile-local cleanup only. Enemy contour meshes (runtime MeshCollider.sharedMesh)
            // are owned and cleaned by DungeonSpriteHitboxAutoSizer; we intentionally do not touch them here.
            TriggerImpact(_target, ResolveHitEffectSorting(null));
        }

        private void TriggerImpact(Vector3 hitPos, HitEffectSortingData sortingData)
        {
            if (_hasImpacted)
                return;

            _hasImpacted = true;
            transform.position = hitPos;

            DisableTravelVisualsAndCollision();
            SpawnHitEffect(hitPos, sortingData);

            // Leave the object alive just briefly so any frame-order callbacks settle after collision.
            Destroy(gameObject, 0.02f);
        }

        private void DisableTravelVisualsAndCollision()
        {
            var spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
                spriteRenderer.enabled = false;

            var collider = GetComponent<Collider>();
            if (collider != null)
                collider.enabled = false;
        }

        private static HitEffectSortingData ResolveHitEffectSorting(Collider hitCollider)
        {
            return new HitEffectSortingData
            {
                hasTargetSorting = false,
                sortingLayerName = HitEffectFallbackSortingLayer,
                sortingOrder = HitEffectFallbackSortingOrder
            };
        }

        private static bool TryResolveEnemyHitTarget(Collider hitCollider, out DungeonEnemyHealth enemyHealth, out DungeonChaserAI enemy)
        {
            enemyHealth = null;
            enemy = null;

            if (hitCollider == null)
                return false;

            enemyHealth = hitCollider.GetComponentInParent<DungeonEnemyHealth>();
            enemy = hitCollider.GetComponentInParent<DungeonChaserAI>();
            return enemyHealth != null || enemy != null;
        }

        private Vector3 ResolveImpactPosition(Collider hitCollider)
        {
            if (hitCollider == null)
                return transform.position;

            // ClosestPoint works for BoxCollider and MeshCollider (including convex trigger meshes).
            // This places the hit FX on the actual collision surface instead of always at the projectile pivot.
            Vector3 p = hitCollider.ClosestPoint(transform.position);
            if (!IsFinite(p))
                return transform.position;

            if ((p - transform.position).sqrMagnitude <= 0.000001f)
                return transform.position;

            return p;
        }

        private static bool IsFinite(Vector3 v)
        {
            return float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
        }

        private static void ApplyHitEffectSorting(GameObject hitGo, HitEffectSortingData sortingData)
        {
            if (hitGo == null)
                return;

            SortingGroup hitGroup = hitGo.GetComponentInChildren<SortingGroup>(true);
            if (hitGroup != null)
            {
                hitGroup.sortingLayerName = sortingData.sortingLayerName;
                hitGroup.sortingOrder = HitEffectFallbackSortingOrder;
                return;
            }

            SpriteRenderer[] renderers = hitGo.GetComponentsInChildren<SpriteRenderer>(true);
            if (renderers == null || renderers.Length == 0)
                return;

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                    continue;

                renderers[i].sortingLayerName = sortingData.sortingLayerName;
                renderers[i].sortingOrder = HitEffectFallbackSortingOrder;
            }
        }

        private void ApplyEnemyHitEffects(Collider hitCollider)
        {
            if (hitCollider == null)
                return;

            DungeonEnemyHealth enemyHealth = hitCollider.GetComponentInParent<DungeonEnemyHealth>();
            if (enemyHealth != null)
            {
                Vector3 push = ComputeKnockbackVector(hitCollider);
                enemyHealth.TakeDamage(
                    Mathf.Max(1, _damage),
                    _knockbackForce > 0f ? (push * _knockbackForce) : Vector3.zero,
                    _stunDuration);

                return;
            }

            // Fallback for enemies that haven't adopted DungeonEnemyHealth yet.
            hitCollider.gameObject.SendMessage("TakeDamage", Mathf.Max(1, _damage), SendMessageOptions.DontRequireReceiver);
        }

        private Vector3 ComputeKnockbackVector(Collider hitCollider)
        {
            Vector3 from = transform.position;
            Vector3 to = hitCollider != null ? hitCollider.bounds.center : (_target != Vector3.zero ? _target : from + transform.forward);
            Vector3 dir = to - from;
            dir.y = 0f;

            if (dir.sqrMagnitude <= 0.0001f)
            {
                dir = transform.forward;
                dir.y = 0f;
            }

            if (dir.sqrMagnitude <= 0.0001f)
                dir = Vector3.forward;

            return dir.normalized;
        }
    }
}
