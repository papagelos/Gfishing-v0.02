using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;   // New Input System
#endif

namespace GalacticFishing.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    [AddComponentMenu("Galactic Fishing/UI/Fullscreen Hub Controller")]
    public sealed class FullscreenHubController : MonoBehaviour
    {
        [Header("Toggle (New Input System)")]
        public bool useRightMouse = true;
        public bool requireHold = false;
        [Range(0f, 1f)] public float holdSeconds = 0.2f;

        [Header("Behavior")]
        public bool pauseTime = true;
        public GameObject[] hideWhenOpen;

        [Header("Animation")]
        [Range(0f, 1f)] public float fadeSeconds = 0.18f;

        [Header("Options")]
        public bool openOnStart = false;
        public bool deactivateOnClose = false;
        public bool logInput = false;

        [Header("Cursor")]
        public bool showCursorWhenOpen = true;
        public bool showCursorWhenClosed = true;   // keep cursor visible in gameplay
        public bool lockCursorWhenOpen = false;
        public bool lockCursorWhenClosed = false;

        [Header("Routing (optional)")]
        [Tooltip("If set: RMB while inside another menu will act like MenuRouter.Back() instead of opening the Hub.")]
        [SerializeField] private MenuRouter menuRouter;

        CanvasGroup _group;
        bool _open, _animating;
        float _holdT;

        void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            SetOpen(openOnStart, true);
        }

        void Update()
        {
            // ------------------------------------------------------------
            // HARD BLOCK: If RMB is globally blocked (AIScreen, HookCard, etc),
            // RMB should do NOTHING (no Hub toggle, no Back).
            // ------------------------------------------------------------
            if (useRightMouse && RMBBlocker.IsBlocked)
            {
                _holdT = 0f; // prevent hold accumulating while blocked
                return;
            }

            bool togglePressed = false;
            bool toggleHeld = false;

#if ENABLE_INPUT_SYSTEM
            if (useRightMouse && Mouse.current != null)
            {
                togglePressed = Mouse.current.rightButton.wasPressedThisFrame;
                toggleHeld = Mouse.current.rightButton.isPressed;
            }
#else
            // Legacy Input
            togglePressed = Input.GetMouseButtonDown(1);
            toggleHeld = Input.GetMouseButton(1);
#endif

            // ------------------------------------------------------------
            // If we're inside any other menu (GameplayBlocked) and the Hub is CLOSED,
            // RMB should behave like BACK (one step back), not open the Hub.
            // ------------------------------------------------------------
            if (!_open && UIBlocksGameplay.GameplayBlocked)
            {
                if (togglePressed)
                {
                    if (logInput) Debug.Log("[Hub] RMB while in menu -> MenuRouter.Back()");
                    if (menuRouter != null) menuRouter.Back();
                    else Debug.LogWarning("[FullscreenHubController] RMB->Back requested but menuRouter is not assigned.");
                }

                _holdT = 0f;
                return;
            }

            // Normal Hub toggle behavior (world/root view)
            if (requireHold)
            {
                if (!_open && toggleHeld)
                {
                    _holdT += Time.unscaledDeltaTime;
                    if (_holdT >= holdSeconds)
                        Toggle();
                }

                if (!toggleHeld)
                    _holdT = 0f;
            }
            else
            {
                if (togglePressed)
                {
                    if (logInput) Debug.Log("[Hub] Toggle via RMB");
                    Toggle();
                }
            }
        }

        public void Toggle()
        {
            if (_animating) return;
            SetOpen(!_open, false);
        }

        /// <summary>Force open immediately (no fade). Used by MenuRouter for Back().</summary>
        public void ForceOpenImmediate() => SetOpen(true, true);

        /// <summary>Force closed immediately (no fade). Used by MenuRouter for ESC / CloseAll.</summary>
        public void ForceClosedImmediate() => SetOpen(false, true);

        /// <summary>Current hub open state.</summary>
        public bool IsOpen => _open;

        // --------------------------------------------------------------------

        void SetOpen(bool open, bool instant)
        {
            _open = open;

            if (hideWhenOpen != null)
            {
                foreach (var go in hideWhenOpen)
                    if (go) go.SetActive(!open);
            }

            StopAllCoroutines();
            if (instant) ApplyImmediate(open);
            else StartCoroutine(Fade(open));
        }

        void ApplyImmediate(bool open)
        {
            if (!_group) _group = GetComponent<CanvasGroup>();

            _group.alpha = open ? 1f : 0f;
            _group.blocksRaycasts = open;
            _group.interactable = open;

            ApplyCursor(open);
            if (pauseTime)
                Time.timeScale = open ? 0f : 1f;

            if (!open && deactivateOnClose)
                gameObject.SetActive(false);
        }

        IEnumerator Fade(bool open)
        {
            _animating = true;
            if (!_group) _group = GetComponent<CanvasGroup>();

            if (open && !gameObject.activeSelf)
                gameObject.SetActive(true);

            float start = _group.alpha;
            float target = open ? 1f : 0f;
            float t = 0f;

            while (t < fadeSeconds)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, fadeSeconds));
                _group.alpha = Mathf.Lerp(start, target, k);
                yield return null;
            }

            _group.alpha = target;
            _group.blocksRaycasts = open;
            _group.interactable = open;

            ApplyCursor(open);
            if (pauseTime)
                Time.timeScale = open ? 0f : 1f;

            if (!open && deactivateOnClose)
                gameObject.SetActive(false);

            _animating = false;
        }

        void ApplyCursor(bool open)
        {
            bool show = open ? showCursorWhenOpen : showCursorWhenClosed;
            bool lockIt = open ? lockCursorWhenOpen : lockCursorWhenClosed;

            Cursor.visible = show;
            Cursor.lockState = lockIt ? CursorLockMode.Locked : CursorLockMode.None;
        }

        // ------- Editor helpers -------
        [ContextMenu("Editor: Force Open (Immediate)")]
        void EditorForceOpen() => SetOpen(true, true);

        [ContextMenu("Editor: Force Closed (Immediate)")]
        void EditorForceClosed() => SetOpen(false, true);
    }
}
