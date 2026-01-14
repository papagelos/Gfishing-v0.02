using System.Collections.Generic;
using UnityEngine;

namespace GalacticFishing.UI
{
    /// <summary>
    /// Owns ONE upgrade deck (Rod, Boat, etc).
    /// When opened:
    /// - Destroys any existing card slots under cardGridRoot.
    /// - Rebuilds exactly maxSlots slots from Slot_Card_Prefab.
    /// - Strips InventorySlot so inventory code cannot touch them.
    /// - Pushes this deck's definitions into the slots.
    /// - Looks up saved "unlocked" state per card and starts them revealed.
    /// - Shows THIS deck root, hides the others.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UpgradeDeckLauncher : MonoBehaviour
    {
        // --------------------------------------------------------------------
        // Card data
        // --------------------------------------------------------------------

        [System.Serializable]
        public class CardDefinition
        {
            [Header("Visuals")]
            public Sprite artSprite;

            [Header("Texts")]
            public string title;
            public string value;
            [TextArea(2, 4)] public string description;

            [Header("State")]
            [Tooltip("If true, this card starts revealed even on a fresh save.")]
            public bool startRevealed;
        }

        // --------------------------------------------------------------------
        // Inspector refs
        // --------------------------------------------------------------------

        [Header("Save / Deck Id")]
        [Tooltip("Logical id used for saving progress, e.g. 'Rod', 'Boat'.")]
        [SerializeField] private string deckId = "Rod";

        [Header("Scene references")]
        [Tooltip("MenuRouter on UIController in the scene.")]
        [SerializeField] private MenuRouter menuRouter;

        [Header("Deck visibility")]
        [Tooltip("Root object for THIS deck (e.g. CardContent_RodUpgrades).")]
        [SerializeField] private GameObject thisDeckRoot;

        [Tooltip("Other deck roots that should be hidden when this one opens " +
                 "(e.g. CardContent_BoatUpgrades).")]
        [SerializeField] private List<GameObject> otherDeckRoots = new();

        [Header("Card grid")]
        [Tooltip("The RectTransform that holds the card slots for THIS deck.")]
        [SerializeField] private RectTransform cardGridRoot;

        [Tooltip("Prefab that has UpgradeCardView (Slot_Card_Prefab).")]
        [SerializeField] private UpgradeCardView slotPrefab;

        [Header("Layout")]
        [Tooltip("Number of slots to build for this deck.")]
        [SerializeField] private int maxSlots = 10;

        [Header("Deck contents (this category only)")]
        [SerializeField] private List<CardDefinition> cards = new List<CardDefinition>();

        // Runtime cache of the slots for the *current* open.
        private readonly List<UpgradeCardView> _slots = new List<UpgradeCardView>();

        // --------------------------------------------------------------------
        // Public API – called from HubPanel (Rod / Boat buttons)
        // --------------------------------------------------------------------

        /// <summary>
        /// Button hook from HubPanel (Rod / Boat).
        /// </summary>
        public void OpenThisDeck()
        {
            // 1) Make sure we are in the Upgrade Cards screen.
            if (menuRouter != null)
            {
                menuRouter.OpenUpgradeCards();
            }

            // 2) Make THIS deck visible.
            if (thisDeckRoot != null)
                thisDeckRoot.SetActive(true);

            if (cardGridRoot != null)
                cardGridRoot.gameObject.SetActive(true);

            // 3) Hide other decks so they don't sit on top.
            if (otherDeckRoots != null)
            {
                for (int i = 0; i < otherDeckRoots.Count; i++)
                {
                    var go = otherDeckRoots[i];
                    if (go && go != thisDeckRoot)
                        go.SetActive(false);
                }
            }

            // 4) Rebuild slots fresh and apply data.
            RebuildSlots();
            ApplyDefinitionsToSlots();

#if UNITY_EDITOR
            Debug.Log(
                $"[UpgradeDeckLauncher] OpenThisDeck on '{name}' (deckId='{deckId}') -> cards={cards?.Count ?? 0}, slots={_slots.Count}",
                this);
#endif
        }

        // --------------------------------------------------------------------
        // Slot management – REBUILD EVERY TIME
        // --------------------------------------------------------------------

        /// <summary>
        /// Completely clears and rebuilds the slots under cardGridRoot.
        /// Guarantees we never get 20 slots, weird leftovers, etc.
        /// </summary>
        private void RebuildSlots()
        {
            _slots.Clear();

            if (!cardGridRoot)
            {
                Debug.LogWarning(
                    $"[UpgradeDeckLauncher] No cardGridRoot assigned on '{name}'.",
                    this);
                return;
            }

            // Destroy any children currently under the grid (editor or runtime).
            for (int i = cardGridRoot.childCount - 1; i >= 0; i--)
            {
                var child = cardGridRoot.GetChild(i);
                if (!child) continue;

                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }

            if (!slotPrefab)
            {
                Debug.LogWarning(
                    $"[UpgradeDeckLauncher] No slotPrefab set on '{name}'. " +
                    "Cannot build upgrade deck.",
                    this);
                return;
            }

            for (int i = 0; i < maxSlots; i++)
            {
                var slot = Instantiate(slotPrefab, cardGridRoot);
                slot.gameObject.name = $"Slot_{i:000}";
                StripInventoryBehaviour(slot);
                _slots.Add(slot);
            }
        }

        /// <summary>
        /// Remove InventorySlot from this view so the normal inventory
        /// system can't hijack texts / clicks.
        /// (Runtime-only; the prefab in the project stays unchanged.)
        /// </summary>
        private void StripInventoryBehaviour(UpgradeCardView view)
        {
            if (!view) return;

            var invSlot = view.GetComponent<InventorySlot>();
            if (invSlot != null)
            {
                Destroy(invSlot);
            }
        }

        // --------------------------------------------------------------------
        // Data → UI  (+ progress lookup)
        // --------------------------------------------------------------------

        private void ApplyDefinitionsToSlots()
        {
            if (_slots.Count == 0)
            {
                Debug.LogWarning(
                    $"[UpgradeDeckLauncher] No UpgradeCardView slots available under '{cardGridRoot?.name}' " +
                    $"for deck '{name}'.",
                    this);
                return;
            }

            int cardCount = cards != null ? cards.Count : 0;
            int used      = Mathf.Min(_slots.Count, cardCount);

            // Load saved unlock state for this deck (may be null for fresh save).
            var unlocked = UpgradeCardProgress.LoadDeck(deckId, cardCount);

            // Fill used slots
            for (int i = 0; i < used; i++)
            {
                var view = _slots[i];
                if (view == null) continue;

                var def = cards[i];

                bool isUnlocked =
                    def.startRevealed ||
                    (unlocked != null && i < unlocked.Length && unlocked[i]);

                view.ApplyDefinition(
                    def.artSprite,
                    def.title,
                    def.value,
                    def.description,
                    isUnlocked);

                // Make sure the recorder knows which deck/card this slot represents.
                var recorder = view.GetComponent<UpgradeCardUnlockRecord>();
                if (recorder != null)
                {
                    recorder.Configure(deckId, i);
                }
            }

            // Clear extra slots
            for (int i = used; i < _slots.Count; i++)
            {
                var view = _slots[i];
                if (view == null) continue;

                view.Clear();

                var recorder = view.GetComponent<UpgradeCardUnlockRecord>();
                if (recorder != null)
                {
                    recorder.Configure(deckId, -1);
                }
            }
        }
    }
}
