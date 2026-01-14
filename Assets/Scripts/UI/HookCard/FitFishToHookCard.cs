using UnityEngine;

public class FitFishToHookCard : MonoBehaviour
{
    [Header("Refs")]
    public HookCardUI hookCard;            // HookCard (root) here
    public RectTransform fishRect;         // Big fish RectTransform (BeepFish)

    [Header("Content margins inside the panel (pixels)")]
    public float left = 120f;
    public float right = 120f;
    public float top = 150f;
    public float bottom = 170f;

    [Header("Layout")]
    [Range(0.6f, 1f)] public float fill = 0.92f; // % of the available square to use
    public float yNudge = 12f;                   // small upward bias in panel space (pixels)

    // original state
    Vector2 _origSize;
    Vector3 _origPos;
    Vector2 _origAnchorMin, _origAnchorMax;
    Vector2 _origPivot;
    bool _saved, _fitted;

    void Reset()
    {
        fishRect = GetComponent<RectTransform>();
    }

    void LateUpdate()
    {
        if (!hookCard || !hookCard.panelRect || !fishRect) return;

        // Card hidden? restore once and bail
        if (!hookCard.gameObject.activeInHierarchy)
        {
            if (_fitted) Restore();
            return;
        }

        if (!_saved) SaveOriginal();

        // Panel content box (subtract asymmetric margins)
        var r = hookCard.panelRect.rect;
        float contentW = Mathf.Max(0f, r.width  - (left + right));
        float contentH = Mathf.Max(0f, r.height - (top  + bottom));
        float target = Mathf.Min(contentW, contentH) * fill;

        // Size fish (square; make sure fish Image has Preserve Aspect ON)
        fishRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, target);
        fishRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,   target);

        // Center + Y nudge in panel local, then convert to world
        var localCenter = new Vector3(r.center.x, r.center.y + yNudge, 0f);
        Vector3 worldCenter = hookCard.panelRect.TransformPoint(localCenter);
        fishRect.position = worldCenter;

        _fitted = true;
    }

    void SaveOriginal()
    {
        _origSize      = fishRect.rect.size;
        _origPos       = fishRect.position;
        _origAnchorMin = fishRect.anchorMin; _origAnchorMax = fishRect.anchorMax;
        _origPivot     = fishRect.pivot;
        _saved         = true;
    }

    void Restore()
    {
        fishRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _origSize.x);
        fishRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,   _origSize.y);
        fishRect.position  = _origPos;
        fishRect.anchorMin = _origAnchorMin; fishRect.anchorMax = _origAnchorMax;
        fishRect.pivot     = _origPivot;
        _fitted            = false;
    }
}
