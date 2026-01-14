using System;
using System.Collections.Generic;
using System.Text;
using GalacticFishing;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class WorldRecordListView : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private CanvasGroup group;
    [SerializeField] private Button closeButton;

    [Header("Scroll List Mode (preferred)")]
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private WorldRecordRowView rowPrefab; // should be a ROW (RowTemplate), not the whole panel

    [Header("Data")]
    [Tooltip("Needed so InventoryService can be initialized (for registryId -> Fish mapping).")]
    [SerializeField] private FishRegistry fishRegistry;

    [Tooltip("If true, hides undiscovered fish from the list.")]
    [SerializeField] private bool showOnlyDiscovered = false;

    [Tooltip("Sort discovered fish first.")]
    [SerializeField] private bool discoveredFirst = true;

    [Tooltip("Scroll to top after refresh.")]
    [SerializeField] private bool snapToTopOnRefresh = true;

    [Header("Fallback Text Mode (optional)")]
    [SerializeField] private TMP_Text listText;
    [SerializeField] private RectTransform viewport;

    [Header("Behaviour")]
    [SerializeField] private bool hideOnStart = true;

    // Old text layout widths (fallback mode only)
    private const int FishColWidth = 31;
    private const int WorldColWidth = 26;

    private readonly List<WorldRecordRowView> _spawned = new();

    private struct SpeciesRow
    {
        public Fish fish;
        public int registryId;
    }

    private void Awake()
    {
        AutoWireIfMissing();

        if (closeButton)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Hide);
        }

        if (hideOnStart) HideImmediate();
        else RefreshList();
    }

    private void OnValidate()
    {
        // Helps catch “forgot to wire” cases early in editor
        if (!Application.isPlaying)
            AutoWireIfMissing();
    }

    [ContextMenu("Refresh Now")]
    public void RefreshList()
    {
        // Prefer prefab list mode if it’s wired or auto-found.
        if (rowPrefab && contentRoot)
        {
            BuildPrefabRows();
            return;
        }

        // Fallback: old text list if user hasn’t wired the row prefab yet.
        BuildTextFallback();
    }

    public void Show()
    {
        RefreshList();

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

    // ---------------------------------------------------------------------
    // PREFAB ROW MODE
    // ---------------------------------------------------------------------
    private void BuildPrefabRows()
    {
        // Ensure InventoryService is ready
        if (fishRegistry && !InventoryService.IsInitialized)
            InventoryService.Initialize(fishRegistry);

        if (!InventoryService.IsInitialized)
        {
            Debug.LogWarning("[WorldRecordListView] InventoryService not initialized. Assign FishRegistry.", this);
            return;
        }

        // If rowPrefab is a template sitting under contentRoot, keep it hidden.
        if (rowPrefab && rowPrefab.transform.IsChildOf(contentRoot))
            rowPrefab.gameObject.SetActive(false);

        // Collect species (registryId is what stats uses)
        var rows = CollectSpeciesFromInventoryService();

        if (rows.Count == 0)
        {
            ClearSpawned();
            Debug.Log("[WorldRecordListView] No species available from InventoryService.All().", this);
            return;
        }

        // Sort by discovery/name
        rows.Sort((a, b) =>
        {
            bool aDisc = IsDiscovered(a.registryId);
            bool bDisc = IsDiscovered(b.registryId);

            if (discoveredFirst && aDisc != bDisc)
                return bDisc.CompareTo(aDisc);

            string nameA = GetFishName(a.fish);
            string nameB = GetFishName(b.fish);
            return string.Compare(nameA, nameB, StringComparison.OrdinalIgnoreCase);
        });

        // Spawn/bind
        int outIndex = 0;
        foreach (var r in rows)
        {
            bool discovered = IsDiscovered(r.registryId);
            if (showOnlyDiscovered && !discovered)
                continue;

            var wr = FishWorldRecords.Instance.GetWorldRecord(r.fish);
            if (!wr.IsValid)
                continue;

            bool hasW = InventoryStatsService.TryGetWeightRecord(r.registryId, out var wRec);
            bool hasL = InventoryStatsService.TryGetLengthRecord(r.registryId, out var lRec);

            var view = GetOrCreateRow(outIndex++);
            view.gameObject.SetActive(true);

            var data = new WorldRecordRowView.RowData
            {
                fish = r.fish,
                registryId = r.registryId,

                discovered = discovered,

                hasPBWeight = hasW,
                pbWeightKg = hasW ? wRec.weightKg : 0f,

                hasPBLength = hasL,
                pbLengthCm = hasL ? lRec.lengthCm : 0f,

                wrWeightKg = wr.maxWeightKg,
                wrLengthCm = wr.maxLengthCm,

                // You don’t have a holder system yet; leave empty.
                worldRecordHolder = ""
            };

            view.Bind(data);
        }

        // Hide any extra pooled rows
        for (int i = outIndex; i < _spawned.Count; i++)
            _spawned[i].gameObject.SetActive(false);

        if (snapToTopOnRefresh && scrollRect)
            scrollRect.verticalNormalizedPosition = 1f;
    }

    private List<SpeciesRow> CollectSpeciesFromInventoryService()
    {
        var seen = new HashSet<int>();
        var rows = new List<SpeciesRow>(256);

        foreach (var (registryId, fish, count) in InventoryService.All())
        {
            if (!fish) continue;
            if (!seen.Add(registryId)) continue;
            rows.Add(new SpeciesRow { fish = fish, registryId = registryId });
        }

        return rows;
    }

    private WorldRecordRowView GetOrCreateRow(int index)
    {
        while (_spawned.Count <= index)
        {
            var v = Instantiate(rowPrefab, contentRoot);
            v.name = $"WorldRecordRow_{_spawned.Count:000}";
            _spawned.Add(v);
        }
        return _spawned[index];
    }

    private void ClearSpawned()
    {
        for (int i = 0; i < _spawned.Count; i++)
        {
            if (_spawned[i])
                Destroy(_spawned[i].gameObject);
        }
        _spawned.Clear();
    }

    private static bool IsDiscovered(int registryId)
    {
        // Auto-sell friendly: stats exist even if inventory count is 0.
        bool hasW = InventoryStatsService.TryGetWeightRecord(registryId, out _);
        bool hasL = InventoryStatsService.TryGetLengthRecord(registryId, out _);
        bool hasQ = InventoryStatsService.TryGetQualityRecord(registryId, out _);
        return hasW || hasL || hasQ;
    }

    private static string GetFishName(Fish fish)
    {
        if (!fish) return "Unknown Fish";
        return string.IsNullOrWhiteSpace(fish.displayName) ? fish.name : fish.displayName;
    }

    // ---------------------------------------------------------------------
    // TEXT FALLBACK MODE
    // ---------------------------------------------------------------------
    private void BuildTextFallback()
    {
        if (!listText)
            return;

        if (fishRegistry && !InventoryService.IsInitialized)
            InventoryService.Initialize(fishRegistry);

        if (!InventoryService.IsInitialized)
        {
            listText.text = "Assign FishRegistry so InventoryService can initialize.";
            ResizeScrollContentToText();
            return;
        }

        var rows = CollectSpeciesFromInventoryService();

        if (rows.Count == 0)
        {
            listText.text = "No species available yet.";
            ResizeScrollContentToText();
            return;
        }

        rows.Sort((a, b) =>
        {
            string nameA = GetFishName(a.fish);
            string nameB = GetFishName(b.fish);
            return string.Compare(nameA, nameB, StringComparison.OrdinalIgnoreCase);
        });

        var sb = new StringBuilder(4096);

        sb.AppendLine(
            "FISH".PadRight(FishColWidth) +
            "WORLD RECORD".PadRight(WorldColWidth) +
            "YOUR RECORD"
        );

        sb.AppendLine(
            "----".PadRight(FishColWidth) +
            "------------".PadRight(WorldColWidth) +
            "-----------"
        );

        foreach (var row in rows)
        {
            var fish = row.fish;
            int registryId = row.registryId;

            var rec = FishWorldRecords.Instance.GetWorldRecord(fish);
            if (!rec.IsValid) continue;

            string fishName = GetFishName(fish);
            string worldLabel = $"{rec.maxWeightKg:0.00} kg / {rec.maxLengthCm:0.#} cm";

            string playerLabel = "--";
            if (InventoryStatsService.TryGetWeightRecord(registryId, out var wRec) &&
                InventoryStatsService.TryGetLengthRecord(registryId, out var lRec))
            {
                playerLabel = $"{wRec.weightKg:0.00} kg / {lRec.lengthCm:0.#} cm";
            }

            sb.AppendLine(
                fishName.PadRight(FishColWidth) +
                worldLabel.PadRight(WorldColWidth) +
                playerLabel
            );
        }

        listText.text = sb.ToString();
        ResizeScrollContentToText();
    }

    private void ResizeScrollContentToText()
    {
        if (!listText || !viewport || !contentRoot)
            return;

        Canvas.ForceUpdateCanvases();
        listText.ForceMeshUpdate();

        float preferred = listText.preferredHeight;

        const float topPadding = 20f;
        const float bottomPadding = 20f;

        var textRT = listText.rectTransform;
        var textSize = textRT.sizeDelta;
        textSize.y = preferred + bottomPadding;
        textRT.sizeDelta = textSize;

        float viewportHeight = viewport.rect.height;
        float targetContentHeight = Mathf.Max(viewportHeight, preferred + topPadding + bottomPadding);

        var contentSize = contentRoot.sizeDelta;
        contentSize.y = targetContentHeight;
        contentRoot.sizeDelta = contentSize;

        contentRoot.anchoredPosition = Vector2.zero;
    }

    // ---------------------------------------------------------------------
    // Auto-wiring to reduce “I missed a field” pain
    // ---------------------------------------------------------------------
    private void AutoWireIfMissing()
    {
        if (!group) group = GetComponent<CanvasGroup>();

        if (!scrollRect)
            scrollRect = GetComponentInChildren<ScrollRect>(true);

        if (!contentRoot && scrollRect)
            contentRoot = scrollRect.content;

        // If you forgot to assign rowPrefab, try to find a template row under the content
        if (!rowPrefab && contentRoot)
        {
            rowPrefab = contentRoot.GetComponentInChildren<WorldRecordRowView>(true);
            if (rowPrefab && rowPrefab.transform.IsChildOf(contentRoot))
                rowPrefab.gameObject.SetActive(false);
        }
    }
}
