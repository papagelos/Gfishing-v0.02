using UnityEngine;

[ExecuteAlways]                       // run in Edit mode too
[RequireComponent(typeof(SpriteRenderer))]
public class FitSpriteToCamera : MonoBehaviour
{
    public enum FitMode { FitInside, Cover }

    [Header("Fit Settings")]
    public FitMode mode = FitMode.Cover;

    [Tooltip("Optional. If not set, uses Camera.main.")]
    public Camera targetCamera;

    void OnEnable()   => Apply();
    void OnValidate() => Apply();

#if UNITY_EDITOR
    // Keep the preview correct while not playing
    void Update()
    {
        if (!Application.isPlaying) Apply();
    }
#endif

    /// <summary>Call this after you change the sprite/background at runtime.</summary>
    public void ApplyNow() => Apply();

    void Apply()
    {
        var sr = GetComponent<SpriteRenderer>();
        if (!sr || !sr.sprite) return;

        var cam = targetCamera ? targetCamera : Camera.main;
        if (!cam) return;

        // Camera world size at the sprite plane
        float worldH, worldW;
        if (cam.orthographic)
        {
            worldH = cam.orthographicSize * 2f;
            worldW = worldH * cam.aspect;
        }
        else
        {
            // Perspective fallback: compute size at sprite depth
            float dist = Mathf.Abs((transform.position - cam.transform.position).z);
            worldH = 2f * dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            worldW = worldH * cam.aspect;
        }

        // Sprite size in world units at scale = 1
        Vector2 spriteWorld = sr.sprite.bounds.size;

        float sx = worldW / spriteWorld.x;
        float sy = worldH / spriteWorld.y;
        float s  = (mode == FitMode.Cover) ? Mathf.Max(sx, sy) : Mathf.Min(sx, sy);

        transform.localScale = new Vector3(s, s, 1f);
    }
}
