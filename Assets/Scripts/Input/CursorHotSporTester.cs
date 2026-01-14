using UnityEngine;
using UnityEngine.InputSystem;

public class CursorHotspotTester : MonoBehaviour
{
    [Header("Cursor")]
    [SerializeField] private Texture2D cursorTexture;
    [SerializeField] private Vector2 hotspot = new Vector2(32, 32);
    [SerializeField] private CursorMode mode = CursorMode.Auto;

    [Header("Nudge")]
    [SerializeField] private float step = 1f;      // arrow keys
    [SerializeField] private float bigStep = 10f;  // hold Shift

    private Texture2D _px;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);

        _px = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        _px.SetPixel(0, 0, Color.white);
        _px.Apply();

        Apply();
    }

    private void Apply()
    {
        if (!cursorTexture) return;
        Cursor.SetCursor(cursorTexture, hotspot, mode);
    }

    private void Update()
    {
        float s = (Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed) ? bigStep : step;

        bool changed = false;
        if (Keyboard.current != null)
        {
            if (Keyboard.current.leftArrowKey.wasPressedThisFrame)  { hotspot.x -= s; changed = true; }
            if (Keyboard.current.rightArrowKey.wasPressedThisFrame) { hotspot.x += s; changed = true; }
            if (Keyboard.current.upArrowKey.wasPressedThisFrame)    { hotspot.y -= s; changed = true; } // y is from TOP
            if (Keyboard.current.downArrowKey.wasPressedThisFrame)  { hotspot.y += s; changed = true; }

            if (Keyboard.current.enterKey.wasPressedThisFrame)
                Debug.Log($"[CursorHotspotTester] Final hotspot = ({hotspot.x}, {hotspot.y})");
        }

        if (changed) Apply();
    }

    private void OnGUI()
    {
        // Mouse position in screen space (bottom-left origin)
        Vector2 m = Mouse.current != null ? Mouse.current.position.ReadValue() : (Vector2)Input.mousePosition;

        // OnGUI uses top-left origin, so flip Y
        float x = m.x;
        float y = Screen.height - m.y;

        // Draw a small "+" exactly at the real click point (hotspot)
        float size = 9f;
        float half = size * 0.5f;

        // Horizontal line
        GUI.DrawTexture(new Rect(x - half, y, size, 1), _px);
        // Vertical line
        GUI.DrawTexture(new Rect(x, y - half, 1, size), _px);

        GUI.Label(new Rect(10, 10, 800, 25), $"Hotspot (top-left px): ({hotspot.x}, {hotspot.y})   Mode: {mode}");
        GUI.Label(new Rect(10, 30, 800, 25), $"MousePos: ({m.x:0}, {m.y:0})   (Press arrows to nudge, Shift=10px, Enter=log)");
    }
}
