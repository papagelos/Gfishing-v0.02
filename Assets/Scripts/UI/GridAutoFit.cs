using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[ExecuteAlways]
[RequireComponent(typeof(GridLayoutGroup))]
public class GridAutoFit : MonoBehaviour
{
    [Header("Refs")]
    public RectTransform ContentFrame;          // inner area the grid should fill
    public GridLayoutGroup Grid;                // the GridLayoutGroup to drive

    [Header("Layout")]
    [Min(1)] public int Columns = 13;
    [Min(1)] public int Rows    = 6;
    [Min(0)] public float Spacing = 8f;         // gap between cells (pixels)
    [Min(0)] public float InnerMargin = 1f;     // inset from ContentFrame edges (pixels)
    public bool SnapToWholePixels = true;
    public bool Verbose = false;

    void Reset()
    {
        Grid = GetComponent<GridLayoutGroup>();
        if (!ContentFrame) ContentFrame = transform.parent as RectTransform;

        Grid.startCorner   = GridLayoutGroup.Corner.UpperLeft;
        Grid.startAxis     = GridLayoutGroup.Axis.Horizontal;
        Grid.childAlignment= TextAnchor.UpperLeft;
        Grid.constraint    = GridLayoutGroup.Constraint.FixedColumnCount;
    }

    void OnEnable()                    => Run();
    void OnRectTransformDimensionsChange() => Run();

#if UNITY_EDITOR
    void OnValidate()                  => Run();
    void Update() { if (!Application.isPlaying) Apply(); }
#endif

    void Run()
    {
        if (!isActiveAndEnabled) return;
#if UNITY_EDITOR
        if (!Application.isPlaying) { Apply(); return; }
#endif
        // Apply *after* other Awake/OnEnable scripts so our values win.
        StopAllCoroutines();
        StartCoroutine(ApplyNextFrame());
    }

    IEnumerator ApplyNextFrame()
    {
        yield return null;  // let CanvasScaler & any other layout scripts finish
        Apply();
    }

    [ContextMenu("Apply Now")]
    public void Apply()
    {
        if (!Grid) Grid = GetComponent<GridLayoutGroup>();
        if (!ContentFrame || !Grid) return;

        // Pixel-accurate frame size (respects Canvas Scaler)
        Canvas canvas = GetComponentInParent<Canvas>();
        Rect framePx  = RectTransformUtility.PixelAdjustRect(ContentFrame, canvas);

        // Usable width/height after inner margin
        float left = InnerMargin, right = InnerMargin, top = InnerMargin, bottom = InnerMargin;
        float usableW = Mathf.Max(0, framePx.width  - left - right);
        float usableH = Mathf.Max(0, framePx.height - top  - bottom);

        // Total spacing the grid needs to reserve
        float sx = Mathf.Max(0, Spacing) * (Columns - 1);
        float sy = Mathf.Max(0, Spacing) * (Rows    - 1);

        // Square cells that fit both axes
        float cw = (usableW - sx) / Columns;
        float ch = (usableH - sy) / Rows;
        float cell = Mathf.Max(1, Mathf.Min(cw, ch));
        if (SnapToWholePixels) cell = Mathf.Floor(cell);
        float spacingOut = SnapToWholePixels ? Mathf.Round(Spacing) : Spacing;

        // Drive the Grid
        Grid.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
        Grid.constraintCount = Mathf.Max(1, Columns);
        Grid.startCorner     = GridLayoutGroup.Corner.UpperLeft;
        Grid.startAxis       = GridLayoutGroup.Axis.Horizontal;
        Grid.childAlignment  = TextAnchor.UpperLeft;

        Grid.padding.left    = Mathf.RoundToInt(left);
        Grid.padding.right   = Mathf.RoundToInt(right);
        Grid.padding.top     = Mathf.RoundToInt(top);
        Grid.padding.bottom  = Mathf.RoundToInt(bottom);
        Grid.spacing         = new Vector2(spacingOut, spacingOut);
        Grid.cellSize        = new Vector2(cell, cell);

        if (Verbose)
            Debug.Log($"[GridAutoFit] frame={framePx.size}  cell={cell}  spacing={spacingOut}  cols={Columns} rows={Rows}");
    }
}
