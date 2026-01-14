using UnityEngine;
using UnityEngine.InputSystem; // New Input System

namespace GalacticFishing
{
    /// <summary>Switch to a crosshair cursor when the mouse is below the water surface.</summary>
    public sealed class UnderwaterCursorSwitcher : MonoBehaviour
    {
        [Header("Refs")]
        public Camera worldCamera;     // optional; falls back to Camera.main
        public Transform waterSurface; // drag your WaterSurface (with WaterSurfaceMarker)

        [Header("Cursors")]
        public Texture2D cursorAbove;        // optional (null keeps OS default)
        public Vector2  hotspotAbove = Vector2.zero;

        public Texture2D cursorUnderwater;   // assign your crosshair PNG
        public Vector2  hotspotUnderwater = new Vector2(16, 16);

        public CursorMode cursorMode = CursorMode.Auto;

        bool _isUnder;
        bool _appliedOnce;

        void Awake()
        {
            if (worldCamera == null) worldCamera = Camera.main;
        }

        void OnDisable()
        {
            Cursor.SetCursor(null, Vector2.zero, cursorMode);
            _appliedOnce = false;
        }

        void Update()
        {
            if (waterSurface == null) return;
            if (worldCamera == null) worldCamera = Camera.main;
            if (worldCamera == null) return;

            float waterY = waterSurface.position.y;

            // New Input System
            Vector2 m = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
            float depth = Mathf.Abs(waterSurface.position.z - worldCamera.transform.position.z);
            Vector3 mw = worldCamera.ScreenToWorldPoint(new Vector3(m.x, m.y, depth));

            bool nowUnder = mw.y < waterY;
            if (!_appliedOnce || nowUnder != _isUnder)
            {
                _isUnder = nowUnder;
                Cursor.SetCursor(
                    nowUnder ? cursorUnderwater : cursorAbove,
                    nowUnder ? hotspotUnderwater : hotspotAbove,
                    cursorMode
                );
                _appliedOnce = true;
            }
        }
    }
}
