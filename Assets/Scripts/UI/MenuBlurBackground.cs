using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RawImage))]
public sealed class MenuBlurBackground : MonoBehaviour
{
    public enum CaptureMode
    {
        Auto = 0,
        CameraSnapshot = 1,
        BackdropUITexture = 2,
        BackdropSpriteRenderer = 3,
        ExplicitTexture = 4
    }

    [Header("Capture Mode")]
    [SerializeField] private CaptureMode captureMode = CaptureMode.Auto;

    [Header("Camera Source (optional)")]
    [Tooltip("Usually your Main Camera. Used when Capture Mode is CameraSnapshot (or Auto fallback).")]
    [SerializeField] private Camera sourceCamera;

    [Header("Backdrop Sources (optional)")]
    [Tooltip("Assign UI_Backdrop if it is a RawImage.")]
    [SerializeField] private RawImage backdropRawImage;

    [Tooltip("Assign UI_Backdrop if it is an Image.")]
    [SerializeField] private Image backdropImage;

    [Tooltip("Assign BackdropWorld (SpriteRenderer) or WorldManager.backdropRenderer here.")]
    [SerializeField] private SpriteRenderer backdropRenderer;

    [Tooltip("Optional: directly provide a texture to blur.")]
    [SerializeField] private Texture explicitTexture;

    [Header("Auto Wiring (optional)")]
    [Tooltip("If Backdrop Renderer is not assigned, try to find a GameObject named 'BackdropWorld' and use its SpriteRenderer.")]
    [SerializeField] private bool autoFindBackdropWorldByName = true;

    [SerializeField] private string backdropWorldName = "BackdropWorld";

    [Header("Quality / Cost")]
    [Range(2, 32)] public int downsample = 8;     // higher = cheaper + more "smooth/flat"
    [Range(0, 10)] public int iterations = 4;     // more = smoother blur
    [Range(0.25f, 8f)] public float blurRadius = 2.5f;

    [Header("Behavior")]
    public bool captureOnEnable = true;

    [Tooltip("In URP/SRP it is often safer to capture at EndOfFrame (after everything rendered).")]
    public bool captureAtEndOfFrame = true;

    [Header("Debug")]
    public bool logDebug = false;

    [Tooltip("Logs the center pixel color after the source copy (before blur).")]
    public bool probeCenterPixel = false;

    private RawImage _raw;
    private Material _blurMat;
    private RenderTexture _rtA, _rtB;
    private Coroutine _captureRoutine;
    private Texture2D _probe1x1;

    private static readonly int BlurDirID = Shader.PropertyToID("_BlurDir");
    private static readonly int MainTexSTID = Shader.PropertyToID("_MainTex_ST");

    private void Awake()
    {
        _raw = GetComponent<RawImage>();

        if (!sourceCamera)
            sourceCamera = Camera.main;

        var shader = Shader.Find("Hidden/GF/SeparableBlur");
        if (shader)
            _blurMat = new Material(shader);
        else if (logDebug)
            Debug.LogWarning("[MenuBlurBackground] Could not find shader: Hidden/GF/SeparableBlur");
    }

    private void OnEnable()
    {
        if (captureOnEnable)
            Capture();
    }

    private void OnDisable()
    {
        if (_captureRoutine != null)
        {
            StopCoroutine(_captureRoutine);
            _captureRoutine = null;
        }

        ReleaseRT();
    }

    public void Capture()
    {
        if (!isActiveAndEnabled)
            return;

        if (_captureRoutine != null)
        {
            StopCoroutine(_captureRoutine);
            _captureRoutine = null;
        }

        if (captureAtEndOfFrame)
            _captureRoutine = StartCoroutine(CaptureEndOfFrame());
        else
            CaptureNow();
    }

    private IEnumerator CaptureEndOfFrame()
    {
        yield return new WaitForEndOfFrame();
        _captureRoutine = null;
        CaptureNow();
    }

    private void CaptureNow()
    {
        if (_blurMat == null)
        {
            if (logDebug) Debug.LogWarning("[MenuBlurBackground] Capture aborted: blur material missing.");
            return;
        }

        TryAutoWireBackdropRenderer();

        int w = Mathf.Max(1, Screen.width / Mathf.Max(1, downsample));
        int h = Mathf.Max(1, Screen.height / Mathf.Max(1, downsample));
        EnsureRT(w, h);

        // Clear RTs so we never show stale frames.
        ClearRT(_rtA);
        ClearRT(_rtB);

        if (!TryBlitSourceIntoA(w, h))
        {
            if (logDebug) Debug.LogWarning("[MenuBlurBackground] Capture aborted: no valid source found.");
            return;
        }

        if (probeCenterPixel)
        {
            var c = ReadCenterPixel(_rtA);
            Debug.Log($"[MenuBlurBackground] Probe center pixel after source copy: {c}");
        }

        // If you set iterations=0 in inspector, you'll see the unblurred copy (useful test).
        // Blur passes
        _blurMat.SetVector(MainTexSTID, new Vector4(1f, 1f, 0f, 0f)); // identity for RT-to-RT
        for (int i = 0; i < iterations; i++)
        {
            _blurMat.SetVector(BlurDirID, new Vector2(blurRadius / w, 0f));
            Graphics.Blit(_rtA, _rtB, _blurMat);

            _blurMat.SetVector(BlurDirID, new Vector2(0f, blurRadius / h));
            Graphics.Blit(_rtB, _rtA, _blurMat);
        }

        _raw.texture = _rtA;
    }

    private bool TryBlitSourceIntoA(int w, int h)
    {
        // ---- Explicit always wins when explicitly selected ----
        if (captureMode == CaptureMode.ExplicitTexture)
        {
            if (explicitTexture != null)
            {
                if (logDebug) Debug.Log($"[MenuBlurBackground] Using Explicit texture: {explicitTexture.name}");
                Graphics.Blit(explicitTexture, _rtA);
                return true;
            }
            return false;
        }

        // ---- UI backdrop (only if ACTIVE) ----
        if (captureMode == CaptureMode.BackdropUITexture || captureMode == CaptureMode.Auto)
        {
            if (backdropRawImage != null && backdropRawImage.isActiveAndEnabled && backdropRawImage.texture != null)
            {
                if (logDebug) Debug.Log($"[MenuBlurBackground] Using ACTIVE Backdrop RawImage texture: {backdropRawImage.texture.name}");
                Graphics.Blit(backdropRawImage.texture, _rtA);
                return true;
            }

            if (backdropImage != null && backdropImage.isActiveAndEnabled && backdropImage.sprite != null && backdropImage.mainTexture != null)
            {
                if (logDebug) Debug.Log($"[MenuBlurBackground] Using ACTIVE Backdrop Image sprite texture: {backdropImage.mainTexture.name}");
                Graphics.Blit(backdropImage.mainTexture, _rtA);
                return true;
            }

            if (captureMode == CaptureMode.BackdropUITexture)
                return false;
        }

        // ---- SpriteRenderer backdrop (World) ----
        if (captureMode == CaptureMode.BackdropSpriteRenderer || captureMode == CaptureMode.Auto)
        {
            if (IsSpriteRendererActive(backdropRenderer) && backdropRenderer.sprite != null && backdropRenderer.sprite.texture != null)
            {
                var sp = backdropRenderer.sprite;
                var tex = sp.texture;
                var rect = sp.textureRect;

                if (logDebug)
                {
                    Debug.Log($"[MenuBlurBackground] Using SpriteRenderer sprite: sprite='{sp.name}'; tex='{tex.name}'; rect=(x:{rect.x:F2}, y:{rect.y:F2}, w:{rect.width:F2}, h:{rect.height:F2})");
                }

                // Copy ONLY the sprite region from the texture into _rtA using _MainTex_ST.
                BlitSpriteRect(tex, rect, _rtA);
                return true;
            }

            if (captureMode == CaptureMode.BackdropSpriteRenderer)
                return false;
        }

        // ---- Camera snapshot (last resort / explicit) ----
        if (captureMode == CaptureMode.CameraSnapshot || captureMode == CaptureMode.Auto)
        {
            if (sourceCamera == null)
                return false;

            var camRT = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            var oldTarget = sourceCamera.targetTexture;

            sourceCamera.targetTexture = camRT;
            sourceCamera.Render();
            sourceCamera.targetTexture = oldTarget;

            Graphics.Blit(camRT, _rtA);
            RenderTexture.ReleaseTemporary(camRT);

            if (logDebug) Debug.Log("[MenuBlurBackground] Using Camera snapshot.");
            return true;
        }

        return false;
    }

    private void BlitSpriteRect(Texture tex, Rect textureRectPixels, RenderTexture dest)
    {
        // Compute scale/offset in UV space for this sprite’s rect within the texture.
        float invW = 1f / Mathf.Max(1f, tex.width);
        float invH = 1f / Mathf.Max(1f, tex.height);

        var scale = new Vector2(textureRectPixels.width * invW, textureRectPixels.height * invH);
        var offset = new Vector2(textureRectPixels.x * invW, textureRectPixels.y * invH);

        // Use our blur shader as a "copy" shader by setting BlurDir=0 and using _MainTex_ST.
        _blurMat.SetVector(BlurDirID, Vector2.zero);
        _blurMat.SetVector(MainTexSTID, new Vector4(scale.x, scale.y, offset.x, offset.y));

        Graphics.Blit(tex, dest, _blurMat);

        // Reset to identity so later RT-to-RT blits are normal.
        _blurMat.SetVector(MainTexSTID, new Vector4(1f, 1f, 0f, 0f));
    }

    private void TryAutoWireBackdropRenderer()
    {
        if (backdropRenderer != null) return;
        if (!autoFindBackdropWorldByName) return;

        var go = GameObject.Find(backdropWorldName);
        if (!go) return;

        var sr = go.GetComponent<SpriteRenderer>();
        if (!sr) return;

        backdropRenderer = sr;

        if (logDebug)
            Debug.Log($"[MenuBlurBackground] Auto-wired Backdrop Renderer from '{backdropWorldName}'.");
    }

    private static bool IsSpriteRendererActive(SpriteRenderer sr)
    {
        // SpriteRenderer is NOT a Behaviour, so no isActiveAndEnabled.
        return sr != null && sr.enabled && sr.gameObject.activeInHierarchy;
    }

    private void EnsureRT(int w, int h)
    {
        if (_rtA != null && (_rtA.width != w || _rtA.height != h))
            ReleaseRT();

        if (_rtA != null) return;

        _rtA = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            useMipMap = false,
            autoGenerateMips = false,
            name = "MenuBlurA"
        };

        _rtB = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            useMipMap = false,
            autoGenerateMips = false,
            name = "MenuBlurB"
        };

        _rtA.Create();
        _rtB.Create();
    }

    private static void ClearRT(RenderTexture rt)
    {
        if (!rt) return;

        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        GL.Clear(true, true, Color.clear);
        RenderTexture.active = prev;
    }

    private Color ReadCenterPixel(RenderTexture rt)
    {
        if (!rt) return Color.magenta;
        if (_probe1x1 == null) _probe1x1 = new Texture2D(1, 1, TextureFormat.RGBA32, false, false);

        var prev = RenderTexture.active;
        RenderTexture.active = rt;

        int x = rt.width / 2;
        int y = rt.height / 2;
        _probe1x1.ReadPixels(new Rect(x, y, 1, 1), 0, 0);
        _probe1x1.Apply(false, false);

        RenderTexture.active = prev;
        return _probe1x1.GetPixel(0, 0);
    }

    private void ReleaseRT()
    {
        if (_rtA) { _rtA.Release(); Destroy(_rtA); _rtA = null; }
        if (_rtB) { _rtB.Release(); Destroy(_rtB); _rtB = null; }

        if (_probe1x1) { Destroy(_probe1x1); _probe1x1 = null; }
    }
}
