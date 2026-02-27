using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GalacticFishing.UI
{
    [DisallowMultipleComponent]
    public sealed class GlobalMenuController : MonoBehaviour
    {
        [SerializeField] private GameObject popupRoot;

        private const string MenuRootName = "MenuButton_Root";
        private const string MainButtonName = "Btn_Menu";
        private const string PopupName = "MenuPopup";
        private const float MainButtonSize = 100f;
        private const float PopupButtonWidth = 200f;
        private const float PopupButtonHeight = 42f;
        private const float PopupGap = 8f;

        private RectTransform _menuRoot;
        private Button _menuButton;
        private Canvas _hostCanvas;
        private bool _built;

        private static readonly (string Label, System.Action<GlobalMenuController> Action)[] Entries =
        {
            ("HUB", c => c.OpenHub()),
            ("Character", c => c.LogPlaceholder("Character")),
            ("Skill Tree", c => c.LogPlaceholder("Skill Tree")),
            ("Gems", c => c.LogPlaceholder("Gems")),
            ("Store", c => c.LogPlaceholder("Store")),
            ("Options", c => c.LogPlaceholder("Options")),
        };

        private void Awake()
        {
            EnsureBuilt();
            SetPopupVisible(false);
        }

        private void OnEnable()
        {
            EnsureBuilt();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Keep menu collapsed when entering a new scene.
            SetPopupVisible(false);
        }

        public void ToggleMenu()
        {
            EnsureBuilt();
            if (popupRoot == null)
                return;

            SetPopupVisible(!popupRoot.activeSelf);
        }

        public void OpenHub()
        {
            SetPopupVisible(false);

            // Prefer routing through MenuRouter if present.
            var router = Object.FindAnyObjectByType<MenuRouter>(FindObjectsInactive.Include);
            if (TryOpenHubViaMenuRouter(router))
                return;

            // Fallback directly to the hub controller.
            var hub = Object.FindAnyObjectByType<FullscreenHubController>(FindObjectsInactive.Include);
            if (hub != null)
            {
                hub.ForceOpenImmediate();
                return;
            }

            Debug.LogWarning("[GlobalMenuController] HUB requested but no MenuRouter/FullscreenHubController was found.");
        }

        private bool TryOpenHubViaMenuRouter(MenuRouter router)
        {
            if (router == null)
                return false;

            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            // If a future public OpenHub method is added, use it automatically.
            MethodInfo openHub = router.GetType().GetMethod("OpenHub", Flags, null, System.Type.EmptyTypes, null);
            if (openHub != null)
            {
                openHub.Invoke(router, null);
                return true;
            }

            // Current project uses a private serialized hubController field.
            FieldInfo hubField = router.GetType().GetField("hubController", Flags);
            if (hubField?.GetValue(router) is FullscreenHubController hub)
            {
                hub.ForceOpenImmediate();
                return true;
            }

            return false;
        }

        private void LogPlaceholder(string label)
        {
            SetPopupVisible(false);
            Debug.Log($"[GlobalMenuController] Placeholder button clicked: {label}");
        }

        private void SetPopupVisible(bool visible)
        {
            if (popupRoot != null)
                popupRoot.SetActive(visible);
        }

        private void EnsureBuilt()
        {
            if (_built && _menuRoot != null && _menuButton != null && popupRoot != null)
                return;

            _hostCanvas = ResolveHostCanvas();
            if (_hostCanvas == null)
            {
                Debug.LogWarning("[GlobalMenuController] No UI Canvas found under GlobalSystems; menu not created.");
                return;
            }

            _menuRoot = FindOrCreateMenuRoot(_hostCanvas.transform as RectTransform);
            if (_menuRoot == null)
                return;

            _menuButton = EnsureMainButton(_menuRoot);
            popupRoot = EnsurePopup(_menuRoot);

            if (_menuButton != null)
            {
                _menuButton.onClick.RemoveAllListeners();
                _menuButton.onClick.AddListener(ToggleMenu);
            }

            _built = (_menuButton != null && popupRoot != null);
        }

        private Canvas ResolveHostCanvas()
        {
            var canvases = GetComponentsInChildren<Canvas>(true);
            for (int i = 0; i < canvases.Length; i++)
            {
                if (canvases[i] == null)
                    continue;

                if (canvases[i].renderMode != RenderMode.WorldSpace)
                    return canvases[i];
            }

            return canvases.Length > 0 ? canvases[0] : null;
        }

        private static RectTransform FindOrCreateMenuRoot(RectTransform canvasRect)
        {
            if (canvasRect == null)
                return null;

            Transform existing = canvasRect.Find(MenuRootName);
            if (existing != null)
                return existing as RectTransform;

            GameObject go = new GameObject(MenuRootName, typeof(RectTransform));
            go.layer = canvasRect.gameObject.layer;
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(canvasRect, false);
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(16f, 16f);
            rt.sizeDelta = new Vector2(MainButtonSize, MainButtonSize);
            return rt;
        }

        private Button EnsureMainButton(RectTransform menuRoot)
        {
            if (menuRoot == null)
                return null;

            Transform existing = menuRoot.Find(MainButtonName);
            GameObject go;
            if (existing != null)
            {
                go = existing.gameObject;
            }
            else
            {
                go = new GameObject(MainButtonName, typeof(RectTransform), typeof(Image), typeof(Button));
                go.layer = menuRoot.gameObject.layer;
                go.transform.SetParent(menuRoot, false);
            }

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(MainButtonSize, MainButtonSize);

            var image = go.GetComponent<Image>();
            image.color = new Color(0.12f, 0.14f, 0.18f, 0.95f);
            image.raycastTarget = true;

            var button = go.GetComponent<Button>();
            ConfigureButtonColors(button, new Color(0.23f, 0.58f, 0.86f, 1f));

            EnsureButtonText(go.transform, "MENU", 22f);
            return button;
        }

        private GameObject EnsurePopup(RectTransform menuRoot)
        {
            if (menuRoot == null)
                return null;

            Transform existing = menuRoot.Find(PopupName);
            GameObject go;
            if (existing != null)
            {
                go = existing.gameObject;
            }
            else
            {
                go = new GameObject(
                    PopupName,
                    typeof(RectTransform),
                    typeof(VerticalLayoutGroup),
                    typeof(ContentSizeFitter),
                    typeof(Image));
                go.layer = menuRoot.gameObject.layer;
                go.transform.SetParent(menuRoot, false);
            }

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(0f, MainButtonSize + PopupGap);
            rt.sizeDelta = new Vector2(PopupButtonWidth, 0f);

            var bg = go.GetComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.25f);
            bg.raycastTarget = false;

            var layout = go.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.LowerCenter;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.spacing = 6f;
            layout.padding = new RectOffset(0, 0, 0, 0);

            var fitter = go.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            EnsurePopupButtons(go.transform);
            go.SetActive(false);
            return go;
        }

        private void EnsurePopupButtons(Transform popup)
        {
            if (popup == null)
                return;

            for (int i = 0; i < Entries.Length; i++)
            {
                string buttonName = $"Btn_{Entries[i].Label.Replace(" ", string.Empty)}";
                Button btn = EnsurePopupButton(popup, buttonName, Entries[i].Label);
                if (btn == null)
                    continue;

                btn.onClick.RemoveAllListeners();
                var action = Entries[i].Action;
                btn.onClick.AddListener(() => action?.Invoke(this));
            }
        }

        private Button EnsurePopupButton(Transform popup, string buttonName, string label)
        {
            Transform existing = popup.Find(buttonName);
            GameObject go;
            if (existing != null)
            {
                go = existing.gameObject;
            }
            else
            {
                go = new GameObject(buttonName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                go.layer = popup.gameObject.layer;
                go.transform.SetParent(popup, false);
            }

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(PopupButtonWidth, PopupButtonHeight);

            var layoutElement = go.GetComponent<LayoutElement>();
            layoutElement.minHeight = PopupButtonHeight;
            layoutElement.preferredHeight = PopupButtonHeight;
            layoutElement.preferredWidth = PopupButtonWidth;
            layoutElement.flexibleWidth = 1f;

            var image = go.GetComponent<Image>();
            image.color = new Color(0.10f, 0.10f, 0.12f, 0.92f);
            image.raycastTarget = true;

            var button = go.GetComponent<Button>();
            ConfigureButtonColors(button, new Color(0.16f, 0.16f, 0.20f, 1f));

            EnsureButtonText(go.transform, label, 20f);
            return button;
        }

        private static void ConfigureButtonColors(Button button, Color normal)
        {
            if (button == null)
                return;

            var colors = button.colors;
            colors.normalColor = normal;
            colors.highlightedColor = normal * 1.15f;
            colors.pressedColor = normal * 0.85f;
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            button.colors = colors;
            button.targetGraphic = button.GetComponent<Graphic>();
        }

        private static void EnsureButtonText(Transform parent, string text, float fontSize)
        {
            if (parent == null)
                return;

            Transform existing = parent.Find("Text");
            GameObject go;
            if (existing != null)
            {
                go = existing.gameObject;
            }
            else
            {
                go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
                go.layer = parent.gameObject.layer;
                go.transform.SetParent(parent, false);
            }

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.enableWordWrapping = false;
            tmp.raycastTarget = false;
        }
    }
}
