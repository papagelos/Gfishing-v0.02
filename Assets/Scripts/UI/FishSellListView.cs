// Assets/Scripts/UI/FishSellListView.cs
//
// Inventory sell list built in the same style as WorldRecordListView.
// Columns: [FISH] [SELL PRICE] [OWNED] [VALUE]
// Text-only list; actual sell buttons can be overlaid in the UI next to each row.

using System.Text;
using System.Collections.Generic;
using System.Reflection;
using GalacticFishing;
using GalacticFishing.Progress;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public sealed class FishSellListView : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private CanvasGroup   group;
    [SerializeField] private TMP_Text      listText;

    [SerializeField] private RectTransform viewport;
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private Button        closeButton;

    [Header("Behaviour")]
    [SerializeField] private bool hideOnStart = true;

    // Column character widths (for padding / centering in the big text block)
    // 1) Fish name   – widest column
    // 2) Sell price  – ~10 characters ("12345 Cr")
    // 3) Owned       – small integer
    // 4) Total value – allows e.g. "123 456 Cr"
    private const int FishColWidth   = 35;
    private const int PriceColWidth  = 10;
    private const int OwnedColWidth  = 8;
    private const int ValueColWidth  = 12;

    // We keep Fish + registry id + total owned together.
    private struct SpeciesRow
    {
        public Fish fish;
        public int  registryId;
        public int  totalCount;
    }

    private void Awake()
    {
        if (!group)
            group = GetComponent<CanvasGroup>();

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Hide);
        }

        if (hideOnStart)
            HideImmediate();
        else
            RefreshList();
    }

    /// <summary>
    /// Called from your button OnClick.
    /// Shows the panel and rebuilds the sell list.
    /// </summary>
    public void Show()
    {
        RefreshList();

        gameObject.SetActive(true);
        if (group)
        {
            group.alpha          = 1f;
            group.interactable   = true;
            group.blocksRaycasts = true;
        }
    }

    /// <summary>Can be wired to a close button.</summary>
    public void Hide()
    {
        if (group)
        {
            group.alpha          = 0f;
            group.interactable   = false;
            group.blocksRaycasts = false;
        }
        gameObject.SetActive(false);
    }

    private void HideImmediate()
    {
        if (group)
        {
            group.alpha          = 0f;
            group.interactable   = false;
            group.blocksRaycasts = false;
        }
        gameObject.SetActive(false);
    }

    // ========================================================================
    // Main list building
    // ========================================================================

    /// <summary>
    /// Builds a 4-column style list:
    /// [Fish Name] [Sell price per fish] [Owned] [Total value].
    /// Uses InventoryService.All() to aggregate counts per species.
    /// </summary>
    private void RefreshList()
    {
        if (!listText)
            return;

        // --------------------------------------------------------------------
        // 1) Collect unique species + total counts from InventoryService
        // --------------------------------------------------------------------
        var rowsById = new Dictionary<int, SpeciesRow>();

        foreach (var (registryId, fish, count) in InventoryService.All())
        {
            if (fish == null) continue;
            if (count <= 0)   continue;

            if (!rowsById.TryGetValue(registryId, out var row))
            {
                row = new SpeciesRow
                {
                    fish       = fish,
                    registryId = registryId,
                    totalCount = 0
                };
            }

            // Aggregate count across all stacks for this species.
            row.totalCount += count;
            rowsById[registryId] = row;
        }

        if (rowsById.Count == 0)
        {
            listText.text = "You do not own any fish yet.\n" +
                            "Catch some fish to populate the sell list.";
            ResizeScrollContentToText();
            return;
        }

        // Flatten to list and sort alphabetically by display name.
        var rows = new List<SpeciesRow>(rowsById.Values);
        rows.Sort((a, b) =>
        {
            string nameA = string.IsNullOrWhiteSpace(a.fish.displayName) ? a.fish.name : a.fish.displayName;
            string nameB = string.IsNullOrWhiteSpace(b.fish.displayName) ? b.fish.name : b.fish.displayName;
            return string.Compare(nameA, nameB, System.StringComparison.OrdinalIgnoreCase);
        });

        // --------------------------------------------------------------------
        // 2) Build the text block
        // --------------------------------------------------------------------
        var sb = new StringBuilder(4096);

        const int fishColWidth  = FishColWidth;
        const int priceColWidth = PriceColWidth;
        const int ownedColWidth = OwnedColWidth;
        const int valueColWidth = ValueColWidth;

        // Column headers
        sb.AppendLine(
            "FISH".PadRight(fishColWidth) +
            "SELL PRICE".PadRight(priceColWidth) +
            "OWNED".PadRight(ownedColWidth) +
            "VALUE".PadRight(valueColWidth)
        );

        sb.AppendLine(
            "----".PadRight(fishColWidth) +
            "----------".PadRight(priceColWidth) +
            "-----".PadRight(ownedColWidth) +
            "-----".PadRight(valueColWidth)
        );

        // ===== ROWS =========================================================
        foreach (var row in rows)
        {
            var fish       = row.fish;
            int registryId = row.registryId;
            int owned      = row.totalCount;

            if (owned <= 0)
                continue;

            string fishName = string.IsNullOrWhiteSpace(fish.displayName)
                ? fish.name
                : fish.displayName;

            // Sell price per fish (credits). 0 is allowed; pricing code can be
            // wired up later. We attempt to call a FishPricing.GetSellPrice(...)
            // method via reflection; if that fails we fall back to 0.
            int pricePerFish = ComputeSellPriceSafe(fish, registryId);

            long totalValue = (long)pricePerFish * owned;

            string priceLabel = pricePerFish > 0
                ? $"{pricePerFish:N0}"
                : "--";

            string ownedLabel = owned.ToString();
            string valueLabel = totalValue > 0
                ? $"{totalValue:N0}"
                : "--";

            // Wrap fish name into multiple lines that stay inside the first column.
            List<string> fishLines = WrapFishName(fishName, FishColWidth);

            for (int i = 0; i < fishLines.Count; i++)
            {
                string fishCol = fishLines[i].PadRight(FishColWidth);

                if (i == 0)
                {
                    // First display line: show all four columns.
                    string priceCol = priceLabel.PadRight(PriceColWidth);
                    string ownedCol = ownedLabel.PadRight(OwnedColWidth);
                    string valueCol = valueLabel.PadRight(ValueColWidth);

                    sb.AppendLine($"{fishCol}{priceCol}{ownedCol}{valueCol}");
                }
                else
                {
                    // Continuation lines: only the fish name in column 1.
                    sb.AppendLine(fishCol);
                }
            }
        }

        listText.text = sb.ToString();

        // Resize ScrollRect content so there is no giant empty tail.
        ResizeScrollContentToText();
    }

    /// <summary>
    /// Tries to compute a per-fish sell price using a FishPricing helper
    /// if it exists. Uses reflection so it won't break compilation if
    /// your pricing API changes; on failure returns 0.
    /// </summary>
    private int ComputeSellPriceSafe(Fish fish, int registryId)
    {
        if (fish == null)
            return 0;

        try
        {
            // Use the same assembly that contains InventoryService / pricing code.
            var asm = typeof(InventoryService).Assembly;

            // Try to locate a FishPricing type by name.
            System.Type pricingType = null;
            foreach (var t in asm.GetTypes())
            {
                if (t.Name == "FishPricing")
                {
                    pricingType = t;
                    break;
                }
            }

            if (pricingType == null)
                return 0;

            // Optional: try to build a baseline RuntimeStats instance if the
            // pricing method needs it. We don't hard-reference the type.
            object runtimeStats = null;
            try
            {
                var invStatsType = asm.GetType("GalacticFishing.Progress.InventoryStatsService") ??
                                   asm.GetType("GalacticFishing.InventoryStatsService");

                var runtimeStatsType = invStatsType?
                    .GetNestedType("RuntimeStats", BindingFlags.Public | BindingFlags.NonPublic);

                var fromMethod = runtimeStatsType?
                    .GetMethod("From", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

                if (fromMethod != null)
                {
                    // Baseline example: 1 kg, 10 cm, quality 50.
                    runtimeStats = fromMethod.Invoke(null, new object[] { 1f, 10f, 50 });
                }
            }
            catch
            {
                // If anything goes wrong we just leave runtimeStats = null.
            }

            // Find a static method named GetSellPrice.
            MethodInfo getSellPrice = null;
            foreach (var m in pricingType.GetMethods(
                         BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            {
                if (m.Name != "GetSellPrice")
                    continue;

                // We accept any overload; we'll pick the "best" one below.
                getSellPrice = m;
                break;
            }

            if (getSellPrice == null)
                return 0;

            var parameters = getSellPrice.GetParameters();
            object result;

            if (parameters.Length == 2 && runtimeStats != null)
            {
                // Assume signature matches: (Fish, RuntimeStats)
                result = getSellPrice.Invoke(null, new object[] { fish, runtimeStats });
            }
            else if (parameters.Length == 1)
            {
                // Assume signature matches: (Fish)
                result = getSellPrice.Invoke(null, new object[] { fish });
            }
            else
            {
                // Unsupported signature – bail out.
                return 0;
            }

            if (result is int i)
                return Mathf.Max(0, i);

            return 0;
        }
        catch
        {
            // Any reflection / invocation failure → treated as "no pricing".
            return 0;
        }
    }

    // ========================================================================
    // ScrollRect sizing helpers (copied from WorldRecordListView style)
    // ========================================================================

    /// <summary>
    /// After we change listText.text, adjust the Content & Text RectTransforms so
    /// the ScrollRect only scrolls over actual text (no huge blank tail).
    /// </summary>
    private void ResizeScrollContentToText()
    {
        if (!listText || !viewport || !contentRoot)
            return;

        Canvas.ForceUpdateCanvases();
        listText.ForceMeshUpdate();

        var textRT = listText.rectTransform;

        float preferred = listText.preferredHeight;

        const float topPadding    = 20f;
        const float bottomPadding = 20f;

        float textHeight = preferred + bottomPadding;

        var textSize = textRT.sizeDelta;
        textSize.y = textHeight;
        textRT.sizeDelta = textSize;

        float viewportHeight      = viewport.rect.height;
        float targetContentHeight = Mathf.Max(viewportHeight, textHeight + topPadding);

        var contentSize = contentRoot.sizeDelta;
        contentSize.y = targetContentHeight;
        contentRoot.sizeDelta = contentSize;

        // Make sure we start scrolled to the top.
        contentRoot.anchoredPosition = Vector2.zero;
    }

    // ========================================================================
    // Helper methods
    // ========================================================================

    /// <summary>
    /// Wraps a fish name so that each line is at most maxChars characters.
    /// Tries to break on '_' or space so we don't split in the middle of a word.
    /// </summary>
    private static List<string> WrapFishName(string name, int maxChars)
    {
        var lines = new List<string>();

        if (string.IsNullOrEmpty(name))
        {
            lines.Add(string.Empty);
            return lines;
        }

        int length = name.Length;
        int start  = 0;

        while (start < length)
        {
            int remaining = length - start;
            if (remaining <= maxChars)
            {
                lines.Add(name.Substring(start));
                break;
            }

            int end = start + maxChars;

            // Try to find a nicer break-point (underscore or space) going backwards.
            int breakIndex = -1;
            for (int i = end; i > start; --i)
            {
                char c = name[i - 1];
                if (c == '_' || c == ' ')
                {
                    breakIndex = i;
                    break;
                }
            }

            if (breakIndex <= start)
            {
                // No good separator found, hard break.
                breakIndex = end;
            }

            string line = name.Substring(start, breakIndex - start).TrimEnd('_', ' ');
            if (line.Length > 0)
                lines.Add(line);

            start = breakIndex;
        }

        if (lines.Count == 0)
            lines.Add(name);

        return lines;
    }
}
