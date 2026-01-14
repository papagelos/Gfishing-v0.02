using UnityEngine;

namespace GalacticFishing.UI
{
    [DisallowMultipleComponent]
    public class ReactionBarUI : MonoBehaviour
    {
        [Header("Assign")]
        [SerializeField] private ReactionBarTrack track;
        [SerializeField] private RectTransform markerRect;

        [Header("Motion")]
        [SerializeField] private float speed = 1.2f;

        [Header("Horizontal Insets (of inner width)")]
        [SerializeField] private float leftInsetPercentInner  = 0f;
        [SerializeField] private float rightInsetPercentInner = 0f;

        [Header("Clamp (optional)")]
        [SerializeField] private bool clampByWidth = false;
        [SerializeField] private float clampWidthPx = 24f;
        [SerializeField] private float extraClampMarginPx = 0f;

        [Header("Vertical Alignment")]
        [SerializeField] private bool  lockYToTrackCenter = true;
        [Tooltip("Marker-only Y offset in SCREEN pixels (resolution-proof). +up / -down")]
        [SerializeField] private float yNudgePx = 0f;

        // runtime
        private bool _running;
        private float _t;
        private int _dir = 1;

        private static float CanvasScaleFactor(Component c)
        {
            var canvas = c.GetComponentInParent<Canvas>();
            return canvas ? Mathf.Max(0.0001f, canvas.scaleFactor) : 1f;
        }

        private void Update()
        {
            if (!_running || track == null || markerRect == null || !track.IsValid) return;

            // ping-pong motion (unscaled time so pause doesn't stop the marker)
            _t += _dir * Mathf.Max(0f, speed) * Time.unscaledDeltaTime;
            if (_t > 1f) { _t = 1f; _dir = -1; }
            else if (_t < 0f) { _t = 0f; _dir = 1; }

            // inner edges (WORLD)
            float L = track.InnerLeftX;
            float R = track.InnerRightX;
            float innerW = Mathf.Max(0.0001f, R - L);

            // travel insets (percent of inner width)
            if (leftInsetPercentInner > 0f || rightInsetPercentInner > 0f)
            {
                L += innerW * Mathf.Clamp01(leftInsetPercentInner);
                R -= innerW * Mathf.Clamp01(rightInsetPercentInner);
            }

            // clamp by tip width, if enabled
            if (clampByWidth)
            {
                float sx = Mathf.Abs(markerRect.lossyScale.x);
                float half = (Mathf.Abs(clampWidthPx) * sx) * 0.5f + (extraClampMarginPx * sx);
                L += half; R -= half; if (R < L) R = L;
            }

            // apply world pos
            Vector3 wpos = markerRect.position;
            wpos.x = Mathf.Lerp(L, R, _t);

            if (lockYToTrackCenter)
            {
                float sf = CanvasScaleFactor(markerRect);
                float yWorldNudge = yNudgePx / sf;
                wpos.y = track.CenterY + yWorldNudge;
            }

            markerRect.position = wpos;
        }

        // -------- Public API (controller-only) --------
        public void StartRun()
        {
            _running = true;
            _t = 0f;
            _dir = 1;
        }

        public void StopRun()
        {
            _running = false;
        }

        /// <summary>Marker normalized X across track inner width [0..1].</summary>
        public float MarkerNormalized01()
        {
            if (track == null || markerRect == null || !track.IsValid) return 0f;
            float L = track.InnerLeftX;
            float R = track.InnerRightX;
            float W = Mathf.Max(0.0001f, R - L);
            float x = markerRect.position.x;
            return Mathf.Clamp01((x - L) / W);
        }
    }
}
