// Assets/Scripts/UI/InventoryDetailView.cs
// Detail panel that shows best records + Breeding Tank,
// with optional faded fish sprite in the background.

using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventoryDetailView : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] private CanvasGroup group;
    [SerializeField] private TMP_Text   title;
    [SerializeField] private TMP_Text   body;
    [SerializeField] private Button     backButton;

    [Header("Optional visuals")]
    [Tooltip("Big faded fish behind the text.")]
    [SerializeField] private Image backgroundImage;
    [Tooltip("Alpha used when a background sprite is set.")]
    [SerializeField, Range(0f, 1f)] private float backgroundAlpha = 0.18f;

    [Header("Layout")]
    [SerializeField, Min(1)]
    [Tooltip("Currently not used for multiple entries – stats service exposes the best record per category.")]
    private int entriesPerList = 1;

    /// <summary>
    /// Ensure there is a DetailArea under the given ContentFrame and
    /// that it has an InventoryDetailView on it. If the hierarchy
    /// doesn't exist yet, it will be created.
    /// </summary>
    public static InventoryDetailView EnsureUnder(RectTransform contentFrame)
    {
        var existing = contentFrame.transform.Find("DetailArea") as RectTransform;
        if (!existing)
        {
            var go = new GameObject("DetailArea", typeof(RectTransform));
            existing = (RectTransform)go.transform;
            existing.SetParent(contentFrame, false);
            existing.anchorMin = Vector2.zero;
            existing.anchorMax = Vector2.one;
            existing.offsetMin = Vector2.zero;
            existing.offsetMax = Vector2.zero;

            // Background image (sits on this object; children render on top)
            var bg = go.AddComponent<Image>();
            bg.raycastTarget = false;
            bg.color = new Color(1f, 1f, 1f, 0f);  // invisible until we set a sprite

            // CanvasGroup controls visibility/interactability
            var cg = go.AddComponent<CanvasGroup>();
            cg.alpha          = 0f;
            cg.interactable   = false;
            cg.blocksRaycasts = false;

            // Vertical layout for Title / Body / Button
            var v = go.AddComponent<VerticalLayoutGroup>();
            v.childAlignment = TextAnchor.UpperLeft;
            v.padding        = new RectOffset(24, 24, 24, 24);
            v.spacing        = 12f;

            // Title
            var tGo = new GameObject("Title", typeof(RectTransform));
            var tRt = (RectTransform)tGo.transform;
            tRt.SetParent(existing, false);
            var t = tGo.AddComponent<TextMeshProUGUI>();
            t.text      = "Fish";
            t.fontSize  = 36;
            t.color     = Color.white;
            t.alignment = TextAlignmentOptions.TopLeft;

            // Body
            var bGo = new GameObject("Body", typeof(RectTransform));
            var bRt = (RectTransform)bGo.transform;
            bRt.SetParent(existing, false);
            var b = bGo.AddComponent<TextMeshProUGUI>();
            b.text               = "";
            b.fontSize           = 30;
            b.enableWordWrapping = true;
            b.color              = Color.white;
            b.alignment          = TextAlignmentOptions.TopLeft;

            // Back button
            var btnGo = new GameObject("Back", typeof(RectTransform), typeof(Image), typeof(Button));
            var btnRt = (RectTransform)btnGo.transform;
            btnRt.SetParent(existing, false);
            btnRt.sizeDelta = new Vector2(220, 48);
            var img = btnGo.GetComponent<Image>();
            img.color = new Color(1, 1, 1, 0.05f);
            var btn = btnGo.GetComponent<Button>();
            btn.transition = Selectable.Transition.None;

            var btnTextGo = new GameObject("Text", typeof(RectTransform));
            var btnTextRt = (RectTransform)btnTextGo.transform;
            btnTextRt.SetParent(btnRt, false);
            btnTextRt.anchorMin = Vector2.zero;
            btnTextRt.anchorMax = Vector2.one;
            btnTextRt.offsetMin = Vector2.zero;
            btnTextRt.offsetMax = Vector2.zero;
            var btnTmp = btnTextGo.AddComponent<TextMeshProUGUI>();
            btnTmp.text      = "BACK";
            btnTmp.alignment = TextAlignmentOptions.Center;
            btnTmp.fontSize  = 26;
            btnTmp.color     = Color.white;

            var view = go.AddComponent<InventoryDetailView>();
            view.group           = cg;
            view.title           = t;
            view.body            = b;
            view.backButton      = btn;
            view.backgroundImage = bg;
            return view;
        }
        else
        {
            // Re-use existing DetailArea set up in the scene
            var view = existing.GetComponent<InventoryDetailView>() ??
                       existing.gameObject.AddComponent<InventoryDetailView>();

            if (!view.group)           view.group           = existing.GetComponent<CanvasGroup>();
            if (!view.backgroundImage) view.backgroundImage = existing.GetComponent<Image>();

            return view;
        }
    }

    // --------------------------------------------------------------------
    // Public API
    // --------------------------------------------------------------------

    /// <summary>
    /// Main entry: show detail with an optional background sprite.
    /// </summary>
    public void Show(string niceName, int registryId, Sprite backgroundSprite, System.Action onBack)
    {
        // ----- Background image -----
        if (backgroundImage)
        {
            if (backgroundSprite)
            {
                backgroundImage.enabled        = true;
                backgroundImage.sprite         = backgroundSprite;
                backgroundImage.preserveAspect = true;

                var c = backgroundImage.color;
                c.a = backgroundAlpha;
                backgroundImage.color = c;

                // Fill the panel
                var rt = backgroundImage.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
            else
            {
                backgroundImage.enabled = false;
            }
        }

        // ----- Text + stats -----
        title.text = niceName;

        var sb = new StringBuilder(768);

        // TOTAL CAUGHT (via InventoryService.All())
        int total = 0;
        foreach (var (id, _, count) in InventoryService.All())
        {
            if (id == registryId)
            {
                total = count;
                break;
            }
        }

        sb.AppendLine($"Total caught: {total}");
        sb.AppendLine();

        // Helper to format the best record with cross-stats
        string CrossStats(InventoryStatsService.Record r, char mode)
        {
            return mode switch
            {
                'W' => $"1. {r.weightKg:0.###} kg (Q {r.quality}, L {r.lengthCm:0.#} cm)",
                'L' => $"1. {r.lengthCm:0.#} cm (Q {r.quality}, W {r.weightKg:0.###} kg)",
                'Q' => $"1. Q{r.quality} (W {r.weightKg:0.###} kg, L {r.lengthCm:0.#} cm)",
                _   => ""
            };
        }

        // NOTE:
        // InventoryStatsService currently exposes only the *best* record per
        // category, not a full Top N list. So we label these as "Best — ...".

        // WEIGHT
        sb.AppendLine("Best — Weight (kg)");
        if (InventoryStatsService.TryGetWeightRecord(registryId, out var rw))
            sb.AppendLine("  " + CrossStats(rw, 'W'));
        else
            sb.AppendLine("  —  no records yet");
        sb.AppendLine();

        // LENGTH
        sb.AppendLine("Best — Length (cm)");
        if (InventoryStatsService.TryGetLengthRecord(registryId, out var rl))
            sb.AppendLine("  " + CrossStats(rl, 'L'));
        else
            sb.AppendLine("  —  no records yet");
        sb.AppendLine();

        // QUALITY
        sb.AppendLine("Best — Quality");
        if (InventoryStatsService.TryGetQualityRecord(registryId, out var rq))
            sb.AppendLine("  " + CrossStats(rq, 'Q'));
        else
            sb.AppendLine("  —  no records yet");
        sb.AppendLine();

        // BREEDING TANK
        sb.Append("BREEDING TANK: ");
        if (!InventoryStatsService.BreedingUnlocked)
        {
            sb.AppendLine("Breeding not yet unlocked");
        }
        else
        {
            var tank = InventoryStatsService.GetBreedingTank(registryId, 3);
            if (tank.Count == 0)
            {
                sb.AppendLine("(empty)");
            }
            else
            {
                sb.AppendLine();
                for (int i = 0; i < tank.Count; i++)
                {
                    var r = tank[i];
                    sb.AppendLine(
                        $"  • {niceName} — W {r.weightKg:0.###} kg, L {r.lengthCm:0.#} cm, Q {r.quality}");
                }
            }
        }

        body.text = sb.ToString();

        backButton.onClick.RemoveAllListeners();
        backButton.onClick.AddListener(() => { onBack?.Invoke(); });

        group.alpha          = 1f;
        group.interactable   = true;
        group.blocksRaycasts = true;
        gameObject.SetActive(true);
    }

    /// <summary>
    /// Backwards-compatible overload (no background sprite).
    /// Used by the main inventory grid.
    /// </summary>
    public void Show(string niceName, int registryId, System.Action onBack)
    {
        Show(niceName, registryId, null, onBack);
    }

    public void Hide()
    {
        group.alpha          = 0f;
        group.interactable   = false;
        group.blocksRaycasts = false;
        gameObject.SetActive(false);
    }
}
