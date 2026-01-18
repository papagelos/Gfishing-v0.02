using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

namespace GalacticFishing.UI
{
    public sealed class HubPanelController : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private Transform leftColumn;   // Panel_Hub/LeftColumn
        [SerializeField] private Transform rightGrid;    // Panel_Hub/RightGrid

        [Header("Worlds Tab")]
        [SerializeField] private GalacticFishing.WorldManager worldManager;
        [SerializeField] private List<GalacticFishing.WorldDefinition> worlds = new();

        [Tooltip("Which LeftColumn index (0-based) should be used for the WORLDS tab?")]
        [SerializeField] private int worldsTabIndex = 1;   // default: second tab from top

        [Header("Custom Tabs")]
        [SerializeField] private List<CustomPage> customPages = new(); // e.g., UPGRADES / MUSEUM / INVENTORY

        [Header("Behaviour")]
        [SerializeField] private bool closePanelAfterWorldPick = false;

        [Header("GRID Label Style (Right side)")]
        [SerializeField] private bool gridUseAutoSize = true;
        [SerializeField] private float gridAutoMin = 16f;
        [SerializeField] private float gridAutoMax = 40f;
        [SerializeField] private TextOverflowModes gridOverflow = TextOverflowModes.Ellipsis;
        [SerializeField] private bool gridWordWrap = false;
        [SerializeField] private bool smartTwoLineIfLong = false;
        [SerializeField] private int twoLineThreshold = 12;

        [Header("LEFT TAB Label Style (override)")]
        [SerializeField] private bool leftUseAutoSize = true;
        [SerializeField] private float leftAutoMin = 28f;
        [SerializeField] private float leftAutoMax = 96f;
        [SerializeField] private TextOverflowModes leftOverflow = TextOverflowModes.Ellipsis;
        [SerializeField] private bool leftWordWrap = false;

        [Header("Pagination (auto builds PREV/NEXT)")]
        [SerializeField] private bool autoBuildPagerUI = true;
        [SerializeField] private bool showPagerWhenSinglePage = false;
        [SerializeField] private Vector2 pagerOffset = new Vector2(0f, -16f);
        [SerializeField] private bool pagerStackVertical = false; // show stack (1, —, 2) instead of 1/2
        [SerializeField] private string pagerDivider = "—";
        [SerializeField] private string pagerSlash = "/";

        // runtime pager refs
        [SerializeField] private Button pagerPrev;
        [SerializeField] private TMP_Text pagerText;
        [SerializeField] private Button pagerNext;
        private LayoutElement pagerLabelLE; // <— was missing

        [Header("Pagination Style")]
        [SerializeField] private string prevLabel = "PREV <";
        [SerializeField] private string nextLabel = "> NEXT";
        [SerializeField] private float pagerFontSize = 32f;
        [SerializeField] private Color pagerTextColor = Color.white;
        [SerializeField] private Vector2 pagerButtonSize = new Vector2(140f, 44f);
        [SerializeField] private float pagerSpacing = 24f;

        [Tooltip("Optional sprite for PREV/NEXT button background (leave null for clean).")]
        [SerializeField] private Sprite pagerButtonSprite = null;

        [Tooltip("Background color; if fully transparent, we'll auto-set to very small alpha so the full rect is clickable.")]
        [SerializeField] private Color pagerButtonColor = new Color(1, 1, 1, 0f);

        // ----- internal -----
        private List<Cell> _left = new();
        private List<Cell> _grid = new();

        // _currentTabType:
        //   -1 = Worlds tab (Worlds / Lakes)
        //   0..N-1 = index into customPages
        private int _currentTabType = 0;
        private int _currentPage = 0;
        private int _pageSize = 15;
        private int _pageCount = 1;

        // When not null, the WORLDS tab is currently showing lakes for this world
        private GalacticFishing.WorldDefinition _selectedWorldForLakes = null;

        [System.Serializable]
        public class CustomPage
        {
            public string tabLabel = "UPGRADES";
            public List<CustomItem> items = new();
        }

        [System.Serializable]
        public class CustomItem
        {
            public string label = "Item";
            public Sprite icon;
            public UnityEvent onClick;
        }

        private struct Cell
        {
            public GameObject go;
            public Button button;
            public TMP_Text label;
            public Image icon;
        }

        void Awake()
        {
            if (!leftColumn || !rightGrid)
            {
                Debug.LogWarning("[HubPanelController] Assign LeftColumn & RightGrid.");
                return;
            }

            _left = CollectCells(leftColumn);
            _grid = CollectCells(rightGrid);
            _pageSize = _grid.Count;

            ApplyLeftLabelStyle();
            ApplyGridLabelStyle();

            if (autoBuildPagerUI) EnsurePagerUI();
            WirePagerButtons();

            SetupLeftTabs();

            // Start on first custom tab if it exists, otherwise Worlds if available.
            if (customPages != null && customPages.Count > 0)
            {
                _currentTabType = 0;
                _currentPage = 0;
                ShowCustomPage(0);
            }
            else if (worlds != null && worlds.Count > 0)
            {
                _currentTabType = -1;
                _currentPage = 0;
                _selectedWorldForLakes = null;
                ShowWorldsPage();
            }
            else
            {
                // Nothing to show – just reset the pager.
                UpdatePager(0, 1);
            }
        }

        // ---------- collect tiles ----------
        private List<Cell> CollectCells(Transform parent)
        {
            var list = new List<Cell>(parent.childCount);
            for (int i = 0; i < parent.childCount; i++)
            {
                var t = parent.GetChild(i);
                var go = t.gameObject;

                var img = go.GetComponent<Image>();
                if (!img) img = go.AddComponent<Image>();

                var btn = go.GetComponent<Button>();
                if (!btn) btn = go.AddComponent<Button>();

                if (!btn.targetGraphic) btn.targetGraphic = img;
                btn.navigation = new Navigation { mode = Navigation.Mode.None };

                var label = go.GetComponentInChildren<TMP_Text>(true);

                Image icon = null;
                var iconTr = go.transform.Find("Icon");
                if (iconTr) icon = iconTr.GetComponent<Image>();

                list.Add(new Cell
                {
                    go = go,
                    button = btn,
                    label = label,
                    icon = icon
                });
            }
            return list;
        }

        private void StyleLabel(TMP_Text L, bool useAuto, float min, float max, TextOverflowModes overflow, bool wrap)
        {
            if (!L) return;
            L.enableAutoSizing = useAuto;
            if (useAuto)
            {
                L.fontSizeMin = min;
                L.fontSizeMax = max;
            }
            L.overflowMode = overflow;
            L.enableWordWrapping = wrap;
            L.ForceMeshUpdate();
        }

        private void ApplyLeftLabelStyle()
        {
            foreach (var c in _left)
                StyleLabel(c.label, leftUseAutoSize, leftAutoMin, leftAutoMax, leftOverflow, leftWordWrap);
        }

        private void ApplyGridLabelStyle()
        {
            foreach (var c in _grid)
                StyleLabel(c.label, gridUseAutoSize, gridAutoMin, gridAutoMax, gridOverflow, gridWordWrap);
        }

        private string MakeFit(string s)
        {
            if (!smartTwoLineIfLong || string.IsNullOrEmpty(s) || s.Length < twoLineThreshold)
                return s;

            // crude but effective: insert newline after first space past threshold
            int idx = s.IndexOf(' ', twoLineThreshold);
            if (idx <= 0 || idx >= s.Length - 1) return s;
            return s.Substring(0, idx) + "\n" + s.Substring(idx + 1);
        }

        // ---------- pager UI ----------

        private void EnsurePagerUI()
        {
            if (!rightGrid) return;

            var parent = rightGrid.parent != null ? rightGrid.parent : rightGrid;
            var existing = parent.Find("PagerRoot");
            RectTransform root;

            if (existing)
            {
                root = existing as RectTransform;
                pagerPrev = root.Find("Prev")?.GetComponent<Button>();
                pagerText = root.Find("Label")?.GetComponent<TMP_Text>();
                pagerNext = root.Find("Next")?.GetComponent<Button>();
            }
            else
            {
                var go = new GameObject("PagerRoot", typeof(RectTransform));
                root = go.GetComponent<RectTransform>();
                root.SetParent(parent, false);

                root.anchorMin = new Vector2(0.5f, 0f);
                root.anchorMax = new Vector2(0.5f, 0f);
                root.pivot = new Vector2(0.5f, 0f);
                root.anchoredPosition = pagerOffset;
                root.sizeDelta = Vector2.zero;

                var layout = go.AddComponent<HorizontalLayoutGroup>();
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;
                layout.spacing = pagerSpacing;
                layout.childAlignment = TextAnchor.MiddleCenter;

                if (pagerStackVertical)
                {
                    var vert = go.AddComponent<VerticalLayoutGroup>();
                    vert.childControlWidth = true;
                    vert.childControlHeight = true;
                    vert.childForceExpandWidth = false;
                    vert.childForceExpandHeight = false;
                    vert.spacing = 4f;
                    vert.childAlignment = TextAnchor.MiddleCenter;
                }

                pagerPrev = BuildPagerButton(root, "Prev", prevLabel);
                pagerText = BuildPagerLabel(root, "Label");
                pagerNext = BuildPagerButton(root, "Next", nextLabel);
            }

            pagerLabelLE = pagerText ? pagerText.GetComponent<LayoutElement>() : null;
        }

        private Button BuildPagerButton(RectTransform parent, string name, string labelText)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.sizeDelta = pagerButtonSize;

            var img = go.AddComponent<Image>();
            img.sprite = pagerButtonSprite;
            img.color = pagerButtonColor.a <= 0f
                ? new Color(pagerButtonColor.r, pagerButtonColor.g, pagerButtonColor.b, 0.001f)
                : pagerButtonColor;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };

            var textGo = new GameObject("Text", typeof(RectTransform));
            var tr = textGo.GetComponent<RectTransform>();
            tr.SetParent(rt, false);
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = Vector2.zero;
            tr.offsetMax = Vector2.zero;

            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = labelText;
            tmp.fontSize = pagerFontSize;
            tmp.color = pagerTextColor;
            tmp.alignment = TextAlignmentOptions.Center;

            return btn;
        }

        private TMP_Text BuildPagerLabel(RectTransform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.sizeDelta = Vector2.zero;

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = "1 / 1";
            tmp.fontSize = pagerFontSize;
            tmp.color = pagerTextColor;
            tmp.alignment = TextAlignmentOptions.Center;

            pagerLabelLE = go.AddComponent<LayoutElement>();
            return tmp;
        }

        private void WirePagerButtons()
        {
            if (pagerPrev)
            {
                pagerPrev.onClick.RemoveAllListeners();
                pagerPrev.onClick.AddListener(PrevPage);
            }
            if (pagerNext)
            {
                pagerNext.onClick.RemoveAllListeners();
                pagerNext.onClick.AddListener(NextPage);
            }
        }

        private void PrevPage()
        {
            if (_pageCount <= 1) return;
            _currentPage = Mathf.Max(0, _currentPage - 1);
            RedrawCurrentTab();
        }

        private void NextPage()
        {
            if (_pageCount <= 1) return;
            _currentPage = Mathf.Min(_pageCount - 1, _currentPage + 1);
            RedrawCurrentTab();
        }

        private void UpdatePager(int page, int pageCount)
        {
            page = Mathf.Clamp(page, 0, Mathf.Max(0, pageCount - 1));

            if (pagerText)
            {
                if (pagerStackVertical)
                {
                    pagerText.text = $"{page + 1}\n{pagerDivider}\n{pageCount}";
                }
                else
                {
                    pagerText.text = $"{page + 1} {pagerSlash} {pageCount}";
                }
            }

            if (pagerPrev) pagerPrev.interactable = page > 0;
            if (pagerNext) pagerNext.interactable = page < pageCount - 1;

            bool show = pageCount > 1 || showPagerWhenSinglePage;
            if (pagerText && pagerText.transform.parent)
                pagerText.transform.parent.gameObject.SetActive(show);
        }

        // ---------- left tabs ----------

        private void SetupLeftTabs()
        {
            // Hide extra left cells beyond customPages + optional Worlds tab.
            int neededLeft = (customPages?.Count ?? 0) + (worlds != null && worlds.Count > 0 ? 1 : 0);
            for (int i = 0; i < _left.Count; i++)
            {
                _left[i].go.SetActive(i < neededLeft);
            }

            int leftIndex = 0;

            // Inject WORLDS tab at configured index (if any worlds).
            int worldsTabPos = (worlds != null && worlds.Count > 0)
                ? Mathf.Clamp(worldsTabIndex, 0, _left.Count - 1)
                : -1;

            if (worldsTabPos >= 0)
            {
                // We'll fill custom tabs while skipping this index; worlds tab will be set below.
            }

            // First pass: custom tabs.
            for (int i = 0; i < (customPages?.Count ?? 0); i++)
            {
                // If this index is reserved for worlds, skip it for now.
                while (leftIndex == worldsTabPos) leftIndex++;

                if (leftIndex >= _left.Count) break;

                var cell = _left[leftIndex];
                var page = customPages[i];

                if (cell.label) cell.label.text = page.tabLabel;
                cell.button.onClick.RemoveAllListeners();

                int capture = i;
                cell.button.onClick.AddListener(() =>
                {
                    _currentTabType = capture;
                    _currentPage = 0;
                    _selectedWorldForLakes = null;
                    ShowCustomPage(capture);
                });

                leftIndex++;
            }

            // Finally, place the WORLDS tab if we have one.
            if (worldsTabPos >= 0 && worlds != null && worlds.Count > 0)
            {
                var cell = _left[worldsTabPos];
                if (cell.label) cell.label.text = "WORLDS";

                cell.button.onClick.RemoveAllListeners();
                cell.button.onClick.AddListener(() =>
                {
                    _currentTabType = -1;
                    _currentPage = 0;
                    _selectedWorldForLakes = null; // reset to show world list first
                    ShowWorldsPage();
                });
            }
        }

        private void RedrawCurrentTab()
        {
            if (_currentTabType == -1)
            {
                ShowWorldsPage();
            }
            else
            {
                ShowCustomPage(_currentTabType);
            }
        }

        // ---------- WORLDS + LAKES special page ----------

        public void ShowWorldsPage()
        {
            // If a world is already selected, this tab acts as a LAKE picker.
            if (_selectedWorldForLakes != null)
            {
                ShowLakesForWorld(_selectedWorldForLakes);
                return;
            }

            int total = worlds != null ? worlds.Count : 0;
            if (total <= 0)
            {
                // Clear grid & hide pager.
                for (int i = 0; i < _grid.Count; i++)
                    _grid[i].go.SetActive(false);

                UpdatePager(0, 1);
                return;
            }

            _pageCount = Mathf.Max(1, Mathf.CeilToInt(total / (float)_pageSize));
            _currentPage = Mathf.Clamp(_currentPage, 0, _pageCount - 1);

            int startIndex = _currentPage * _pageSize;
            int endIndex = Mathf.Min(startIndex + _pageSize, total);

            for (int i = 0; i < _grid.Count; i++)
            {
                var c = _grid[i];
                int dataIndex = startIndex + i;
                if (dataIndex >= endIndex)
                {
                    c.go.SetActive(false);
                    continue;
                }

                if (!c.button || !c.label)
                {
                    c.go.SetActive(false);
                    continue;
                }

                var worldDef = worlds[dataIndex];
                c.go.SetActive(true);

                string labelText = worldDef ? worldDef.displayName : "World";
                if (smartTwoLineIfLong) labelText = MakeFit(labelText);
                c.label.text = labelText;

                if (c.icon)
                {
                    // Optionally assign icons per world; leaving null for now.
                    c.icon.enabled = false;
                }

                c.button.onClick.RemoveAllListeners();
                int captureIndex = dataIndex;

                // NEW: clicking a world now opens its LAKE grid instead of warping immediately.
                c.button.onClick.AddListener(() =>
                {
                    _selectedWorldForLakes = worlds[captureIndex];
                    _currentPage = 0;
                    ShowLakesForWorld(_selectedWorldForLakes);
                });
            }

            UpdatePager(_currentPage, _pageCount);
        }

        /// <summary>
        /// Shows a 5×3 style grid of "LAKE X" tiles for the chosen world.
        /// </summary>
        private void ShowLakesForWorld(GalacticFishing.WorldDefinition world)
        {
            if (world == null)
            {
                // Safety: if something went wrong, fall back to world list.
                _selectedWorldForLakes = null;
                ShowWorldsPage();
                return;
            }

            int lakeCount = GetLakeCountForWorld(world);
            if (lakeCount <= 0) lakeCount = 1;

            _pageCount = Mathf.Max(1, Mathf.CeilToInt(lakeCount / (float)_pageSize));
            _currentPage = Mathf.Clamp(_currentPage, 0, _pageCount - 1);

            int startIndex = _currentPage * _pageSize;
            int endIndex = Mathf.Min(startIndex + _pageSize, lakeCount);

            for (int i = 0; i < _grid.Count; i++)
            {
                var c = _grid[i];
                int lakeIndex = startIndex + i;

                if (lakeIndex >= endIndex)
                {
                    c.go.SetActive(false);
                    continue;
                }

                if (!c.button || !c.label)
                {
                    c.go.SetActive(false);
                    continue;
                }

                c.go.SetActive(true);

                string labelText = $"LAKE {lakeIndex + 1}";
                if (smartTwoLineIfLong) labelText = MakeFit(labelText);
                c.label.text = labelText;

                if (c.icon)
                {
                    c.icon.enabled = false;
                }

                bool unlocked = IsLakeUnlocked(world, lakeIndex);
                c.button.interactable = unlocked;

                c.button.onClick.RemoveAllListeners();
                int capturedLake = lakeIndex;

                c.button.onClick.AddListener(() =>
                {
                    if (!unlocked) return;
                    PickLakeAndWarp(world, capturedLake);
                });
            }

            UpdatePager(_currentPage, _pageCount);
        }

        /// <summary>
        /// Attempts to determine how many lakes exist for a given world.
        /// Tries WorldManager helpers first; falls back to probing WorldDefinition via reflection.
        /// If everything fails, assumes at least 1 lake.
        /// </summary>
        private int GetLakeCountForWorld(GalacticFishing.WorldDefinition world)
        {
            if (world == null) return 1;

            const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            // Ask WorldManager, if it exposes something useful.
            if (worldManager != null)
            {
                var mgrType = worldManager.GetType();

                try
                {
                    // int GetLakeCount(WorldDefinition world)
                    var m = mgrType.GetMethod("GetLakeCount", BF, null,
                        new[] { typeof(GalacticFishing.WorldDefinition) }, null)
                            ?? mgrType.GetMethod("GetLakeCountForWorld", BF, null,
                                new[] { typeof(GalacticFishing.WorldDefinition) }, null);

                    if (m != null)
                    {
                        var result = m.Invoke(worldManager, new object[] { world });
                        if (result is int mi && mi > 0)
                            return mi;
                    }

                    // int LakeCount { get; }
                    var p = mgrType.GetProperty("LakeCount", BF) ?? mgrType.GetProperty("lakeCount", BF);
                    if (p != null && p.CanRead)
                    {
                        var v = p.GetValue(worldManager);
                        if (v is int pi && pi > 0)
                            return pi;
                    }
                }
                catch { /* swallow & fall through */ }
            }

            // Probe WorldDefinition itself for lakes / LakeCount.
            try
            {
                var wt = world.GetType();
                const BindingFlags BF2 = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                var lakesField = wt.GetField("lakes", BF2) ?? wt.GetField("Lakes", BF2);
                if (lakesField != null)
                {
                    var val = lakesField.GetValue(world);
                    if (val is System.Collections.ICollection col && col.Count > 0)
                        return col.Count;

                    if (val is System.Collections.IEnumerable en)
                    {
                        int c = 0;
                        foreach (var _ in en) c++;
                        if (c > 0) return c;
                    }
                }

                var lakesProp = wt.GetProperty("lakes", BF2) ?? wt.GetProperty("Lakes", BF2);
                if (lakesProp != null && lakesProp.CanRead)
                {
                    var val = lakesProp.GetValue(world);
                    if (val is System.Collections.ICollection col && col.Count > 0)
                        return col.Count;

                    if (val is System.Collections.IEnumerable en)
                    {
                        int c = 0;
                        foreach (var _ in en) c++;
                        if (c > 0) return c;
                    }
                }

                var countProp = wt.GetProperty("LakeCount", BF2) ?? wt.GetProperty("lakeCount", BF2);
                if (countProp != null && countProp.CanRead)
                {
                    var v = countProp.GetValue(world);
                    if (v is int ci && ci > 0)
                        return ci;
                }
            }
            catch { /* ignore */ }

            // Fallback – safest default.
            return 1;
        }

        /// <summary>
        /// Whether a given lake index is unlocked.
        /// CURRENTLY: all lakes are treated as unlocked (progression is handled elsewhere).
        /// When you add real progression, you can re-enable the WorldManager hook here.
        /// </summary>
        private bool IsLakeUnlocked(GalacticFishing.WorldDefinition world, int lakeIndex)
        {
            // TEMP behaviour: everything unlocked so World→Lake navigation always works.
            return true;

            /*
            // If you later want to use WorldManager's IsLakeUnlocked, restore this:

            if (worldManager != null)
            {
                const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                var mgrType = worldManager.GetType();

                try
                {
                    // bool IsLakeUnlocked(int lakeIndex)
                    var m1 = mgrType.GetMethod("IsLakeUnlocked", BF, null, new[] { typeof(int) }, null);
                    if (m1 != null)
                    {
                        var result = m1.Invoke(worldManager, new object[] { lakeIndex });
                        if (result is bool b1) return b1;
                    }

                    // bool IsLakeUnlocked(WorldDefinition,int)
                    var m2 = mgrType.GetMethod("IsLakeUnlocked", BF, null,
                        new[] { typeof(GalacticFishing.WorldDefinition), typeof(int) }, null);
                    if (m2 != null)
                    {
                        var result = m2.Invoke(worldManager, new object[] { world, lakeIndex });
                        if (result is bool b2) return b2;
                    }
                }
        catch
        {
            // fall back
        }
            }

            // Default rule: all lakes are available.
            return true;
            */
        }

        /// <summary>
        /// Final action when the player picks a lake: set world + lake index and warp.
        /// </summary>
        private void PickLakeAndWarp(GalacticFishing.WorldDefinition world, int lakeIndex)
        {
            if (worldManager && world != null)
            {
                // Use the WorldManager helper so WorldChanged event fires properly.
                worldManager.SetWorld(world, lakeIndex);
            }

            if (closePanelAfterWorldPick)
            {
                var hub = GetComponentInParent<FullscreenHubController>();
                if (hub != null)
                {
                    hub.ForceClosedImmediate();
                }
            }
        }

        // ---------- CUSTOM PAGES ----------

        private void ShowCustomPage(int pageIndex)
        {
            if (customPages == null || pageIndex < 0 || pageIndex >= customPages.Count)
            {
                // Clear grid
                for (int i = 0; i < _grid.Count; i++)
                    _grid[i].go.SetActive(false);

                UpdatePager(0, 1);
                return;
            }

            var page = customPages[pageIndex];
            int total = page.items != null ? page.items.Count : 0;

            _pageCount = Mathf.Max(1, Mathf.CeilToInt(total / (float)_pageSize));
            _currentPage = Mathf.Clamp(_currentPage, 0, _pageCount - 1);

            int startIndex = _currentPage * _pageSize;
            int endIndex = Mathf.Min(startIndex + _pageSize, total);

            for (int i = 0; i < _grid.Count; i++)
            {
                var c = _grid[i];
                int dataIndex = startIndex + i;
                if (dataIndex >= endIndex)
                {
                    c.go.SetActive(false);
                    continue;
                }

                if (!c.button || !c.label)
                {
                    c.go.SetActive(false);
                    continue;
                }

                var item = page.items[dataIndex];
                c.go.SetActive(true);

                string labelText = item.label;
                if (smartTwoLineIfLong) labelText = MakeFit(labelText);
                c.label.text = labelText;

                if (c.icon)
                {
                    c.icon.sprite = item.icon;
                    c.icon.enabled = item.icon != null;
                }

                c.button.onClick.RemoveAllListeners();
                if (item.onClick != null)
                {
                    c.button.onClick.AddListener(() => item.onClick.Invoke());
                }
            }

            UpdatePager(_currentPage, _pageCount);
        }
    }
}
