using TMPro;
using UnityEngine;

/// <summary>
/// Bezel hover label with a simple UI "lock":
/// - Inventory calls ShowUI/ClearUI while the cursor is over a slot.
/// - World code (if any) can call ShowWorld/ClearWorld, but it's ignored while UI is active.
/// Attach this to the TMP text you created for the inventory hover name.
/// </summary>
public class InventoryHoverName : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [Tooltip("What to show when nothing is hovered. Leave empty to show nothing.")]
    [SerializeField] private string defaultText = "";

    private static InventoryHoverName _instance;
    private static int _uiHoverDepth = 0;   // >0 means UI has control

    void Awake()
    {
        if (!label) label = GetComponent<TMP_Text>();
    }

    void OnEnable()
    {
        _instance = this;
        Set(defaultText);
        _uiHoverDepth = 0;
    }

    void OnDisable()
    {
        if (_instance == this) _instance = null;
        _uiHoverDepth = 0;
    }

    // ---------- UI (inventory) API ----------
    public static void ShowUI(string text)
    {
        if (_instance == null) return;
        _uiHoverDepth++;
        _instance.Set(string.IsNullOrWhiteSpace(text) ? _instance.defaultText : text);
    }

    public static void ClearUI()
    {
        if (_instance == null) return;
        _uiHoverDepth = Mathf.Max(0, _uiHoverDepth - 1);
        if (_uiHoverDepth == 0) _instance.Set(_instance.defaultText);
    }

    // ---------- World (optional) API ----------
    public static void ShowWorld(string text)
    {
        if (_instance == null) return;
        if (_uiHoverDepth > 0) return; // UI has priority
        _instance.Set(text);
    }

    public static void ClearWorld()
    {
        if (_instance == null) return;
        if (_uiHoverDepth > 0) return; // UI has priority
        _instance.Set(_instance.defaultText);
    }

    // ---------- Internal ----------
    private void Set(string text)
    {
        if (!label) return;
        label.text = text ?? "";
        label.enabled = true;
        label.gameObject.SetActive(true);
    }
}
