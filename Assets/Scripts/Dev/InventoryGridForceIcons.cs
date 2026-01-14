using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using GalacticFishing;

public class InventoryGridForceIcons : MonoBehaviour
{
    [SerializeField] InventoryGridController grid;
    [SerializeField] string iconFields = "icon,sprite,thumbnail,preview,image,Icon,Sprite,Thumbnail,Preview,Image";
    [SerializeField] bool runOnStart = true;
    [SerializeField] bool verbose = false;
    [SerializeField] bool ignoreZeroCheck = false;

    void Reset() => grid = GetComponent<InventoryGridController>();
    void Start() { if (runOnStart) Apply(); }

    [ContextMenu("Apply Now")]
    public void Apply()
    {
        if (!grid) grid = GetComponent<InventoryGridController>();
        if (!grid || grid.FishRegistry == null)
        {
            Debug.LogWarning("[InventoryGridForceIcons] Missing grid or FishRegistry.");
            return;
        }

        var slots = grid.Content
            ? grid.Content.GetComponentsInChildren<InventorySlot>(true)
            : Array.Empty<InventorySlot>();
        var fishes = grid.FishRegistry.fishes;
        if (slots.Length == 0 || fishes == null || fishes.Count == 0)
        {
            Debug.LogWarning("[InventoryGridForceIcons] No slots or fishes found.");
            return;
        }

        var names = iconFields.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
        int n = Mathf.Min(slots.Length, fishes.Count);
        int assigned = 0;
        for (int i = 0; i < n; i++)
        {
            var fish = fishes[i];
            var slot = slots[i];
            if (!ignoreZeroCheck && slot.CurrentCount <= 0) continue; // keep question mark

            var sprite = ResolveSprite(fish, names);
            if (sprite != null)
            {
                slot.SetIcon(sprite);
                assigned++;
                if (verbose && i < 4) Debug.Log($"[InventoryGridForceIcons] #{i} -> {sprite.name}");
            }
            else if (verbose && i < 4)
            {
                Debug.Log($"[InventoryGridForceIcons] #{i} -> (no sprite found)");
            }
        }
        if (verbose) Debug.Log($"[InventoryGridForceIcons] Assigned {assigned}/{n} icons.");
    }

    public void ApplyForce()
    {
        var prev = ignoreZeroCheck;
        ignoreZeroCheck = true;
        Apply();
        ignoreZeroCheck = prev;
    }

    static Sprite ResolveSprite(object obj, string[] candidateNames)
    {
        if (obj == null) return null;
        var t = obj.GetType();
        const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        foreach (var name in candidateNames)
        {
            var f = t.GetField(name, BF);
            if (f != null)
            {
                if (f.GetValue(obj) is Sprite s) return s;
            }
        }
        foreach (var name in candidateNames)
        {
            var p = t.GetProperty(name, BF);
            if (p != null && p.CanRead)
            {
                if (p.GetValue(obj) is Sprite s) return s;
            }
        }
        return null;
    }
}
