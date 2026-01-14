using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class IntroSlideshowController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image slideImage;
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private GameObject dialogueBG;
    [SerializeField] private TMP_Text skipHintText;

    [Header("Navigation Buttons (drag your UI Buttons here)")]
    [SerializeField] private Button prevButton;
    [SerializeField] private Button nextButton;

    [Header("Legacy Transition Fade (optional fallback)")]
    [Tooltip("Only used if ScreenFader is NOT present. CanvasGroup on a full-screen Image (black) that sits above everything.")]
    [SerializeField] private CanvasGroup fadeGroup;
    [SerializeField] private float fadeOutSeconds = 0.35f;

    [Header("Slides")]
    [SerializeField] private List<Sprite> slides = new();
    [TextArea(2, 8)]
    [SerializeField] private List<string> dialogueLines = new();

    [Header("Dialogue Paging")]
    [Tooltip("Use this token inside a dialogue line to split it into multiple 'pages'. Example: page1 <page> page2")]
    [SerializeField] private string pageToken = "<page>";

    [Tooltip("Also treat blank lines (double Enter) as page splits if no <page> token is present.")]
    [SerializeField] private bool allowBlankLinePagingFallback = true;

    [Tooltip("If true, slide 0 starts with text already shown (page 1).")]
    [SerializeField] private bool startFirstSlideWithText = true;

    [Header("Scene flow")]
    [SerializeField] private string nextSceneName = "SampleScene";

    private int _index;

    // 0 = dialogue hidden (clean image)
    // 1..N = show dialogue page (pageIndex-1)
    private int _pageIndex;

    private bool _isTransitioning;

    private struct SlideState
    {
        public int index;
        public int pageIndex;
        public SlideState(int i, int p) { index = i; pageIndex = p; }
    }

    private readonly Stack<SlideState> _history = new();
    private readonly List<RaycastResult> _uiRaycastResults = new();

    private void OnEnable()
    {
        Time.timeScale = 1f;

        if (prevButton) prevButton.onClick.AddListener(OnPrevPressed);
        if (nextButton) nextButton.onClick.AddListener(OnNextPressed);

        _history.Clear();
        _index = 0;
        _isTransitioning = false;

        // Start state
        if (startFirstSlideWithText && GetDialoguePages(_index).Count > 0)
            _pageIndex = 1;   // show first page immediately
        else
            _pageIndex = 0;   // clean image first

        // Legacy fallback fadeGroup init (only used if no ScreenFader)
        if (fadeGroup)
        {
            fadeGroup.alpha = 0f;
            fadeGroup.blocksRaycasts = false;
            fadeGroup.interactable = false;
        }

        ApplyState(_index, _pageIndex);
        UpdateNavButtons();
    }

    private void OnDisable()
    {
        if (prevButton) prevButton.onClick.RemoveListener(OnPrevPressed);
        if (nextButton) nextButton.onClick.RemoveListener(OnNextPressed);
    }

    private void Update()
    {
        if (_isTransitioning) return;

        // ESC to skip
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            LoadNextScene();
            return;
        }

        // LEFT / RIGHT arrows
        if (Keyboard.current != null && Keyboard.current.leftArrowKey.wasPressedThisFrame)
        {
            OnPrevPressed();
            return;
        }

        if (Keyboard.current != null && Keyboard.current.rightArrowKey.wasPressedThisFrame)
        {
            OnNextPressed();
            return;
        }

        bool anyKeyboard = Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame;

        bool mouseClick =
            (Mouse.current != null) &&
            (Mouse.current.leftButton.wasPressedThisFrame ||
             Mouse.current.rightButton.wasPressedThisFrame ||
             Mouse.current.middleButton.wasPressedThisFrame);

        if (anyKeyboard)
        {
            AdvanceWithHistory();
        }
        else if (mouseClick && !IsPointerOverUI())
        {
            AdvanceWithHistory();
        }
    }

    // ===== Button hooks =====
    public void OnPrevPressed()
    {
        if (_isTransitioning) return;
        if (_history.Count == 0) return;

        SlideState prev = _history.Pop();
        _index = prev.index;
        _pageIndex = prev.pageIndex;

        ApplyState(_index, _pageIndex);
        UpdateNavButtons();
    }

    public void OnNextPressed()
    {
        if (_isTransitioning) return;
        AdvanceWithHistory();
    }

    // ===== Core flow =====
    private void AdvanceWithHistory()
    {
        _history.Push(new SlideState(_index, _pageIndex));
        AdvanceCore();
        UpdateNavButtons();
    }

    private void AdvanceCore()
    {
        List<string> pages = GetDialoguePages(_index);

        if (pages.Count > 0)
        {
            // Clean image -> show page 1
            if (_pageIndex == 0)
            {
                _pageIndex = 1;
                ApplyState(_index, _pageIndex);
                return;
            }

            // Page 1 -> Page 2 -> ...
            if (_pageIndex < pages.Count)
            {
                _pageIndex++;
                ApplyState(_index, _pageIndex);
                return;
            }

            // Last page already shown -> next slide
        }

        NextSlide();
    }

    private void NextSlide()
    {
        if (slides == null || slides.Count == 0)
        {
            LoadNextScene();
            return;
        }

        int next = _index + 1;

        if (next >= slides.Count)
        {
            LoadNextScene();
            return;
        }

        _index = next;
        _pageIndex = 0; // new slide starts clean (image only)
        ApplyState(_index, _pageIndex);
    }

    private void ApplyState(int i, int pageIndex)
    {
        if (slideImage)
        {
            slideImage.sprite = (slides != null && i >= 0 && i < slides.Count) ? slides[i] : null;
            slideImage.enabled = (slideImage.sprite != null);
        }

        List<string> pages = GetDialoguePages(i);

        bool shouldShow = (pageIndex > 0) && (pageIndex <= pages.Count);
        string textToShow = shouldShow ? pages[pageIndex - 1] : string.Empty;

        if (dialogueBG) dialogueBG.SetActive(shouldShow);

        if (dialogueText)
        {
            dialogueText.gameObject.SetActive(shouldShow);
            dialogueText.text = textToShow;
        }

        if (skipHintText)
            skipHintText.gameObject.SetActive(true);
    }

    private void UpdateNavButtons()
    {
        if (prevButton) prevButton.interactable = _history.Count > 0;
        if (nextButton) nextButton.interactable = true;
    }

    private string GetDialogueLine(int i)
    {
        if (dialogueLines != null && i >= 0 && i < dialogueLines.Count)
            return dialogueLines[i];

        return "";
    }

    private List<string> GetDialoguePages(int i)
    {
        string raw = GetDialogueLine(i);
        if (string.IsNullOrWhiteSpace(raw))
            return new List<string>(0);

        string s = raw.Replace("\r\n", "\n").Replace("\r", "\n");

        // Prefer explicit <page> token (case-insensitive)
        if (!string.IsNullOrWhiteSpace(pageToken) &&
            s.IndexOf(pageToken, StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return SplitByTokenCaseInsensitive(s, pageToken);
        }

        // Fallback: split by blank lines (double newline)
        if (allowBlankLinePagingFallback && s.Contains("\n\n"))
        {
            string[] parts = s.Split(new[] { "\n\n" }, StringSplitOptions.None);
            var pages = new List<string>(parts.Length);
            foreach (var p in parts)
            {
                var t = p.Trim();
                if (!string.IsNullOrWhiteSpace(t))
                    pages.Add(t);
            }
            return pages;
        }

        return new List<string>(1) { s.Trim() };
    }

    private static List<string> SplitByTokenCaseInsensitive(string s, string token)
    {
        var pages = new List<string>();
        int start = 0;

        while (true)
        {
            int idx = s.IndexOf(token, start, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) break;

            string part = s.Substring(start, idx - start).Trim();
            if (!string.IsNullOrWhiteSpace(part))
                pages.Add(part);

            start = idx + token.Length;
        }

        string last = s.Substring(start).Trim();
        if (!string.IsNullOrWhiteSpace(last))
            pages.Add(last);

        return pages;
    }

    private void LoadNextScene()
    {
        if (_isTransitioning) return;
        _isTransitioning = true;

        // ✅ Preferred: persistent fader (fade out + load + fade in)
        if (ScreenFader.Instance != null)
        {
            ScreenFader.GoToScene(nextSceneName);
            return;
        }

        // Fallback: old per-scene fadeGroup
        if (!fadeGroup)
        {
            SceneManager.LoadScene(nextSceneName);
            return;
        }

        fadeGroup.blocksRaycasts = true;
        fadeGroup.interactable = true;
        StartCoroutine(FadeOutAndLoad());
    }

    private IEnumerator FadeOutAndLoad()
    {
        float start = fadeGroup.alpha;
        float t = 0f;

        while (t < fadeOutSeconds)
        {
            t += Time.unscaledDeltaTime;
            fadeGroup.alpha = Mathf.Lerp(start, 1f, t / fadeOutSeconds);
            yield return null;
        }

        fadeGroup.alpha = 1f;
        SceneManager.LoadScene(nextSceneName);
    }

    private bool IsPointerOverUI()
    {
        if (EventSystem.current == null || Mouse.current == null)
            return false;

        var pointerData = new PointerEventData(EventSystem.current)
        {
            position = Mouse.current.position.ReadValue()
        };

        _uiRaycastResults.Clear();
        EventSystem.current.RaycastAll(pointerData, _uiRaycastResults);
        return _uiRaycastResults.Count > 0;
    }
}
