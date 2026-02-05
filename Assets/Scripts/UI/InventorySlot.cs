using System;
using System.Linq;
using GalacticFishing;        // Fish
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum Rarity { Common, Uncommon, Rare, Epic, Legendary, Mythic }

[Serializable]
public class ItemData
{
    // Identity info so a slot can open the right records page
    public int  RegistryId = -1;     // index in FishRegistry (or -1 for non-fish items later)
    public Fish FishDef;             // optional reference to the Fish ScriptableObject

    public Sprite Icon;
    public int    Count    = 0;
    public Rarity Rarity   = Rarity.Common;
    public bool   Disabled = false;

    [TextArea]
    public string Tooltip;           // pretty name
}

public class InventorySlot : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler
{
    [Header("Wiring")]
    [SerializeField] private Image    icon;        // main icon image (child "Icon")
    [SerializeField] private TMP_Text countLabel;  // child "Count"

    [Header("Fish Name Text (Encyclopedia)")]
    [Tooltip("Optional: if assigned, shows the fish name top-centered over the icon when discovered.")]
    [SerializeField] private TMP_Text fishNameLabel;

    [Header("Optional Visuals")]
    [SerializeField] private CanvasGroup canvasGroup;   // for dimming/disable
    [SerializeField] private Image      hoverGlow;      // optional
    [SerializeField] private Image      pressedMask;    // optional

    [Header("Legacy/Compat (optional)")]
    [SerializeField] private Image frame;               // optional (for older utilities)
    [SerializeField] private Image disabledMask;        // optional (for older utilities)
    [SerializeField] private Image countBadge;          // optional badge under the count label

    [Header("Visuals")]
    [SerializeField] private Sprite zeroCountSprite;    // fallback sprite when empty / unknown (legacy)

    [Header("Unknown Fish Text (Encyclopedia)")]
    [Tooltip("If true, unknown fish (FishDef != null, Count==0, Icon==null) will show text instead of the zeroCountSprite.")]
    [SerializeField] private bool useUnknownTextInsteadOfZeroSprite = true;

    [TextArea]
    [SerializeField] private string unknownFishText = "Not Found Yet!\nFish More!";

    [Tooltip("Optional: if assigned, this label is used. If null, the script will auto-find or auto-create one under Icon.")]
    [SerializeField] private TMP_Text unknownLabel;

    [Header("Rarity (Optional ring swap)")]
    [SerializeField] private Image rarityRing;
    [SerializeField] private Sprite common, uncommon, rare, epic, legendary, mythic;

    [Header("Formatting")]
    [SerializeField] private bool useCompactNumbers = true;

    // Turn this on temporarily if you need spammy logs again.
    private const bool VerboseLogs = false;

    // -------------------------------------------------
    // Bound data
    // -------------------------------------------------
    private ItemData bound;
    public ItemData Data => bound;
    public int CurrentCount => bound != null ? bound.Count : 0;

    /// <summary>Grid / encyclopedia listens to this to open the Records page.</summary>
    public Action<InventorySlot> Clicked;

    // -------------------------------------------------
    // LIFECYCLE
    // -------------------------------------------------
    private void Awake()
    {
        EnsureIconReference();
        EnsureCountLabelReference();
        EnsureUnknownLabelReference();
        EnsureFishNameLabelReference();

        if (icon)
        {
            icon.preserveAspect = true;
            icon.raycastTarget  = true;   // slot receives clicks via Icon image
        }

        if (countLabel)
        {
            countLabel.enabled = true;
            countLabel.gameObject.SetActive(true);
            countLabel.raycastTarget = false;
        }

        if (unknownLabel)
        {
            unknownLabel.enabled = false;
            unknownLabel.gameObject.SetActive(true);
            unknownLabel.raycastTarget = false;
        }

        if (fishNameLabel)
        {
            fishNameLabel.enabled = false;
            fishNameLabel.gameObject.SetActive(true);
            fishNameLabel.raycastTarget = false;
        }

        if (countBadge)
        {
            countBadge.raycastTarget = false;
            countBadge.enabled = true;
            if (countBadge.gameObject) countBadge.gameObject.SetActive(false);
        }

        if (hoverGlow)    hoverGlow.raycastTarget    = false;
        if (pressedMask)  pressedMask.raycastTarget  = false;
        if (disabledMask) disabledMask.raycastTarget = false;
        if (rarityRing)   rarityRing.raycastTarget   = false;
    }

    // Make sure "icon" is always valid even if prefab wiring is broken.
    private void EnsureIconReference()
    {
        if (icon) return;

        // 1) Try a direct child named "Icon"
        var tIcon = transform.Find("Icon");
        if (tIcon) icon = tIcon.GetComponent<Image>();

        // 2) Any child image whose name contains "icon"
        if (!icon)
        {
            icon = GetComponentsInChildren<Image>(true)
                   .FirstOrDefault(i =>
                       i != null &&
                       i.gameObject != gameObject &&
                       i.name.IndexOf("icon", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        // 3) Fallback: first non-root Image in hierarchy
        if (!icon)
        {
            var all = GetComponentsInChildren<Image>(true);
            if (all.Length > 1)       icon = all[1];
            else if (all.Length == 1) icon = all[0];
        }
    }

    // Make sure "countLabel" is always valid.
    private void EnsureCountLabelReference()
    {
        if (countLabel) return;

        countLabel = GetComponentsInChildren<TMP_Text>(true)
            .FirstOrDefault(t =>
                t != null &&
                t.name.IndexOf("count", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    // Find or create the unknown label (centered over the Icon area).
    private void EnsureUnknownLabelReference()
    {
        if (unknownLabel) return;

        // Try find an existing TMP label by name
        unknownLabel = GetComponentsInChildren<TMP_Text>(true)
            .FirstOrDefault(t =>
                t != null &&
                (t.name.IndexOf("unknown", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 t.name.IndexOf("not", StringComparison.OrdinalIgnoreCase) >= 0));

        if (unknownLabel) return;

        // Auto-create one under the Icon (preferred)
        EnsureIconReference();
        if (!icon) return;

        var go = new GameObject("UnknownText (TMP)", typeof(RectTransform));
        go.transform.SetParent(icon.transform, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(8, 8);
        rt.offsetMax = new Vector2(-8, -8);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.raycastTarget = false;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = true;
        tmp.fontSize = 18f;
        tmp.text = string.Empty;

        unknownLabel = tmp;
    }

    // Find or create the fish name label (top-centered over the Icon area).
    private void EnsureFishNameLabelReference()
    {
        if (fishNameLabel) return;

        // Try find an existing TMP label by name (FishName...)
        fishNameLabel = GetComponentsInChildren<TMP_Text>(true)
            .FirstOrDefault(t =>
                t != null &&
                t.name.IndexOf("fishname", StringComparison.OrdinalIgnoreCase) >= 0);

        if (fishNameLabel) return;

        // Auto-create one under the Icon (preferred)
        EnsureIconReference();
        if (!icon) return;

        var go = new GameObject("FishNameText (TMP)", typeof(RectTransform));
        go.transform.SetParent(icon.transform, false);

        var rt = (RectTransform)go.transform;

        // Top-stretch within icon, small height strip
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);

        // Offsets for a ~24px bar at the top
        rt.offsetMin = new Vector2(6f, -28f);
        rt.offsetMax = new Vector2(-6f, -4f);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.raycastTarget = false;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.fontSize = 18f;
        tmp.text = string.Empty;

        fishNameLabel = tmp;
    }

    /// <summary>
    /// Enforces draw order so:
    /// Frame/background stay behind,
    /// Icon stays above frame,
    /// Overlays (FishName / Unknown / Count) stay above icon.
    ///
    /// This fixes the issue where we call icon.transform.SetAsLastSibling()
    /// and accidentally push the icon ABOVE FishNameText in the prefab.
    /// </summary>
    private void EnforceOverlayDrawOrder()
    {
        // 1) Keep icon above frame/background
        if (icon)
            icon.transform.SetAsLastSibling();

        // 2) Keep overlays above the icon
        // Fish name (if present)
        if (fishNameLabel)
        {
            // Make sure it's above icon even if it is a sibling of Icon (your prefab case)
            fishNameLabel.transform.SetAsLastSibling();
        }

        // Unknown label (if present)
        if (unknownLabel && unknownLabel.enabled)
        {
            unknownLabel.transform.SetAsLastSibling();
        }

        // Count overlay: prefer moving the "Count" root object, not just TMP child
        if (countLabel)
        {
            Transform t = countLabel.transform;

            // If TMP is a child under a container called "Count", move the container.
            if (t.parent && t.parent.name.Equals("Count", StringComparison.OrdinalIgnoreCase))
                t = t.parent;

            t.SetAsLastSibling();
        }
    }

    // -------------------------------------------------
    // BIND
    // -------------------------------------------------
    public InventorySlot Bind(ItemData data)
    {
        bound = data;

        EnsureIconReference();
        EnsureCountLabelReference();
        EnsureUnknownLabelReference();
        EnsureFishNameLabelReference();

        int count = data != null ? data.Count : 0;

        bool isFishEntry = data != null && data.FishDef != null;
        bool isUnknownFish = isFishEntry && count <= 0 && data.Icon == null;

        // ---------- UNKNOWN LABEL ----------
        bool showUnknownText = false;
        if (unknownLabel)
        {
            showUnknownText = useUnknownTextInsteadOfZeroSprite && isUnknownFish;
            unknownLabel.enabled = showUnknownText;
            unknownLabel.text = showUnknownText ? (unknownFishText ?? string.Empty) : string.Empty;
        }

        // ---------- FISH NAME LABEL ----------
        if (fishNameLabel)
        {
            // Show name only when discovered/owned (Count > 0) and not unknown
            bool showName = isFishEntry && count > 0 && !isUnknownFish;

            fishNameLabel.enabled = showName;

            if (showName)
            {
                string n =
                    (data.FishDef != null)
                        ? (string.IsNullOrWhiteSpace(data.FishDef.displayName) ? data.FishDef.name : data.FishDef.displayName)
                        : (data.Tooltip ?? string.Empty);

                if (string.IsNullOrWhiteSpace(n))
                    n = data.Tooltip ?? string.Empty;

                fishNameLabel.text = n;
            }
            else
            {
                fishNameLabel.text = string.Empty;
            }
        }

        // Decide which sprite to show
        Sprite spriteToUse = null;

        if (data != null && count > 0 && data.Icon != null)
        {
            // Owned / discovered fish
            spriteToUse = data.Icon;
        }
        else
        {
            // Unknown fish: prefer text instead of question mark sprite
            if (!(useUnknownTextInsteadOfZeroSprite && isUnknownFish))
            {
                if (zeroCountSprite != null && data != null)
                    spriteToUse = zeroCountSprite;
            }
        }

        // ---------- ICON ----------
        if (icon)
        {
            icon.sprite = spriteToUse;

            if (useUnknownTextInsteadOfZeroSprite && isUnknownFish)
            {
                // Keep the Icon graphic alive for raycasts/hover,
                // but make it invisible so text is the “replacement”.
                icon.enabled = true;
                icon.color = new Color(1, 1, 1, 0f);
                icon.gameObject.SetActive(true);
            }
            else
            {
                icon.enabled = spriteToUse != null;
                icon.color   = Color.white;
                icon.gameObject.SetActive(spriteToUse != null);
            }
        }

        // ---------- COUNT LABEL / BADGE ----------
        if (countLabel)
        {
            bool has = data != null && count > 0;
            if (has)
            {
                countLabel.text = useCompactNumbers
                    ? Compact(count)
                    : Mathf.Max(0, count).ToString();
            }
            else
            {
                countLabel.text = string.Empty;
            }

            if (countBadge)
            {
                bool showBadge = has;
                if (countBadge.gameObject) countBadge.gameObject.SetActive(showBadge);
                countBadge.enabled = showBadge;
            }
        }

        // ---------- RARITY RING ----------
        if (rarityRing)
        {
            var r = data != null ? data.Rarity : Rarity.Common;
            rarityRing.enabled = true;
            rarityRing.sprite = r switch
            {
                Rarity.Uncommon  => uncommon,
                Rarity.Rare      => rare,
                Rarity.Epic      => epic,
                Rarity.Legendary => legendary,
                Rarity.Mythic    => mythic,
                _                => common,
            };
        }

        // Reset hover / pressed visuals
        if (hoverGlow)    hoverGlow.enabled    = false;
        if (pressedMask)  pressedMask.enabled  = false;
        if (disabledMask) disabledMask.enabled = false;

        ApplyDisabled(data != null && data.Disabled);

        // IMPORTANT: enforce hierarchy order AFTER everything is enabled/disabled.
        // This fixes FishNameText being hidden under the icon.
        EnforceOverlayDrawOrder();

        if (VerboseLogs)
        {
            var rootImage = GetComponent<Image>();
            string rootSpriteName = (rootImage && rootImage.sprite) ? rootImage.sprite.name : "null";
            Debug.Log(
                $"[InventorySlot] Bind on {name} " +
                $"tooltip='{(data?.Tooltip ?? "null")}', " +
                $"iconField={(icon ? icon.name : "null")}, " +
                $"dataIcon={(data?.Icon ? data.Icon.name : "null")}, " +
                $"count={count}, " +
                $"unknownFish={(isUnknownFish)}, " +
                $"unknownLabel={(unknownLabel ? unknownLabel.name : "null")}, " +
                $"fishNameLabel={(fishNameLabel ? fishNameLabel.name : "null")}, " +
                $"iconEnabled={(icon && icon.enabled)}, " +
                $"iconSprite={(icon && icon.sprite ? icon.sprite.name : "null")}, " +
                $"rootSprite={rootSpriteName}",
                this
            );
        }

        return this;
    }

    // -------------------------------------------------
    // Helpers
    // -------------------------------------------------
    public void SetIcon(Sprite sprite)
    {
        EnsureIconReference();
        EnsureUnknownLabelReference();
        EnsureFishNameLabelReference();

        // This method is used by some systems, keep legacy behavior:
        // if no sprite, use zeroCountSprite (unless unknown text label is active via Bind())
        Sprite s = sprite;
        if (!s && zeroCountSprite != null)
            s = zeroCountSprite;

        if (unknownLabel)
        {
            unknownLabel.enabled = false;
            unknownLabel.text = string.Empty;
        }

        if (fishNameLabel)
        {
            fishNameLabel.enabled = false;
            fishNameLabel.text = string.Empty;
        }

        if (icon)
        {
            icon.sprite  = s;
            icon.enabled = s != null;
            icon.color   = Color.white;
            icon.gameObject.SetActive(s != null);
        }

        // Keep correct draw order even when only setting icon
        EnforceOverlayDrawOrder();
    }

    private static string Compact(int value)
    {
        // Use 100,000 threshold to match game-wide abbreviation standard
        if (value >= 1_000_000_000) return (value / 1_000_000_000f).ToString("0.#") + "b";
        if (value >= 1_000_000)     return (value / 1_000_000f).ToString("0.#") + "m";
        if (value >= 100_000)       return (value / 1_000f).ToString("0.#") + "k";
        return Mathf.Max(0, value).ToString();
    }

    private void ApplyDisabled(bool disabled)
    {
        if (canvasGroup)
        {
            canvasGroup.alpha          = disabled ? 0.33f : 1f;
            canvasGroup.interactable   = !disabled;
            canvasGroup.blocksRaycasts = !disabled;
        }
        else
        {
            if (icon)         icon.color            = disabled ? new Color(1, 1, 1, 0.33f) : icon.color;
            if (countLabel)   countLabel.alpha      = disabled ? 0.5f : 1f;
            if (unknownLabel) unknownLabel.alpha    = disabled ? 0.5f : 1f;
            if (fishNameLabel) fishNameLabel.alpha  = disabled ? 0.5f : 1f;
        }

        if (disabledMask) disabledMask.enabled = disabled;
    }

    private bool IsDisabled() => bound != null && bound.Disabled;

    // -------------------------------------------------
    // Pointer events
    // -------------------------------------------------
    public void OnPointerEnter(PointerEventData _)
    {
        if (!IsDisabled() && hoverGlow) hoverGlow.enabled = true;

        if (bound != null && bound.Count > 0 && bound.Icon != null)
            InventoryHoverName.ShowUI(bound.Tooltip);
        else
            InventoryHoverName.ShowUI(""); // clears to default
    }

    public void OnPointerExit(PointerEventData _)
    {
        if (hoverGlow) hoverGlow.enabled = false;
        InventoryHoverName.ClearUI();
    }

    public void OnPointerDown(PointerEventData _)
    {
        if (!IsDisabled() && pressedMask) pressedMask.enabled = true;
    }

    public void OnPointerUp(PointerEventData _)
    {
        if (pressedMask) pressedMask.enabled = false;

        // click-to-open (only if this slot actually represents at least one owned item)
        if (!IsDisabled() && bound != null && bound.Count > 0)
        {
            Clicked?.Invoke(this);
        }
    }

    // Legacy accessors so older tools compile without changes.
    public Image    Frame         => frame;
    public Image    IconImage     => icon;
    public Image    RarityRingImg => rarityRing;
    public Image    PressedMaskImg=> pressedMask;
    public Image    DisabledMask  => disabledMask;
    public TMP_Text CountText     => countLabel;
}
