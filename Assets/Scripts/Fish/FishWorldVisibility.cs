using System.Collections.Generic;
using UnityEngine;

/// Centralized show/hide for all live fish.
/// Registry-based (GameObject references), with a safe tag fallback if nothing is registered.
/// No UnityEditor usage; safe for builds.
public static class FishWorldVisibility
{
    // Registered live fish (weak-ish: destroyed objects compare == null)
    private static readonly HashSet<GameObject> _fish = new HashSet<GameObject>();
    private static readonly Dictionary<GameObject, int> _origLayer = new Dictionary<GameObject, int>();
    private static bool _hidden;

    // -------- Registry API (called by FishInstance) --------
    public static void Register(GameObject fish)
    {
        if (!fish) return;
        _fish.Add(fish);
        if (_hidden) ApplyHidden(fish, true); // if currently hidden, apply immediately
    }

    public static void Unregister(GameObject fish)
    {
        if (!fish) return;
        _fish.Remove(fish);
        _origLayer.Remove(fish);
    }

    // -------- Public control --------
    public static void HideAll()
    {
        if (_hidden) return;
        _hidden = true;

        foreach (var go in EnumerateFish())
            ApplyHidden(go, true);
    }

    public static void ShowAll()
    {
        if (!_hidden) return;
        _hidden = false;

        foreach (var go in EnumerateFish())
            ApplyHidden(go, false);

        _origLayer.Clear();
    }

    // -------- Internals --------
    private static IEnumerable<GameObject> EnumerateFish()
    {
        // Prefer registry
        bool any = false;
        foreach (var go in _fish)
        {
            if (go) { any = true; yield return go; }
        }
        if (any) yield break;

        // Fallback: tag-based search if nothing registered yet
        var tagged = GameObject.FindGameObjectsWithTag("Fish");
        for (int i = 0; i < tagged.Length; i++)
            if (tagged[i]) yield return tagged[i];
    }

    private static void ApplyHidden(GameObject root, bool hidden)
    {
        if (!root) return;

        // Renderers
        var rs = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < rs.Length; i++)
            if (rs[i]) rs[i].enabled = !hidden;

        // Colliders 3D & 2D
        var c3 = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < c3.Length; i++)
            if (c3[i]) c3[i].enabled = !hidden;

        var c2 = root.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < c2.Length; i++)
            if (c2[i]) c2[i].enabled = !hidden;

        // Layer swap for click-through
        if (hidden)
        {
            if (!_origLayer.ContainsKey(root))
                _origLayer[root] = root.layer;
            SetLayerRecursive(root, 2); // IgnoreRaycast
        }
        else
        {
            if (_origLayer.TryGetValue(root, out int original))
                SetLayerRecursive(root, original);
        }
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        if (!go) return;
        go.layer = layer;
        var t = go.transform;
        for (int i = 0; i < t.childCount; i++)
            SetLayerRecursive(t.GetChild(i).gameObject, layer);
    }
}
