using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Makes a GridLayoutGroup fill its RectTransform with cards,
/// given a fixed row/column count and a card aspect (width / height).
/// It chooses the largest cell size that fits BOTH horizontally and vertically.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(GridLayoutGroup))]
public class CardGridAutoSizer : MonoBehaviour
{
    [Header("Grid logical layout")]
    [Min(1)] public int columns = 5;
    [Min(1)] public int rows    = 2;

    [Header("Card shape")]
    [Tooltip("Card width / height (e.g. 0.83 means portrait, 1.2 is more landscape).")]
    public float cardAspect = 0.83f;

    [Header("Debug")]
    public bool logComputedSize = false;

    RectTransform _rect;
    GridLayoutGroup _grid;

    void Awake()
    {
        Cache();
    }

    void OnEnable()
    {
        Cache();
        Apply();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        Cache();
        Apply();
    }
#endif

    void OnRectTransformDimensionsChange()
    {
        // Called when game view size / panel size changes
        Apply();
    }

    void Cache()
    {
        if (!_rect) _rect = GetComponent<RectTransform>();
        if (!_grid) _grid = GetComponent<GridLayoutGroup>();
    }

    void Apply()
    {
        if (!_rect || !_grid) return;
        if (!isActiveAndEnabled && !Application.isEditor) return;

        int c = Mathf.Max(1, columns);
        int r = Mathf.Max(1, rows);
        float aspect = Mathf.Max(0.01f, cardAspect);

        // Total rect size in canvas units
        var rect = _rect.rect;
        float totalW = rect.width;
        float totalH = rect.height;

        if (totalW <= 0f || totalH <= 0f)
            return;

        // Subtract grid padding
        var pad = _grid.padding;
        float availW = totalW - pad.left - pad.right;
        float availH = totalH - pad.top - pad.bottom;

        // Subtract spacing between cells
        availW -= _grid.spacing.x * (c - 1);
        availH -= _grid.spacing.y * (r - 1);

        if (availW <= 0f || availH <= 0f)
            return;

        // --- Compute the HEIGHT limit and WIDTH limit ------------------------
        // Cell height cannot exceed:
        //   1) vertical space / rows
        //   2) horizontal space / (columns * aspect)
        float maxHeightFromRows   = availH / r;
        float maxHeightFromWidth  = availW / (c * aspect);
        float cellH = Mathf.Min(maxHeightFromRows, maxHeightFromWidth);
        float cellW = cellH * aspect;

        if (cellH <= 0f || cellW <= 0f)
            return;

        // Apply to grid
        _grid.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
        _grid.constraintCount = c;
        _grid.cellSize        = new Vector2(cellW, cellH);

        if (logComputedSize)
        {
            Debug.Log(
                $"[CardGridAutoSizer] rect=({totalW:0},{totalH:0}) " +
                $"avail=({availW:0},{availH:0}) cols={c} rows={r} " +
                $"=> cell=({cellW:0},{cellH:0})",
                this
            );
        }
    }
}
