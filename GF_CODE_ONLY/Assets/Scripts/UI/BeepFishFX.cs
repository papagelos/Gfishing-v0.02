using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace GalacticFishing.UI
{
    [RequireComponent(typeof(Image))]
    [AddComponentMenu("Galactic Fishing/UI/Beep Fish FX")]
    public sealed class BeepFishFX : MonoBehaviour
    {
        [Header("Placement")]
        [SerializeField] Vector2 anchoredOffset = Vector2.zero; // relative to screen center
        [SerializeField] bool useNativeSize = false;
        [Tooltip("Max on-screen pixels for the longest side (before Canvas scaling).")]
        [SerializeField] float maxPixels = 1024f;

        [Header("Fade (fallback)")]
        [SerializeField] float defaultFadeSeconds = 0.6f;

        Image _image;
        RectTransform _rect;

        void Awake()
        {
            _image = GetComponent<Image>();
            _rect = GetComponent<RectTransform>();
            _image.raycastTarget = false;
            SetAlpha(0f);
        }

        public void Show(Sprite sprite, float seconds)
        {
            if (!_image || sprite == null) return;
            StopAllCoroutines();
            _image.sprite = sprite;
            _image.enabled = true;

            _rect.anchorMin = _rect.anchorMax = new Vector2(0.5f, 0.5f);
            _rect.pivot = new Vector2(0.5f, 0.5f);
            _rect.anchoredPosition = anchoredOffset;

            ApplySizing(sprite);

            SetAlpha(1f);
            float duration = seconds > 0f ? seconds : Mathf.Max(0.0001f, defaultFadeSeconds);
            StartCoroutine(FadeRoutine(duration));
        }

        public void Show(Sprite sprite) => Show(sprite, defaultFadeSeconds);

        public void HideImmediate()
        {
            StopAllCoroutines();
            SetAlpha(0f);
        }

        void ApplySizing(Sprite sprite)
        {
            float canvasScale = 1f;
            var canvas = _rect ? _rect.GetComponentInParent<Canvas>() : null;
            if (canvas) canvasScale = Mathf.Max(0.0001f, canvas.scaleFactor);

            float screenShortest = Mathf.Min(Screen.width, Screen.height);
            float targetPixels = Mathf.Min(maxPixels, screenShortest - 40f);
            float targetUnits = targetPixels / canvasScale;

            if (!useNativeSize || sprite == null)
            {
                _rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetUnits);
                _rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetUnits);
                return;
            }

            var rect = sprite.rect;
            float w = Mathf.Max(1f, rect.width);
            float h = Mathf.Max(1f, rect.height);
            float scale = targetUnits / Mathf.Max(w, h);
            _rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, w * scale);
            _rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, h * scale);
        }

        IEnumerator FadeRoutine(float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                SetAlpha(1f - t / duration);
                yield return null;
            }
            SetAlpha(0f);
        }

        void SetAlpha(float a)
        {
            if (!_image) return;
            var c = _image.color; c.a = a; _image.color = c;
        }
    }
}
