using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ScreenFader : MonoBehaviour
{
    public static ScreenFader Instance { get; private set; }

    [Header("Fade Settings")]
    [SerializeField] private float fadeOutSeconds = 0.35f;
    [SerializeField] private float fadeInSeconds = 0.45f;
    [SerializeField] private Color fadeColor = Color.black;

    [Tooltip("1 = linear. Higher values keep the scene visible longer and darken more near the end (often feels better).")]
    [SerializeField] private float fadeCurvePower = 1.0f;

    [Header("Behavior")]
    [Tooltip("If true, the fader starts black and fades in once when it is first created.")]
    [SerializeField] private bool fadeInOnStartup = true;

    private Canvas _canvas;
    private CanvasGroup _group;
    private Image _image;

    private bool _isTransitioning;

    private void Awake()
    {
        // Singleton
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // IMPORTANT:
        // If you placed ScreenFader under your Canvas, it inherits scaling/rect weirdness.
        // Detach so the overlay canvas is NOT a nested canvas.
        if (transform.parent != null)
            transform.SetParent(null, false);

        transform.localScale = Vector3.one;

        DontDestroyOnLoad(gameObject);

        BuildOverlayIfNeeded();

        if (fadeInOnStartup)
        {
            _group.alpha = 1f;
            _group.blocksRaycasts = true;
            _group.interactable = true;
            StartCoroutine(FadeTo(0f, fadeInSeconds, disableRaycastsAtEnd: true));
        }
        else
        {
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void FadeToScene(string sceneName)
    {
        if (_isTransitioning) return;
        StartCoroutine(FadeToSceneRoutine(sceneName));
    }

    public static void GoToScene(string sceneName)
    {
        if (Instance == null)
        {
            SceneManager.LoadScene(sceneName);
            return;
        }

        Instance.FadeToScene(sceneName);
    }

    private IEnumerator FadeToSceneRoutine(string sceneName)
    {
        _isTransitioning = true;

        _group.blocksRaycasts = true;
        _group.interactable = true;

        // Start loading immediately, but don't activate yet
        AsyncOperation loadOp = SceneManager.LoadSceneAsync(sceneName);
        loadOp.allowSceneActivation = false;

        // Fade OUT to black
        yield return FadeTo(1f, fadeOutSeconds, disableRaycastsAtEnd: false);

        // Wait until scene is ready
        while (loadOp.progress < 0.9f)
            yield return null;

        // Activate while already black
        loadOp.allowSceneActivation = true;

        while (!loadOp.isDone)
            yield return null;

        // Fade IN from black
        yield return FadeTo(0f, fadeInSeconds, disableRaycastsAtEnd: true);

        _isTransitioning = false;
    }

    private IEnumerator FadeTo(float targetAlpha, float seconds, bool disableRaycastsAtEnd)
    {
        float start = _group.alpha;

        if (seconds <= 0f)
        {
            _group.alpha = targetAlpha;
        }
        else
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / seconds);

                if (!Mathf.Approximately(fadeCurvePower, 1f))
                    k = Mathf.Pow(k, fadeCurvePower);

                _group.alpha = Mathf.Lerp(start, targetAlpha, k);
                yield return null;
            }
            _group.alpha = targetAlpha;
        }

        if (disableRaycastsAtEnd && Mathf.Approximately(targetAlpha, 0f))
        {
            _group.blocksRaycasts = false;
            _group.interactable = false;
        }
    }

    private void BuildOverlayIfNeeded()
    {
        // Try reuse if it already exists under this object
        _canvas = GetComponentInChildren<Canvas>(true);
        _group  = GetComponentInChildren<CanvasGroup>(true);
        _image  = GetComponentInChildren<Image>(true);

        if (_canvas != null && _group != null && _image != null)
        {
            ConfigureCanvasAndImage();
            return;
        }

        // Build from scratch
        GameObject canvasGO = new GameObject("FaderCanvas", typeof(RectTransform));
        canvasGO.transform.SetParent(transform, false);

        _canvas = canvasGO.AddComponent<Canvas>();
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        // Make sure the canvas rect fills the screen even if something tries to size it
        RectTransform canvasRT = canvasGO.GetComponent<RectTransform>();
        canvasRT.anchorMin = Vector2.zero;
        canvasRT.anchorMax = Vector2.one;
        canvasRT.offsetMin = Vector2.zero;
        canvasRT.offsetMax = Vector2.zero;

        GameObject imgGO = new GameObject("FadeImage", typeof(RectTransform));
        imgGO.transform.SetParent(canvasGO.transform, false);

        _group = imgGO.AddComponent<CanvasGroup>();
        _image = imgGO.AddComponent<Image>();

        RectTransform rt = imgGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        ConfigureCanvasAndImage();
    }

    private void ConfigureCanvasAndImage()
    {
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // Ensure it renders above everything
        _canvas.overrideSorting = true;
        _canvas.sortingOrder = 32767;

        // IMPORTANT: fadeColor alpha must be 1, or you'll "see no fade" no matter what alpha you set on CanvasGroup.
        if (fadeColor.a <= 0.001f) fadeColor.a = 1f;

        _image.color = fadeColor;
    }
}
