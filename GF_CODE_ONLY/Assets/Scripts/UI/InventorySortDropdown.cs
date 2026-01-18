using UnityEngine;
using TMPro;

[DisallowMultipleComponent]
public class InventorySortDropdown : MonoBehaviour
{
    [SerializeField] private InventoryGridController grid;
    [SerializeField] private TMP_Dropdown dropdown;
    [SerializeField] private bool autoFind = true;
    [SerializeField] private bool autoApplyOnStart = true;
    [SerializeField] private bool verbose = false;

    void Awake()
    {
        FindRefs();
        Wire(true);
        if (autoApplyOnStart) ApplyCurrent();
    }

    void OnEnable()
    {
        Wire(true);
        if (autoApplyOnStart) ApplyCurrent();
    }

    void OnDisable()
    {
        Wire(false);
    }

    void FindRefs()
    {
        if (autoFind)
        {
            if (!grid) grid = GetComponentInParent<InventoryGridController>(true);
            if (!dropdown) dropdown = GetComponent<TMP_Dropdown>() ?? GetComponentInChildren<TMP_Dropdown>(true);
        }
    }

    void Wire(bool enable)
    {
        if (!dropdown) return;
        dropdown.onValueChanged.RemoveListener(OnChanged);
        if (enable) dropdown.onValueChanged.AddListener(OnChanged);
    }

    void ApplyCurrent()
    {
        if (!dropdown) return;
        OnChanged(dropdown.value);
    }

    public void OnChanged(int index)
    {
        if (!grid || !dropdown) return;

        string label = index >= 0 && index < dropdown.options.Count
            ? dropdown.options[index].text
            : string.Empty;

        var key = label.Trim().ToLowerInvariant();
        InventorySortMode mode = key switch
        {
            "owned"     => InventorySortMode.OwnedFirst,
            "quantity"  => InventorySortMode.OwnedFirst, // treat old label as owned
            "rarity"    => InventorySortMode.Rarity,
            "name"      => InventorySortMode.Name,
            _           => InventorySortMode.OwnedFirst
        };

        if (verbose)
        {
            Debug.Log($"[InventorySortDropdown] Selected '{label}' -> {mode}");
        }

        grid.SetSortMode(mode);
    }
}
