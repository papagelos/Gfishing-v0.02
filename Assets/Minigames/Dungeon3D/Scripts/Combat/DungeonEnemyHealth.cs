using UnityEngine;
using GalacticFishing.UI;
using System.Collections;
using UnityEngine.AI;

namespace GalacticFishing.Minigames.Dungeon3D
{
    [DisallowMultipleComponent]
    public sealed class DungeonEnemyHealth : MonoBehaviour
    {
        private static readonly int ColorPropId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorPropId = Shader.PropertyToID("_BaseColor");

        [SerializeField, Min(1)] private int maxHp = 15;
        [SerializeField, Min(0)] private int currentHp = 15;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField, ColorUsage(true, true)] private Color flashColor = Color.white;
        [SerializeField, Min(0.01f)] private float flashDuration = 0.5f;
        [SerializeField, Min(0f)] private float maxKnockbackStep = 0.75f;

        private bool _isDead;
        private Color _baseColor = Color.white;
        private Coroutine _flashRoutine;
        private Coroutine _stunRoutine;
        private NavMeshAgent _navAgent;
        private DungeonChaserAI _chaserAi;
        private MaterialPropertyBlock _basePropertyBlock;
        private MaterialPropertyBlock _flashPropertyBlock;

        public int MaxHp => Mathf.Max(1, maxHp);
        public int CurrentHp => Mathf.Max(0, currentHp);

        private void Awake()
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);

            if (spriteRenderer != null)
            {
                _baseColor = spriteRenderer.color;
                _basePropertyBlock = new MaterialPropertyBlock();
                _flashPropertyBlock = new MaterialPropertyBlock();
                spriteRenderer.GetPropertyBlock(_basePropertyBlock);
            }

            _navAgent = GetComponent<NavMeshAgent>();
            _chaserAi = GetComponent<DungeonChaserAI>();
            SyncFallbackHitboxToSpriteBounds();

            currentHp = Mathf.Clamp(currentHp, 0, MaxHp);
            if (currentHp <= 0)
                currentHp = MaxHp;

            _isDead = false;
        }

        public void ResetHealth()
        {
            _isDead = false;
            currentHp = MaxHp;
            RestoreSpriteColor();
        }

        public void TakeDamage()
        {
            TakeDamage(1);
        }

        public void TakeDamage(int amount)
        {
            TakeDamage(amount, Vector3.zero, 0f);
        }

        public void TakeDamage(int amount, Vector3 knockbackWorldOffset, float stunDuration)
        {
            if (_isDead)
                return;

            if (amount <= 0)
                amount = 1;

            int next = Mathf.Max(0, currentHp - amount);
            if (next == currentHp)
                return;

            currentHp = next;

            var ftm = FloatingTextManager.Instance;
            if (ftm != null)
                ftm.SpawnWorld($"-{amount}", transform.position + (Vector3.up * 1f), Color.yellow);

            if (spriteRenderer != null)
            {
                if (_flashRoutine != null)
                    StopCoroutine(_flashRoutine);
                _flashRoutine = StartCoroutine(FlashRoutine());
            }

            if (knockbackWorldOffset.sqrMagnitude > 0.0001f)
                ApplyKnockback(knockbackWorldOffset);

            if (stunDuration > 0f)
                ApplyStun(stunDuration);

            if (currentHp <= 0)
                Die();
        }

        public void ApplyKnockback(Vector3 worldOffset)
        {
            if (_isDead)
                return;

            Vector3 planar = new Vector3(worldOffset.x, 0f, worldOffset.z);
            if (planar.sqrMagnitude <= 0.0001f)
                return;

            float maxStep = Mathf.Max(0f, maxKnockbackStep);
            if (maxStep > 0f && planar.magnitude > maxStep)
                planar = planar.normalized * maxStep;

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb == null)
                rb = GetComponentInParent<Rigidbody>();

            if (rb != null && !rb.isKinematic)
            {
                rb.AddForce(planar, ForceMode.VelocityChange);
                return;
            }

            Vector3 target = transform.position + planar;
            if (_navAgent != null && _navAgent.isActiveAndEnabled && _navAgent.isOnNavMesh)
            {
                _navAgent.Move(planar);
            }
            else
            {
                transform.position = target;
            }
        }

        public void ApplyStun(float duration)
        {
            if (_isDead)
                return;

            duration = Mathf.Max(0f, duration);
            if (duration <= 0f)
                return;

            if (_stunRoutine != null)
                StopCoroutine(_stunRoutine);

            _stunRoutine = StartCoroutine(StunRoutine(duration));
        }

        private IEnumerator FlashRoutine()
        {
            if (spriteRenderer == null)
                yield break;

            spriteRenderer.color = flashColor;
            ApplyFlashPropertyBlock();
            yield return new WaitForSeconds(flashDuration);

            RestoreSpriteColor();
            _flashRoutine = null;
        }

        private void OnDisable()
        {
            RestoreSpriteColor();
            _flashRoutine = null;
            if (_chaserAi != null && !_isDead)
                _chaserAi.enabled = true;
            _stunRoutine = null;
        }

        private void RestoreSpriteColor()
        {
            if (spriteRenderer == null)
                return;

            spriteRenderer.color = _baseColor;

            if (_basePropertyBlock != null)
                spriteRenderer.SetPropertyBlock(_basePropertyBlock);
            else
                spriteRenderer.SetPropertyBlock(null);
        }

        private void ApplyFlashPropertyBlock()
        {
            if (spriteRenderer == null)
                return;

            if (_flashPropertyBlock == null)
                _flashPropertyBlock = new MaterialPropertyBlock();

            _flashPropertyBlock.Clear();

            // Apply both common sprite color properties so the flash wins across built-in/URP sprite shaders.
            _flashPropertyBlock.SetColor(ColorPropId, flashColor);
            _flashPropertyBlock.SetColor(BaseColorPropId, flashColor);
            spriteRenderer.SetPropertyBlock(_flashPropertyBlock);
        }

        private void Die()
        {
            if (_isDead)
                return;

            _isDead = true;
            if (_chaserAi != null)
                _chaserAi.enabled = false;
            RestoreSpriteColor();
            Destroy(gameObject);
        }

        private IEnumerator StunRoutine(float duration)
        {
            bool hadAi = _chaserAi != null;
            bool wasEnabled = hadAi && _chaserAi.enabled;
            if (hadAi)
                _chaserAi.enabled = false;

            yield return new WaitForSeconds(duration);

            if (!_isDead && hadAi && wasEnabled)
                _chaserAi.enabled = true;

            _stunRoutine = null;
        }

        // Safety fallback: if the enemy prefab is missing the auto-sizer component, keep the 3D trigger
        // aligned to the visible sprite so projectile hits still track the silhouette footprint (box-wise).
        private void SyncFallbackHitboxToSpriteBounds()
        {
            if (spriteRenderer == null)
                return;

            if (GetComponent<DungeonSpriteHitboxAutoSizer>() != null)
                return;

            BoxCollider box = GetComponent<BoxCollider>();
            if (box == null)
                return;

            Bounds lb = spriteRenderer.localBounds;
            if (lb.size.x <= 0f || lb.size.y <= 0f)
                return;

            Vector3 size = lb.size;
            size.z = Mathf.Max(0.01f, box.size.z);
            box.center = new Vector3(lb.center.x, lb.center.y, box.center.z);
            box.size = size;
        }
    }
}
