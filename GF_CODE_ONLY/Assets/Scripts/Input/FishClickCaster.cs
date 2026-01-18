using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;

[System.Serializable] public class FishClickedEvent : UnityEvent<FishIdentity> { }
[System.Serializable] public class MissClickedEvent : UnityEvent<Vector2> { }

public class FishClickCaster : MonoBehaviour
{
    [Header("Assign")]
    [SerializeField] private Camera worldCamera;        // leave empty -> Camera.main
    [SerializeField] private LayerMask fishMask = ~0;   // set to Fish layer or Everything

    [Header("Events")]
    public FishClickedEvent OnFishClicked;
    public MissClickedEvent OnMissClicked;

    void Awake()
    {
        if (!worldCamera) worldCamera = Camera.main;
    }

    void Update()
    {
        // NEW Input System only
        bool pressed = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        if (!pressed) return;

        if (!worldCamera) { worldCamera = Camera.main; if (!worldCamera) return; }

        Vector2 screen = Mouse.current.position.ReadValue();
        Vector2 world  = worldCamera.ScreenToWorldPoint(screen);

        var hits = Physics2D.OverlapPointAll(world, fishMask);

        FishIdentity best = null;
        int bestKey = int.MinValue;

        foreach (var h in hits)
        {
            var id = h.GetComponentInParent<FishIdentity>();
            if (!id) continue;

            var sr = id.GetComponentInChildren<SpriteRenderer>();
            int layerVal = sr ? SortingLayer.GetLayerValueFromID(sr.sortingLayerID) : 0;
            int order    = sr ? sr.sortingOrder : 0;
            int key      = (layerVal << 16) + order;
            float z      = sr ? sr.transform.position.z : id.transform.position.z;
            key += Mathf.RoundToInt(-z * 10f);

            if (key > bestKey) { bestKey = key; best = id; }
        }

        if (best != null) OnFishClicked?.Invoke(best);
        else              OnMissClicked?.Invoke(world);
    }
}
