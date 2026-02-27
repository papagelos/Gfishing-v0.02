using System;
using System.Collections;
using UnityEngine;
using GalacticFishing.UI;

namespace GalacticFishing.Minigames.Dungeon3D
{
    [DisallowMultipleComponent]
    public sealed class PlayerHealth : MonoBehaviour
    {
        private static readonly int ColorPropId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorPropId = Shader.PropertyToID("_BaseColor");

        [SerializeField, Min(1)] private int maxHealth = 100;
        [SerializeField, Min(0)] private int currentHealth = 100;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField, ColorUsage(true, true)] private Color flashColor = Color.white;
        [SerializeField, Min(0.01f)] private float flashDuration = 0.05f;

        public event Action OnDamaged;

        public int MaxHealth => Mathf.Max(1, maxHealth);
        public int CurrentHealth => currentHealth;

        private Color _baseColor = Color.white;
        private Coroutine _flashRoutine;
        private MaterialPropertyBlock _basePropertyBlock;
        private MaterialPropertyBlock _flashPropertyBlock;

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

            currentHealth = Mathf.Clamp(currentHealth, 0, MaxHealth);
            if (currentHealth <= 0)
                currentHealth = MaxHealth;
        }

        public void ResetHealth()
        {
            currentHealth = MaxHealth;
            RestoreSpriteColor();
        }

        public void TakeDamage()
        {
            TakeDamage(1);
        }

        public void TakeDamage(int amount)
        {
            if (amount <= 0)
                amount = 1;

            int next = Mathf.Max(0, currentHealth - amount);
            if (next == currentHealth)
                return;

            currentHealth = next;

            var ftm = FloatingTextManager.Instance;
            if (ftm != null)
            {
                // Offset above the player so stacked hits remain readable over the sprite/body.
                ftm.SpawnWorld($"-{amount}hp", transform.position + (Vector3.up * 1f), Color.red);
            }

            if (spriteRenderer != null)
            {
                if (_flashRoutine != null)
                    StopCoroutine(_flashRoutine);
                _flashRoutine = StartCoroutine(FlashRoutine());
            }

            OnDamaged?.Invoke();
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
            _flashPropertyBlock.SetColor(ColorPropId, flashColor);
            _flashPropertyBlock.SetColor(BaseColorPropId, flashColor);
            spriteRenderer.SetPropertyBlock(_flashPropertyBlock);
        }
    }
}
