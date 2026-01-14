using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace GalacticFishing.UI
{
    /// Lightweight PREV / X/Y / NEXT pager for InventoryGridController.
    /// Put this on the same object as InventoryGridController (e.g., inventory-background).
    [DisallowMultipleComponent]
    [ExecuteAlways] // <- build/position in edit mode too
    public class InventoryPager : MonoBehaviour
    {
        [Header("Hook to your grid")]
        [SerializeField] private InventoryGridController grid;

        [Header("Build & Style")]
        [SerializeField] private bool   autoBuildUI  = true;
        [SerializeField] private Vector2 offset      = new Vector2(-280f, -400f); // what you liked
        [SerializeField] private float  spacing      = 24f;
        [SerializeField] private Vector2 buttonSize  = new Vector2(140f, 44f);
        [SerializeField] private string prevLabel    = "PREV <";
        [SerializeField] private string nextLabel    = "> NEXT";
        [SerializeField] private float  fontSize     = 24f;
        [SerializeField] private Color  textColor    = Color.white;
        [SerializeField] private Sprite buttonSprite = null;
        [SerializeField] private Color  buttonColor  = new Color(1f, 1f, 1f, 0f);

        [Header("Use existing UI (optional)")]
        [SerializeField] private Button   prevButton;
        [SerializeField] private TMP_Text pageText;
        [SerializeField] private Button   nextButton;

        // runtime/editor refs
        RectTransform pagerRT;
        HorizontalLayoutGroup hlg;

        void Awake()
        {
            HookGrid();
            if (autoBuildUI) EnsureUI();
            Wire();
            ApplyLayout();
            Refresh();
        }

        void OnEnable()
        {
            HookGrid();
            if (autoBuildUI) EnsureUI();
            Wire();
            ApplyLayout();
            Refresh();
        }

        // When values change in Inspector (edit mode), push to UI
        void OnValidate()
        {
            // Delay one frame in edit mode to let Unity serialize first-time values.
            if (!isActiveAndEnabled) return;
            HookGrid();
            if (autoBuildUI) EnsureUI();
            ApplyLayout();
            Refresh();
        }

        void HookGrid()
        {
            if (!grid)
            {
                grid = GetComponent<InventoryGridController>();
                if (!grid) grid = FindObjectOfType<InventoryGridController>();
            }
        }

        void EnsureUI()
        {
            if (!prevButton || !pageText || !nextButton || !pagerRT)
            {
                // Try to reuse an existing child called "Pager"
                var existing = transform.Find("Pager");
                if (existing)
                {
                    pagerRT = existing as RectTransform;
                    hlg     = pagerRT.GetComponent<HorizontalLayoutGroup>();
                    if (!hlg) hlg = pagerRT.gameObject.AddComponent<HorizontalLayoutGroup>();

                    // Attempt to pick up already-built children
                    if (!prevButton) prevButton = pagerRT.GetComponentInChildren<Button>(true);
                    if (!pageText)   pageText   = pagerRT.GetComponentInChildren<TMP_Text>(true);
                    if (!nextButton)
                    {
                        // If there was only one Button found, create the rest below
                        prevButton = null; pageText = null; nextButton = null;
                    }
                }

                if (!pagerRT)
                {
                    var parentRT = transform as RectTransform;
                    if (!parentRT) parentRT = gameObject.AddComponent<RectTransform>();

                    var rootGO = new GameObject("Pager", typeof(RectTransform));
                    pagerRT = (RectTransform)rootGO.transform;
                    pagerRT.SetParent(parentRT, false);
                    hlg = rootGO.AddComponent<HorizontalLayoutGroup>();
                }

                // (Re)build children if any are missing
                if (!prevButton || !pageText || !nextButton)
                {
                    // Clear children
                    for (int i = pagerRT.childCount - 1; i >= 0; i--)
                        DestroyImmediate(pagerRT.GetChild(i).gameObject);

                    prevButton = CreateTextButton("Prev", prevLabel, pagerRT);
                    pageText   = CreateLabel("Page", "1/1", pagerRT);
                    nextButton = CreateTextButton("Next", nextLabel, pagerRT);
                }
            }
        }

        Button CreateTextButton(string name, string label, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);

            var le = go.GetComponent<LayoutElement>();
            le.minWidth = buttonSize.x;  le.preferredWidth  = buttonSize.x;
            le.minHeight = buttonSize.y; le.preferredHeight = buttonSize.y;

            var img = go.GetComponent<Image>();
            img.sprite = buttonSprite;
            img.color  = buttonColor;
            img.raycastTarget = true;

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.transition    = Selectable.Transition.None;

            var txtGO = new GameObject("Text", typeof(RectTransform));
            var txtRT = (RectTransform)txtGO.transform;
            txtRT.SetParent(rt, false);
            txtRT.anchorMin = Vector2.zero; txtRT.anchorMax = Vector2.one;
            txtRT.offsetMin = Vector2.zero; txtRT.offsetMax = Vector2.zero;

            var tmp = txtGO.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableAutoSizing = false;
            tmp.fontSize = fontSize;
            tmp.color = textColor;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.raycastTarget = false;

            return btn;
        }

        TMP_Text CreateLabel(string name, string label, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);

            var le = go.GetComponent<LayoutElement>();
            le.minWidth = 120f; le.preferredWidth  = 120f;
            le.minHeight = buttonSize.y; le.preferredHeight = buttonSize.y;

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableAutoSizing = false;
            tmp.fontSize = Mathf.Max(18f, fontSize * 0.8f);
            tmp.color = textColor;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.raycastTarget = false;

            return tmp;
        }

        void ApplyLayout()
        {
            if (!pagerRT) return;

            // Anchor to bottom-center; use pivot Y=1 so negative Y moves upward (matches your hand tweak)
            pagerRT.anchorMin = new Vector2(0.5f, 0f);
            pagerRT.anchorMax = new Vector2(0.5f, 0f);
            pagerRT.pivot     = new Vector2(0.5f, 1f);
            pagerRT.anchoredPosition = offset;
            pagerRT.localScale = Vector3.one;

            if (!hlg) hlg = pagerRT.GetComponent<HorizontalLayoutGroup>();
            if (hlg)
            {
                hlg.childAlignment = TextAnchor.MiddleCenter;
                hlg.spacing = spacing;
                hlg.padding = new RectOffset(0, 0, 0, 0);
            }

            // Ensure button/label sizes reflect inspector values
            ResizeChild(prevButton);
            ResizeChild(nextButton);
        }

        void ResizeChild(Button btn)
        {
            if (!btn) return;
            var le = btn.GetComponent<LayoutElement>();
            if (!le) le = btn.gameObject.AddComponent<LayoutElement>();
            le.minWidth = buttonSize.x;  le.preferredWidth  = buttonSize.x;
            le.minHeight = buttonSize.y; le.preferredHeight = buttonSize.y;

            var tmp = btn.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp)
            {
                tmp.fontSize = fontSize;
                tmp.color    = textColor;
            }
        }

        void Wire()
        {
            if (prevButton)
            {
                prevButton.onClick.RemoveAllListeners();
                prevButton.onClick.AddListener(OnPrev);
            }
            if (nextButton)
            {
                nextButton.onClick.RemoveAllListeners();
                nextButton.onClick.AddListener(OnNext);
            }
        }

        void OnPrev()
        {
            if (!grid) return;
            grid.PrevPage();
            Refresh();
        }

        void OnNext()
        {
            if (!grid) return;
            grid.NextPage();
            Refresh();
        }

        public void Refresh()
        {
            if (!grid) return;
            int cur = grid.Page + 1;
            int max = Mathf.Max(1, grid.TotalPages);
            if (pageText) pageText.text = cur.ToString() + "/" + max.ToString();
            if (prevButton) prevButton.interactable = (grid.Page > 0);
            if (nextButton) nextButton.interactable = (grid.Page < max - 1);
        }

        [ContextMenu("Rebuild Pager UI")]
        void RebuildNow()
        {
            EnsureUI();
            ApplyLayout();
            Wire();
            Refresh();
        }
    }
}
