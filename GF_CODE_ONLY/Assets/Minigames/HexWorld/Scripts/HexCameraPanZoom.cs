using UnityEngine;
using UnityEngine.InputSystem;

namespace GalacticFishing.Minigames.HexWorld
{
    [RequireComponent(typeof(Camera))]
    public class HexCameraPanZoom : MonoBehaviour
    {
        public float minOrthoSize = 1.8f;
        public float maxOrthoSize = 14.0f;
        public float zoomSpeed = 0.02f;
        public float panSpeed = 1.0f;

        Camera _cam;
        Vector3 _dragStartWorld;
        bool _dragging;

        void Awake()
        {
            _cam = GetComponent<Camera>();
            _cam.orthographic = true;
        }

        void Update()
        {
            if (Mouse.current == null) return;

            // Zoom
            float scrollY = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scrollY) > 0.01f)
            {
                float delta = -scrollY * zoomSpeed * (_cam.orthographicSize * 0.15f);
                _cam.orthographicSize = Mathf.Clamp(_cam.orthographicSize + delta, minOrthoSize, maxOrthoSize);
            }

            // Pan (RMB or MMB)
            bool panHeld = Mouse.current.middleButton.isPressed || Mouse.current.rightButton.isPressed;

            if (panHeld && !_dragging)
            {
                _dragging = true;
                _dragStartWorld = _cam.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            }
            else if (!panHeld && _dragging)
            {
                _dragging = false;
            }

            if (_dragging)
            {
                Vector3 nowWorld = _cam.ScreenToWorldPoint(Mouse.current.position.ReadValue());
                Vector3 deltaMove = _dragStartWorld - nowWorld;
                transform.position += deltaMove * panSpeed;
            }
        }
    }
}
