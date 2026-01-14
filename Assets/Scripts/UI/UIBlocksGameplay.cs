using UnityEngine;

namespace GalacticFishing.UI
{
    [DisallowMultipleComponent]
    public class UIBlocksGameplay : MonoBehaviour
    {
        [Header("Detect inventory visibility (optional)")]
        public CanvasGroup inventoryCanvasGroup;   // drag the Inventory-background CanvasGroup here

        [Header("Disable while inventory is open")]
        public GameObject[] objectsToDisable;      // drag WHOLE scene objects here (HookCard, ReactionBar, etc.)
        public Behaviour[] behavioursToDisable;    // or drag specific components (e.g., HookCardService)

        [Header("Optional global flag other scripts can read")]
        public bool setGlobalBlockFlag = true;
        public static bool GameplayBlocked;

        bool _lastOpen;

        void Awake()
        {
            if (!inventoryCanvasGroup)
                inventoryCanvasGroup = GetComponent<CanvasGroup>();
        }

        void OnEnable()  => ApplyIfChanged();
        void Update()    => ApplyIfChanged();

        void ApplyIfChanged()
        {
            bool open = IsInventoryOpen();
            if (open == _lastOpen) return;
            _lastOpen = open;

            if (objectsToDisable != null)
                foreach (var go in objectsToDisable) if (go) go.SetActive(!open);

            if (behavioursToDisable != null)
                foreach (var b in behavioursToDisable) if (b) b.enabled = !open;

            if (setGlobalBlockFlag) GameplayBlocked = open;
        }

        bool IsInventoryOpen()
        {
            if (!gameObject.activeInHierarchy) return false;
            if (inventoryCanvasGroup)
                return inventoryCanvasGroup.alpha > 0.5f && inventoryCanvasGroup.blocksRaycasts;
            // fallback if no CanvasGroup was set
            return true;
        }
    }
}
