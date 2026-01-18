using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GalacticFishing.Minigames.HexWorld
{
    public sealed class HexWorldTileSlotUI : MonoBehaviour
    {
        [Header("Wiring (auto-filled if left empty)")]
        [SerializeField] private Button button;
        [SerializeField] private Image frame;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text nameText;

        [Header("Optional locked overlay (CanvasGroup on child named 'Locked')")]
        [SerializeField] private CanvasGroup lockedGroup;
        [SerializeField] private TMP_Text lockedText;

        public HexWorldTileStyle Style { get; private set; }
        public bool IsUnlocked { get; private set; } = true;
        public Button Button => button;

        private void Awake()
        {
            // Button
            if (!button) button = GetComponent<Button>();
            if (!button) button = gameObject.AddComponent<Button>();

            var rootImage = GetComponent<Image>();
            if (button && button.targetGraphic == null && rootImage)
                button.targetGraphic = rootImage;

            // Children by name (matches your prefab)
            if (!frame) frame = transform.Find("Frame")?.GetComponent<Image>();
            if (!icon) icon = transform.Find("Icon")?.GetComponent<Image>();
            if (!nameText) nameText = transform.Find("TileNameText")?.GetComponent<TMP_Text>();

            // Optional locked overlay
            if (!lockedGroup)
                lockedGroup = transform.Find("Locked")?.GetComponent<CanvasGroup>();

            if (!lockedText && lockedGroup)
                lockedText = lockedGroup.GetComponentInChildren<TMP_Text>(true);

            ApplyLockedVisual();
        }

        public void Bind(HexWorldTileStyle style, bool unlocked)
        {
            Style = style;
            IsUnlocked = unlocked;

            if (icon)
            {
                icon.sprite = style ? style.thumbnail : null;
                icon.preserveAspect = true;
                icon.enabled = (icon.sprite != null);
            }

            if (nameText)
                nameText.text = style ? (string.IsNullOrWhiteSpace(style.displayName) ? style.name : style.displayName) : "";

            ApplyLockedVisual();
        }

        public void SetUnlocked(bool unlocked)
        {
            IsUnlocked = unlocked;
            ApplyLockedVisual();
        }

        public void SetSelectedVisual(bool selected)
        {
            transform.localScale = selected ? new Vector3(1.06f, 1.06f, 1f) : Vector3.one;

            // Optional: if you later want a stronger frame highlight, we can add it here.
            // For now, scale is enough and safe.
        }

        private void ApplyLockedVisual()
        {
            if (!lockedGroup) return;

            lockedGroup.alpha = IsUnlocked ? 0f : 1f;
            lockedGroup.blocksRaycasts = false;
            lockedGroup.interactable = false;

            if (lockedText && !IsUnlocked)
                lockedText.text = "LOCKED";
        }
    }
}
