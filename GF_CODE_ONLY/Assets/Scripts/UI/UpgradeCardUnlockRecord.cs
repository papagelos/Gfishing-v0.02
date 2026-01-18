using UnityEngine;
using UnityEngine.Events;

namespace GalacticFishing.UI
{
    /// <summary>
    /// Lives on Slot_Card_Prefab.
    /// Called by UpgradeDeckLauncher to know which deck/card index this slot
    /// represents, and listens to the card's "revealed" event to record unlock.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UpgradeCardUnlockRecord : MonoBehaviour
    {
        [Header("Optional overrides")]
        [Tooltip("Leave empty to use the deckId from UpgradeDeckLauncher.")]
        [SerializeField] private string deckIdOverride;

        [Tooltip("Leave -1 to use the index from UpgradeDeckLauncher.")]
        [SerializeField] private int cardIndexOverride = -1;

        private string _deckId;
        private int _cardIndex = -1;

        private UpgradeCardView _view;
        private bool _hooked;

        /// <summary>
        /// Called by UpgradeDeckLauncher when it fills the slots.
        /// </summary>
        public void Configure(string deckId, int index)
        {
            _deckId = string.IsNullOrEmpty(deckIdOverride) ? deckId : deckIdOverride;
            _cardIndex = (cardIndexOverride >= 0) ? cardIndexOverride : index;
        }

        private void Awake()
        {
            _view = GetComponent<UpgradeCardView>();
            if (_view != null)
            {
                _view.RegisterRevealListener(OnRevealed);
                _hooked = true;
            }
        }

        private void OnRevealed()
        {
            if (string.IsNullOrWhiteSpace(_deckId) || _cardIndex < 0)
                return;

            UpgradeCardProgress.MarkUnlocked(_deckId, _cardIndex);
        }
    }
}
