using UnityEngine;

namespace GalacticFishing
{
    [ExecuteAlways]
    public sealed class OrthoCameraFitBounds : MonoBehaviour
    {
        public Camera targetCamera;

        [Header("World area to keep fully visible")]
        public Transform boundsMin; // bottom-left
        public Transform boundsMax; // top-right

        [Header("Padding (world units)")]
        public float padX = 0f;
        public float padY = 0f;

        [Header("Camera placement")]
        public Vector3 centerOffset = new Vector3(0, 0, -10f);
        public bool lockZToOffset = true;

        float _lastAspect = -1f;
        Vector2 _lastMin, _lastMax;

        void OnEnable()   { Refit(true); }
        void OnValidate() { Refit(true); }

        void Update()
        {
            var cam = targetCamera ? targetCamera : Camera.main;
            if (!cam || !cam.orthographic || !boundsMin || !boundsMax) return;

            float aspect = cam.aspect;
            Vector2 min = boundsMin.position;
            Vector2 max = boundsMax.position;

            if (!Mathf.Approximately(aspect, _lastAspect) || min != _lastMin || max != _lastMax)
            {
                Refit(false);
                _lastAspect = aspect;
                _lastMin = min;
                _lastMax = max;
            }
        }

        public void Refit(bool forceCenter)
        {
            var cam = targetCamera ? targetCamera : Camera.main;
            if (!cam || !boundsMin || !boundsMax) return;
            cam.orthographic = true;

            Vector2 min = boundsMin.position;
            Vector2 max = boundsMax.position;

            float xMin = Mathf.Min(min.x, max.x);
            float xMax = Mathf.Max(min.x, max.x);
            float yMin = Mathf.Min(min.y, max.y);
            float yMax = Mathf.Max(min.y, max.y);

            float width  = (xMax - xMin) + 2f * Mathf.Max(0f, padX);
            float height = (yMax - yMin) + 2f * Mathf.Max(0f, padY);

            float halfH = height * 0.5f;
            float halfW = width  * 0.5f;
            float halfHFromW = halfW / Mathf.Max(0.0001f, cam.aspect);
            cam.orthographicSize = Mathf.Max(halfH, halfHFromW);

            Vector3 center = new Vector3((xMin + xMax) * 0.5f, (yMin + yMax) * 0.5f, 0f);
            Vector3 pos = center + centerOffset;
            if (!lockZToOffset) pos.z = cam.transform.position.z;
            cam.transform.position = pos;
        }
    }
}
