using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace GalacticFishing.UI
{
    /// <summary>
    /// Simple front/back upgrade card.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UpgradeCardView : MonoBehaviour, IPointerClickHandler
    {
        [Header("Face roots")]
        [SerializeField] private GameObject backRoot;
        [SerializeField] private GameObject frontRoot;

        [Header("Front visuals")]
        [SerializeField] private Image artImage;

        [Header("Text on front (optional)")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text valueText;
        [SerializeField] private TMP_Text descriptionText;

        [Header("Behaviour")]
        [Tooltip("If true, once revealed the card can never be flipped back.")]
        [SerializeField] private bool oneWayReveal = true;

        // Fired the first time the player reveals this card (per instance).
        [SerializeField] private UnityEvent onRevealed = new UnityEvent();

        private bool _isRevealed;

        // ------------------------------------------------------------
        // API used by UpgradeDeckLauncher
        // ------------------------------------------------------------

        public void ApplyDefinition(Sprite art, string title, string value, string description, bool startRevealed)
        {
            if (artImage)        artImage.sprite      = art;
            if (titleText)       titleText.text       = title       ?? string.Empty;
            if (valueText)       valueText.text       = value       ?? string.Empty;
            if (descriptionText) descriptionText.text = description ?? string.Empty;

            _isRevealed = startRevealed;
            UpdateFace();
        }

        public void Clear()
        {
            if (artImage)        artImage.sprite = null;
            if (titleText)       titleText.text = string.Empty;
            if (valueText)       valueText.text = string.Empty;
            if (descriptionText) descriptionText.text = string.Empty;

            _isRevealed = false;
            UpdateFace();
        }

        public void ForceReveal(bool reveal)
        {
            _isRevealed = reveal;
            UpdateFace();
        }

        // Allow other scripts (UpgradeCardUnlockRecord) to hook the reveal event.
        public void RegisterRevealListener(UnityAction listener)
        {
            if (listener != null)
                onRevealed.AddListener(listener);
        }

        // ------------------------------------------------------------
        // Click handling
        // ------------------------------------------------------------

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_isRevealed)
            {
                _isRevealed = true;
                UpdateFace();
                onRevealed?.Invoke();
            }
            else if (!oneWayReveal)
            {
                _isRevealed = false;
                UpdateFace();
            }
        }

        // ------------------------------------------------------------

        private void UpdateFace()
        {
            if (backRoot)  backRoot.SetActive(!_isRevealed);
            if (frontRoot) frontRoot.SetActive(_isRevealed);
        }
    }
}
