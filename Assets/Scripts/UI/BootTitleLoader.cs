using UnityEngine;
using UnityEngine.SceneManagement;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

public class BootTitleLoader : MonoBehaviour
{
    [Header("Next scene to load (must be in Build Profiles scene list)")]
    [SerializeField] private string nextSceneName = "Intro";

    [Header("Timing")]
    [SerializeField] private float delaySeconds = 1.0f;

    [Header("Skip")]
    [SerializeField] private bool allowSkipWithAnyKey = true;

    private float _t;
    private bool _loading;

    private void OnEnable()
    {
        Time.timeScale = 1f;
        _t = 0f;
        _loading = false;
    }

    private void Update()
    {
        if (_loading) return;

        if (allowSkipWithAnyKey && AnyInputPressedThisFrame())
        {
            LoadNext();
            return;
        }

        _t += Time.unscaledDeltaTime;
        if (_t >= delaySeconds)
            LoadNext();
    }

    private bool AnyInputPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        // Keyboard
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
            return true;

        // Mouse buttons
        if (Mouse.current != null)
        {
            if (Mouse.current.leftButton.wasPressedThisFrame) return true;
            if (Mouse.current.rightButton.wasPressedThisFrame) return true;
            if (Mouse.current.middleButton.wasPressedThisFrame) return true;
        }

        // Touch
        if (Touchscreen.current != null &&
            Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            return true;

        // Gamepad "any button"
        if (Gamepad.current != null)
        {
            foreach (var c in Gamepad.current.allControls)
            {
                if (c is ButtonControl b && b.wasPressedThisFrame)
                    return true;
            }
        }

        return false;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        // Legacy fallback (only if legacy is enabled in Player Settings)
        return Input.anyKeyDown || Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1);
#else
        return false;
#endif
    }

    private void LoadNext()
    {
        _loading = true;

        // Optional: protect against typos
        if (string.IsNullOrWhiteSpace(nextSceneName))
        {
            Debug.LogError("[BootTitleLoader] Next Scene Name is empty.");
            return;
        }

        SceneManager.LoadScene(nextSceneName);
    }
}
