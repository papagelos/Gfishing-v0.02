// Assets/Scripts/UI/HookCardOwnedBinder.cs
// v4: Same behavior as v3 + world–record hookup to HookCardUI.
// - Still uses reflection to read "fishes" from any registry asset.
// - Adds an optional HookCardUI reference and pushes world-record info
//   into that card via HookCardUI.SetWorldRecord(Fish, int).

using UnityEngine;
using TMPro;
using System.Collections;
using System.Reflection;
using GalacticFishing;   // For Fish + world-record integration

[DefaultExecutionOrder(200)]
public class HookCardOwnedBinder : MonoBehaviour
{
    [Header("HookCard UI (assign in prefab)")]
    [SerializeField] private TMP_Text speciesName;
    [SerializeField] private TMP_Text subText;

    [Tooltip("Optional: HookCardUI on the same prefab, used to show world-record info.")]
    [SerializeField] private HookCardUI hookCardUi;

    [Header("Data (same source as grid)")]
    [Tooltip("Drag your FishRegistry asset here. The script will read its 'fishes' list via reflection.")]
    [SerializeField] private UnityEngine.Object registry;

    [Header("Options")]
    [SerializeField] private bool logs = false;

    string _lastTitle = null;
    int _currentId = -1;

    // Cache for reflection
    FieldInfo _fishesField;
    PropertyInfo _fishesProp;

    // --- Public API: allow external systems to set the id directly (best case).
    public void SetSpeciesId(int speciesId)
    {
        _currentId = speciesId;
        if (logs) Debug.Log($"[HookCardOwnedBinder] External SetSpeciesId → {speciesId}");
        UpdateText();             // also updates world-record panel
    }

    void Awake()
    {
        if (!hookCardUi)
            hookCardUi = GetComponent<HookCardUI>();

        if (registry != null)
        {
            var t = registry.GetType();
            _fishesField = t.GetField("fishes", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            _fishesProp  = t.GetProperty("fishes", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (logs) Debug.Log($"[HookCardOwnedBinder] Registry bound via reflection. field? {(_fishesField!=null)} prop? {(_fishesProp!=null)}");
        }
    }

    void OnEnable()
    {
        InventoryService.OnChanged += OnInventoryChanged;
        ForceRefresh();
    }

    void OnDisable()
    {
        InventoryService.OnChanged -= OnInventoryChanged;
        _currentId = -1;
        _lastTitle = null;

        // Clear WR when the card is disabled (optional)
        if (hookCardUi)
            hookCardUi.SetWorldRecord(null, -1);
    }

    void Update()
    {
        if (!speciesName) return;

        var title = speciesName.text?.Trim();
        if (title != _lastTitle)
        {
            _lastTitle = title;
            _currentId = ResolveIdFromTitle(title);
            if (logs) Debug.Log($"[HookCardOwnedBinder] Title change → '{title}'  id={_currentId}");
            UpdateText();         // also updates world-record panel
        }
    }

    void OnInventoryChanged()
    {
        if (isActiveAndEnabled)
            UpdateText();         // also updates world-record panel
    }

    void ForceRefresh()
    {
        if (speciesName)
        {
            _lastTitle = speciesName.text?.Trim();
            _currentId = ResolveIdFromTitle(_lastTitle);
        }
        UpdateText();             // also updates world-record panel
    }

    int ResolveIdFromTitle(string title)
    {
        if (registry == null || string.IsNullOrWhiteSpace(title))
            return _currentId >= 0 ? _currentId : -1;

        string want = Normalize(title);
        string wantNoDigits = StripTrailingDigits(want);

        IList fishes = GetFishesList();
        if (fishes == null) return -1;

        int best = -1;

        for (int i = 0; i < fishes.Count; i++)
        {
            var defObj = fishes[i] as UnityEngine.Object;
            if (!defObj) continue;

            string assetName   = Normalize(defObj.name);
            string displayName = Normalize(GetDisplayName(defObj));

            // Exact match first
            if (want == displayName || want == assetName) return i;

            // Variant-safe trailing digits
            if (wantNoDigits == displayName || wantNoDigits == assetName) best = i;
            else if (want.StartsWith(displayName) || want.StartsWith(assetName) ||
                     displayName.StartsWith(wantNoDigits) || assetName.StartsWith(wantNoDigits))
                best = i;
        }

        return best;
    }

    IList GetFishesList()
    {
        if (registry == null) return null;
        if (_fishesField != null) return _fishesField.GetValue(registry) as IList;
        if (_fishesProp  != null) return _fishesProp.GetValue(registry) as IList;
        return null;
    }

    string GetDisplayName(UnityEngine.Object fishDef)
    {
        // Try field or property named "displayName"
        var t = fishDef.GetType();
        var f = t.GetField("displayName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (f != null && f.FieldType == typeof(string))
            return (string)f.GetValue(fishDef);

        var p = t.GetProperty("displayName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (p != null && p.PropertyType == typeof(string))
            return (string)p.GetValue(fishDef, null);

        return fishDef.name; // fallback
    }

    void UpdateText()
    {
        int n = 0;

        if (_currentId >= 0 && InventoryService.IsInitialized)
        {
            foreach (var t in InventoryService.All())
            {
                if (t.id == _currentId) { n = Mathf.Max(0, t.count); break; }
            }
        }

        if (subText) subText.text = $"OWNED: {n}";
        if (logs) Debug.Log($"[HookCardOwnedBinder] Shown → OWNED: {n} (id={_currentId})");

        // Also keep the world-record info in sync with this id.
        UpdateWorldRecordPanel();
    }

    // ----------------- helpers -----------------

    void UpdateWorldRecordPanel()
    {
        if (!hookCardUi)
            return;

        if (_currentId < 0)
        {
            hookCardUi.SetWorldRecord(null, -1);
            return;
        }

        IList fishes = GetFishesList();
        if (fishes == null || _currentId >= fishes.Count)
        {
            hookCardUi.SetWorldRecord(null, _currentId);
            return;
        }

        // Your registry "fishes" list is expected to contain GalacticFishing.Fish assets.
        Fish fish = fishes[_currentId] as Fish;
        hookCardUi.SetWorldRecord(fish, _currentId);
    }

    static string Normalize(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        System.Text.StringBuilder sb = new System.Text.StringBuilder(s.Length);
        foreach (char c in s)
        {
            if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    static string StripTrailingDigits(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        int end = s.Length;
        while (end > 0 && char.IsDigit(s[end - 1])) end--;
        return s.Substring(0, end);
    }
}
