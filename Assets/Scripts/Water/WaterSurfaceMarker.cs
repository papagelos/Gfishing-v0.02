using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GalacticFishing
{
    /// <summary>
    /// Attach to your WaterSurface GameObject.
    /// SurfaceY = transform.position.y (WORLD position).
    /// Draws a visible gizmo line in the Scene view and shows the world Y value.
    /// </summary>
    [ExecuteAlways]
    public sealed class WaterSurfaceMarker : MonoBehaviour
    {
        public float SurfaceY => transform.position.y;

        [Header("Editor Gizmo")]
        [Tooltip("Half-width of the gizmo line in world units.")]
        public float gizmoHalfWidth = 50f;

        [Tooltip("Vertical offset for the label in world units.")]
        public float labelYOffset = 0.25f;

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            float y = SurfaceY;
            float w = Mathf.Max(1f, gizmoHalfWidth);

            // Line
            Gizmos.color = new Color(0f, 1f, 1f, 0.9f); // cyan
            Gizmos.DrawLine(new Vector3(transform.position.x - w, y, 0f),
                            new Vector3(transform.position.x + w, y, 0f));

            // Small tick at the marker position
            Gizmos.color = Color.white;
            Gizmos.DrawLine(new Vector3(transform.position.x, y - 0.5f, 0f),
                            new Vector3(transform.position.x, y + 0.5f, 0f));

            // Label (WORLD Y)
            Handles.Label(new Vector3(transform.position.x, y + labelYOffset, 0f),
                $"WaterSurfaceMarker SurfaceY (world): {y:0.###}");
        }

        [ContextMenu("Copy SurfaceY (world) to Clipboard")]
        void CopySurfaceYToClipboard()
        {
            GUIUtility.systemCopyBuffer = SurfaceY.ToString("0.###");
            Debug.Log($"[WaterSurfaceMarker] Copied SurfaceY(world)={SurfaceY:0.###} to clipboard.", this);
        }
#endif
    }
}
