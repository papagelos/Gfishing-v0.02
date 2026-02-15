using UnityEngine;
using UnityEngine.Rendering;

namespace GalacticFishing.Minigames.HexWorld
{
    [DisallowMultipleComponent]
    public sealed class DistanceToCameraTracker : MonoBehaviour
    {
        [Header("Camera")]
        [Tooltip("If empty, uses Camera.main")]
        public Camera targetCamera;

        [Header("Tracking Root (important for Thick meshes)")]
        [Tooltip("If empty, uses parent transform if available, otherwise this transform.")]
        public Transform trackingRoot;

        [Header("Sorting Group (optional)")]
        [Tooltip("If empty, auto-finds SortingGroup in parents (including self).")]
        public SortingGroup sortingGroup;

        [Header("Sprite Sorting (fallback)")]
        [Tooltip("If empty, auto-finds SpriteRenderer on this GameObject, then in children.")]
        public SpriteRenderer spriteRenderer;

        public bool forcePivotSortPoint = true;
        public bool driveSortingOrder = true;
        public float sortingOrderScale = 50f;
        public int sortingOrderBias = 0;
        public bool flattenAxisToXZ = true;

        [Header("Depth Offset")]
        [Tooltip("Tiny nudge towards camera to break ties. Keep small: 0 to 0.02.")]
        public float depthOffset = 0f;

        [Header("Named child renderers (auto)")]
        [Tooltip("If enabled, we auto-drive sorting for children named Visual/BackVisual/Shadow/Thick.")]
        public bool autoDriveNamedChildren = true;
        public string visualName = "Visual";
        public string backVisualName = "BackVisual";
        public string shadowName = "Shadow";
        public string thickName = "Thick";

        [Tooltip("Offset applied to the closer face (Visual or BackVisual).")]
        public int faceOrderOffset = 1;

        [Tooltip("Offset for Shadow renderer relative to the base order.")]
        public int shadowOrderOffset = -5;

        [Tooltip("Offset for Thick renderer relative to the base order.")]
        public int thickOrderOffset = 0;

        [Header("Extra renderers (optional)")]
        [Tooltip("Any additional renderers that should follow the same base sorting order.")]
        public Renderer[] extraRenderers;

        private Vector3 _appliedWorldOffset = Vector3.zero;

        // Cached named children (optional)
        private SpriteRenderer _visual;
        private SpriteRenderer _backVisual;
        private SpriteRenderer _shadow;
        private MeshRenderer _thick;

        private void Reset()
        {
            AutoAssign();
            ApplySpriteDefaults();
            ClampValues();
        }

        private void OnValidate()
        {
            AutoAssign();
            ApplySpriteDefaults();
            ClampValues();
        }

        private void Awake()
        {
            AutoAssign();
            ApplySpriteDefaults();
            ClampValues();
        }

        private void LateUpdate()
        {
            Camera cam = targetCamera ? targetCamera : Camera.main;
            if (!cam)
                return;

            Transform root = trackingRoot ? trackingRoot : (transform.parent ? transform.parent : transform);

            // Optional: tiny physical nudge of the WHOLE prop
            if (depthOffset > 0f)
            {
                Vector3 toCamera = cam.transform.position - root.position;
                if (flattenAxisToXZ)
                    toCamera.y = 0f;

                if (toCamera.sqrMagnitude > 0.000001f)
                {
                    toCamera.Normalize();

                    Vector3 basePos = root.position - _appliedWorldOffset;
                    Vector3 newOffset = toCamera * depthOffset;

                    root.position = basePos + newOffset;
                    _appliedWorldOffset = newOffset;
                }
            }
            else if (_appliedWorldOffset != Vector3.zero)
            {
                root.position -= _appliedWorldOffset;
                _appliedWorldOffset = Vector3.zero;
            }

            if (!driveSortingOrder)
                return;

            // Depth axis
            Vector3 axis = cam.transform.forward;
            if (flattenAxisToXZ)
            {
                axis.y = 0f;
                if (axis.sqrMagnitude < 0.000001f)
                    axis = Vector3.forward;
            }
            axis.Normalize();

            float rootDepth = Vector3.Dot(root.position - cam.transform.position, axis);
            int baseOrder = sortingOrderBias - Mathf.RoundToInt(rootDepth * sortingOrderScale);
            baseOrder = Mathf.Clamp(baseOrder, -32000, 32000);

            // Determine sorting layer to use (prefer Visual sprite's layer)
            int layerId = 0;
            SpriteRenderer layerSource = spriteRenderer ? spriteRenderer : _visual;
            if (layerSource)
                layerId = layerSource.sortingLayerID;

            // Drive SortingGroup if present
            SortingGroup sg = sortingGroup ? sortingGroup : GetComponentInParent<SortingGroup>();
            if (sg)
            {
                sg.sortingOrder = baseOrder;
                if (layerSource)
                    sg.sortingLayerID = layerId;
            }

            // Drive named children (Visual/BackVisual/Shadow/Thick)
            if (autoDriveNamedChildren)
            {
                // Face swap: whichever face is actually closer gets +faceOrderOffset.
                if (_visual && _backVisual)
                {
                    float dv = Vector3.Dot(_visual.transform.position - cam.transform.position, axis);
                    float db = Vector3.Dot(_backVisual.transform.position - cam.transform.position, axis);

                    bool visualIsCloser = dv <= db;

                    int vOrder = baseOrder + (visualIsCloser ? faceOrderOffset : -faceOrderOffset);
                    int bOrder = baseOrder + (visualIsCloser ? -faceOrderOffset : faceOrderOffset);

                    SetRendererSort(_visual, layerId, vOrder);
                    SetRendererSort(_backVisual, layerId, bOrder);
                }
                else if (_visual)
                {
                    SetRendererSort(_visual, layerId, baseOrder);
                }

                if (_shadow)
                    SetRendererSort(_shadow, layerId, baseOrder + shadowOrderOffset);

                if (_thick)
                    SetRendererSort(_thick, layerId, baseOrder + thickOrderOffset);
            }

            // Fallback: if no named Visual, at least drive the configured spriteRenderer
            if (!autoDriveNamedChildren)
            {
                SpriteRenderer fallbackSr = spriteRenderer ? spriteRenderer : FindAnySpriteRenderer();
                if (fallbackSr)
                    SetRendererSort(fallbackSr, fallbackSr.sortingLayerID, baseOrder);
            }

            // Drive any extras
            if (extraRenderers != null)
            {
                for (int i = 0; i < extraRenderers.Length; i++)
                {
                    Renderer r = extraRenderers[i];
                    if (!r) continue;
                    SetRendererSort(r, layerId, baseOrder);
                }
            }
        }

        private void AutoAssign()
        {
            if (!trackingRoot)
                trackingRoot = transform.parent ? transform.parent : transform;

            if (!sortingGroup)
                sortingGroup = GetComponentInParent<SortingGroup>();

            if (!spriteRenderer)
                spriteRenderer = GetComponent<SpriteRenderer>() ? GetComponent<SpriteRenderer>() : GetComponentInChildren<SpriteRenderer>(true);

            if (autoDriveNamedChildren)
            {
                Transform root = trackingRoot ? trackingRoot : transform;

                _visual = FindChildSpriteRenderer(root, visualName);
                _backVisual = FindChildSpriteRenderer(root, backVisualName);
                _shadow = FindChildSpriteRenderer(root, shadowName);
                _thick = FindChildMeshRenderer(root, thickName);
            }
        }

        private static void SetRendererSort(Renderer r, int layerId, int order)
        {
            if (!r) return;
            r.sortingLayerID = layerId;
            r.sortingOrder = order;
        }

        private static SpriteRenderer FindChildSpriteRenderer(Transform root, string childName)
        {
            if (!root || string.IsNullOrEmpty(childName))
                return null;

            Transform t = FindDeepChild(root, childName);
            return t ? t.GetComponent<SpriteRenderer>() : null;
        }

        private static MeshRenderer FindChildMeshRenderer(Transform root, string childName)
        {
            if (!root || string.IsNullOrEmpty(childName))
                return null;

            Transform t = FindDeepChild(root, childName);
            return t ? t.GetComponent<MeshRenderer>() : null;
        }

        private static Transform FindDeepChild(Transform parent, string name)
        {
            // Depth-first search by exact name.
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform c = parent.GetChild(i);
                if (c.name == name)
                    return c;

                Transform found = FindDeepChild(c, name);
                if (found)
                    return found;
            }
            return null;
        }

        private SpriteRenderer FindAnySpriteRenderer()
        {
            if (spriteRenderer)
                return spriteRenderer;

            return GetComponentInChildren<SpriteRenderer>(true);
        }

        private void ApplySpriteDefaults()
        {
            if (forcePivotSortPoint)
            {
                if (spriteRenderer)
                    spriteRenderer.spriteSortPoint = SpriteSortPoint.Pivot;

                if (_visual)
                    _visual.spriteSortPoint = SpriteSortPoint.Pivot;

                if (_backVisual)
                    _backVisual.spriteSortPoint = SpriteSortPoint.Pivot;
            }
        }

        private void ClampValues()
        {
            if (sortingOrderScale < 0.001f)
                sortingOrderScale = 0.001f;

            if (depthOffset < 0f)
                depthOffset = 0f;
        }
    }
}
