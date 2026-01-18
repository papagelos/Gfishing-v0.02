using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using GalacticFishing.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// TopTaskbarController (TEXT-ONLY + PIN)
/// - Reveals when mouse is near top of screen (configurable, optionally uses bar height)
/// - Can be pinned (persisted via PlayerPrefs)
/// - Optionally hides while in menus (hub open or gameplay blocked)
/// - EXTRA: Can suppress hover-reveal via SetHoverRevealSuppressed(true) (for bullseye, etc.)
/// </summary>
public class TopTaskbarController : MonoBehaviour
{
    public static TopTaskbarController Instance { get; private set; }

    [Header("Wiring")]
    [SerializeField] private RectTransform barRect;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Toggle pinToggle;

    [Header("State sources")]
    [SerializeField] private FullscreenHubController hubController;

    [Header("Behavior")]
    [Tooltip("Height in pixels at top of the screen that triggers the bar to show when not pinned (fallback if not using bar height).")]
    [SerializeField] private float hoverRevealHeightPx = 28f;

    [Tooltip("If true, reveal area uses the bar's RectTransform height (recommended).")]
    [SerializeField] private bool useBarHeightForReveal = true;

    [Tooltip("Extra pixels added to the reveal area when using bar height.")]
    [SerializeField] private float extraRevealPaddingPx = 50f;

    [Tooltip("How far above the screen (positive Y) the bar sits when hidden (anchoredPosition.y).")]
    [SerializeField] private float hiddenOffsetY = 90f;

    [Tooltip("Seconds to animate show/hide.")]
    [SerializeField] private float animSeconds = 0.12f;

    [Tooltip("If false, the bar will hide during menus unless pinned.")]
    [SerializeField] private bool keepVisibleInMenus = true;

    private const string PrefKeyPinned = "GF_TaskbarPinned";

    private float _targetY;
    private float _vel;
    private bool _pinned;

    // NEW: external suppression (e.g. bullseye minigame)
    [SerializeField] private bool suppressHoverReveal = false;

    /// <summary>
    /// Call this from other systems (FishingMinigameController) to disable hover reveal temporarily.
    /// If suppressed and NOT pinned, the bar will stay hidden and won't pop down when mouse hits top.
    /// </summary>
    public static void SetHoverRevealSuppressed(bool suppressed)
    {
        if (Instance == null) return;

        Instance.suppressHoverReveal = suppressed;

        // If we are suppressing and not pinned, hide immediately so it doesn't interfere.
        if (suppressed && !Instance._pinned)
            Instance.SetShownImmediate(false);
    }

    private void Reset()
    {
        barRect = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
    }

    private void Awake()
    {
        // Singleton-ish (so static setter works)
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (!barRect) barRect = GetComponent<RectTransform>();
        if (!canvasGroup) canvasGroup = GetComponent<CanvasGroup>();

        if (!hubController) hubController = FindFirstObjectByType<FullscreenHubController>();

        _pinned = PlayerPrefs.GetInt(PrefKeyPinned, 0) == 1;

        if (pinToggle)
        {
            pinToggle.isOn = _pinned;
            pinToggle.onValueChanged.RemoveListener(OnPinChanged);
            pinToggle.onValueChanged.AddListener(OnPinChanged);
        }

        // Start hidden unless pinned.
        SetShownImmediate(_pinned);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;

        if (pinToggle)
            pinToggle.onValueChanged.RemoveListener(OnPinChanged);
    }

    private void Update()
    {
        bool hubOpen = hubController != null && hubController.IsOpen;
        bool blocked = UIBlocksGameplay.GameplayBlocked; // "in some menu" signal
        bool inAnyMenu = hubOpen || blocked;

        // If you don't want taskbar during menus, force hide unless pinned is true.
        if (!keepVisibleInMenus && inAnyMenu && !_pinned)
        {
            SetShown(false);
        }
        else
        {
            // NEW: suppress hover reveal (bullseye)
            // Still respects "pinned" (if pinned, it stays shown).
            if (suppressHoverReveal && !_pinned)
            {
                SetShown(false);
            }
            else
            {
                Vector2 mousePos = GetMouseScreenPositionOrDefault();

                float revealHeight = GetRevealHeightPx();
                bool mouseInTopStrip = mousePos.y >= (Screen.height - revealHeight);
                bool mouseOverBar = IsPointerOver(barRect, mousePos);

                bool wantShown = _pinned || mouseInTopStrip || mouseOverBar;
                SetShown(wantShown);
            }
        }

        AnimateToTarget();
    }

    private float GetRevealHeightPx()
    {
        float reveal = Mathf.Max(1f, hoverRevealHeightPx);

        if (useBarHeightForReveal && barRect != null)
        {
            float h = barRect.rect.height + extraRevealPaddingPx;
            reveal = Mathf.Max(reveal, h);
        }

        return reveal;
    }

    private void OnPinChanged(bool on)
    {
        _pinned = on;
        PlayerPrefs.SetInt(PrefKeyPinned, _pinned ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void SetShown(bool shown)
    {
        _targetY = shown ? 0f : hiddenOffsetY;

        if (canvasGroup)
        {
            float wantAlpha = shown ? 1f : 0f;
            canvasGroup.alpha = Mathf.MoveTowards(
                canvasGroup.alpha,
                wantAlpha,
                Time.unscaledDeltaTime * (1f / Mathf.Max(0.01f, animSeconds))
            );
            canvasGroup.blocksRaycasts = shown;
            canvasGroup.interactable = shown;
        }
    }

    private void SetShownImmediate(bool shown)
    {
        _targetY = shown ? 0f : hiddenOffsetY;

        if (barRect)
        {
            var p = barRect.anchoredPosition;
            p.y = _targetY;
            barRect.anchoredPosition = p;
        }

        if (canvasGroup)
        {
            canvasGroup.alpha = shown ? 1f : 0f;
            canvasGroup.blocksRaycasts = shown;
            canvasGroup.interactable = shown;
        }
    }

    private void AnimateToTarget()
    {
        if (!barRect) return;

        var p = barRect.anchoredPosition;
        p.y = Mathf.SmoothDamp(p.y, _targetY, ref _vel, animSeconds, Mathf.Infinity, Time.unscaledDeltaTime);
        barRect.anchoredPosition = p;
    }

    // --------------------------------------------------------------------
    // Input helpers (New Input System + legacy fallback)
    // --------------------------------------------------------------------

    private static Vector2 GetMouseScreenPositionOrDefault()
    {
#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        if (mouse == null) return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        return mouse.position.ReadValue();
#else
        return Input.mousePosition;
#endif
    }

    private static bool IsPointerOver(RectTransform rect, Vector2 mouseScreenPos)
    {
        if (!rect) return false;
        if (!EventSystem.current) return false;

        return RectTransformUtility.RectangleContainsScreenPoint(rect, mouseScreenPos, null);
    }
}
