using UnityEngine;

public class CursorSetter : MonoBehaviour
{
    [Header("Cursor")]
    [SerializeField] private Texture2D cursorTexture;

    // Hotspot is the "click point" inside the cursor image (in pixels from top-left).
    [SerializeField] private Vector2 hotspot = Vector2.zero;

    private void Awake()
    {
        // This makes this object survive scene changes
        DontDestroyOnLoad(gameObject);

        ApplyCursor();
    }

    private void OnEnable()
    {
        ApplyCursor();
    }

    private void ApplyCursor()
    {
        if (!cursorTexture) return;
        Cursor.SetCursor(cursorTexture, hotspot, CursorMode.Auto);
    }
}
