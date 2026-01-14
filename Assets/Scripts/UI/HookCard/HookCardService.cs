using UnityEngine;
using System;
using System.Collections;
using Object = UnityEngine.Object;

/// <summary>
/// Central service for the Hook Card overlay.
/// - Auto-finds HookCardUI even if inactive or in another canvas.
/// - Keeps existing API working: ShowInProgress/ShowCaught/ShowEscaped.
/// - Adds global hold times so ✓/✕ stay visible consistently.
/// </summary>
[DefaultExecutionOrder(-100)]
public class HookCardService : MonoBehaviour
{
    [SerializeField] private HookCardUI ui;  // optional direct reference

    private static HookCardService _i;
    private static bool _warned;

    // ------- configurable holds (edit these or set at runtime) -------
    public static float CaughtHoldSeconds  = 2f;   // ✓ visible time
    public static float EscapedHoldSeconds = 2f;   // ✕ visible time
    // -----------------------------------------------------------------

    // Events so other systems (e.g., future UI or analytics) can react to overlay changes.
    public static event Action<HookState> OverlayShown;
    public static event Action OverlayHidden;

    private static HookCardService I
    {
        get
        {
            if (_i != null) return _i;
            _i = Object.FindObjectOfType<HookCardService>(true);
            if (_i != null) return _i;
            var go = new GameObject("HookCardService (auto)");
            Object.DontDestroyOnLoad(go);
            _i = go.AddComponent<HookCardService>();
            return _i;
        }
    }

    private void Awake()
    {
        if (_i == null) _i = this;
        else if (_i != this) { Destroy(gameObject); return; }
        TryBindUI();
    }

    private bool TryBindUI()
    {
        if (ui) return true;

        ui = Object.FindObjectOfType<HookCardUI>(true);
        if (ui) return true;

        var all = Resources.FindObjectsOfTypeAll<HookCardUI>();
        if (all != null && all.Length > 0) { ui = all[0]; return true; }

        if (!_warned)
        {
            _warned = true;
            Debug.LogWarning("[HookCardService] No HookCardUI found in scene.");
        }
        return false;
    }

    // ---------------------- Public API ----------------------

    // Pill only (no auto-hide)
    public static void ShowInProgress(string fishName)
    {
        var s = I; if (!s.TryBindUI()) return;
        s.StopAllCoroutines();
        FishWorldVisibility.HideAll();
        s.ui.Show(fishName, HookState.InProgress);
        OverlayShown?.Invoke(HookState.InProgress);
    }

    // Compatibility: force through flash logic with global hold
    public static void ShowCaught(string fishName)  => FlashCaught(fishName, -1f);
    public static void ShowEscaped(string fishName) => FlashEscaped(fishName, -1f);

    // Flash helpers; pass <= 0 to use global defaults
    public static void FlashCaught(string fishName, float seconds = -1f)
    {
        var s = I; if (!s.TryBindUI()) return;
        s.StopAllCoroutines();
        s.ui.Show(fishName, HookState.Caught);
        OverlayShown?.Invoke(HookState.Caught);
        float hold = seconds > 0f ? seconds : CaughtHoldSeconds;
        s.StartCoroutine(s.HideAfter(hold));
    }

    public static void FlashEscaped(string fishName, float seconds = -1f)
    {
        var s = I; if (!s.TryBindUI()) return;
        s.StopAllCoroutines();
        s.ui.Show(fishName, HookState.Escaped);
        OverlayShown?.Invoke(HookState.Escaped);
        float hold = seconds > 0f ? seconds : EscapedHoldSeconds;
        s.StartCoroutine(s.HideAfter(hold));
    }

    public static void Hide()
    {
        var s = I; if (!s.TryBindUI()) return;
        s.StopAllCoroutines();
        s.ui.Hide();
        FishWorldVisibility.ShowAll();
        OverlayHidden?.Invoke();
    }

    private IEnumerator HideAfter(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        if (ui) ui.Hide();
        FishWorldVisibility.ShowAll();
        OverlayHidden?.Invoke();
    }
}
