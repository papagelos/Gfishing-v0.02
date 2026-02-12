using UnityEngine;
using UnityEngine.InputSystem;

namespace GalacticFishing.Minigames.HexWorld
{
    [RequireComponent(typeof(Camera))]
    public sealed class HexCameraPanZoom3D : MonoBehaviour
    {
        [Header("Target (orbit/pan around this)")]
        [SerializeField] private Transform orbitTarget;

        [Header("Pan (MMB drag)")]
        [SerializeField] private float panSpeed = 1.0f;

        [Header("Rotate (RMB drag)")]
        [SerializeField] private float rotateSpeed = 0.15f;
        [SerializeField] private float minPitch = 15f;
        [SerializeField] private float maxPitch = 75f;

        [Header("Zoom (Mouse wheel -> Orthographic Size)")]
        [SerializeField] private float zoomSpeed = 1.0f;
        [SerializeField] private float minOrthoSize = 0.8f;
        [SerializeField] private float maxOrthoSize = 12f;

        [Header("Camera distance (for orbit)")]
        [SerializeField] private float distance = 8f;

        private Camera _cam;

        private Vector2 _lastMousePos;
        private bool _panning;
        private bool _rotating;

        private float _yaw;
        private float _pitch;
        private Vector3 _lastOrbitTargetPos;
        private bool _hasLastOrbitTargetPos;

        public Transform OrbitTarget => orbitTarget;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            if (!_cam.orthographic)
                Debug.LogWarning("HexCameraPanZoom3D expects an Orthographic camera.");

            // Fix common "mesh gets sliced" issue for low-angle cameras
            _cam.nearClipPlane = Mathf.Min(_cam.nearClipPlane, 0.01f);
            _cam.farClipPlane = Mathf.Max(_cam.farClipPlane, 5000f);

            // Create/find a target if none assigned
            if (!orbitTarget)
            {
                var existing = GameObject.Find("CameraFocus_Origin");
                if (existing) orbitTarget = existing.transform;
                else
                {
                    var go = new GameObject("CameraFocus_Origin");
                    go.transform.position = Vector3.zero;
                    orbitTarget = go.transform;
                }
            }

            // Initialize yaw/pitch from current camera rotation
            var e = transform.rotation.eulerAngles;
            _pitch = NormalizePitch(e.x);
            _yaw = e.y;

            // If distance not set, infer it
            if (distance <= 0.01f)
                distance = Vector3.Distance(transform.position, orbitTarget.position);

            ApplyOrbit();
            _lastOrbitTargetPos = orbitTarget.position;
            _hasLastOrbitTargetPos = true;
        }

        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            // ---- Zoom ----
            float scrollY = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scrollY) > 0.01f)
            {
                float step = scrollY / 120f; // normalize-ish
                _cam.orthographicSize = Mathf.Clamp(
                    _cam.orthographicSize - step * zoomSpeed,
                    minOrthoSize,
                    maxOrthoSize
                );
            }

            // ---- Pan (MMB) ----
            if (mouse.middleButton.wasPressedThisFrame)
            {
                _panning = true;
                _lastMousePos = mouse.position.ReadValue();
            }
            else if (mouse.middleButton.wasReleasedThisFrame)
            {
                _panning = false;
            }

            if (_panning && mouse.middleButton.isPressed)
            {
                Vector2 cur = mouse.position.ReadValue();
                Vector2 delta = cur - _lastMousePos;
                _lastMousePos = cur;

                // world units per pixel for ortho camera
                float worldPerPixel = (_cam.orthographicSize * 2f) / Mathf.Max(1, Screen.height);

                Vector3 right = transform.right;
                Vector3 forward = transform.forward;

                // keep movement on XZ plane
                right.y = 0f; forward.y = 0f;
                right.Normalize(); forward.Normalize();

                Vector3 move = (-right * delta.x + -forward * delta.y) * (worldPerPixel * panSpeed);
                orbitTarget.position += move;
                ApplyOrbit();
            }

            // ---- Rotate (RMB) ----
            if (mouse.rightButton.wasPressedThisFrame)
            {
                _rotating = true;
                _lastMousePos = mouse.position.ReadValue();
            }
            else if (mouse.rightButton.wasReleasedThisFrame)
            {
                _rotating = false;
            }

            if (_rotating && mouse.rightButton.isPressed)
            {
                Vector2 cur = mouse.position.ReadValue();
                Vector2 delta = cur - _lastMousePos;
                _lastMousePos = cur;

                _yaw += delta.x * rotateSpeed;
                _pitch -= delta.y * rotateSpeed;
                _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);

                ApplyOrbit();
            }

            if (orbitTarget && (!_hasLastOrbitTargetPos || orbitTarget.position != _lastOrbitTargetPos))
            {
                ApplyOrbit();
                _lastOrbitTargetPos = orbitTarget.position;
                _hasLastOrbitTargetPos = true;
            }
        }

        private void ApplyOrbit()
        {
            Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);

            // Orbit position: behind the target in camera-forward direction
            Vector3 offset = rot * new Vector3(0f, 0f, -distance);
            transform.position = orbitTarget.position + offset;
            transform.rotation = rot;
        }

        public void SetOrbitTarget(Transform target, bool snapImmediately = true)
        {
            if (!target)
                return;

            orbitTarget = target;

            if (distance <= 0.01f)
                distance = Vector3.Distance(transform.position, orbitTarget.position);

            if (snapImmediately)
                ApplyOrbit();

            _lastOrbitTargetPos = orbitTarget.position;
            _hasLastOrbitTargetPos = true;
        }

        private static float NormalizePitch(float x)
        {
            // Unity gives 0..360, convert to -180..180 then make it positive-ish pitch
            if (x > 180f) x -= 360f;
            return Mathf.Clamp(x, -89f, 89f);
        }
    }
}
