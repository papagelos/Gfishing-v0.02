using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GalacticFishing.Progress;   // for PlayerProgressManager

/// <summary>
/// Visual for one row in the Workshop list.
/// - Icon / title / description
/// - Segmented progress bar (level / maxLevel)
/// - "level/maxLevel" text
/// - Price text ("100\nUpgrade", "100\nCan't Afford!", or "Purchased")
/// - Right-block color changes with state
/// </summary>
public class WorkshopUpgradeRowView : MonoBehaviour
{
    [Header("Left side")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;

    [Header("Progress")]
    [Tooltip("Container under ProgressBarBackground (e.g. 'SegmentsRoot').")]
    [SerializeField] private RectTransform segmentContainer;
    [SerializeField] private TMP_Text levelText;      // "8/13"

    [Header("Progress colors")]
    [SerializeField] private Color segmentFilledColor = Color.white;
    [SerializeField] private Color segmentEmptyColor  = new Color(1f, 1f, 1f, 0.15f);
    [SerializeField] private float segmentSpacing     = 4f;

    [Header("Right side")]
    [SerializeField] private TMP_Text priceText;      // "100\nUpgrade"
    [SerializeField] private Button  priceButton;     // RightBlock button
    [SerializeField] private Image   priceBackground; // Image on RightBlock

    [Header("Right-side colors")]
    [SerializeField] private Color purchasableColor  = new Color32(0x7F, 0x3A, 0x93, 0xFF);
    [SerializeField] private Color cantAffordColor   = new Color32(0x55, 0x55, 0x55, 0xFF);
    [SerializeField] private Color purchasedColor    = new Color32(0x33, 0x66, 0x33, 0xFF);

    private WorkshopUpgrade _data;
    private Action<WorkshopUpgrade> _onClicked;

    // ---- segmented progress internals ----
    private static Sprite _solidWhiteSprite;
    private readonly List<Image> _segments = new List<Image>();

    private static Sprite SolidWhiteSprite
    {
        get
        {
            if (_solidWhiteSprite == null)
            {
                var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                tex.SetPixel(0, 0, Color.white);
                tex.Apply();

                _solidWhiteSprite = Sprite.Create(
                    tex,
                    new Rect(0, 0, 1, 1),
                    new Vector2(0.5f, 0.5f),
                    1f
                );
            }

            return _solidWhiteSprite;
        }
    }

    private void Awake()
    {
        // Ensure the container has a HorizontalLayoutGroup we control
        if (segmentContainer != null)
        {
            var h = segmentContainer.GetComponent<HorizontalLayoutGroup>();
            if (h == null)
                h = segmentContainer.gameObject.AddComponent<HorizontalLayoutGroup>();

            h.padding = new RectOffset(0, 0, 0, 0);
            h.spacing = segmentSpacing;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = true;
        }
    }

    /// <summary>
    /// Called by WorkshopUI when building the list.
    /// </summary>
    public void Bind(WorkshopUpgrade data, bool canAfford, Action<WorkshopUpgrade> onClicked)
    {
        _data      = data;
        _onClicked = onClicked;

        if (titleText != null)       titleText.text       = data.title;
        if (descriptionText != null) descriptionText.text = data.description;
        // NOTE: We do NOT touch iconImage.sprite here.
        // The prefab sprite stays in control so your fish icon keeps working.

        RefreshPriceAndProgress(canAfford);

        if (priceButton != null)
        {
            priceButton.onClick.RemoveAllListeners();
            priceButton.onClick.AddListener(OnClickPrice);
        }
    }

    /// <summary>
    /// Call this from WorkshopUI whenever:
    /// - level changed
    /// - maxLevel changed
    /// - player currency changed (can/can’t afford)
    /// </summary>
    public void RefreshPriceAndProgress(bool canAfford)
    {
        RefreshProgress();
        RefreshPrice(canAfford);
    }

    // ---------- PROGRESS (segmented) ----------

    private void EnsureSegments(int count)
    {
        if (segmentContainer == null)
            return;

        if (count <= 0)
            count = 1;

        // Rebuild only when max level changed
        if (_segments.Count == count)
            return;

        // Clear old ones
        for (int i = 0; i < _segments.Count; i++)
        {
            if (_segments[i] != null)
                Destroy(_segments[i].gameObject);
        }
        _segments.Clear();

        // Make sure layout has the right spacing (could be changed in inspector)
        var h = segmentContainer.GetComponent<HorizontalLayoutGroup>();
        if (h == null)
            h = segmentContainer.gameObject.AddComponent<HorizontalLayoutGroup>();

        h.spacing = segmentSpacing;

        // Create new segments
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject($"Segment_{i}", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(segmentContainer, false);

            var img = go.GetComponent<Image>();
            img.sprite = SolidWhiteSprite;
            img.type   = Image.Type.Simple;
            img.color  = segmentEmptyColor;

            var le = go.GetComponent<LayoutElement>();
            le.flexibleWidth  = 1;
            le.flexibleHeight = 1;

            _segments.Add(img);
        }
    }

    private void RefreshProgress()
    {
        int max = Mathf.Max(1, _data.maxLevel);
        int lvl = Mathf.Clamp(_data.level, 0, max);

        EnsureSegments(max);

        for (int i = 0; i < _segments.Count; i++)
        {
            var img = _segments[i];
            if (img == null) continue;

            img.color = (i < lvl) ? segmentFilledColor : segmentEmptyColor;
        }

        if (levelText != null)
        {
            levelText.text = $"{lvl}/{max}";
        }
    }

    // ---------- PRICE / BUTTON ----------

    private void RefreshPrice(bool canAfford)
    {
        bool isMaxed = _data.IsMaxed;

        if (priceText != null)
        {
            if (isMaxed)
            {
                priceText.text = "Purchased";
            }
            else if (!canAfford)
            {
                priceText.text = $"{_data.cost}\nCan't Afford!";
            }
            else
            {
                priceText.text = $"{_data.cost}\nUpgrade";
            }
        }

        if (priceButton != null)
        {
            priceButton.interactable = !isMaxed && canAfford;
        }

        if (priceBackground != null)
        {
            if (isMaxed)
                priceBackground.color = purchasedColor;
            else if (!canAfford)
                priceBackground.color = cantAffordColor;
            else
                priceBackground.color = purchasableColor;
        }
    }

    private void OnClickPrice()
    {
        if (_data == null)
            return;

        // Do not buy if already maxed.
        if (_data.IsMaxed)
            return;

        var ppm = PlayerProgressManager.Instance;
        if (ppm == null)
        {
            Debug.LogWarning("[WorkshopUpgradeRowView] No PlayerProgressManager instance found.");
            return;
        }

        // Current credits from the save data
        float currentCredits = ppm.GetCredits();   // assumes GetCredits() exists, as NotebookLM described
        bool canAffordNow = currentCredits >= _data.cost;

        if (!canAffordNow)
        {
            // Live re-check: show "Can't Afford!" and block purchase.
            RefreshPrice(false);
            if (priceButton != null)
                priceButton.interactable = false;

            Debug.Log("[WorkshopUpgradeRowView] Player can't afford this upgrade.");
            return;
        }

        // Spend credits directly from the save data
        if (ppm.Data != null && ppm.Data.currency != null)
        {
            ppm.Data.currency.credits -= _data.cost;
            if (ppm.Data.currency.credits < 0f)
                ppm.Data.currency.credits = 0f;

            Debug.Log($"[WorkshopUpgradeRowView] Spent {_data.cost:N0} credits. New total: {ppm.Data.currency.credits:N0}");
        }

        // Let higher-level WorkshopUI actually apply the upgrade
        // (increase level, apply effects, rebuild the list, etc.)
        _onClicked?.Invoke(_data);
    }
}
