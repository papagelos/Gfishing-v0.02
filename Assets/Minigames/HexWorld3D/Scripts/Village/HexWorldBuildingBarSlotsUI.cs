// Assets/Minigames/HexWorld3D/Scripts/HexWorldBuildingBarSlotsUI.cs
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace GalacticFishing.Minigames.HexWorld
{
    public sealed class HexWorldBuildingBarSlotsUI : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private HexWorld3DController controller;
        [SerializeField] private RectTransform slotsContainer;
        [SerializeField] private HexWorldBuildingSlotUI slotPrefab;

        [Header("Buildings (order = UI order)")]
        [SerializeField] private HexWorldBuildingDefinition[] buildings;

        [Header("Unlocking (future-proof)")]
        [SerializeField] private bool startAllUnlocked = true;
        [SerializeField] private HexWorldBuildingDefinition[] initiallyUnlocked;

        [Header("Selection UX")]
        [Tooltip("If ON: clicking the currently selected building again will deselect.")]
        [SerializeField] private bool clickSelectedAgainToDeselect = true;

        [Header("Toast (optional)")]
        [SerializeField] private CanvasGroup toastGroup;
        [SerializeField] private TMP_Text toastText;
        [SerializeField] private float toastFadeIn = 0.08f;
        [SerializeField] private float toastHold = 0.9f;
        [SerializeField] private float toastFadeOut = 0.2f;

        private readonly Dictionary<HexWorldBuildingDefinition, HexWorldBuildingSlotUI> _slots = new();
        private Coroutine _toastCo;

        private void Awake()
        {
            if (toastGroup)
            {
                toastGroup.alpha = 0f;
                toastGroup.interactable = false;
                toastGroup.blocksRaycasts = false;
            }

            if (!toastText && toastGroup)
                toastText = toastGroup.GetComponentInChildren<TMP_Text>(true);
        }

        private void Start()
        {
            if (!controller)
            {
                Debug.LogError("HexWorldBuildingBarSlotsUI: controller missing.");
                enabled = false;
                return;
            }
            if (!slotsContainer)
            {
                Debug.LogError("HexWorldBuildingBarSlotsUI: slotsContainer missing.");
                enabled = false;
                return;
            }
            if (!slotPrefab)
            {
                Debug.LogError("HexWorldBuildingBarSlotsUI: slotPrefab missing.");
                enabled = false;
                return;
            }

            BuildSlots();

            // Requires controller additions:
            // - public event Action<HexWorldBuildingDefinition> SelectedBuildingChanged;
            // - public HexWorldBuildingDefinition SelectedBuilding { get; }
            // - public void SetSelectedBuilding(HexWorldBuildingDefinition def)
            controller.SelectedBuildingChanged += OnSelectedBuildingChanged;
            controller.ToastRequested += ShowToast;

            OnSelectedBuildingChanged(controller.SelectedBuilding);
        }

        private void OnDestroy()
        {
            if (controller)
            {
                controller.SelectedBuildingChanged -= OnSelectedBuildingChanged;
                controller.ToastRequested -= ShowToast;
            }
        }

        private void BuildSlots()
        {
            for (int i = slotsContainer.childCount - 1; i >= 0; i--)
                Destroy(slotsContainer.GetChild(i).gameObject);

            _slots.Clear();

            if (buildings == null) return;

            var unlockedSet = new HashSet<HexWorldBuildingDefinition>();
            if (!startAllUnlocked && initiallyUnlocked != null)
            {
                foreach (var b in initiallyUnlocked)
                    if (b) unlockedSet.Add(b);
            }

            foreach (var def in buildings)
            {
                if (!def) continue;

                bool unlocked = startAllUnlocked || unlockedSet.Contains(def);

                var slot = Instantiate(slotPrefab, slotsContainer);
                slot.name = $"BuildingSlot_{(string.IsNullOrWhiteSpace(def.displayName) ? def.name : def.displayName)}";
                slot.Bind(def, unlocked);

                // Capture locals (avoid closure issues)
                var capturedDef = def;
                var capturedSlot = slot;

                capturedSlot.Button.onClick.RemoveAllListeners();
                capturedSlot.Button.onClick.AddListener(() =>
                {
                    if (!capturedSlot.IsUnlocked)
                    {
                        ShowToast("LOCKED");
                        return;
                    }

                    if (clickSelectedAgainToDeselect && controller.SelectedBuilding == capturedDef)
                        controller.SetSelectedBuilding(null);
                    else
                        controller.SetSelectedBuilding(capturedDef);
                });

                _slots[capturedDef] = capturedSlot;
            }
        }

        private void OnSelectedBuildingChanged(HexWorldBuildingDefinition selected)
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

        public void SetUnlocked(HexWorldBuildingDefinition def, bool unlocked)
        {
            if (!_slots.TryGetValue(def, out var slot) || !slot) return;
            slot.SetUnlocked(unlocked);
        }
    }
}
