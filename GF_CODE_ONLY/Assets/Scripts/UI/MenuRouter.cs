using UnityEngine;
using UnityEngine.InputSystem;
using GalacticFishing.UI;

public class MenuRouter : MonoBehaviour
{
    [Header("Inventory Window")]
    [SerializeField] private InventoryWindowController inventoryWindow;

    [Header("Inventory Views (inside Inventory-background)")]
    [Tooltip("The root that shows the normal 13x6 inventory view.")]
    [SerializeField] private GameObject contentFrame;

    [Tooltip("The root that shows the 2x4 card/upgrade view (CardviewRoot).")]
    [SerializeField] private GameObject cardviewRoot;

    [Header("Other UI Roots (optional)")]
    [SerializeField] private GameObject museumPanel;  // MuseumPanel (if used)

    [Header("Encyclopedia (optional)")]
    [Tooltip("Root GameObject of the Fish Encyclopedia inside Inventory-background.")]
    [SerializeField] private GameObject fishEncyclopediaPanel;

    [Header("Hub (optional)")]
    [SerializeField] private FullscreenHubController hubController;

    [Header("World Records (optional)")]
    [SerializeField] private GameObject worldRecordPanel;   // WorldRecordPanel root

    [Header("Workshop (optional)")]
    [Tooltip("Root GameObject of the Workshop panel (WorkshopPanel).")]
    [SerializeField] private GameObject workshopPanel;

    [Header("Blur Background (optional)")]
    [Tooltip("Root object that contains your fullscreen blur RawImage + optional tint overlay.")]
    [SerializeField] private GameObject menuBlurRoot;

    [Tooltip("Reference to the MenuBlurBackground component (the script on the RawImage).")]
    [SerializeField] private MenuBlurBackground menuBlur;

    [Header("World restore (optional)")]
    [Tooltip("World objects whose active state should be restored whenever we return fully to gameplay. " +
             "Example: BackdropWorld, WaterSurface, UIRoot, etc.")]
    [SerializeField] private GameObject[] worldObjects;

    // --------------------------------------------------------------------
    // Internal state
    // --------------------------------------------------------------------

    // Inventory-type state
    private bool inventoryOpen;          // is the inventory window currently open?
    private bool inventoryOpenedFromHub; // did we open it from Panel_Hub?
    private bool museumInventoryOpen;    // Museum + cards open together? (legacy MuseumSell path)

    // Generic content panel (world art + floating deck, etc.)
    // Example: RodUpgradesPanel, future mini-game panels.
    private GameObject _currentPanel;
    private bool genericPanelOpenedFromHub;

    // Startup active state of worldObjects
    private bool[] _worldInitialActive;

    // Just for debugging / clarity
    private enum MenuScreen
    {
        Gameplay,
        InventoryGrid,
        UpgradeCards,
        Museum, // also used as "generic content panel" for debug
        Hub
    }

    [SerializeField]
    private MenuScreen currentScreen = MenuScreen.Gameplay;

    private void Awake()
    {
        // HARD RESET: avoid "RMB dead until ESC" on boot.
        UIBlocksGameplay.GameplayBlocked = false;

        inventoryOpen             = false;
        inventoryOpenedFromHub    = false;
        museumInventoryOpen       = false;
        _currentPanel             = null;
        genericPanelOpenedFromHub = false;

        // Make sure inventory views are NOT active at boot.
        if (contentFrame != null)
            contentFrame.SetActive(false);

        if (cardviewRoot != null)
            cardviewRoot.SetActive(false);

        if (fishEncyclopediaPanel != null)
            fishEncyclopediaPanel.SetActive(false);

        if (museumPanel != null)
            museumPanel.SetActive(false);

        // Ensure Workshop is hidden on startup (we open it via hub)
        if (workshopPanel != null)
            workshopPanel.SetActive(false);

        if (worldRecordPanel != null)
            worldRecordPanel.SetActive(false);

        // Ensure blur is hidden on startup
        if (menuBlurRoot != null)
            menuBlurRoot.SetActive(false);

        // If InventoryWindowController uses CanvasGroup, hide it too.
        if (inventoryWindow != null)
            inventoryWindow.Hide();

        CacheWorldInitialStates();

        // Make MenuRouter authoritative for GameplayBlocked from frame 0.
        SyncGameplayBlocked();
    }

    private void CacheWorldInitialStates()
    {
        if (worldObjects == null || worldObjects.Length == 0)
        {
            _worldInitialActive = null;
            return;
        }

        _worldInitialActive = new bool[worldObjects.Length];
        for (int i = 0; i < worldObjects.Length; i++)
        {
            var go = worldObjects[i];
            _worldInitialActive[i] = (go != null) && go.activeSelf;
        }
    }

    private void RestoreWorldInitialStates()
    {
        if (_worldInitialActive == null || worldObjects == null)
            return;

        int len = Mathf.Min(worldObjects.Length, _worldInitialActive.Length);
        for (int i = 0; i < len; i++)
        {
            var go = worldObjects[i];
            if (!go) continue;
            go.SetActive(_worldInitialActive[i]);
        }
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null)
            return;

        // ESC = hard close → world
        if (kb[Key.Escape].wasPressedThisFrame)
        {
            Debug.Log("[MenuRouter] ESC -> CloseAllAndReturnToGameplay()");
            CloseAllAndReturnToGameplay();
        }

        // Backspace = one step back (like clicking a Back button)
        if (kb[Key.Backspace].wasPressedThisFrame)
        {
            Debug.Log("[MenuRouter] Backspace -> Back()");
            Back();
        }

        // Keep GameplayBlocked correct even if some other script sets it incorrectly.
        SyncGameplayBlocked();
    }

    /// <summary>
    /// Authoritative "are we inside a non-world menu?" signal.
    /// IMPORTANT: do NOT use contentFrame/cardviewRoot activeSelf as blockers,
    /// because many UI systems keep them active even when visually hidden.
    /// </summary>
    private void SyncGameplayBlocked()
    {
        bool blocked =
            inventoryOpen ||
            museumInventoryOpen ||
            (_currentPanel != null && _currentPanel.activeSelf);

        UIBlocksGameplay.GameplayBlocked = blocked;
    }

    // --------------------------------------------------------------------
    // Blur helpers
    // --------------------------------------------------------------------

    private void ShowBlur()
    {
        if (menuBlurRoot != null && !menuBlurRoot.activeSelf)
            menuBlurRoot.SetActive(true);

        if (menuBlur != null)
            menuBlur.Capture();
    }

    private void HideBlur()
    {
        if (menuBlurRoot != null && menuBlurRoot.activeSelf)
            menuBlurRoot.SetActive(false);
    }

    // --------------------------------------------------------------------
    // PUBLIC API – called from buttons / quickbar / hub
    // --------------------------------------------------------------------

    public void OpenInventoryFromHub()
    {
        inventoryOpenedFromHub = true;
        OpenInventoryGrid();
    }

    public void OpenInventoryFromWorld()
    {
        inventoryOpenedFromHub = false;
        OpenInventoryGrid();
    }

    public void OpenUpgradeCardsFromWorld()
    {
        inventoryOpenedFromHub = false;
        OpenUpgradeCards();
    }

    public void OpenUpgradeCardsFromHub()
    {
        inventoryOpenedFromHub = true;
        OpenUpgradeCards();
    }

    public void OpenWorldRecordsFromHub()
    {
        if (worldRecordPanel == null)
        {
            Debug.LogWarning("[MenuRouter] OpenWorldRecordsFromHub: worldRecordPanel not assigned.");
            return;
        }

        SwitchPanelFromHub(worldRecordPanel);

        var listView = worldRecordPanel.GetComponent<WorldRecordListView>();
        if (listView != null)
            listView.Show();
    }

    public void OpenWorkshopFromHub()
    {
        if (workshopPanel == null)
        {
            Debug.LogWarning("[MenuRouter] OpenWorkshopFromHub: workshopPanel not assigned.");
            return;
        }

        SwitchPanelFromHub(workshopPanel);
    }

    public void OpenFishEncyclopediaFromHub()
    {
        inventoryOpenedFromHub = true;
        Debug.Log("[MenuRouter] OpenFishEncyclopediaFromHub()");

        if (hubController != null && hubController.IsOpen)
            hubController.ForceClosedImmediate();

        if (_currentPanel != null)
        {
            _currentPanel.SetActive(false);
            _currentPanel = null;
            genericPanelOpenedFromHub = false;
        }

        if (museumPanel != null)
            museumPanel.SetActive(false);

        ShowBlur();

        if (contentFrame != null)
            contentFrame.SetActive(false);

        if (cardviewRoot != null)
            cardviewRoot.SetActive(false);

        if (fishEncyclopediaPanel != null)
            fishEncyclopediaPanel.SetActive(true);

        inventoryOpen       = true;
        museumInventoryOpen = false;
        currentScreen       = MenuScreen.InventoryGrid;

        SyncGameplayBlocked();
    }

    public void SwitchPanelFromHub(GameObject panelRoot) => SwitchPanelInternal(panelRoot, true);
    public void SwitchPanelFromWorld(GameObject panelRoot) => SwitchPanelInternal(panelRoot, false);

    private void SwitchPanelInternal(GameObject panelRoot, bool openedFromHub)
    {
        if (panelRoot == null)
            return;

        if (_currentPanel == panelRoot && panelRoot.activeSelf)
            return;

        Debug.Log($"[MenuRouter] SwitchPanelInternal -> {panelRoot.name} (fromHub={openedFromHub})");

        if (hubController != null && hubController.IsOpen)
            hubController.ForceClosedImmediate();

        CloseInventory();

        if (_currentPanel != null && _currentPanel != panelRoot)
            _currentPanel.SetActive(false);

        ShowBlur();

        panelRoot.SetActive(true);

        _currentPanel = panelRoot;
        genericPanelOpenedFromHub = openedFromHub;
        currentScreen = MenuScreen.Museum;

        SyncGameplayBlocked();
    }

    public void OpenInventoryGrid()
    {
        Debug.Log("[MenuRouter] OpenInventoryGrid()");

        if (hubController != null && hubController.IsOpen)
            hubController.ForceClosedImmediate();

        if (_currentPanel != null)
        {
            _currentPanel.SetActive(false);
            _currentPanel = null;
            genericPanelOpenedFromHub = false;
        }

        if (museumPanel != null)
            museumPanel.SetActive(false);

        ShowBlur();

        if (inventoryWindow != null)
            inventoryWindow.Show();

        if (contentFrame != null)
            contentFrame.SetActive(true);

        if (cardviewRoot != null)
            cardviewRoot.SetActive(false);

        if (fishEncyclopediaPanel != null)
            fishEncyclopediaPanel.SetActive(false);

        inventoryOpen       = true;
        museumInventoryOpen = false;
        currentScreen       = MenuScreen.InventoryGrid;

        SyncGameplayBlocked();
    }

    public void OpenUpgradeCards()
    {
        Debug.Log("[MenuRouter] OpenUpgradeCards()");

        if (hubController != null && hubController.IsOpen)
            hubController.ForceClosedImmediate();

        if (_currentPanel != null)
        {
            _currentPanel.SetActive(false);
            _currentPanel = null;
            genericPanelOpenedFromHub = false;
        }

        if (museumPanel != null)
            museumPanel.SetActive(false);

        ShowBlur();

        if (inventoryWindow != null)
            inventoryWindow.Show();

        if (contentFrame != null)
            contentFrame.SetActive(false);

        if (cardviewRoot != null)
            cardviewRoot.SetActive(true);

        if (fishEncyclopediaPanel != null)
            fishEncyclopediaPanel.SetActive(false);

        inventoryOpen       = true;
        museumInventoryOpen = false;
        currentScreen       = MenuScreen.UpgradeCards;

        SyncGameplayBlocked();
    }

    public void OpenMuseumSell()
    {
        museumInventoryOpen = true;

        if (_currentPanel != null)
        {
            _currentPanel.SetActive(false);
            _currentPanel = null;
        }
        genericPanelOpenedFromHub = false;

        if (hubController != null && hubController.IsOpen)
            hubController.ForceClosedImmediate();

        ShowBlur();

        if (museumPanel != null)
            museumPanel.SetActive(true);

        if (inventoryWindow != null)
            inventoryWindow.Show();

        if (contentFrame != null)
            contentFrame.SetActive(false);

        if (cardviewRoot != null)
            cardviewRoot.SetActive(true);

        if (fishEncyclopediaPanel != null)
            fishEncyclopediaPanel.SetActive(false);

        inventoryOpen = true;
        currentScreen = MenuScreen.Museum;

        SyncGameplayBlocked();
    }

    // --------------------------------------------------------------------
    // ESC hard-close path
    // --------------------------------------------------------------------

    private void CloseAllAndReturnToGameplay()
    {
        Debug.Log("[MenuRouter] CloseAllAndReturnToGameplay()");

        CloseInventory();

        if (museumPanel != null)
            museumPanel.SetActive(false);

        if (_currentPanel != null)
        {
            _currentPanel.SetActive(false);
            _currentPanel = null;
        }

        if (workshopPanel != null)
            workshopPanel.SetActive(false);

        if (worldRecordPanel != null)
            worldRecordPanel.SetActive(false);

        if (hubController != null)
            hubController.ForceClosedImmediate();

        inventoryOpen             = false;
        inventoryOpenedFromHub    = false;
        museumInventoryOpen       = false;
        genericPanelOpenedFromHub = false;
        currentScreen             = MenuScreen.Gameplay;

        ResetGameplayState();
    }

    private void ResetGameplayState()
    {
        HideBlur();

        if (Time.timeScale != 1f)
            Time.timeScale = 1f;

        UIBlocksGameplay.GameplayBlocked = false;

        RestoreWorldInitialStates();

        SyncGameplayBlocked();
    }

    public void Back()
    {
        if (_currentPanel != null && _currentPanel.activeSelf)
        {
            _currentPanel.SetActive(false);
            _currentPanel = null;

            SyncGameplayBlocked();

            if (genericPanelOpenedFromHub && hubController != null)
            {
                HideBlur();
                hubController.ForceOpenImmediate();
                currentScreen = MenuScreen.Hub;
            }
            else
            {
                currentScreen = MenuScreen.Gameplay;
                ResetGameplayState();
            }

            genericPanelOpenedFromHub = false;
            return;
        }

        if (museumInventoryOpen)
        {
            museumInventoryOpen = false;

            CloseInventory();

            if (museumPanel != null)
                museumPanel.SetActive(false);

            SyncGameplayBlocked();

            if (hubController != null)
            {
                HideBlur();
                hubController.ForceOpenImmediate();
            }

            currentScreen = MenuScreen.Hub;
            return;
        }

        if (inventoryOpen)
        {
            Debug.Log("[MenuRouter] Back: closing inventory/upgrade/encyclopedia view, returning to hub or game");

            bool fromUpgradeCards = (currentScreen == MenuScreen.UpgradeCards);

            CloseInventory();
            SyncGameplayBlocked();

            if ((inventoryOpenedFromHub || fromUpgradeCards) && hubController != null)
            {
                HideBlur();
                hubController.ForceOpenImmediate();
                currentScreen = MenuScreen.Hub;
            }
            else
            {
                currentScreen = MenuScreen.Gameplay;
                ResetGameplayState();
            }

            return;
        }

        if (museumPanel != null && museumPanel.activeSelf)
        {
            museumPanel.SetActive(false);
            currentScreen = MenuScreen.Gameplay;
            ResetGameplayState();
            return;
        }

        if (hubController != null && hubController.IsOpen)
        {
            hubController.ForceClosedImmediate();
            currentScreen = MenuScreen.Gameplay;
            ResetGameplayState();
            return;
        }

        SyncGameplayBlocked();
    }

    private void CloseInventory()
    {
        if (inventoryWindow != null)
            inventoryWindow.Hide();

        inventoryOpen = false;

        if (contentFrame != null)
            contentFrame.SetActive(false);

        if (cardviewRoot != null)
            cardviewRoot.SetActive(false);

        if (fishEncyclopediaPanel != null)
            fishEncyclopediaPanel.SetActive(false);

        SyncGameplayBlocked();
    }
}
