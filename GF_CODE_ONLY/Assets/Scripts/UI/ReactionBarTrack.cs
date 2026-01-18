using UnityEngine;
using UnityEngine.UI;

namespace GalacticFishing.UI
{
    [ExecuteAlways]
    public class ReactionBarTrack : MonoBehaviour
    {
        [Header("Assign")]
        [SerializeField] private RectTransform barRect;   // required
        [SerializeField] private Image barImage;          // optional (Sliced sprite)

        [Header("Inner Edges")]
        [SerializeField] private bool useSpriteBorder = true;
        [SerializeField] private float leftInsetPercent  = 0f;
        [SerializeField] private float rightInsetPercent = 0f;
        [SerializeField] private float extraLeftPx = 0f;
        [SerializeField] private float extraRightPx = 0f;
        [SerializeField] private float extraTopPx = 0f;
        [SerializeField] private float extraBottomPx = 0f;

        [Header("Vertical Center")]
        [SerializeField] private float yCenterNudgePx = 0f;

        private float _innerLeftX, _innerRightX, _centerY;
        private float _innerTopY, _innerBottomY;
        private static readonly Vector3[] _corners = new Vector3[4];

        public bool IsValid => barRect != null;
        public float InnerLeftX   => _innerLeftX;
        public float InnerRightX  => _innerRightX;
        public float CenterY      => _centerY;
        public float InnerTopY    => _innerTopY;
        public float InnerBottomY => _innerBottomY;
        public float InnerHeightWorld => _innerTopY - _innerBottomY;

        private void OnEnable() { Recompute(); }
        private void Update() { Recompute(); }
        private void OnRectTransformDimensionsChange() { Recompute(); }

        public void Recompute()
        {
            if (!barRect) return;

            barRect.GetWorldCorners(_corners); // 0=BL,1=TL,2=TR,3=BR
            float leftX   = _corners[0].x, rightX = _corners[2].x;
            float bottomY = _corners[0].y, topY   = _corners[1].y;
            float widthW  = rightX - leftX;

            // Sprite border (px -> world)
            float insetLw = 0f, insetRw = 0f, insetTw = 0f, insetBw = 0f;
            if (useSpriteBorder && barImage && barImage.type == Image.Type.Sliced && barImage.sprite)
            {
                var border = barImage.sprite.border;
                float ppu  = barImage.sprite.pixelsPerUnit > 0 ? barImage.sprite.pixelsPerUnit : 100f;
                insetLw = (border.x / ppu) * barRect.lossyScale.x;
                insetRw = (border.z / ppu) * barRect.lossyScale.x;
                insetTw = (border.y / ppu) * barRect.lossyScale.y;
                insetBw = (border.w / ppu) * barRect.lossyScale.y;
            }

            // Percent insets (negative allowed)
            float percLw = widthW * leftInsetPercent;
            float percRw = widthW * rightInsetPercent;

            // Extra px -> world
            insetLw += extraLeftPx   * barRect.lossyScale.x;
            insetRw += extraRightPx  * barRect.lossyScale.x;
            insetTw += extraTopPx    * barRect.lossyScale.y;
            insetBw += extraBottomPx * barRect.lossyScale.y;

            _innerLeftX  = leftX  + insetLw + percLw;
            _innerRightX = rightX - insetRw - percRw;

            _innerTopY    = topY    - insetTw;
            _innerBottomY = bottomY + insetBw;

            float center = (_innerTopY + _innerBottomY) * 0.5f;
            _centerY = center + (yCenterNudgePx * barRect.lossyScale.y);
        }

        public float NormalizedToWorldX(float u01) =>
            Mathf.Lerp(_innerLeftX, _innerRightX, Mathf.Clamp01(u01));

        public float WorldXToNormalized(float worldX)
        {
            float w = _innerRightX - _innerLeftX;
            if (Mathf.Approximately(w, 0f)) return 0f;
            return Mathf.Clamp01((worldX - _innerLeftX) / w);
        }
    }
}
