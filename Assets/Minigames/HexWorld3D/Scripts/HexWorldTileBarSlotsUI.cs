// Assets/Minigames/HexWorld3D/Scripts/HexWorldTileBarSlotsUI.cs
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GalacticFishing.Minigames.HexWorld
{
    public sealed class HexWorldTileBarSlotsUI : MonoBehaviour, HexWorldSharedPagingButtonsRouter.IHexPager

    {
        [Header("Refs")]
        [SerializeField] private HexWorld3DController controller;
        [SerializeField] private RectTransform slotsContainer;
        [SerializeField] private HexWorldTileSlotUI slotPrefab;

        [Header("Tile styles (order = UI order)")]
        [Tooltip("If true, uses controller.GetStyleCatalog() instead of local styles array.")]
        [SerializeField] private bool syncWithControllerCatalog = true;

        [SerializeField] private HexWorldTileStyle[] styles;

        [Header("Unlocking (future-proof)")]
        [SerializeField] private bool startAllUnlocked = true;
        [SerializeField] private HexWorldTileStyle[] initiallyUnlocked;

        [Header("Selection UX")]
        [Tooltip("If ON: clicking the currently selected style again will deselect (normal cursor, no ghost tile).")]
        [SerializeField] private bool clickSelectedAgainToDeselect = true;

        [Header("Paging")]
        [Tooltip("How many tiles to show per page (e.g. 5 now, 6 later if you shrink).")]
        [SerializeField] private int pageSize = 5;

        [Tooltip("Optional: hook up left arrow button here.")]
        [SerializeField] private Button prevPageButton;

        [Tooltip("Optional: hook up right arrow button here.")]
        [SerializeField] private Button nextPageButton;

        [Tooltip("Optional: shows something like 1/3.")]
        [SerializeField] private TMP_Text pageLabel;

        [Header("Toast (optional)")]
        [SerializeField] private CanvasGroup toastGroup;
        [SerializeField] private TMP_Text toastText;
        [SerializeField] private float toastFadeIn = 0.08f;
        [SerializeField] private float toastHold = 0.9f;
        [SerializeField] private float toastFadeOut = 0.2f;

        private readonly Dictionary<HexWorldTileStyle, HexWorldTileSlotUI> _slots = new();
        private readonly List<HexWorldTileSlotUI> _orderedSlots = new();


// ---- Shared paging router support ----
public int PageIndex => _pageIndex;
public int TotalPagesCount => TotalPages;
public bool CanPrev => _pageIndex > 0;
public bool CanNext => _pageIndex < (TotalPages - 1);
public void RefreshPaging() => ApplyPaging();



        private int _pageIndex;
        private Coroutine _toastCo;

        private void Awake()
        {
            if (toastGroup)
            {
                toastGroup.alpha = 0f;
                toastGroup.interactable = false;
                toastGroup.blocksRaycasts = false;
            }
            if (!toastText && toastGroup) toastText = toastGroup.GetComponentInChildren<TMP_Text>(true);
        }

        private void Start()
        {
            if (!controller)
            {
                Debug.LogError("HexWorldTileBarSlotsUI: controller missing.");
                enabled = false;
                return;
            }
            if (!slotsContainer)
            {
                Debug.LogError("HexWorldTileBarSlotsUI: slotsContainer missing.");
                enabled = false;
                return;
            }
            if (!slotPrefab)
            {
                Debug.LogError("HexWorldTileBarSlotsUI: slotPrefab missing.");
                enabled = false;
                return;
            }

            BuildSlots();
            HookPagingButtons();

            controller.SelectedStyleChanged += OnSelectedStyleChanged;
            controller.ToastRequested += ShowToast;

            OnSelectedStyleChanged(controller.SelectedStyle);
        }

        private void OnDestroy()
        {
            if (controller)
            {
                controller.SelectedStyleChanged -= OnSelectedStyleChanged;
                controller.ToastRequested -= ShowToast;
            }
        }

        private void HookPagingButtons()
        {
            if (prevPageButton)
            {
                prevPageButton.onClick.RemoveAllListeners();
                prevPageButton.onClick.AddListener(PrevPage);
            }

            if (nextPageButton)
            {
                nextPageButton.onClick.RemoveAllListeners();
                nextPageButton.onClick.AddListener(NextPage);
            }

            ApplyPaging();
        }

        private int TotalPages
        {
            get
            {
                int ps = Mathf.Max(1, pageSize);
                int count = _orderedSlots.Count;
                return Mathf.Max(1, Mathf.CeilToInt(count / (float)ps));
            }
        }

        private void ClampPage()
        {
            int tp = TotalPages;
            _pageIndex = Mathf.Clamp(_pageIndex, 0, tp - 1);
        }

        private void ApplyPaging()
        {
            ClampPage();

            int ps = Mathf.Max(1, pageSize);
            int start = _pageIndex * ps;
            int end = start + ps;

            for (int i = 0; i < _orderedSlots.Count; i++)
            {
                var slot = _orderedSlots[i];
                if (slot) slot.gameObject.SetActive(i >= start && i < end);
            }

            if (prevPageButton) prevPageButton.interactable = _pageIndex > 0;
            if (nextPageButton) nextPageButton.interactable = _pageIndex < (TotalPages - 1);

            if (pageLabel)
            {
                pageLabel.text = $"{_pageIndex + 1}/{TotalPages}";
                pageLabel.gameObject.SetActive(TotalPages > 1);
            }
        }

        public void NextPage()
        {
            _pageIndex++;
            ApplyPaging();
        }

        public void PrevPage()
        {
            _pageIndex--;
            ApplyPaging();
        }

        private void BuildSlots()
        {
            for (int i = slotsContainer.childCount - 1; i >= 0; i--)
                Destroy(slotsContainer.GetChild(i).gameObject);

            _slots.Clear();
            _orderedSlots.Clear();

            // Determine styles source
            HexWorldTileStyle[] stylesToUse = styles;
            if (syncWithControllerCatalog && controller != null)
            {
                var catalog = controller.GetStyleCatalog();
                if (catalog != null && catalog.Length > 0)
                    stylesToUse = catalog;
            }

            if (stylesToUse == null || stylesToUse.Length == 0) return;

            var unlockedSet = new HashSet<HexWorldTileStyle>();
            if (!startAllUnlocked && initiallyUnlocked != null)
            {
                foreach (var s in initiallyUnlocked)
                    if (s) unlockedSet.Add(s);
            }

            foreach (var style in stylesToUse)
            {
                if (!style) continue;

                bool unlocked = startAllUnlocked || unlockedSet.Contains(style);

                var slot = Instantiate(slotPrefab, slotsContainer);
                slot.name = $"TileSlot_{(string.IsNullOrWhiteSpace(style.displayName) ? style.name : style.displayName)}";
                slot.Bind(style, unlocked);

                var capturedStyle = style;
                var capturedSlot = slot;

                capturedSlot.Button.onClick.RemoveAllListeners();
                capturedSlot.Button.onClick.AddListener(() =>
                {
                    if (!capturedSlot.IsUnlocked)
                    {
                        ShowToast("LOCKED");
                        return;
                    }

                    if (clickSelectedAgainToDeselect && controller.SelectedStyle == capturedStyle)
                        controller.SetSelectedStyle(null);
                    else
                        controller.SetSelectedStyle(capturedStyle);
                });

                _slots[capturedStyle] = capturedSlot;
                _orderedSlots.Add(capturedSlot);
            }

            // If selection moved to something not on this page, optionally snap to its page.
            SnapToSelectedIfNeeded(controller ? controller.SelectedStyle : null);

            ApplyPaging();
        }

        private void SnapToSelectedIfNeeded(HexWorldTileStyle selected)
        {
            if (!selected) return;
            if (!_slots.TryGetValue(selected, out var slot) || !slot) return;

            int idx = _orderedSlots.IndexOf(slot);
            if (idx < 0) return;

            int ps = Mathf.Max(1, pageSize);
            int wantedPage = idx / ps;
            if (wantedPage != _pageIndex)
                _pageIndex = wantedPage;
        }

        private void OnSelectedStyleChanged(HexWorldTileStyle selected)
        {
            foreach (var kv in _slots)
                if (kv.Value) kv.Value.SetSelectedVisual(kv.Key == selected);

            // If user selected something on a different page (e.g. from code), snap.
            SnapToSelectedIfNeeded(selected);
            ApplyPaging();
        }

        private void ShowToast(string msg)
        {
            if (!toastGroup || !toastText) return;

            toastText.text = msg;

            if (_toastCo != null) StopCoroutine(_toastCo);
            _toastCo = StartCoroutine(ToastRoutine());
        }

        private IEnumerator ToastRoutine()
        {
            float t = 0f;
            while (t < toastFadeIn)
            {
                t += Time.unscaledDeltaTime;
                toastGroup.alpha = toastFadeIn <= 0f ? 1f : Mathf.Clamp01(t / toastFadeIn);
                yield return null;
            }

            toastGroup.alpha = 1f;

            float hold = 0f;
            while (hold < toastHold)
            {
                hold += Time.unscaledDeltaTime;
                yield return null;
            }

            float o = 0f;
            while (o < toastFadeOut)
            {
                o += Time.unscaledDeltaTime;
                toastGroup.alpha = 1f - (toastFadeOut <= 0f ? 1f : Mathf.Clamp01(o / toastFadeOut));
                yield return null;
            }

            toastGroup.alpha = 0f;
        }

        // You’ll use this later when you unlock tiles via progression.
        public void SetUnlocked(HexWorldTileStyle style, bool unlocked)
        {
            if (!_slots.TryGetValue(style, out var slot) || !slot) return;
            slot.SetUnlocked(unlocked);
        }
    }
}
