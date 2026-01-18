using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class GameMessagePanel : MonoBehaviour
{
    public static GameMessagePanel Instance { get; private set; }

    [Header("Wiring")]
    [SerializeField] private CanvasGroup group;
    [SerializeField] private TMP_Text messageText;

    [Header("Behavior")]
    [SerializeField] private bool closeOnAnyPress = true;

    [Tooltip("Strong safety: when CLOSED, disable this GameObject so it can never steal UI clicks.")]
    [SerializeField] private bool deactivateGameObjectWhenClosed = true;

    [Tooltip("Extra safety: when CLOSED, force all child Graphics (TMP/Image/etc) to raycastTarget=false. " +
             "When OPEN, restore original values.")]
    [SerializeField] private bool autoToggleRaycastTargets = true;

    // Internal truth (do NOT infer “open” from alpha)
    private bool _open = false;

    private int _ignorePressUntilFrame;
    private Action _onClosed;

    // Cached graphics + their original raycastTarget values
    private Graphic[] _graphics;
    private bool[] _graphicsRaycastDefaults;

    public bool IsOpen => _open;

    private void Awake()
    {
        // Singleton (simple + safe)
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Auto-wire if someone forgot to assign
        if (!group) group = GetComponent<CanvasGroup>();
        if (!messageText) messageText = GetComponentInChildren<TMP_Text>(true);

        CacheGraphicsIfNeeded();

        // Ensure initial state is non-blocking.
        ApplyVisualState();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void OnEnable()
    {
        // If something re-enables us, immediately apply the correct state.
        ApplyVisualState();
    }

    private void OnDisable()
    {
        // Never leave raycast blocking behind.
        if (group)
        {
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            group.ignoreParentGroups = false;
        }

        if (autoToggleRaycastTargets)
            SetGraphicsRaycasts(open: false);

        _open = false;
        _onClosed = null;
    }

    /// <summary>
    /// Show a modal message. While open it blocks clicks to the game/UI underneath.
    /// </summary>
    public void Show(string message, Action onClosed = null)
    {
        _onClosed = onClosed;
        _open = true;

        if (messageText) messageText.text = message ?? string.Empty;

        // If we were inactive, this will trigger Awake/OnEnable now.
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        ApplyVisualState();

        // Eat the click that caused it to appear
        _ignorePressUntilFrame = Time.frameCount + 1;
    }

    /// <summary>
    /// Close and invoke callback once.
    /// </summary>
    public void Hide()
    {
        if (!_open)
        {
            ForceClosed_NoCallback();
            return;
        }

        // Capture callback BEFORE we potentially deactivate the GO.
        var cb = _onClosed;
        _onClosed = null;

        _open = false;
        ApplyVisualState();

        cb?.Invoke();
    }

    /// <summary>
    /// Emergency close without callback.
    /// </summary>
    public void ForceClosed_NoCallback()
    {
        _open = false;
        _onClosed = null;
        ApplyVisualState();
    }

    private void ApplyVisualState()
    {
        if (group)
        {
            if (_open)
            {
                group.alpha = 1f;
                group.interactable = true;
                group.blocksRaycasts = true;

                // Helps if this sits under a hidden parent CanvasGroup
                group.ignoreParentGroups = true;
            }
            else
            {
                group.alpha = 0f;
                group.interactable = false;
                group.blocksRaycasts = false;
                group.ignoreParentGroups = false;
            }
        }

        if (autoToggleRaycastTargets)
            SetGraphicsRaycasts(open: _open);

        // Strongest guarantee: disable GO when closed (prevents ANY raycast weirdness).
        if (deactivateGameObjectWhenClosed && !_open)
        {
            if (gameObject.activeSelf)
                gameObject.SetActive(false);
        }
    }

    private void CacheGraphicsIfNeeded()
    {
        if (!autoToggleRaycastTargets) return;

        if (_graphics == null || _graphics.Length == 0)
        {
            _graphics = GetComponentsInChildren<Graphic>(true);
            _graphicsRaycastDefaults = new bool[_graphics.Length];

            for (int i = 0; i < _graphics.Length; i++)
            {
                var g = _graphics[i];
                _graphicsRaycastDefaults[i] = (g != null) && g.raycastTarget;
            }
        }
    }

    private void SetGraphicsRaycasts(bool open)
    {
        CacheGraphicsIfNeeded();
        if (_graphics == null) return;

        for (int i = 0; i < _graphics.Length; i++)
        {
            var g = _graphics[i];
            if (!g) continue;

            // When closed: force OFF
            // When open: restore whatever it originally was in the prefab/scene
            g.raycastTarget = open ? _graphicsRaycastDefaults[i] : false;
        }
    }

    private void Update()
    {
        if (!_open) return;
        if (!closeOnAnyPress) return;
        if (Time.frameCount < _ignorePressUntilFrame) return;

        var mouse = Mouse.current;
        var kb = Keyboard.current;

        bool mouseAny =
            mouse != null &&
            (mouse.leftButton.wasPressedThisFrame ||
             mouse.rightButton.wasPressedThisFrame ||
             mouse.middleButton.wasPressedThisFrame);

        bool keyAny = (kb != null && kb.anyKey != null && kb.anyKey.wasPressedThisFrame);

        if (mouseAny || keyAny)
            Hide();
    }

#if UNITY_EDITOR
    [ContextMenu("TEST: Show")]
    private void __TestShow()
    {
        Show("TEST MESSAGE\n(Click or press any key to close)");
    }

    [ContextMenu("TEST: Force Close")]
    private void __TestForceClose()
    {
        ForceClosed_NoCallback();
    }
#endif
}
