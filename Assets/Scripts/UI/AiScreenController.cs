using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace GalacticFishing.UI
{
    public class AIScreenController : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text terminalText;

        [Tooltip("Optional: the TMP object that says 'Press any key to continue...'")]
        [SerializeField] private TMP_Text continueHintText;

        [Header("Auto-Wire Paths")]
        [Tooltip("Child path to search for CanvasGroup if not assigned")]
        [SerializeField] private string groupPath = "AI_AssistantPanel";

        [Tooltip("Child path to search for terminalText if not assigned")]
        [SerializeField] private string terminalTextPath = "AI_AssistantPanel/Background/TerminalText";

        [Tooltip("Child path to search for continueHintText if not assigned")]
        [SerializeField] private string continueHintPath = "AI_AssistantPanel/Background/ContinueHint";

        [Header("Paging")]
        [Tooltip("Put this in your story text to split pages. Example: Page1\\n||\\nPage2")]
        [SerializeField] private string pageDelimiter = "||";

        [SerializeField] private bool showPageCounter = true;

        [TextArea]
        [SerializeField] private string hintContinue = "Press any key to continue...";

        [TextArea]
        [SerializeField] private string hintClose = "Press any key to close...";

        [Header("Logging")]
        [Tooltip("If true, the full message shown here is also appended to the AI log (menu log panel).")]
        [SerializeField] private bool mirrorToLog = true;

        private readonly List<string> _pages = new();
        private int _pageIndex = 0;

        private bool _isBlockingRmb = false;

        public bool IsOpen => group != null && group.gameObject.activeInHierarchy && group.alpha > 0.01f;

        private void Awake()
        {
            AutoWireMissingReferences();
            HideImmediate();
        }

        /// <summary>
        /// Auto-wires missing references by searching for specific child paths.
        /// Supports multi-panel hierarchy (e.g., AI_AssistantPanel vs Log Panel).
        /// </summary>
        private void AutoWireMissingReferences()
        {
            // Auto-wire CanvasGroup
            if (!group && !string.IsNullOrEmpty(groupPath))
            {
                var found = transform.Find(groupPath);
                if (found)
                    group = found.GetComponent<CanvasGroup>();
            }

            // Fallback: try GetComponent on self
            if (!group)
                group = GetComponent<CanvasGroup>();

            // Auto-wire terminalText
            if (!terminalText && !string.IsNullOrEmpty(terminalTextPath))
            {
                var found = transform.Find(terminalTextPath);
                if (found)
                    terminalText = found.GetComponent<TMP_Text>();
            }

            // Auto-wire continueHintText
            if (!continueHintText && !string.IsNullOrEmpty(continueHintPath))
            {
                var found = transform.Find(continueHintPath);
                if (found)
                    continueHintText = found.GetComponent<TMP_Text>();
            }

            // Log warnings for critical missing references
            if (!group)
                Debug.LogWarning($"[AIScreenController] CanvasGroup not found on '{name}'. Assign manually or check groupPath.", this);
            if (!terminalText)
                Debug.LogWarning($"[AIScreenController] terminalText not found on '{name}'. Assign manually or check terminalTextPath.", this);
        }

        private void OnDisable()
        {
            // Safety: if disabled while open, release RMB block
            if (_isBlockingRmb)
            {
                RMBBlocker.Pop();
                _isBlockingRmb = false;
            }
        }

        private void OnDestroy()
        {
            // Safety: if destroyed while open, release RMB block
            if (_isBlockingRmb)
            {
                RMBBlocker.Pop();
                _isBlockingRmb = false;
            }
        }

        public void Show(string text)
        {
            if (!group || !terminalText) return;

            BuildPages(text);

            group.gameObject.SetActive(true);
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;

            // Block RMB while AIScreen is open
            if (!_isBlockingRmb)
            {
                RMBBlocker.Push();
                _isBlockingRmb = true;
            }

            _pageIndex = 0;
            ApplyCurrentPage();

            // Mirror the full message to the log (joined pages, delimiter removed)
            if (mirrorToLog)
            {
                var full = GetFullTextForLog();
                if (!string.IsNullOrWhiteSpace(full))
                    AIMessageLogService.Instance?.AddImportant(full);
            }
        }

        /// <summary>
        /// Advances one page if there is another page.
        /// Returns true if it advanced; false if already on last page (caller should close).
        /// </summary>
        public bool TryAdvancePage()
        {
            if (_pages.Count <= 1) return false;

            if (_pageIndex < _pages.Count - 1)
            {
                _pageIndex++;
                ApplyCurrentPage();
                return true;
            }

            return false; // last page
        }

        public void HideImmediate()
        {
            if (continueHintText) continueHintText.gameObject.SetActive(false);
            if (terminalText) terminalText.text = "";

            if (!group) return;

            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            group.gameObject.SetActive(false);

            // Release RMB block when closed
            if (_isBlockingRmb)
            {
                RMBBlocker.Pop();
                _isBlockingRmb = false;
            }

            _pages.Clear();
            _pageIndex = 0;
        }

        private void BuildPages(string raw)
        {
            _pages.Clear();

            if (string.IsNullOrEmpty(raw))
            {
                _pages.Add(string.Empty);
                return;
            }

            string delim = string.IsNullOrEmpty(pageDelimiter) ? "||" : pageDelimiter;

            // Split on the delimiter and trim each page.
            var parts = raw.Split(new[] { delim }, StringSplitOptions.None);
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i]?.Trim();
                if (string.IsNullOrEmpty(p)) continue;
                _pages.Add(p);
            }

            if (_pages.Count == 0)
                _pages.Add(raw.Trim());
        }

        private void ApplyCurrentPage()
        {
            if (!terminalText) return;

            terminalText.text = (_pageIndex >= 0 && _pageIndex < _pages.Count) ? _pages[_pageIndex] : "";
            UpdateHint();
        }

        private void UpdateHint()
        {
            if (!continueHintText) return;

            continueHintText.gameObject.SetActive(true);

            bool hasMorePages = (_pages.Count > 1) && (_pageIndex < _pages.Count - 1);
            string baseHint = hasMorePages ? hintContinue : hintClose;

            if (showPageCounter && _pages.Count > 1)
                baseHint = $"{baseHint} ({_pageIndex + 1}/{_pages.Count})";

            continueHintText.text = baseHint;
        }

        private string GetFullTextForLog()
        {
            if (_pages.Count == 0) return string.Empty;
            if (_pages.Count == 1) return _pages[0];

            // Join pages cleanly without any "Page X/Y" noise
            var sb = new StringBuilder();
            for (int i = 0; i < _pages.Count; i++)
            {
                if (i > 0) sb.Append("\n\n");
                sb.Append(_pages[i]);
            }
            return sb.ToString();
        }
    }
}
