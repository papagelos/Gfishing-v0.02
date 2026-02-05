// Assets/Scripts/UI/HexWorldTopTaskbarToolkitController.cs
using System;
using UnityEngine;
using UnityEngine.UIElements;
using GalacticFishing.Minigames.HexWorld;
using GalacticFishing.Progress;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Top taskbar controller (UI Toolkit).
///
/// Spec alignment:
/// - Legacy "Quality Lab" is deprecated.
/// - BtnQualityLab now opens the Town Hall (TownInfraController).
/// - BtnMaterialQuality opens the Material Quality panel (MaterialQualityController).
/// - LabelIP shows Infrastructure Points (IP).
/// - LabelQP shows Unspent Quality Points (QP).
/// </summary>
public sealed class HexWorldTopTaskbarToolkitController : MonoBehaviour
{
    [Header("Data Sources")]
    [SerializeField] private MonoBehaviour controllerBehaviour; // assign your HexWorld3DController here
    [SerializeField] private MonoBehaviour warehouseBehaviour;  // assign your Warehouse inventory here

    [Header("External Panels")]
    [Tooltip("Assign the TownInfraController (Town Hall panel). BtnQualityLab toggles this now.")]
    [SerializeField] private TownInfraController townInfraController;

    [Tooltip("Assign the MaterialQualityController panel. BtnMaterialQuality toggles this.")]
    [SerializeField] private MaterialQualityController materialQualityController;

    [Header("Reveal / Pin")]
    [SerializeField] private float animSpeed = 12f;
    [SerializeField] private float revealZoneHeightPx = 100f;     // hover zone height at top of screen
    [SerializeField] private bool autoSizeHoverZone = true;

    [Tooltip("If true, when the bar hides we also close Stats/Warehouse panels.")]
    [SerializeField] private bool closePanelsWhenHidden = true;

    [Header("Hide Offset")]
    [Tooltip("If true, hiddenOffsetY is auto-calculated from the Taskbar height so it is fully off-screen.")]
    [SerializeField] private bool autoComputeHiddenOffset = true;

    [Tooltip("Fallback when autoComputeHiddenOffset=false. Must be negative to move bar up.")]
    [SerializeField] private float hiddenOffsetY = -70f;

    [Tooltip("Extra pixels to hide past the top edge (prevents 1px peeking due to rounding).")]
    [SerializeField] private float extraHidePaddingPx = 2f;

    private const string PrefPinnedKey = "GF_TopTaskbarPinned";

    // UI (Top taskbar)
    private VisualElement _screenRoot;
    private VisualElement _hoverZone;
    private VisualElement _taskbar;

    private VisualElement _statsPanel;
    private VisualElement _resourcesPanel;

    // Lake name is removed from the current UXML, but we keep this for backward compatibility
    // and explicitly hide it if an older UXML still contains it.
    private Label _lakeNameLabel;

    private Label _creditsLabel;
    private Label _ipLabel;
    private Label _qpLabel;

    private Label _tilesLabel;
    private Label _slotsLabel;
    private Label _warehouseTotalLabel;

    private Label _woodLabel;
    private Label _stoneLabel;
    private Label _fiberLabel;
    private Label _baitLabel;

    private Toggle _pinToggle;
    private Button _btnStats;
    private Button _btnWarehouse;
    private Button _btnQualityLab;         // legacy name; now opens Town Hall
    private Button _btnMaterialQuality;

    // Legacy property name used by older code paths.
    // Kept to reduce breakage; now returns whether Town Hall is open.
    [Obsolete("Quality Lab is deprecated. Use IsTownHallOpen or IsMaterialQualityOpen.")]
    public static bool IsQualityLabOpen => IsTownHallOpen;

    public static bool IsTownHallOpen =>
        _townInfraControllerStatic != null && _townInfraControllerStatic.IsVisible;

    public static bool IsMaterialQualityOpen =>
        _materialQualityControllerStatic != null && _materialQualityControllerStatic.IsVisible;

    private static TownInfraController _townInfraControllerStatic;
    private static MaterialQualityController _materialQualityControllerStatic;

    private bool _isPinned;
    private float _currentY;
    private float _targetY;

    // computed hide offset
    private float _computedHiddenY;
    private bool _hasComputedHiddenY;

    // polling refresh
    private float _nextPollTime;

    private void OnEnable()
    {
        var doc = GetComponent<UIDocument>();
        if (doc == null)
        {
            Debug.LogError("[HexWorldTopTaskbarToolkitController] Missing UIDocument.");
            return;
        }

        var root = doc.rootVisualElement;

        _screenRoot = root.Q<VisualElement>("ScreenRoot");
        _hoverZone = root.Q<VisualElement>("HoverZone");
        _taskbar = root.Q<VisualElement>("Taskbar");

        _statsPanel = root.Q<VisualElement>("StatsPanel");
        _resourcesPanel = root.Q<VisualElement>("ResourcesPanel");

        // Backward-compatible query (older UXML versions)
        _lakeNameLabel = QAny<Label>(root, "LakeNameLabel", "LabelLakeName");
        _creditsLabel = QAny<Label>(root, "CreditsLabel", "LabelCredits");

        // Per Ticket 17 / Contract: LabelIP & LabelQP
        // Fallbacks included to not break older UXML variants.
        _ipLabel = QAny<Label>(root, "LabelIP", "IPLabel");
        _qpLabel = QAny<Label>(root, "LabelQP", "QPLabel");

        _tilesLabel = QAny<Label>(root, "TilesValue");
        _slotsLabel = QAny<Label>(root, "SlotsValue");
        _warehouseTotalLabel = QAny<Label>(root, "WarehouseTotalValue");

        _woodLabel = QAny<Label>(root, "Res_Wood");
        _stoneLabel = QAny<Label>(root, "Res_Stone");
        _fiberLabel = QAny<Label>(root, "Res_Fiber");
        _baitLabel = QAny<Label>(root, "Res_Bait");

        _pinToggle = QAny<Toggle>(root, "PinToggle");
        _btnStats = QAny<Button>(root, "BtnStats");
        _btnWarehouse = QAny<Button>(root, "BtnWarehouse");
        _btnQualityLab = QAny<Button>(root, "BtnQualityLab");
        _btnMaterialQuality = QAny<Button>(root, "BtnMaterialQuality");

        if (_taskbar == null)
        {
            Debug.LogError("[HexWorldTopTaskbarToolkitController] UI Toolkit: Could not find VisualElement named 'Taskbar'.");
            return;
        }

        // IMPORTANT: Lake name is removed from the bar.
        // If an older UXML still contains LakeNameLabel, hide it so it can't show "HexWorld" etc.
        if (_lakeNameLabel != null)
        {
            _lakeNameLabel.text = string.Empty;
            _lakeNameLabel.style.display = DisplayStyle.None;
        }

        // Predictable anchoring: top edge, then hide/show via translate Y.
        _taskbar.style.position = Position.Absolute;
        _taskbar.style.top = 0;
        _taskbar.style.left = 0;
        _taskbar.style.right = 0;

        if (_hoverZone != null && autoSizeHoverZone)
            _hoverZone.style.height = revealZoneHeightPx;

        // Load pinned state
        _isPinned = PlayerPrefs.GetInt(PrefPinnedKey, 0) == 1;
        if (_pinToggle != null) _pinToggle.value = _isPinned;

        // Start with dropdowns closed
        SetPanelVisible(_statsPanel, false);
        SetPanelVisible(_resourcesPanel, false);

        // Cache static references for open-state properties
        _townInfraControllerStatic = townInfraController;
        _materialQualityControllerStatic = materialQualityController;

        // Ensure external panels are hidden on startup
        if (townInfraController != null) townInfraController.Hide();
        if (materialQualityController != null) materialQualityController.Hide();

        // Until we know real height, use fallback hidden offset (unless pinned)
        _computedHiddenY = hiddenOffsetY;
        _hasComputedHiddenY = false;

        _currentY = _isPinned ? 0f : _computedHiddenY;
        _targetY = _currentY;
        ApplyTranslate(_currentY);

        RegisterUiCallbacks();
        SubscribeCurrencyEvents();

        if (autoComputeHiddenOffset)
        {
            _taskbar.RegisterCallback<GeometryChangedEvent>(_ => TryComputeHiddenOffsetFromHeight());
            TryComputeHiddenOffsetFromHeight();
        }

        RefreshAll();
    }

    private void SubscribeCurrencyEvents()
    {
        var pm = PlayerProgressManager.Instance;
        if (pm != null)
        {
            pm.IPChanged += OnIPChanged;
            pm.QPChanged += OnQPChanged;
        }
    }

    private void UnsubscribeCurrencyEvents()
    {
        var pm = PlayerProgressManager.Instance;
        if (pm != null)
        {
            pm.IPChanged -= OnIPChanged;
            pm.QPChanged -= OnQPChanged;
        }
    }

    private void OnIPChanged(long newValue)
    {
        // Keep the "IP:" prefix visible (previous code overwrote it with only a number)
        if (_ipLabel != null)
            _ipLabel.text = $"IP: {newValue:N0}";
    }

    private void OnQPChanged(long newValue)
    {
        // Keep the "QP:" prefix visible (previous code overwrote it with only a number)
        if (_qpLabel != null)
            _qpLabel.text = $"QP: {newValue:N0}";
    }

    private void OnDisable()
    {
        UnsubscribeCurrencyEvents();

        if (_btnQualityLab != null) _btnQualityLab.clicked -= ToggleTownHall;
        if (_btnMaterialQuality != null) _btnMaterialQuality.clicked -= ToggleMaterialQuality;
        if (_btnStats != null) _btnStats.clicked -= OnStatsClicked;
        if (_btnWarehouse != null) _btnWarehouse.clicked -= OnWarehouseClicked;
    }

    private void RegisterUiCallbacks()
    {
        if (_pinToggle != null)
        {
            _pinToggle.RegisterValueChangedCallback(evt =>
            {
                _isPinned = evt.newValue;
                PlayerPrefs.SetInt(PrefPinnedKey, _isPinned ? 1 : 0);

                if (_isPinned)
                {
                    _targetY = 0f;
                }
                else
                {
                    if (!IsMouseInRevealOrTaskbar())
                    {
                        _targetY = GetHiddenY();
                        if (closePanelsWhenHidden) CloseAllDropdownPanels();
                    }
                }
            });
        }

        if (_btnStats != null) _btnStats.clicked += OnStatsClicked;
        if (_btnWarehouse != null) _btnWarehouse.clicked += OnWarehouseClicked;

        // BtnQualityLab now toggles Town Hall
        if (_btnQualityLab != null) _btnQualityLab.clicked += ToggleTownHall;

        // New: Material Quality panel button
        if (_btnMaterialQuality != null) _btnMaterialQuality.clicked += ToggleMaterialQuality;
    }

    private void OnStatsClicked()
    {
        bool next = !IsVisible(_statsPanel);
        SetPanelVisible(_resourcesPanel, false);
        SetPanelVisible(_statsPanel, next);
    }

    private void OnWarehouseClicked()
    {
        bool next = !IsVisible(_resourcesPanel);
        SetPanelVisible(_statsPanel, false);
        SetPanelVisible(_resourcesPanel, next);
    }

    private void Update()
    {
        if (_taskbar == null || _taskbar.panel == null)
            return;

        // Strict rule:
        // - pinned => always open
        // - not pinned => open only while mouse is in top reveal zone OR over taskbar
        if (_isPinned)
        {
            _targetY = 0f;
        }
        else
        {
            bool hover = IsMouseInRevealOrTaskbar();
            if (hover)
            {
                _targetY = 0f;
            }
            else
            {
                _targetY = GetHiddenY();
                if (closePanelsWhenHidden) CloseAllDropdownPanels();
            }
        }

        // Smooth slide (unscaled)
        _currentY = Mathf.Lerp(_currentY, _targetY, Time.unscaledDeltaTime * animSpeed);
        ApplyTranslate(_currentY);

        // Polling refresh (5 times/sec)
        if (Time.unscaledTime >= _nextPollTime)
        {
            _nextPollTime = Time.unscaledTime + 0.2f;
            RefreshAll();
        }
    }

    private bool IsMouseInRevealOrTaskbar()
    {
        if (_taskbar == null || _taskbar.panel == null)
            return false;

        Vector2 screenPos;

#if ENABLE_INPUT_SYSTEM
        var m = Mouse.current;
        if (m == null) return false;
        screenPos = m.position.ReadValue();
#else
        screenPos = Input.mousePosition;
#endif

        Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(_taskbar.panel, screenPos);

        // "Top reveal zone" is top N pixels of the panel.
        float panelHeight = _taskbar.panel.visualTree.worldBound.height;
        float topThreshold = Mathf.Max(0f, panelHeight - revealZoneHeightPx);

        bool inRevealZone = panelPos.y >= topThreshold && panelPos.y <= panelHeight;
        bool inTaskbar = _taskbar.worldBound.Contains(panelPos);

        return inRevealZone || inTaskbar;
    }

    private void CloseAllDropdownPanels()
    {
        SetPanelVisible(_statsPanel, false);
        SetPanelVisible(_resourcesPanel, false);
    }

    private float GetHiddenY()
    {
        if (autoComputeHiddenOffset && _hasComputedHiddenY)
            return _computedHiddenY;

        return hiddenOffsetY;
    }

    private void TryComputeHiddenOffsetFromHeight()
    {
        if (_taskbar == null) return;

        float h = _taskbar.resolvedStyle.height;
        if (h <= 0.5f) return;

        _computedHiddenY = -(h + extraHidePaddingPx);
        _hasComputedHiddenY = true;

        // Snap once so it starts fully hidden when not pinned
        if (!_isPinned)
        {
            _currentY = _computedHiddenY;
            _targetY = _computedHiddenY;
            ApplyTranslate(_currentY);
        }
        else
        {
            _currentY = 0f;
            _targetY = 0f;
            ApplyTranslate(_currentY);
        }
    }

    private void ApplyTranslate(float y)
    {
        if (_taskbar == null) return;
        _taskbar.style.translate = new StyleTranslate(new Translate(0, y, 0));
    }

    private static void SetPanelVisible(VisualElement panel, bool visible)
    {
        if (panel == null) return;
        panel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private static bool IsVisible(VisualElement panel)
    {
        if (panel == null) return false;
        return panel.resolvedStyle.display != DisplayStyle.None;
    }

    // -------------------------
    // Town Hall + Material Quality toggles
    // -------------------------

    private void ToggleTownHall()
    {
        if (townInfraController == null)
        {
            Debug.LogWarning("Town Hall: townInfraController is null. Assign TownInfraController in the inspector.");
            return;
        }

        if (townInfraController.IsVisible)
        {
            townInfraController.Hide();
            return;
        }

        // Opening Town Hall closes the other panel + dropdowns.
        materialQualityController?.Hide();
        CloseAllDropdownPanels();
        townInfraController.Show();
    }

    private void ToggleMaterialQuality()
    {
        if (materialQualityController == null)
        {
            Debug.LogWarning("Material Quality: materialQualityController is null. Assign MaterialQualityController in the inspector.");
            return;
        }

        if (materialQualityController.IsVisible)
        {
            materialQualityController.Hide();
            return;
        }

        // Opening Material Quality closes the other panel + dropdowns.
        townInfraController?.Hide();
        CloseAllDropdownPanels();
        materialQualityController.Show();
    }

    // -------------------------
    // Data refresh (your existing placeholders)
    // -------------------------

    private void RefreshAll()
    {
        // Lake name is removed from the bar; do not write anything to _lakeNameLabel.
        if (_creditsLabel != null) _creditsLabel.text = "Credits: —";

        // Ticket 17: IP / QP labels (KEEP prefix text visible)
        long ip = PlayerProgressManager.Instance?.InfrastructurePoints ?? 0;
        long qp = PlayerProgressManager.Instance?.UnspentQP ?? 0;

        if (_ipLabel != null) _ipLabel.text = $"IP: {ip:N0}";
        if (_qpLabel != null) _qpLabel.text = $"QP: {qp:N0}";

        if (controllerBehaviour != null)
        {
            var t = controllerBehaviour.GetType();

            int tilesPlaced = GetInt(t, controllerBehaviour, "TilesPlaced");
            int tileCap = GetInt(t, controllerBehaviour, "TileCapacityMax");

            int activeUsed = GetInt(t, controllerBehaviour, "ActiveBuildingsUsed");
            int activeTotal = GetInt(t, controllerBehaviour, "ActiveSlotsTotal");
            int townHallLevel = GetInt(t, controllerBehaviour, "TownHallLevel");

            if (_tilesLabel != null) _tilesLabel.text = $"Tiles: {tilesPlaced}/{tileCap}";
            if (_slotsLabel != null) _slotsLabel.text = $"Active: {activeUsed}/{activeTotal} (L{townHallLevel})";
        }

        if (warehouseBehaviour != null)
        {
            var t = warehouseBehaviour.GetType();

            int total = GetInt(t, warehouseBehaviour, "TotalStored");
            int cap = GetInt(t, warehouseBehaviour, "Capacity");
            if (_warehouseTotalLabel != null) _warehouseTotalLabel.text = $"Warehouse: {total}/{cap}";

            if (_woodLabel != null) _woodLabel.text = $"Wood: {TryGetAmount(warehouseBehaviour, "Wood")}";
            if (_stoneLabel != null) _stoneLabel.text = $"Stone: {TryGetAmount(warehouseBehaviour, "Stone")}";
            if (_fiberLabel != null) _fiberLabel.text = $"Fiber: {TryGetAmount(warehouseBehaviour, "Fiber")}";
            if (_baitLabel != null) _baitLabel.text = $"Bait: {TryGetAmount(warehouseBehaviour, "BaitIngredients")}";
        }
    }

    private static int GetInt(Type type, object instance, string propName)
    {
        var p = type.GetProperty(propName);
        if (p != null && p.PropertyType == typeof(int))
            return (int)p.GetValue(instance);

        var f = type.GetField(propName);
        if (f != null && f.FieldType == typeof(int))
            return (int)f.GetValue(instance);

        return 0;
    }

    private static int TryGetAmount(object warehouseInstance, string key)
    {
        var t = warehouseInstance.GetType();

        var m1 = t.GetMethod("Get", new[] { typeof(string) });
        if (m1 != null && m1.ReturnType == typeof(int))
            return (int)m1.Invoke(warehouseInstance, new object[] { key });

        var m2 = t.GetMethod("GetAmount", new[] { typeof(string) });
        if (m2 != null && m2.ReturnType == typeof(int))
            return (int)m2.Invoke(warehouseInstance, new object[] { key });

        return 0;
    }

    private static T QAny<T>(VisualElement root, params string[] names) where T : VisualElement
    {
        if (root == null || names == null) return null;

        for (int i = 0; i < names.Length; i++)
        {
            string n = names[i];
            if (string.IsNullOrEmpty(n)) continue;
            var el = root.Q<T>(n);
            if (el != null) return el;
        }

        return null;
    }
}
