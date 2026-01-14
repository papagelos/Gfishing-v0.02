using UnityEngine;
using UnityEngine.UI;

namespace GalacticFishing.UI
{
    /// <summary>
    /// Simple full-screen flash using an Image on a UI Canvas.
    /// Call Flash() to trigger one red flash.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScreenFlash : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private Image overlayImage;

        [Header("Flash Settings")]
        [SerializeField] private Color flashColor = new Color(1f, 0f, 0f, 0.8f);
        [SerializeField] private float duration = 0.18f;
        [SerializeField] private AnimationCurve alphaCurve =
            AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        private Color _baseColor;
        private Coroutine _routine;

        private void Awake()
        {
            // Auto-grab Image if not wired.
            if (!overlayImage)
                overlayImage = GetComponent<Image>();

            if (!overlayImage)
            {
                Debug.LogError("[ScreenFlash] No Image assigned.", this);
                enabled = false;
                return;
            }

            overlayImage.raycastTarget = false;

            // Start fully transparent.
            _baseColor = flashColor;
            _baseColor.a = 0f;
            overlayImage.color = _baseColor;
        }

        /// <summary>Trigger one flash.</summary>
        public void Flash()
        {
            if (!overlayImage || !gameObject.activeInHierarchy)
                return;

            if (_routine != null)
                StopCoroutine(_routine);

            _routine = StartCoroutine(FlashRoutine());
        }

        private System.Collections.IEnumerator FlashRoutine()
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / duration);
                float a = alphaCurve.Evaluate(u);

                var c = flashColor;
                c.a *= a;
                overlayImage.color = c;

                yield return null;
            }

            // Return to transparent.
            var end = flashColor;
            end.a = 0f;
            overlayImage.color = end;
        }
    }
}
