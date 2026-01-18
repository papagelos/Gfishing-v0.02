using System.Collections.Generic;
using GalacticFishing;
using UnityEngine;
using UnityEngine.UI;

public sealed class WorldRecordListView_WorkshopStyle : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private CanvasGroup group;
    [SerializeField] private Button closeButton;

    [Header("Scroll")]
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform contentRoot;

    [Header("Row Source (choose one)")]
    [Tooltip("Optional: assign a separate row prefab. If null, we will look for a disabled child under contentRoot named RowTemplate (or any row view).")]
    [SerializeField] private WorldRecordWorkshopRowView rowPrefab;

    [Header("Data")]
    [SerializeField] private FishRegistry fishRegistry;

    [Header("Behaviour")]
    [SerializeField] private bool hideOnStart = true;
    [SerializeField] private bool snapToTopOnRefresh = true;

    private WorldRecordWorkshopRowView _templateInScene; // RowTemplate child (if used)
    private readonly List<WorldRecordWorkshopRowView> _spawned = new();

    private void Awake()
    {
        if (!group) group = GetComponent<CanvasGroup>();

        if (closeButton)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Hide);
        }

        if (hideOnStart) HideImmediate();
    }

    public void Show()
    {
        EnsureRowTemplate();
        Refresh();

        gameObject.SetActive(true);
        if (group)
        {
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
        }
    }

    public void Hide()
    {
        if (group)
        {
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }
        gameObject.SetActive(false);
    }

    private void HideImmediate()
    {
        if (group)
        {
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }
        gameObject.SetActive(false);
    }

    private void EnsureRowTemplate()
    {
        if (!contentRoot) return;

        // If user assigned a prefab, we don't need a scene template.
        if (rowPrefab) return;

        // Find an existing row view under contentRoot (your RowTemplate).
        if (!_templateInScene)
            _templateInScene = contentRoot.GetComponentInChildren<WorldRecordWorkshopRowView>(true);

        if (_templateInScene)
        {
            // Treat this as template, keep it disabled.
            _templateInScene.gameObject.SetActive(false);

            // Remove any other children that might have been left in the prefab (old clones).
            // (Keep ONLY the template.)
            for (int i = contentRoot.childCount - 1; i >= 0; i--)
            {
                var child = contentRoot.GetChild(i);
                if (child == _templateInScene.transform) continue;
                Destroy(child.gameObject);
            }
        }
        else
        {
            Debug.LogError("[WorldRecordListView_WorkshopStyle] No rowPrefab assigned and no RowTemplate (WorldRecordWorkshopRowView) found under ContentRoot.", this);
        }
    }

    private WorldRecordWorkshopRowView SpawnRow()
    {
        if (!contentRoot) return null;

        WorldRecordWorkshopRowView src = rowPrefab ? rowPrefab : _templateInScene;
        if (!src) return null;

        var r = Instantiate(src, contentRoot);
        r.gameObject.SetActive(true);
        r.name = $"WorldRecordRow_{_spawned.Count:000}";
        _spawned.Add(r);
        return r;
    }

    private void ClearSpawned()
    {
        for (int i = 0; i < _spawned.Count; i++)
        {
            if (_spawned[i]) Destroy(_spawned[i].gameObject);
        }
        _spawned.Clear();
    }

    private void Refresh()
    {
        if (!contentRoot) return;

        // Init inventory service (needed for InventoryService.All())
        if (fishRegistry && !InventoryService.IsInitialized)
            InventoryService.Initialize(fishRegistry);

        if (!InventoryService.IsInitialized)
        {
            Debug.LogWarning("[WorldRecordListView_WorkshopStyle] InventoryService not initialized.", this);
            return;
        }

        ClearSpawned();

        int shown = 0;

        // NOTE: This iterates whatever InventoryService.All() returns (often "all registry entries with counts").
        foreach (var (registryId, fish, count) in InventoryService.All())
        {
            if (!fish) continue;

            // "Discovered" must NOT rely on count (auto-sell keeps it at 0).
            bool hasW = InventoryStatsService.TryGetWeightRecord(registryId, out var wRec);
            bool hasL = InventoryStatsService.TryGetLengthRecord(registryId, out var lRec);
            bool discovered = hasW || hasL;

            var wr = FishWorldRecords.Instance.GetWorldRecord(fish);
            if (!wr.IsValid) continue;

            var row = SpawnRow();
            if (!row) break;

            float pbW = hasW ? wRec.weightKg : 0f;
            float pbL = hasL ? lRec.lengthCm : 0f;

            row.Bind(
                fish: fish,
                registryId: registryId,
                discovered: discovered,
                pbWkg: pbW,
                pbLcm: pbL,
                wrWkg: wr.maxWeightKg,
                wrLcm: wr.maxLengthCm
            );

            shown++;
        }

        if (snapToTopOnRefresh && scrollRect)
            scrollRect.verticalNormalizedPosition = 1f;

        Debug.Log($"[WorldRecordListView_WorkshopStyle] Built rows: {shown}", this);
    }
}
