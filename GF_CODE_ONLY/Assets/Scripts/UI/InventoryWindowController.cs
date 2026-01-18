using UnityEngine;
using UnityEngine.InputSystem;   // new input system keyboard API

public class InventoryWindowController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject windowRoot;
    [SerializeField] private InventoryGridController grid;

    [Header("Background (optional)")]
    [Tooltip("CanvasGroup on the 'Inventory-background' object.")]
    [SerializeField] private CanvasGroup inventoryBackgroundGroup;

    [Header("Startup")]
    [SerializeField] private bool startOpen = false;

    [Header("Hotkey")]
    [SerializeField] private bool enableHotkey = true;
    [SerializeField] private Key hotkey = Key.I;

    private bool initialized;
    private bool _visible;

    private void Awake()
    {
        // Fallbacks so we never end up with a null root
        if (!windowRoot)
        {
            if (inventoryBackgroundGroup != null)
                windowRoot = inventoryBackgroundGroup.gameObject;
            else
                windowRoot = gameObject;
        }

        _visible = startOpen;

        Debug.Log("[InventoryWindowController] Awake on " + gameObject.name);
        ApplyVisibility(_visible);
    }

    private void OnEnable()
    {
        Debug.Log("[InventoryWindowController] OnEnable on " + gameObject.name);
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null)
        {
            // This would mean the new Input System isn't active at all.
            return;
        }

        if (!enableHotkey)
            return;

        // WATCH: I key (or whatever is set in the inspector)
        if (kb[hotkey].wasPressedThisFrame)
        {
            Debug.Log("[InventoryWindowController] Hotkey pressed: " + hotkey + " on " + gameObject.name);
            Toggle();
        }
    }

    // ---------- PUBLIC API (used by panel_hub button) ----------

    public void Show()  => ApplyVisibility(true);
    public void Hide()  => ApplyVisibility(false);

    /// <summary>
    /// Toggle between open/closed using our own _visible flag.
    /// This avoids depending on GameObject.activeSelf, which may not
    /// reflect whether the CanvasGroup is currently visible.
    /// </summary>
    public void Toggle()
    {
        ApplyVisibility(!_visible);
    }

    // ---------- INTERNAL ----------

    private void ApplyVisibility(bool visible)
    {
        _visible = visible;

        if (!windowRoot)
        {
            if (inventoryBackgroundGroup != null)
                windowRoot = inventoryBackgroundGroup.gameObject;
            else
                windowRoot = gameObject;
        }

        Debug.Log("[InventoryWindowController] ApplyVisibility(" + visible + ") on " + windowRoot.name);

        // Turn the whole window root on/off so OnEnable/OnDisable fire on children
        windowRoot.SetActive(visible);

        // Background CanvasGroup (if present) still controls fade and raycasts
        if (inventoryBackgroundGroup != null)
        {
            inventoryBackgroundGroup.alpha          = visible ? 1f : 0f;
            inventoryBackgroundGroup.interactable   = visible;
            inventoryBackgroundGroup.blocksRaycasts = visible;
        }

        // One-time grid initialization
        if (visible && !initialized && grid != null)
        {
            if (grid.FishRegistry && !InventoryService.IsInitialized)
                InventoryService.Initialize(grid.FishRegistry);

            grid.Populate();
            initialized = true;
        }
    }
}
