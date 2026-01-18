using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GalacticFishing.UI
{
    public sealed class AIScreenPopup : MonoBehaviour
    {
        public static AIScreenPopup Instance { get; private set; }

        [Header("Assign in Inspector")]
        [SerializeField] private CanvasGroup group;          // CanvasGroup on the AIScreen root/panel
        [SerializeField] private TMP_Text terminalText;      // main terminal TMP
        [SerializeField] private TMP_Text dismissHintText;   // optional: "Press any key..."

        [Header("Behavior")]
        [Tooltip("If 0 or less, messages require 'press any key' to advance/close.")]
        [SerializeField] private float defaultAutoHideSeconds = 0f;

        [SerializeField] private bool startHidden = true;

        [Header("Dismiss Hint Text")]
        [SerializeField] private string hintWhenSingle = "Press any key to close";
        [SerializeField] private string hintWhenMoreQueued = "Press any key to continue ({0} more)";

        // runtime
        private readonly Queue<string> _queue = new Queue<string>();
        private Coroutine _hideRoutine;
        private int _ignoreDismissUntilFrame = 0;
        private bool _isOpen = false;
        private bool _waitingForDismiss = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (startHidden)
                HideImmediate();
        }

        private void Update()
        {
            if (!_isOpen) return;
            if (!_waitingForDismiss) return;
            if (Time.frameCount < _ignoreDismissUntilFrame) return;

            if (AnyDismissPressedThisFrame())
            {
                AdvanceOrClose();
            }
        }

        /// <summary>
        /// Enqueue a message. If the screen is closed, it opens immediately and waits for dismiss.
        /// </summary>
        public void Enqueue(string message)
        {
            if (string.IsNullOrEmpty(message)) return;

            if (!_isOpen)
            {
                OpenWithMessage(message, autoHideSeconds: defaultAutoHideSeconds);
                return;
            }

            _queue.Enqueue(message);
            UpdateDismissHint();
        }

        /// <summary>
        /// Show immediately (optionally timed). If timedSeconds <= 0, waits for dismiss.
        /// </summary>
        public void Show(string message, float timedSeconds = -1f)
        {
            if (string.IsNullOrEmpty(message)) return;

            float seconds = timedSeconds;
            if (seconds < 0f) seconds = defaultAutoHideSeconds;

            OpenWithMessage(message, seconds);
        }

        public void HideImmediate()
        {
            StopAutoHide();

            _queue.Clear();
            _isOpen = false;
            _waitingForDismiss = false;

            if (dismissHintText)
            {
                dismissHintText.text = "";
                dismissHintText.gameObject.SetActive(false);
            }

            if (!group) return;

            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        // ---------------- internal ----------------

        private void OpenWithMessage(string message, float autoHideSeconds)
        {
            if (!group || !terminalText)
            {
                Debug.LogWarning("[AIScreenPopup] Missing group or terminalText reference.");
                return;
            }

            StopAutoHide();

            // ensure active
            if (!group.gameObject.activeSelf)
                group.gameObject.SetActive(true);

            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;

            terminalText.text = message;

            _isOpen = true;

            // Prevent the SAME click/key that triggered the popup from instantly dismissing it
            _ignoreDismissUntilFrame = Time.frameCount + 1;

            if (autoHideSeconds > 0f)
            {
                _waitingForDismiss = false;
                if (dismissHintText) dismissHintText.gameObject.SetActive(false);
                _hideRoutine = StartCoroutine(HideAfter(autoHideSeconds));
            }
            else
            {
                _waitingForDismiss = true;
                UpdateDismissHint();
            }
        }

        private void AdvanceOrClose()
        {
            if (_queue.Count > 0)
            {
                string next = _queue.Dequeue();
                // show next, still manual dismiss
                OpenWithMessage(next, autoHideSeconds: 0f);
                return;
            }

            HideImmediate();
        }

        private void UpdateDismissHint()
        {
            if (!dismissHintText) return;

            int more = _queue.Count;

            dismissHintText.gameObject.SetActive(true);
            dismissHintText.text = (more <= 0)
                ? hintWhenSingle
                : string.Format(hintWhenMoreQueued, more);
        }

        private void StopAutoHide()
        {
            if (_hideRoutine != null)
            {
                StopCoroutine(_hideRoutine);
                _hideRoutine = null;
            }
        }

        private IEnumerator HideAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            HideImmediate();
        }

        private static bool AnyDismissPressedThisFrame()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.anyKey != null && kb.anyKey.wasPressedThisFrame)
                return true;

            var mouse = Mouse.current;
            if (mouse != null)
            {
                if (mouse.leftButton.wasPressedThisFrame) return true;
                if (mouse.rightButton.wasPressedThisFrame) return true;
                if (mouse.middleButton.wasPressedThisFrame) return true;
                if (mouse.backButton != null && mouse.backButton.wasPressedThisFrame) return true;
                if (mouse.forwardButton != null && mouse.forwardButton.wasPressedThisFrame) return true;
            }

            return false;
        }
    }
}
