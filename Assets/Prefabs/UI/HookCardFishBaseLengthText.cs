using System;
using System.Reflection;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class HookCardFishBaseLengthText : MonoBehaviour
{
    public enum SourceMode
    {
        /// Drag the caught fish GameObject (or whatever object represents it on the hook card).
        FishGameObject,

        /// Drag the component that has a "species" field/property referencing the Fish asset (e.g. GFishSpawnTag).
        FishTagComponent,

        /// Drag a script that "knows" the current fish, then set Member Name to a field/property/method name.
        /// The member can return GameObject, Component, or the Fish asset directly.
        ProviderMember,

        /// Quick test: drag the Fish ScriptableObject directly.
        FishAsset
    }

    [Header("UI")]
    [SerializeField] private TMP_Text label;

    [Header("Source")]
    [SerializeField] private SourceMode sourceMode = SourceMode.FishGameObject;

    [Tooltip("Used in FishGameObject mode. Drag the fish object (or the root object representing the fish).")]
    [SerializeField] private GameObject fishGameObject;

    [Tooltip("Used in FishTagComponent mode. Drag the component (e.g. GFishSpawnTag) from the fish object.")]
    [SerializeField] private Component fishTagComponent;

    [Tooltip("Used in ProviderMember mode. Drag the script that knows the current fish.")]
    [SerializeField] private MonoBehaviour fishProvider;

    [Tooltip("Used in ProviderMember mode. Field/Property/Method name on the provider that returns the fish (or tag).")]
    [SerializeField] private string providerMemberName = "CurrentFish";

    [Tooltip("Used in FishAsset mode. Drag the Fish ScriptableObject.")]
    [SerializeField] private ScriptableObject fishAsset;

    [Header("Formatting")]
    [SerializeField] private string prefix = "";
    [SerializeField] private string suffix = " cm";
    [SerializeField] private string missingText = "—";
    [SerializeField] private int decimals = 0;

    [Header("Refresh")]
    [SerializeField] private float refreshSeconds = 0.25f;

    private float _nextRefresh;
    private string _last;

    private void Reset()
    {
        label = GetComponent<TMP_Text>();
    }

    private void Awake()
    {
        if (!label) label = GetComponent<TMP_Text>();
        Apply(true);
    }

    private void OnEnable()
    {
        if (!label) label = GetComponent<TMP_Text>();
        Apply(true);
    }

    private void Update()
    {
        if (!label) return;
        if (Time.unscaledTime < _nextRefresh) return;

        _nextRefresh = Time.unscaledTime + Mathf.Max(0.05f, refreshSeconds);
        Apply(false);
    }

    private void Apply(bool force)
    {
        string msg = BuildText();
        if (force || !string.Equals(_last, msg, StringComparison.Ordinal))
        {
            _last = msg;
            label.text = msg;
        }
    }

    private string BuildText()
    {
        if (!TryGetBaselineMeters(out float meters))
            return prefix + missingText;

        // meters -> cm
        float cm = meters * 100f;

        string fmt = decimals <= 0 ? "0" : (decimals == 1 ? "0.#" : "0.##");
        return prefix + cm.ToString(fmt) + suffix;
    }

    private bool TryGetBaselineMeters(out float baselineMeters)
    {
        baselineMeters = 0f;

        object fishObj = null;

        switch (sourceMode)
        {
            case SourceMode.FishGameObject:
                fishObj = ResolveFishFromGameObject(fishGameObject);
                break;

            case SourceMode.FishTagComponent:
                fishObj = ResolveFishFromTagComponent(fishTagComponent);
                break;

            case SourceMode.ProviderMember:
                fishObj = ResolveFishFromProvider(fishProvider, providerMemberName);
                break;

            case SourceMode.FishAsset:
                fishObj = fishAsset;
                break;
        }

        if (fishObj == null) return false;

        // fishObj might be:
        // - the Fish ScriptableObject itself (has baselineMeters)
        // - a tag component with "species" (which then has baselineMeters)
        // - a GameObject/Component that we can search for a tag component
        if (TryReadBaselineMetersFromFishAsset(fishObj, out baselineMeters))
            return true;

        // if it wasn't the fish asset itself, try extracting species from it
        var species = GetMemberValue(fishObj, "species");
        if (species != null && TryReadBaselineMetersFromFishAsset(species, out baselineMeters))
            return true;

        return false;
    }

    private object ResolveFishFromGameObject(GameObject go)
    {
        if (!go) return null;

        // Try to find any component on this GO (or children/parents) that has "species"
        var comps = go.GetComponentsInChildren<Component>(true);
        foreach (var c in comps)
        {
            if (!c) continue;
            if (HasMember(c, "species"))
                return c;
        }

        comps = go.GetComponentsInParent<Component>(true);
        foreach (var c in comps)
        {
            if (!c) continue;
            if (HasMember(c, "species"))
                return c;
        }

        return null;
    }

    private object ResolveFishFromTagComponent(Component tag)
    {
        if (!tag) return null;
        return tag;
    }

    private object ResolveFishFromProvider(MonoBehaviour provider, string memberName)
    {
        if (!provider) return null;
        if (string.IsNullOrWhiteSpace(memberName)) return null;

        object result = GetMemberValue(provider, memberName);
        if (result == null) return null;

        // If provider returns a GO/component, resolve into a tag component
        if (result is GameObject go)
            return ResolveFishFromGameObject(go);

        if (result is Component c)
        {
            if (HasMember(c, "species")) return c;
            if (c.gameObject) return ResolveFishFromGameObject(c.gameObject);
        }

        // Or it might already be the Fish asset
        return result;
    }

    private bool TryReadBaselineMetersFromFishAsset(object fishAssetObj, out float meters)
    {
        meters = 0f;
        if (fishAssetObj == null) return false;

        object val = GetMemberValue(fishAssetObj, "baselineMeters");
        if (val == null) return false;

        try
        {
            meters = Convert.ToSingle(val);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool HasMember(object obj, string name)
    {
        if (obj == null) return false;
        var t = obj.GetType();
        return t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) != null
            || t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) != null
            || t.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) != null;
    }

    private static object GetMemberValue(object obj, string name)
    {
        if (obj == null || string.IsNullOrEmpty(name)) return null;

        var t = obj.GetType();

        var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (f != null) return f.GetValue(obj);

        var p = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (p != null && p.GetIndexParameters().Length == 0) return p.GetValue(obj);

        var m = t.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (m != null && m.GetParameters().Length == 0) return m.Invoke(obj, null);

        return null;
    }
}
