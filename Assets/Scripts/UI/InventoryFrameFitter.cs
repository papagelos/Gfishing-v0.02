using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class InventoryFrameFitter : MonoBehaviour
{
    [Header("Refs")]
    public RectTransform windowRoot;   // InventoryWindow (RectTransform)
    public Image windowImage;          // Image on InventoryWindow (sliced sprite)
    public RectTransform header;       // InventoryWindow/Header
    public RectTransform body;         // InventoryWindow/Body
    public RectTransform scrollView;   // InventoryWindow/Body/ScrollView
    public RectTransform viewport;     // InventoryWindow/Body/ScrollView/Viewport
    public InventoryGridController grid;

    [Header("Layout")]
    public float extraMargin = 8f;     // extra space inside the neon rail
    public float headerHeight = 128f;  // visual height of the header strip

    [ContextMenu("Apply Layout Now")]
    public void Apply()
    {
        if (!windowRoot || !windowImage || !header || !body || !scrollView || !viewport)
            return;

        // Sprite.border is (Left, Right, Top, Bottom)
        Vector4 b = windowImage.sprite ? windowImage.sprite.border : new Vector4(64, 64, 64, 64);
        float left   = b.x + extraMargin; // L
        float right  = b.y + extraMargin; // R
        float top    = b.z + extraMargin; // T  (FIXED)
        float bottom = b.w + extraMargin; // B  (FIXED)

        // ----- Header (top strip inside the rail) -----
        header.anchorMin = new Vector2(0f, 1f);
        header.anchorMax = new Vector2(1f, 1f);
        header.pivot     = new Vector2(0.5f, 1f);
        header.offsetMin = new Vector2(left, -headerHeight); // left,  bottom (negative height)
        header.offsetMax = new Vector2(-right, -top);        // right, top

        // ----- Body (area below header, inside the rail) -----
        body.anchorMin = new Vector2(0f, 0f);
        body.anchorMax = new Vector2(1f, 1f);
        body.pivot     = new Vector2(0.5f, 0.5f);
        body.offsetMin = new Vector2(left, bottom);                 // left / bottom inside rail
        body.offsetMax = new Vector2(-right, -(top + headerHeight)); // right / top (reserve header)

        // ----- ScrollView fills Body -----
        scrollView.anchorMin = new Vector2(0f, 0f);
        scrollView.anchorMax = new Vector2(1f, 1f);
        scrollView.pivot     = new Vector2(0.5f, 0.5f);
        scrollView.offsetMin = Vector2.zero;
        scrollView.offsetMax = Vector2.zero;

        // ----- Viewport fills ScrollView -----
        viewport.anchorMin = new Vector2(0f, 0f);
        viewport.anchorMax = new Vector2(1f, 1f);
        viewport.pivot     = new Vector2(0f, 1f); // top-left so Content (0,1) aligns
        viewport.offsetMin = Vector2.zero;
        viewport.offsetMax = Vector2.zero;

        // Wire ScrollRect (if present) and refresh content size
        var sr = scrollView.GetComponent<ScrollRect>();
        if (sr)
        {
            sr.viewport = viewport;
            if (grid && grid.Content) sr.content = grid.Content;
            sr.horizontal = false;
            sr.vertical   = true;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 30f;
        }

        // Only resize grids that actually want auto-sizing
        if (grid && grid.AutoSizeContent)
        {
            grid.UpdateContentSize();
        }
    }

    void OnEnable()  { Apply(); }
    void OnValidate(){ Apply(); }
    void OnRectTransformDimensionsChange()
    {
        if (isActiveAndEnabled) Apply();
    }
}
