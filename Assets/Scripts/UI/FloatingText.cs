using UnityEngine;
using TMPro;

namespace GalacticFishing.UI
{
    /// <summary>
    /// Single floating text instance. It:
    /// - converts a world position to canvas space
    /// - drifts upward
    /// - fades out, then destroys itself
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasGroup))]
    public class FloatingText : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TMP_Text text;

        [Header("Animation")]
        [SerializeField] private float lifetime = 1.0f;
        [SerializeField] private Vector2 moveOffset = new Vector2(0f, 60f);   // pixels up
        [SerializeField] private AnimationCurve alphaOverLifetime =
            AnimationCurve.EaseInOut(0, 1, 1, 0); // 1 → 0 fade

        private RectTransform _rect;
        private CanvasGroup _group;

        private Camera _worldCamera;
        private Vector3 _worldPos;
        private bool _hasWorldPos;
        private float _age;

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
            _group = GetComponent<CanvasGroup>();

            if (!text)
                text = GetComponent<TMP_Text>();
        }

        /// <summary>
        /// Must be called right after Instantiate.
        /// </summary>
        public void Init(string message, Vector3 worldPos, Camera worldCam, Color color)
        {
            _worldPos = worldPos;
            _worldCamera = worldCam;
            _hasWorldPos = true;

            if (text != null)
            {
                text.text = message;
                text.color = color;
            }

            // Place at start position
            UpdatePosition(0f);
            _age = 0f;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float t = Mathf.Clamp01(_age / Mathf.Max(0.01f, lifetime));

            UpdatePosition(t);
            UpdateAlpha(t);

            if (_age >= lifetime)
                Destroy(gameObject);
        }

        private void UpdatePosition(float t)
        {
            if (!_hasWorldPos || _worldCamera == null || _rect == null)
                return;

            // World → Screen
            Vector3 screen = RectTransformUtility.WorldToScreenPoint(_worldCamera, _worldPos);

            // Screen → Canvas local
            RectTransform canvasRect = _rect.parent as RectTransform;
            if (canvasRect == null) return;

            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screen, null, out localPoint);

            // Apply upward offset over lifetime
            localPoint += moveOffset * t;
            _rect.anchoredPosition = localPoint;
        }

        private void UpdateAlpha(float t)
        {
            if (_group == null) return;

            float a = alphaOverLifetime != null
                ? alphaOverLifetime.Evaluate(t)
                : (1f - t);

            _group.alpha = a;
        }
    }
}
