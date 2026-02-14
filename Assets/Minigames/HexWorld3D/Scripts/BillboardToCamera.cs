using UnityEngine;

namespace GalacticFishing.Minigames.HexWorld
{
    [DisallowMultipleComponent]
    public sealed class BillboardToCamera : MonoBehaviour
    {
        [Header("Camera")]
        [Tooltip("If empty, uses Camera.main")]
        public Camera targetCamera;

        [Tooltip("If true, only rotates around Y so it stays upright.")]
        public bool yAxisOnly = true;

        [Header("Facing Fix")]
        [Tooltip("Extra yaw applied after billboarding. If your sprite looks rotated wrong, try 180.")]
        public float yawOffsetDegrees = 0f;

        [Tooltip("If your sprite still looks mirrored after fixing facing, toggle this (it flips which side is considered 'front').")]
        public bool invertFacing = false;

        [Header("Sorting (recommended for 360° camera rotation)")]
        [Tooltip("If empty, will try to auto-find a SpriteRenderer on this GameObject.")]
        public SpriteRenderer spriteRenderer;

        [Tooltip("Force SpriteRenderer to sort using Pivot (not Center).")]
        public bool forcePivotSortPoint = true;

        [Tooltip("Continuously sets sortingOrder based on camera direction so closer billboards draw on top.")]
        public bool driveSortingOrder = true;

        [Tooltip("Higher = more separation between nearby objects. Typical: 10-200 depending on world scale.")]
        public float sortingOrderScale = 50f;

        [Tooltip("Adds a constant offset to sortingOrder (useful if you reserve ranges).")]
        public int sortingOrderBias = 0;

        [Header("Depth Offset (optional tie-breaker)")]
        [Tooltip("Tiny nudge towards camera to break ties when two billboards sort the same. Keep very small: 0 to 0.02.")]
        public float depthOffset = 0f;

        private Vector3 _appliedWorldOffset = Vector3.zero;

        private void Awake()
        {
            if (!spriteRenderer)
                spriteRenderer = GetComponent<SpriteRenderer>();

            if (forcePivotSortPoint && spriteRenderer)
                spriteRenderer.spriteSortPoint = SpriteSortPoint.Pivot;

            if (sortingOrderScale < 0.001f)
                sortingOrderScale = 0.001f;

            if (depthOffset < 0f)
                depthOffset = 0f;
        }

        private void LateUpdate()
        {
            var cam = targetCamera ? targetCamera : Camera.main;
            if (!cam) return;

            // ---------- 1) Billboard rotation ----------
            Vector3 toCamera = cam.transform.position - transform.position;
            if (yAxisOnly) toCamera.y = 0f;

            if (toCamera.sqrMagnitude > 0.000001f)
            {
                toCamera.Normalize();

                // FIX: default should face TOWARD the camera, not away.
                Vector3 facing = invertFacing ? -toCamera : toCamera;

                var rot = Quaternion.LookRotation(facing, Vector3.up);

                if (Mathf.Abs(yawOffsetDegrees) > 0.0001f)
                    rot = rot * Quaternion.Euler(0f, yawOffsetDegrees, 0f);

                transform.rotation = rot;
            }

            // ---------- 2) Optional depth nudge (no drift) ----------
            if (depthOffset > 0f)
            {
                Vector3 dir = toCamera; // reuse the same direction we already computed
                if (yAxisOnly) dir.y = 0f;

                if (dir.sqrMagnitude > 0.000001f)
                {
                    dir.Normalize();

                    Vector3 basePos = transform.position - _appliedWorldOffset;
                    Vector3 newOffset = dir * depthOffset;

                    transform.position = basePos + newOffset;
                    _appliedWorldOffset = newOffset;
                }
            }
            else if (_appliedWorldOffset != Vector3.zero)
            {
                transform.position -= _appliedWorldOffset;
                _appliedWorldOffset = Vector3.zero;
            }

            // ---------- 3) Sorting order driven by camera direction ----------
            if (driveSortingOrder && spriteRenderer)
            {
                Vector3 axis = cam.transform.forward;
                if (yAxisOnly)
                {
                    axis.y = 0f;
                    if (axis.sqrMagnitude < 0.000001f) axis = Vector3.forward;
                }
                axis.Normalize();

                float depth = Vector3.Dot(transform.position - cam.transform.position, axis);
                int order = sortingOrderBias - Mathf.RoundToInt(depth * sortingOrderScale);
                order = Mathf.Clamp(order, -32000, 32000);

                spriteRenderer.sortingOrder = order;
            }
        }
    }
}
