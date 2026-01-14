using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-10)]
public class FishVisibilityManager : MonoBehaviour
{
    [Header("Where are the fish?")]
    [Tooltip("Parent transform that contains all currently spawned fish. If left null, the manager will use tag 'Fish'.")]
    public Transform fishRoot;

    [Tooltip("If fishRoot is null, all GameObjects with this tag are treated as fish.")]
    public string fishTag = "Fish";

    [Header("What to toggle while the panel is visible?")]
    public bool disableRenderers = true;
    public bool disableColliders = true;
    [Tooltip("Temporarily move fish to IgnoreRaycast (2) so they can't be targeted/clicked.")]
    public bool switchLayerToIgnoreRaycast = true;

    [Header("Optional extras that should also be disabled while panel is open")]
    public List<GameObject> extraToDisable = new List<GameObject>();

    // cache for restoring original layers
    private readonly Dictionary<GameObject, int> _originalLayer = new Dictionary<GameObject, int>(256);

    void OnEnable()
    {
        HookCardService.OverlayShown += OnOverlayShown;
        HookCardService.OverlayHidden += OnOverlayHidden;
    }

    void OnDisable()
    {
        HookCardService.OverlayShown -= OnOverlayShown;
        HookCardService.OverlayHidden -= OnOverlayHidden;
    }

    private void OnOverlayShown(HookState state)
    {
        // We want to hide/disable for both success and fail panels.
        if (state == HookState.Caught || state == HookState.Escaped)
        {
            SetFishVisible(false);
            SetExtrasActive(false);
        }
    }

    private void OnOverlayHidden()
    {
        SetFishVisible(true);
        SetExtrasActive(true);
    }

    private void SetExtrasActive(bool active)
    {
        for (int i = 0; i < extraToDisable.Count; i++)
        {
            var go = extraToDisable[i];
            if (go) go.SetActive(active);
        }
    }

    private void SetFishVisible(bool visible)
    {
        // Collect targets
        var roots = GetFishRoots();

        for (int r = 0; r < roots.Count; r++)
        {
            var root = roots[r];
            if (!root) continue;

            // Renderers
            if (disableRenderers)
            {
                var renderers = root.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                    if (renderers[i]) renderers[i].enabled = visible;
            }

            // Colliders (3D & 2D)
            if (disableColliders)
            {
                var cols = root.GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < cols.Length; i++)
                    if (cols[i]) cols[i].enabled = visible;

                var cols2D = root.GetComponentsInChildren<Collider2D>(true);
                for (int i = 0; i < cols2D.Length; i++)
                    if (cols2D[i]) cols2D[i].enabled = visible;
            }

            // Layer swap for click/hover prevention
            if (switchLayerToIgnoreRaycast)
                SwapLayerRecursive(root.gameObject, visible);
        }
    }

    private List<Transform> GetFishRoots()
    {
        var list = new List<Transform>(128);

        if (fishRoot)
        {
            // All children of this container are treated as fish roots.
            for (int i = 0; i < fishRoot.childCount; i++)
                list.Add(fishRoot.GetChild(i));
        }
        else
        {
            // Fallback: all objects tagged as fishTag
            var tagged = GameObject.FindGameObjectsWithTag(fishTag);
            for (int i = 0; i < tagged.Length; i++)
                list.Add(tagged[i].transform);
        }

        return list;
    }

    private void SwapLayerRecursive(GameObject go, bool restore)
    {
        if (!go) return;

        if (restore)
        {
            if (_originalLayer.TryGetValue(go, out int layer))
            {
                go.layer = layer;
            }
        }
        else
        {
            if (!_originalLayer.ContainsKey(go))
                _originalLayer.Add(go, go.layer);

            go.layer = 2; // IgnoreRaycast
        }

        // Recurse
        var t = go.transform;
        for (int i = 0; i < t.childCount; i++)
            SwapLayerRecursive(t.GetChild(i).gameObject, restore);
    }
}
