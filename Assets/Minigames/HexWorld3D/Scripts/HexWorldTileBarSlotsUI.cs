// Assets/Minigames/HexWorld3D/Scripts/HexWorldTileBarSlotsUI.cs
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace GalacticFishing.Minigames.HexWorld
{
    public sealed class HexWorldTileBarSlotsUI : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private HexWorld3DController controller;
        [SerializeField] private RectTransform slotsContainer;
        [SerializeField] private HexWorldTileSlotUI slotPrefab;

        [Header("Tile styles (order = UI order)")]
        [SerializeField] private HexWorldTileStyle[] styles;

        [Header("Unlocking (future-proof)")]
        [SerializeField] private bool startAllUnlocked = true;
        [SerializeField] private HexWorldTileStyle[] initiallyUnlocked;

        [Header("Selection UX")]
        [Tooltip("If ON: clicking the currently selected style again will deselect (normal cursor, no ghost tile).")]
        [SerializeField] private bool clickSelectedAgainToDeselect = true;

        [Header("Toast (optional)")]
        [SerializeField] private CanvasGroup toastGroup;
        [SerializeField] private TMP_Text toastText;
        [SerializeField] private float toastFadeIn = 0.08f;
        [SerializeField] private float toastHold = 0.9f;
        [SerializeField] private float toastFadeOut = 0.2f;

        private readonly Dictionary<HexWorldTileStyle, HexWorldTileSlotUI> _slots = new();
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

        private void BuildSlots()
        {
            for (int i = slotsContainer.childCount - 1; i >= 0; i--)
                Destroy(slotsContainer.GetChild(i).gameObject);

            _slots.Clear();

            if (styles == null) return;

            var unlockedSet = new HashSet<HexWorldTileStyle>();
            if (!startAllUnlocked && initiallyUnlocked != null)
            {
                foreach (var s in initiallyUnlocked)
                    if (s) unlockedSet.Add(s);
            }

            foreach (var style in styles)
            {
                if (!style) continue;

                bool unlocked = startAllUnlocked || unlockedSet.Contains(style);

                var slot = Instantiate(slotPrefab, slotsContainer);
                slot.name = $"TileSlot_{(string.IsNullOrWhiteSpace(style.displayName) ? style.name : style.displayName)}";
                slot.Bind(style, unlocked);

                // IMPORTANT: capture locals per-iteration (avoid closure issues)
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

                    // Toggle: clicking same style again deselects
                    if (clickSelectedAgainToDeselect && controller.SelectedStyle == capturedStyle)
                        controller.SetSelectedStyle(null);
                    else
                        controller.SetSelectedStyle(capturedStyle);
                });

                _slots[capturedStyle] = capturedSlot;
            }
        }

        private void OnSelectedStyleChanged(HexWorldTileStyle selected)
        {
            foreach (var kv in _slots)
                if (kv.Value) kv.Value.SetSelectedVisual(kv.Key == selected);
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
