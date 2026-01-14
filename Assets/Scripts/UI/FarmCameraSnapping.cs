using UnityEngine;
using UnityEngine.InputSystem;

public sealed class FarmMapCameraSnap : MonoBehaviour
{
    [Header("References")]
    public Camera cam;

    public SpriteRenderer bgBL;
    public SpriteRenderer bgBR;
    public SpriteRenderer bgTL;
    public SpriteRenderer bgTR;

    [Header("Zoom")]
    public float fitPadding = 0.0f;
    public float smooth = 10f;

    [Header("Test Controls (optional)")]
    public bool enableTestKeys = true;   // 1-4 to focus, Z to zoom out/in

    Vector3 _targetPos;
    float _targetSize;

    bool _zoomedOut;

    void Awake()
    {
        if (!cam) cam = GetComponent<Camera>();
        Focus(bgBL);
        FitOnePanel(bgBL);
    }

    void Update()
    {
        if (enableTestKeys) HandleKeys();

        // smooth move + zoom
        var p = cam.transform.position;
        p = Vector3.Lerp(p, _targetPos, 1f - Mathf.Exp(-smooth * Time.deltaTime));
        cam.transform.position = p;

        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, _targetSize, 1f - Mathf.Exp(-smooth * Time.deltaTime));
    }

    void HandleKeys()
    {
        var k = Keyboard.current;
        if (k == null) return;

        if (k.digit1Key.wasPressedThisFrame) { Focus(bgBL); if (!_zoomedOut) FitOnePanel(bgBL); }
        if (k.digit2Key.wasPressedThisFrame) { Focus(bgBR); if (!_zoomedOut) FitOnePanel(bgBR); }
        if (k.digit3Key.wasPressedThisFrame) { Focus(bgTL); if (!_zoomedOut) FitOnePanel(bgTL); }
        if (k.digit4Key.wasPressedThisFrame) { Focus(bgTR); if (!_zoomedOut) FitOnePanel(bgTR); }

        if (k.zKey.wasPressedThisFrame)
        {
            _zoomedOut = !_zoomedOut;
            if (_zoomedOut) FitAllPanels();
            else FitOnePanel(GetCurrentFocused());
        }
    }

    SpriteRenderer GetCurrentFocused()
    {
        // pick whichever is closest to target pos (good enough)
        SpriteRenderer best = bgBL;
        float bestD = float.MaxValue;

        foreach (var sr in new[] { bgBL, bgBR, bgTL, bgTR })
        {
            if (!sr) continue;
            float d = Vector2.Distance(sr.bounds.center, (Vector2)_targetPos);
            if (d < bestD) { bestD = d; best = sr; }
        }
        return best;
    }

    public void Focus(SpriteRenderer sr)
    {
        if (!sr) return;
        var c = sr.bounds.center;
        _targetPos = new Vector3(c.x, c.y, cam.transform.position.z);
    }

    public void FitOnePanel(SpriteRenderer sr)
    {
        if (!sr) return;

        // Fit HEIGHT of a single panel
        float panelH = sr.bounds.size.y;
        _targetSize = (panelH * 0.5f) + fitPadding;
    }

    public void FitAllPanels()
    {
        Bounds b = bgBL.bounds;
        b.Encapsulate(bgBR.bounds);
        b.Encapsulate(bgTL.bounds);
        b.Encapsulate(bgTR.bounds);

        // Fit HEIGHT of full 2x2 map
        float h = b.size.y;
        _targetSize = (h * 0.5f) + fitPadding;

        // center camera on the whole map
        var c = b.center;
        _targetPos = new Vector3(c.x, c.y, cam.transform.position.z);
    }
}
