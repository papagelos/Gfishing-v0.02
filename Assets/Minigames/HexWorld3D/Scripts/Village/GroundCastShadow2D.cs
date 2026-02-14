using UnityEngine;

[ExecuteAlways]
[DefaultExecutionOrder(10000)] // run after most scripts (incl billboard scripts)
public class GroundCastShadow2D : MonoBehaviour
{
    public enum AnchorMode
    {
        MainRendererPivot,   // BEST for your case: uses the prop placement pivot (transform.position)
        BoundsBottom,        // Bottom of sprite bounds (stable if we use sprite.bounds local)
        CustomTransform      // Use a dedicated anchor Transform (optional)
    }

    [Header("Auto refs (no wiring needed)")]
    public SpriteRenderer mainRenderer;      // the real prop sprite (usually Visual)
    public SpriteRenderer shadowRenderer;    // this object's SpriteRenderer

    [Header("Anchor")]
    public AnchorMode anchorMode = AnchorMode.MainRendererPivot;

    [Tooltip("Only used when Anchor Mode = CustomTransform.")]
    public Transform customAnchor;

    [Tooltip("Optional world-space nudge applied to the anchor point (good for tiny corrections).")]
    public Vector3 anchorOffset = Vector3.zero;

    [Header("Shadow direction")]
    [Tooltip("OPTION B (Screen-space): If enabled, the shadow direction is locked to the screen and rotates with the camera.\n" +
             "OPTION A (World-space): If disabled, uses yaw override or sun direction in world space.")]
    public bool screenSpaceDirection = false;

    [Tooltip("Angle in degrees.\n" +
             "OPTION A (World): yaw in world degrees.\n" +
             "OPTION B (Screen): 0=down, 45=down-right, 90=right, -45=down-left.")]
    public float yawDegrees = 45f;

    [Tooltip("OPTION A (World) only: If true, uses yawDegrees. If false, may use sun direction.")]
    public bool useYawOverride = true;

    [Tooltip("OPTION A (World) only: Use RenderSettings.sun if available (Directional Light).")]
    public bool useSunIfAvailable = true;

    [Tooltip("OPTION A (World) only: Invert sun direction if needed.")]
    public bool invertSunDirection = false;

    [Tooltip("OPTION B (Screen) only: camera used to derive screen-right and screen-down.\n" +
             "Leave empty to use Camera.main. Do NOT wire scene cameras into prefabs.")]
    public Camera screenSpaceCamera;

    [Header("Placement")]
    [Tooltip("Lift above ground to avoid z-fighting / being hidden by the tile mesh.")]
    public float groundLift = 0.02f;

    [Tooltip("How far the shadow is cast, measured in PROP HEIGHTS (so scaling doesn't make it 'tiny').")]
    public float castDistanceInHeights = 0.8f;

    [Header("Shape")]
    public float widthScale = 1.0f;
    public float lengthScale = 1.6f;

    [Range(0f, 1f)]
    public float alpha = 0.18f;

    [Header("Ground orientation")]
    [Tooltip("Try -90 first. If it lies wrong, change to +90.")]
    public float groundTiltX = -90f;

    [Header("Optional: keep shadow sprite synced (useful for animated characters)")]
    public bool followMainSprite = true;

    static readonly int ColorId = Shader.PropertyToID("_Color");
    MaterialPropertyBlock mpb;

    void Reset()
    {
        shadowRenderer = GetComponent<SpriteRenderer>();
        AutoFindMainRenderer();
    }

    void OnEnable()
    {
        if (!shadowRenderer) shadowRenderer = GetComponent<SpriteRenderer>();
        AutoFindMainRenderer();
    }

    void AutoFindMainRenderer()
    {
        if (mainRenderer != null) return;

        // Try: parent
        if (transform.parent)
        {
            var sr = transform.parent.GetComponent<SpriteRenderer>();
            if (sr && sr != shadowRenderer)
            {
                mainRenderer = sr;
                return;
            }

            // Try: any SpriteRenderer under the same prefab root (siblings / children), excluding our own
            var all = transform.parent.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] && all[i] != shadowRenderer)
                {
                    mainRenderer = all[i];
                    return;
                }
            }
        }

        // Fallback: search upward
        var inParents = GetComponentsInParent<SpriteRenderer>(true);
        for (int i = 0; i < inParents.Length; i++)
        {
            if (inParents[i] && inParents[i] != shadowRenderer)
            {
                mainRenderer = inParents[i];
                return;
            }
        }
    }

    void LateUpdate()
    {
        if (!shadowRenderer) shadowRenderer = GetComponent<SpriteRenderer>();
        if (!mainRenderer) AutoFindMainRenderer();
        if (!shadowRenderer || !mainRenderer) return;

        // 0) Optionally sync sprite (helps if you later use this for characters)
        if (followMainSprite && mainRenderer.sprite != null)
        {
            shadowRenderer.sprite = mainRenderer.sprite;
            shadowRenderer.flipX = mainRenderer.flipX;
            shadowRenderer.flipY = mainRenderer.flipY;
        }

        // 1) Pick direction (yaw + ground dir)
        float yaw;
        Vector3 dir;

        if (screenSpaceDirection)
        {
            // OPTION B: screen-locked cast direction (rotates with camera)
            Camera cam = screenSpaceCamera ? screenSpaceCamera : Camera.main;

            if (!cam)
            {
                // No camera -> fallback to world forward
                yaw = yawDegrees;
                float r = yaw * Mathf.Deg2Rad;
                dir = new Vector3(Mathf.Sin(r), 0f, Mathf.Cos(r));
            }
            else
            {
                // Build ground-plane basis from camera (screen-right and screen-down)
                Vector3 screenRight = cam.transform.right;
                screenRight.y = 0f;

                // "down on screen" is -camera.up; if that projects poorly (top-down cam), fallback to -forward.
                Vector3 screenDown = -cam.transform.up;
                screenDown.y = 0f;

                if (screenDown.sqrMagnitude < 0.000001f)
                {
                    screenDown = -cam.transform.forward;
                    screenDown.y = 0f;
                }

                if (screenRight.sqrMagnitude < 0.000001f)
                {
                    screenRight = cam.transform.right;
                    screenRight.y = 0f;
                }

                if (screenRight.sqrMagnitude > 0.000001f) screenRight.Normalize();
                else screenRight = Vector3.right;

                if (screenDown.sqrMagnitude > 0.000001f) screenDown.Normalize();
                else screenDown = Vector3.back;

                // yawDegrees interpreted as SCREEN angle: 0=down, 45=down-right, 90=right
                float rad = yawDegrees * Mathf.Deg2Rad;
                dir = (screenDown * Mathf.Cos(rad) + screenRight * Mathf.Sin(rad));
                dir.y = 0f;

                if (dir.sqrMagnitude > 0.000001f) dir.Normalize();
                else dir = Vector3.forward;

                yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            }
        }
        else
        {
            // OPTION A: world-space yaw (override or sun)
            yaw = yawDegrees;

            if (!useYawOverride && useSunIfAvailable)
            {
                var sun = RenderSettings.sun;
                if (sun && sun.type == LightType.Directional)
                {
                    Vector3 d = -sun.transform.forward;
                    if (invertSunDirection) d = -d;
                    d.y = 0f;
                    if (d.sqrMagnitude > 0.0001f)
                    {
                        d.Normalize();
                        yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                    }
                }
            }

            float rad = yaw * Mathf.Deg2Rad;
            dir = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
        }

        if (dir.sqrMagnitude > 0.0001f) dir.Normalize();
        else dir = Vector3.forward;

        // 2) Use LOCAL sprite bounds (stable) when possible
        Bounds localSpriteBounds = default;
        bool hasSpriteBounds = (mainRenderer.sprite != null);
        if (hasSpriteBounds) localSpriteBounds = mainRenderer.sprite.bounds;

        // Prop "height" in world units using sprite local bounds * lossy scale
        float propHeightWorld;
        if (hasSpriteBounds)
            propHeightWorld = Mathf.Max(0.0001f, localSpriteBounds.size.y * Mathf.Abs(mainRenderer.transform.lossyScale.y));
        else
            propHeightWorld = Mathf.Max(0.0001f, mainRenderer.bounds.size.y);

        float castDist = castDistanceInHeights * propHeightWorld;

        // 3) Compute anchor/base position
        Vector3 basePosWorld;

        switch (anchorMode)
        {
            case AnchorMode.BoundsBottom:
                if (hasSpriteBounds)
                {
                    // Stable even if Visual rotates (billboarding): only adjust Y from pivot using local minY.
                    float bottomOffsetY = localSpriteBounds.min.y * Mathf.Abs(mainRenderer.transform.lossyScale.y);
                    basePosWorld = mainRenderer.transform.position + Vector3.up * bottomOffsetY;
                }
                else
                {
                    Bounds b = mainRenderer.bounds;
                    basePosWorld = new Vector3(b.center.x, b.min.y, b.center.z);
                }
                break;

            case AnchorMode.CustomTransform:
                basePosWorld = (customAnchor != null) ? customAnchor.position : mainRenderer.transform.position;
                break;

            case AnchorMode.MainRendererPivot:
            default:
                basePosWorld = mainRenderer.transform.position;
                break;
        }

        basePosWorld += anchorOffset;

        // 4) Place on ground + cast direction
        transform.position = basePosWorld + Vector3.up * groundLift + dir * castDist;

        // 5) Lay flat and aim
        transform.rotation = Quaternion.Euler(groundTiltX, yaw, 0f);

        // 6) Shape
        transform.localScale = new Vector3(widthScale, lengthScale, 1f);

        // 7) Sorting: behind prop
        shadowRenderer.sortingLayerID = mainRenderer.sortingLayerID;
        shadowRenderer.sortingOrder = mainRenderer.sortingOrder - 1;

        // 8) Alpha via property block
        if (mpb == null) mpb = new MaterialPropertyBlock();
        shadowRenderer.GetPropertyBlock(mpb);
        mpb.SetColor(ColorId, new Color(0f, 0f, 0f, alpha));
        shadowRenderer.SetPropertyBlock(mpb);
    }
}
