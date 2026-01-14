// HoverTargetHUD.cs — topmost fish wins, 2D trigger-friendly
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System;
using System.Reflection;
using GalacticFishing;
using GalacticFishing.Progress;
using GalacticFishing.Data;

public class HoverTargetHUD : MonoBehaviour
{
    [Header("Assign")]
    [SerializeField] private Camera worldCamera;          // leave empty to use Camera.main
    [SerializeField] private LayerMask fishMask = ~0;     // set to your Fish layer

    [Header("UI (drag Text OR TMP_Text)")]
    [SerializeField] private MonoBehaviour textComponent; // UnityEngine.UI.Text OR TMPro.TMP_Text

    [Header("Copy")]
    [SerializeField] private string prefix = "Target: ";
    [SerializeField] private string noneText = "Target: —";

    [Header("Quality")]
    [SerializeField] private bool showQuality = true;
    [SerializeField] private bool useMetaFallback = false;   // show FishMeta-level quality if runtime has none
    [SerializeField] private string qualityPrefix = " - Q: ";
    [SerializeField] private string qualityNotRolledText = " - Q: (not rolled)";
    [SerializeField] private string qualityUnavailableText = " - Q: (n/a)";

    [Header("Power")]
    [SerializeField] private bool showPower = true;
    [SerializeField] private string powerPrefix = " - P: ";
    [SerializeField] private string powerUnavailableText = " - P: (n/a)";

    [Header("Weight")]
    [SerializeField] private bool showWeight = true;
    [SerializeField] private string weightPrefix = " - W: ";
    [SerializeField] private string weightNotRolledText = " - W: (?)";

    [Header("Length")]
    [SerializeField] private bool showLength = true;
    [SerializeField] private string lengthPrefix = " - L: ";
    [SerializeField] private string lengthNotRolledText = " - L: (?)";

    [Header("Price")]
    [SerializeField] private bool showPrice = true;
    [SerializeField] private string pricePrefix = " - Price: ";
    [SerializeField] private string priceUnavailableText = " - Price: (N/A)";

    [Header("Lookups")]
    [SerializeField] private FishMetaIndex fishMetaIndex;

    private Text _u;
    private TMPro.TMP_Text _tmp;

    void Awake()
    {
        if (!worldCamera) worldCamera = Camera.main;

        _u = textComponent as Text;
        _tmp = textComponent as TMPro.TMP_Text;

        if (!_u && !_tmp)
        {
            TryGetComponent(out _u);
            TryGetComponent(out _tmp);
        }

        SetLabel(null);
    }

    void Update()
    {
        if (!worldCamera) worldCamera = Camera.main;

        Vector2 screen = (Mouse.current != null)
            ? Mouse.current.position.ReadValue()
            : (Vector2)Input.mousePosition;

        Vector2 world = worldCamera.ScreenToWorldPoint(screen);

        // Grab all colliders under the cursor (triggers included)
        var hits = Physics2D.OverlapPointAll(world, fishMask);

        FishIdentity best = null;
        int bestKey = int.MinValue;

        foreach (var h in hits)
        {
            var id = h.GetComponentInParent<FishIdentity>();
            if (!id) continue;

            // Find the renderer we actually see
            var sr = id.GetComponentInChildren<SpriteRenderer>();
            if (!sr)
            {
                best = id;
                bestKey = int.MaxValue;
                break;
            }

            // Build a render priority key: sorting layer value then order in layer
            int layerVal = SortingLayer.GetLayerValueFromID(sr.sortingLayerID);
            int key = (layerVal << 16) + sr.sortingOrder;

            // Optional: tie-breaker by Z (higher Z in front if you use perspective)
            key += Mathf.RoundToInt(-sr.transform.position.z * 10f);

            if (key > bestKey)
            {
                bestKey = key;
                best = id;
            }
        }

        SetLabel(best);
    }

    void SetLabel(FishIdentity fish)
    {
        string t = fish ? (prefix + fish.DisplayName) : noneText;

        if (fish)
        {
            // --- Central W/L/Q + price fetch ---
            bool foundData = TryGetFishData(fish, out var stats, out Fish fishDef);

            // ----- Length -----
            if (showLength)
            {
                if (stats.hasLength)
                    t += lengthPrefix + stats.lengthCm.ToString("0.#") + " cm";
                else
                    t += lengthNotRolledText;
            }

            // ----- Weight -----
            if (showWeight)
            {
                if (stats.hasWeight)
                    t += weightPrefix + stats.weightKg.ToString("0.###") + " kg";
                else
                    t += weightNotRolledText;
            }

            // ----- Sell Price -----
            if (showPrice)
            {
                if (foundData && fishDef != null)
                {
                    int price = FishPricing.GetSellPrice(fishDef, stats);
                    t += pricePrefix + price.ToString("N0") + " Cr";
                }
                else
                {
                    t += priceUnavailableText;
                }
            }

            // ----- Quality (keep existing behaviour) -----
            if (showQuality)
            {
                if (TryGetRuntimeQuality(fish, out var qVal, out var hasValue))
                {
                    t += hasValue ? (qualityPrefix + qVal) : qualityNotRolledText;
                }
                else if (useMetaFallback && TryGetMetaQuality(fish, out var metaQ))
                {
                    t += qualityPrefix + metaQ;
                }
                else
                {
                    t += qualityUnavailableText;
                }
            }

            // ----- Power (from FishMeta, as before) -----
            if (showPower)
            {
                if (TryGetMetaPower(fish, out var power))
                {
                    t += powerPrefix + power;
                }
                else
                {
                    t += powerUnavailableText;
                }
            }
        }

        if (_u) _u.text = t;
        if (_tmp) _tmp.text = t;
    }

    // --------------------------------------------------------------------
    // CENTRAL DATA HELPER (W/L/Q + Fish asset)
    // --------------------------------------------------------------------

    /// <summary>
    /// Extracts runtime stats (W/L/Q) and the Fish asset needed for pricing.
    /// Returns true if we have enough info to show W/L/Price.
    /// </summary>
    private bool TryGetFishData(
        FishIdentity fishIdentity,
        out InventoryStatsService.RuntimeStats stats,
        out Fish fishDef)
    {
        stats = default;
        fishDef = null;

        if (!fishIdentity) return false;

        float? w = null;
        float? l = null;
        int? q = null;

        GameObject root = fishIdentity.gameObject;

        // -------------------------------
        // 1) Weight (kg) from runtime
        // -------------------------------
        if (TryGetRuntimeFloat(
                root,
                new[] { "FishWeightRuntime" },
                new[] { "ValueKg", "Value" },
                out float wVal,
                out bool wHas,
                out _)
            && wHas)
        {
            w = wVal;
        }

        // -------------------------------
        // 2) Length (cm) from runtime
        // -------------------------------
        if (TryGetRuntimeFloat(
                root,
                new[] { "FishLengthRuntime" },
                new[] { "ValueCm", "Value" },
                out float lVal,
                out bool lHas,
                out _)
            && lHas)
        {
            l = lVal;
        }

        // -------------------------------
        // 3) Quality (int) from runtime
        // -------------------------------
        if (TryGetRuntimeInt(
                root,
                new[] { "FishQualityRuntime" },
                new[] { "Value" },
                out int qVal,
                out bool qHas,
                out _)
            && qHas)
        {
            q = qVal;
        }

        // Build RuntimeStats (same order as before: W, L, Q)
        stats = InventoryStatsService.RuntimeStats.From(w, l, q);

        // -------------------------------
        // 4) Resolve Fish definition
        // -------------------------------

        ScriptableObject metaSo = null;

        // First, try to grab the meta ScriptableObject from the identity
        if (TryGetFishMeta(fishIdentity, out metaSo) && metaSo != null)
        {
            var tMeta = metaSo.GetType();
            const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            // 4a) Use FishMetaIndex if available (most reliable)
            //     This mirrors GFishSpawner.ResolveSpeciesFromScriptable logic.
            if (fishMetaIndex != null && metaSo is FishMeta metaAsset)
            {
                try
                {
                    var fromIndex = fishMetaIndex.FindFishByMeta(metaAsset);
                    if (fromIndex != null)
                    {
                        fishDef = fromIndex;
                    }
                }
                catch
                {
                    // fail silently, we'll fall back to reflection
                }
            }

            // 4b) If still null, scan FIELDS on the meta for a Fish reference
            if (fishDef == null)
            {
                foreach (var field in tMeta.GetFields(BF))
                {
                    if (!typeof(Fish).IsAssignableFrom(field.FieldType)) continue;
                    try
                    {
                    var val = field.GetValue(metaSo) as Fish;
                    if (val != null)
                    {
                        fishDef = val;
                        break;
                    }
                    }
                    catch { }
                }
            }

            // 4c) If still null, scan PROPERTIES on the meta for a Fish reference
            if (fishDef == null)
            {
                foreach (var prop in tMeta.GetProperties(BF))
                {
                    if (!prop.CanRead || !typeof(Fish).IsAssignableFrom(prop.PropertyType)) continue;
                    try
                    {
                        var val = prop.GetValue(metaSo, null) as Fish;
                        if (val != null)
                        {
                            fishDef = val;
                            break;
                        }
                    }
                    catch { }
                }
            }
        }

        // 4d) Final fallback: look for a Fish field/property on the identity itself
        if (fishDef == null)
        {
            fishDef = TryGetFishDefFromIdentity(fishIdentity);
        }

        // For W/L/Price we require at least W + L *and* a valid Fish def
        return fishDef != null && stats.hasWeight && stats.hasLength;
    }

    // --------------------------------------------------------------------
    // GENERIC REFLECTION HELPERS (runtime W/L/Q)
    // --------------------------------------------------------------------

    /// <summary>
    /// Tries to find a component whose type name contains any of the given strings,
    /// then reads a float Value* field/property plus a HasValue boolean.
    /// </summary>
    private bool TryGetRuntimeFloat(
        GameObject root,
        string[] typeNameContainsAny,
        string[] valueFieldNames,
        out float value,
        out bool hasValue,
        out Component foundComp)
    {
        value = 0f;
        hasValue = false;
        foundComp = null;

        if (!root || typeNameContainsAny == null || typeNameContainsAny.Length == 0)
            return false;

        const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        var comps = root.GetComponentsInChildren<Component>(true);
        foreach (var c in comps)
        {
            if (!c) continue;

            var t = c.GetType();
            string typeName = t.Name;

            bool typeMatch = false;
            foreach (var key in typeNameContainsAny)
            {
                if (!string.IsNullOrEmpty(key) &&
                    typeName.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    typeMatch = true;
                    break;
                }
            }

            if (!typeMatch) continue;

            // HasValue bool
            bool localHas = false;
            var hasProp = t.GetProperty("HasValue", BF) ?? t.GetProperty("hasValue", BF);
            if (hasProp != null && hasProp.CanRead)
            {
                try
                {
                    var hv = hasProp.GetValue(c);
                    if (hv != null) localHas = Convert.ToBoolean(hv);
                }
                catch { }
            }
            else
            {
                var hasField = t.GetField("HasValue", BF) ?? t.GetField("hasValue", BF);
                if (hasField != null)
                {
                    try
                    {
                        var hv = hasField.GetValue(c);
                        if (hv != null) localHas = Convert.ToBoolean(hv);
                    }
                    catch { }
                }
            }

            hasValue = localHas;

            // Value (float)
            float localVal = 0f;
            bool gotVal = false;

            foreach (var name in valueFieldNames ?? Array.Empty<string>())
            {
                if (string.IsNullOrEmpty(name)) continue;

                var vp = t.GetProperty(name, BF);
                if (vp != null && vp.CanRead)
                {
                    try
                    {
                        var v = vp.GetValue(c);
                        if (v != null)
                        {
                            localVal = Convert.ToSingle(v);
                            gotVal = true;
                            break;
                        }
                    }
                    catch { }
                }

                var vf = t.GetField(name, BF);
                if (vf != null)
                {
                    try
                    {
                        var v = vf.GetValue(c);
                        if (v != null)
                        {
                            localVal = Convert.ToSingle(v);
                            gotVal = true;
                            break;
                        }
                    }
                    catch { }
                }
            }

            if (!gotVal)
                continue;

            value = localVal;
            foundComp = c;
            return true;
        }

        return false;
    }

    private bool TryGetRuntimeInt(
        GameObject root,
        string[] typeNameContainsAny,
        string[] valueFieldNames,
        out int value,
        out bool hasValue,
        out Component foundComp)
    {
        value = 0;
        hasValue = false;
        foundComp = null;

        if (!root || typeNameContainsAny == null || typeNameContainsAny.Length == 0)
            return false;

        const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        var comps = root.GetComponentsInChildren<Component>(true);
        foreach (var c in comps)
        {
            if (!c) continue;

            var t = c.GetType();
            string typeName = t.Name;

            bool typeMatch = false;
            foreach (var key in typeNameContainsAny)
            {
                if (!string.IsNullOrEmpty(key) &&
                    typeName.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    typeMatch = true;
                    break;
                }
            }

            if (!typeMatch) continue;

            // HasValue bool
            bool localHas = false;
            var hasProp = t.GetProperty("HasValue", BF) ?? t.GetProperty("hasValue", BF);
            if (hasProp != null && hasProp.CanRead)
            {
                try
                {
                    var hv = hasProp.GetValue(c);
                    if (hv != null) localHas = Convert.ToBoolean(hv);
                }
                catch { }
            }
            else
            {
                var hasField = t.GetField("HasValue", BF) ?? t.GetField("hasValue", BF);
                if (hasField != null)
                {
                    try
                    {
                        var hv = hasField.GetValue(c);
                        if (hv != null) localHas = Convert.ToBoolean(hv);
                    }
                    catch { }
                }
            }

            hasValue = localHas;

            // Value (int)
            int localVal = 0;
            bool gotVal = false;

            foreach (var name in valueFieldNames ?? Array.Empty<string>())
            {
                if (string.IsNullOrEmpty(name)) continue;

                var vp = t.GetProperty(name, BF);
                if (vp != null && vp.CanRead)
                {
                    try
                    {
                        var v = vp.GetValue(c);
                        if (v != null)
                        {
                            localVal = Convert.ToInt32(v);
                            gotVal = true;
                            break;
                        }
                    }
                    catch { }
                }

                var vf = t.GetField(name, BF);
                if (vf != null)
                {
                    try
                    {
                        var v = vf.GetValue(c);
                        if (v != null)
                        {
                            localVal = Convert.ToInt32(v);
                            gotVal = true;
                            break;
                        }
                    }
                    catch { }
                }
            }

            if (!gotVal)
                continue;

            value = localVal;
            foundComp = c;
            return true;
        }

        return false;
    }

    // --------------------------------------------------------------------
    // QUALITY HELPERS (existing behaviour)
    // --------------------------------------------------------------------

    bool TryGetRuntimeQuality(FishIdentity fish, out int value, out bool hasValue)
    {
        value = 0;
        hasValue = false;
        if (!fish) return false;

        var qType = Type.GetType("GalacticFishing.FishQualityRuntime") ?? Type.GetType("FishQualityRuntime");
        if (qType == null) return false;

        var comp = fish.GetComponentInChildren(qType);
        if (!comp) return false;

        var hasProp = qType.GetProperty("HasValue", BindingFlags.Instance | BindingFlags.Public);
        var valProp = qType.GetProperty("Value", BindingFlags.Instance | BindingFlags.Public);
        if (hasProp == null || valProp == null) return false;

        hasValue = (bool)hasProp.GetValue(comp);
        if (hasValue)
        {
            var obj = valProp.GetValue(comp);
            if (obj != null) value = Convert.ToInt32(obj);
        }

        return true;
    }

    bool TryGetMetaQuality(FishIdentity fish, out int value)
    {
        value = 0;
        if (!fish) return false;

        if (!TryGetFishMeta(fish, out var so))
            return false;

        var mt = so.GetType();
        var qf = mt.GetField("quality", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
              ?? mt.GetField("Quality", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (qf != null)
        {
            value = Mathf.RoundToInt(Convert.ToSingle(qf.GetValue(so)));
            return true;
        }

        var qp = mt.GetProperty("quality", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
               ?? mt.GetProperty("Quality", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (qp != null && qp.CanRead)
        {
            value = Mathf.RoundToInt(Convert.ToSingle(qp.GetValue(so)));
            return true;
        }

        return false;
    }

    // ---------- POWER HELPER ----------

    bool TryGetMetaPower(FishIdentity fish, out int value)
    {
        value = 0;
        if (!fish) return false;

        if (!TryGetFishMeta(fish, out var so))
            return false;

        var mt = so.GetType();
        var pf = mt.GetField("power", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
              ?? mt.GetField("Power", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (pf != null)
        {
            value = Mathf.RoundToInt(Convert.ToSingle(pf.GetValue(so)));
            return true;
        }

        var pp = mt.GetProperty("power", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
               ?? mt.GetProperty("Power", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (pp != null && pp.CanRead)
        {
            value = Mathf.RoundToInt(Convert.ToSingle(pp.GetValue(so)));
            return true;
        }

        return false;
    }

    // --------------------------------------------------------------------
    // FISH META / DEF HELPERS
    // --------------------------------------------------------------------

    bool TryGetFishMeta(FishIdentity fish, out ScriptableObject so)
    {
        so = null;
        if (!fish) return false;

        object metaObj = null;
        var t = fish.GetType();

        var fi = t.GetField("meta", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
              ?? t.GetField("Meta", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (fi != null)
        {
            metaObj = fi.GetValue(fish);
        }
        else
        {
            var pi = t.GetProperty("meta", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                   ?? t.GetProperty("Meta", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (pi != null && pi.CanRead)
                metaObj = pi.GetValue(fish);
        }

        if (metaObj is ScriptableObject soObj)
        {
            so = soObj;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Generic helper to look for a Fish field/property on the identity itself.
    /// Tries common names like Fish, Definition, Def, FishDef, Data, etc.
    /// </summary>
    private Fish TryGetFishDefFromIdentity(object identity)
    {
        if (identity == null) return null;

        var t = identity.GetType();
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic;

        string[] names =
        {
            "Fish", "fish",
            "Definition", "definition",
            "Def", "def",
            "FishDef", "fishDef",
            "Data", "data"
        };

        foreach (var n in names)
        {
            var p = t.GetProperty(n, flags);
            if (p != null && typeof(Fish).IsAssignableFrom(p.PropertyType))
            {
                try
                {
                    var v = p.GetValue(identity);
                    if (v is Fish f) return f;
                }
                catch { }
            }

            var fInfo = t.GetField(n, flags);
            if (fInfo != null && typeof(Fish).IsAssignableFrom(fInfo.FieldType))
            {
                try
                {
                    var v = fInfo.GetValue(identity);
                    if (v is Fish f) return f;
                }
                catch { }
            }
        }

        return null;
    }
}
