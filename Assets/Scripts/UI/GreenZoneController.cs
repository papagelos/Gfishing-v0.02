using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GalacticFishing.UI
{
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public class GreenZoneController : MonoBehaviour
    {
        [Serializable]
        public struct Segment
        {
            [Range(0f, 1f)] public float start01;   // left edge normalized
            [Range(0f, 1f)] public float length01;  // width normalized
        }

        public enum ThicknessMode { Pixels, PercentOfInnerHeight }

        [Header("Assign")]
        [SerializeField] private ReactionBarTrack track;          // required
        [SerializeField] private RectTransform container;         // optional; default = this
        [SerializeField] private Sprite segmentSprite;
        [SerializeField] private Color segmentColor = new Color(0.3f, 1f, 0.4f, 1f);

        [Header("Thickness")]
        [SerializeField] private ThicknessMode thicknessMode = ThicknessMode.PercentOfInnerHeight;
        [Tooltip("If Pixels: exact screen px. If Percent: 0..1 of inner height.")]
        [SerializeField, Min(0f)] private float thickness = 0.35f;

        [Header("Insets / Nudge (screen px)")]
        [Tooltip("Inward padding on each end, in screen pixels.")]
        [SerializeField] private float insetLeftPx = 0f;
        [SerializeField] private float insetRightPx = 0f;
        [Tooltip("Small vertical offset in screen pixels. Keep tiny (±0..5).")]
        [SerializeField] private float centerYNudgePx = 0f;

        [Header("Segments (normalized)")]
        [SerializeField] private List<Segment> segments = new List<Segment> {
            new Segment { start01 = 0.35f, length01 = 0.30f }
        };

        [Header("Live Rebuild")]
        [SerializeField] private bool rebuildEveryFrame = true;

        private RectTransform _self;

        private void OnEnable()
        {
            _self = GetComponent<RectTransform>();
            if (!container) container = _self;
            Rebuild();
        }

        private void Update()
        {
            if (rebuildEveryFrame) Rebuild();
        }

        private void OnRectTransformDimensionsChange()
        {
            if (isActiveAndEnabled && !Application.isPlaying) Rebuild();
        }

        // ---- Public helpers -------------------------------------------------

        public void SetSingleCentered(float width01)
        {
            width01 = Mathf.Clamp01(width01);
            float start = Mathf.Clamp01(0.5f - width01 * 0.5f);
            segments = new List<Segment> { new Segment { start01 = start, length01 = width01 } };
            Rebuild();
        }

        public void SetSegments(params Segment[] segs)
        {
            segments = new List<Segment>(segs);
            Rebuild();
        }

        public IReadOnlyList<Segment> CurrentSegments => segments;

        // ---- Internals ------------------------------------------------------

        private static float CanvasScaleFactor(Component c)
        {
            var canvas = c.GetComponentInParent<Canvas>();
            return canvas ? Mathf.Max(0.0001f, canvas.scaleFactor) : 1f;
        }

        private static float PxToLocalX(RectTransform parent, float px)
        {
            float sf = CanvasScaleFactor(parent);
            float sx = Mathf.Max(0.0001f, Mathf.Abs(parent.lossyScale.x));
            return px / (sf * sx);
        }

        private static float PxToLocalY(RectTransform parent, float px)
        {
            float sf = CanvasScaleFactor(parent);
            float sy = Mathf.Max(0.0001f, Mathf.Abs(parent.lossyScale.y));
            return px / (sf * sy);
        }

        public void Rebuild()
        {
            if (track == null || !track.IsValid || !_self) return;

            RectTransform parent = container ? container : _self;

            // Ensure children for segments
            EnsureChildCount(parent, segments.Count);

            // Track inner edges (WORLD)
            float Lw = track.InnerLeftX;
            float Rw = track.InnerRightX;
            float Ww = Rw - Lw;

            // Convert inner top/bottom to LOCAL for height
            float topLocal    = parent.InverseTransformPoint(new Vector3(0f, track.InnerTopY,    0f)).y;
            float bottomLocal = parent.InverseTransformPoint(new Vector3(0f, track.InnerBottomY, 0f)).y;
            float innerHLocal = Mathf.Max(0f, topLocal - bottomLocal);

            // *** THIS is the important line: use the SAME center as the marker ***
            float centerLocalY =
                parent.InverseTransformPoint(new Vector3(0f, track.CenterY, 0f)).y
                + PxToLocalY(parent, centerYNudgePx);

            for (int i = 0; i < segments.Count; i++)
            {
                var seg = segments[i];
                float s0 = Mathf.Clamp01(seg.start01);
                float s1 = Mathf.Clamp01(seg.start01 + seg.length01);
                if (s1 < s0) s1 = s0;

                // Horizontal edges in WORLD → LOCAL
                float leftLocalX  = parent.InverseTransformPoint(new Vector3(Lw + Ww * s0, track.CenterY, 0f)).x;
                float rightLocalX = parent.InverseTransformPoint(new Vector3(Lw + Ww * s1, track.CenterY, 0f)).x;

                // Apply pixel insets (converted to local)
                leftLocalX  += PxToLocalX(parent, insetLeftPx);
                rightLocalX -= PxToLocalX(parent, insetRightPx);

                float widthLocal = Mathf.Max(0f, rightLocalX - leftLocalX);

                // Height
                float heightLocal;
                if (thicknessMode == ThicknessMode.PercentOfInnerHeight)
                    heightLocal = Mathf.Clamp01(thickness) * innerHLocal;
                else // Pixels
                    heightLocal = thickness > 0f ? Mathf.Max(0f, PxToLocalY(parent, thickness)) : innerHLocal;

                // Apply to child rect
                var child = (RectTransform)parent.GetChild(i);
                child.anchorMin = child.anchorMax = new Vector2(0f, 0f);
                child.pivot = new Vector2(0.5f, 0.5f);
                child.sizeDelta = new Vector2(widthLocal, heightLocal);
                child.anchoredPosition = new Vector2(leftLocalX + widthLocal * 0.5f, centerLocalY);

                // Visual
                var img = child.GetComponent<Image>() ?? child.gameObject.AddComponent<Image>();
                img.sprite = segmentSprite;
                img.type   = (segmentSprite && segmentSprite.border != Vector4.zero) ? Image.Type.Sliced : Image.Type.Simple;
                img.color  = segmentColor;
                img.raycastTarget = false;

                child.gameObject.SetActive(true);
            }

            // Disable extras if needed
            for (int i = segments.Count; i < parent.childCount; i++)
                parent.GetChild(i).gameObject.SetActive(false);
        }

        private static void EnsureChildCount(RectTransform parent, int n)
        {
            for (int i = parent.childCount; i < n; i++)
            {
                var go = new GameObject($"Segment_{i}", typeof(RectTransform), typeof(Image));
                var rt = go.GetComponent<RectTransform>();
                go.transform.SetParent(parent, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
                rt.pivot = new Vector2(0.5f, 0.5f);
            }
        }
    }
}
