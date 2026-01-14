// Assets/Scripts/UI/HookCard/HookCardUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GalacticFishing; // For Fish + FishWorldRecords (and typically InventoryStatsService)

public enum HookState { InProgress, Caught, Escaped }
public enum HeaderMode { Text, BakedSprite }

public class HookCardUI : MonoBehaviour
{
    [Header("Behavior")]
    public bool hideOnAwake = true;
    public HeaderMode headerMode = HeaderMode.BakedSprite;

    [Header("Wiring (panel & panelRect required)")]
    public Image panel;
    public RectTransform panelRect;

    [Header("Header (either TMP text or baked sprites)")]
    public Image headerChipBG;   // ← the pill Image we position
    public TMP_Text headerText;  // used in Text mode
    public TMP_Text speciesText; // optional
    public TMP_Text subText;     // optional

    [Header("Center Icon (optional)")]
    public Image centerIcon;
    public Sprite checkSprite;
    public Sprite xSprite;

    [Header("Baked header sprites (used if HeaderMode = BakedSprite)")]
    public Sprite chipInProgress;
    public Sprite chipCaught;
    public Sprite chipEscaped;

    [Header("Text mode colors (ignored in BakedSprite mode)")]
    public Color amber = new Color32(255, 200, 87, 255);
    public Color green = new Color32(52, 211, 153, 255);
    public Color red = new Color32(248, 113, 113, 255);

    [Header("Optional internal fish image (unused in your setup)")]
    public Image fishImage;
    [Range(0.5f, 0.9f)] public float fishHeightFrac = 0.75f;

    [Header("World Record UI (optional)")]
    [Tooltip("Line like: 150.8 cm")]
    public TMP_Text worldLengthText;

    [Tooltip("Repurposed: <length> cm (used by HookCardThisFishBinder)")]
    public TMP_Text worldLengthHolderText;

    [Tooltip("Line like: 28.27 kg")]
    public TMP_Text worldWeightText;

    [Tooltip("Repurposed: <weight> kg (used by HookCardThisFishBinder)")]
    public TMP_Text worldWeightHolderText;

    [Header("Personal Best UI (optional)")]
    [Tooltip("Value-only personal best length (e.g. 84 cm). Leave null if not used.")]
    public TMP_Text personalBestLengthText;

    [Tooltip("Value-only personal best weight (e.g. 5.54 kg). Leave null if not used.")]
    public TMP_Text personalBestWeightText;

    [SerializeField] private string personalBestPlaceholder = "--";

    // ------------------------------------------------------------------
    // NPC holder name pool (fake world-record champions)
    // (names are now unused but kept around in case you ever want them.)
    // ------------------------------------------------------------------
    static readonly string[] NpcNames =
    {
        "Captain Mira",
        "Old Man Brookes",
        "Rhea Nighttide",
        "Tomas \"Hook\" Halvard",
        "Lina the Luremaster",
        "Kaito Deepline",
        "Grandma Nettie",
        "Jax Trawler",
        "Iris Stormwake",
        "Borin Reefwalker",
        "Selene Tideborn",
        "Otto Brinebeard",
        "Nyx Starfisher",
        "Gunnar Pike",
        "Mila Wavecrest",
        "Rurik Netcaster",
        "Fenna Driftwhisper",
        "Cassian Float",
        "Yara Moonhook",
        "Viktor Gull"
    };

    void Awake()
    {
        if (hideOnAwake)
            gameObject.SetActive(false);

        // Guard against empty Images to avoid white boxes at boot.
        if (headerChipBG && headerChipBG.sprite == null) headerChipBG.enabled = false;
        if (centerIcon && centerIcon.sprite == null) centerIcon.enabled = false;
        if (fishImage && fishImage.sprite == null) fishImage.enabled = false;

        // Default texts so they never show garbage.
        ClearWorldRecordFields();
        ClearPersonalBestFields();
    }

    // ------------------------------------------------------------------
    // Public API
    // ------------------------------------------------------------------

    /// <summary>
    /// Main entry used by your binder. Still only needs the name + state
    /// for header + subtext + icon.
    /// </summary>
    public void Show(string fishName, HookState state)
    {
        if (speciesText) speciesText.text = fishName ?? string.Empty;

        // ---- Header ----
        if (headerMode == HeaderMode.BakedSprite)
        {
            if (headerText) headerText.gameObject.SetActive(false);

            if (headerChipBG)
            {
                headerChipBG.preserveAspect = true;

                Sprite s = state switch
                {
                    HookState.InProgress => chipInProgress,
                    HookState.Caught => chipCaught,
                    HookState.Escaped => chipEscaped,
                    _ => null
                };

                if (s == null)
                {
                    // No pill provided for this state → hide the image (prevents white rect)
                    headerChipBG.enabled = false;
                }
                else
                {
                    headerChipBG.sprite = s;
                    headerChipBG.enabled = true;
                    headerChipBG.color = Color.white;
                }
            }
        }
        else // Text mode
        {
            if (headerText)
            {
                headerText.gameObject.SetActive(true);
                headerText.text = state switch
                {
                    HookState.InProgress => "IN-PROGRESS",
                    HookState.Caught => "CAUGHT!",
                    _ => "GOT AWAY"
                };
            }

            if (headerChipBG)
            {
                headerChipBG.enabled = true;
                headerChipBG.preserveAspect = false; // likely 9-sliced background
                headerChipBG.color = state switch
                {
                    HookState.InProgress => amber,
                    HookState.Caught => green,
                    _ => red
                };
            }
        }

        // Subline
        if (subText)
        {
            subText.text = state switch
            {
                HookState.InProgress => "Not in collection yet",
                HookState.Caught => "Added to collection",
                _ => "Escaped"
            };
        }

        // Center icon
        switch (state)
        {
            case HookState.Caught: SetCenterIcon(checkSprite); break;
            case HookState.Escaped: SetCenterIcon(xSprite); break;
            default: SetCenterIcon(null); break;
        }

        FitFish();
        transform.SetAsLastSibling();
        if (!gameObject.activeSelf) gameObject.SetActive(true);
    }

    public void Hide() => gameObject.SetActive(false);

    /// <summary>
    /// Looks up the world record for this fish and fills the WR fields.
    /// Also updates Personal Best fields (if wired).
    /// NOTE: holder fields are NO LONGER TOUCHED here – they are used
    /// by HookCardThisFishBinder for THIS FISH lines.
    /// </summary>
    public void SetWorldRecord(Fish fish, int registryId)
    {
        // If nothing is wired, do nothing.
        if (!HasAnyRecordFields())
            return;

        if (fish == null)
        {
            ClearWorldRecordFields();
            ClearPersonalBestFields();
            return;
        }

        // Always update PB (even if no world record exists)
        SetPersonalBest(registryId);

        // World record can fail independently; PB should still be shown.
        var db = FishWorldRecords.Instance;
        if (db == null)
        {
            ClearWorldRecordFields();
            return;
        }

        var rec = db.GetWorldRecord(fish);
        if (!rec.IsValid)
        {
            ClearWorldRecordFields();
            return;
        }

        // Values
        string lengthStr = $"{rec.maxLengthCm:0.#} cm";
        string weightStr = $"{rec.maxWeightKg:0.##} kg";

        // Stable fake holder names (kept in case you ever want to reuse them)
        string holderLen = PickHolderName("L", registryId, fish.name);
        string holderWgt = PickHolderName("W", registryId, fish.name);
        _ = holderLen;
        _ = holderWgt;

        // ✅ Label removed:
        if (worldLengthText)
            worldLengthText.text = lengthStr;

        if (worldWeightText)
            worldWeightText.text = weightStr;

        // IMPORTANT:
        // We intentionally do NOT touch worldLengthHolderText/worldWeightHolderText here.
        // Those are now driven exclusively by HookCardThisFishBinder.
    }

    // ------------------------------------------------------------------
    // Layout helpers
    // ------------------------------------------------------------------

    void OnRectTransformDimensionsChange() => FitFish();

    void FitFish()
    {
        if (!fishImage || !panelRect || !fishImage.sprite) return;

        var r = panelRect.rect;
        float targetH = r.height * fishHeightFrac;
        float aspect = fishImage.sprite.rect.width / fishImage.sprite.rect.height;
        float targetW = targetH * aspect;

        var frt = fishImage.rectTransform;
        frt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetH);
        frt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetW);
        frt.anchoredPosition = Vector2.zero;
    }

    void SetCenterIcon(Sprite s)
    {
        if (!centerIcon) return;
        if (s == null)
        {
            centerIcon.enabled = false;
            return;
        }

        centerIcon.sprite = s;
        centerIcon.enabled = true;
        centerIcon.preserveAspect = true;
    }

    // ------------------------------------------------------------------
    // World-record + PB helpers
    // ------------------------------------------------------------------

    bool HasAnyRecordFields()
    {
        // WR lines OR PB lines mean this method is relevant.
        return worldLengthText || worldWeightText || personalBestLengthText || personalBestWeightText;
    }

    void ClearWorldRecordFields()
    {
        // ✅ Label removed:
        if (worldLengthText)
            worldLengthText.text = "--";

        if (worldWeightText)
            worldWeightText.text = "--";

        // NOTE: We intentionally leave the holder fields alone here so
        // HookCardThisFishBinder can fully own their contents.
    }

    void ClearPersonalBestFields()
    {
        if (personalBestLengthText)
            personalBestLengthText.text = personalBestPlaceholder;

        if (personalBestWeightText)
            personalBestWeightText.text = personalBestPlaceholder;
    }

    void SetPersonalBest(int fishId)
    {
        // Length PB
        if (personalBestLengthText)
        {
            if (InventoryStatsService.TryGetLengthRecord(fishId, out var lenRec))
                personalBestLengthText.text = $"{lenRec.lengthCm:0.#} cm";
            else
                personalBestLengthText.text = personalBestPlaceholder;
        }

        // Weight PB
        if (personalBestWeightText)
        {
            if (InventoryStatsService.TryGetWeightRecord(fishId, out var wRec))
                personalBestWeightText.text = $"{wRec.weightKg:0.##} kg";
            else
                personalBestWeightText.text = personalBestPlaceholder;
        }
    }

    static string PickHolderName(string salt, int registryId, string fishName)
    {
        // Simple stable hash so the same fish always maps to the same NPC.
        unchecked
        {
            int h = 23;
            h = h * 31 + registryId;

            if (!string.IsNullOrEmpty(salt))
                h = h * 31 + salt.GetHashCode();

            if (!string.IsNullOrEmpty(fishName))
            {
                for (int i = 0; i < fishName.Length; i++)
                    h = h * 31 + fishName[i];
            }

            if (h < 0) h = -h;
            return NpcNames[h % NpcNames.Length];
        }
    }
}
