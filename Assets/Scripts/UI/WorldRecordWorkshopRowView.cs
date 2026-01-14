using GalacticFishing;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class WorldRecordWorkshopRowView : MonoBehaviour
{
    [Header("Auto-wired (by name)")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text rightText;

    [Header("Optional")]
    [SerializeField] private GameObject progressBarBackground; // will be disabled if found
    [SerializeField] private GameObject levelText;             // will be disabled if found

    [Header("Unknown / Undiscovered")]
    [SerializeField] private Sprite unknownSprite;
    [TextArea] [SerializeField] private string undiscoveredMessage = "Not Found Yet!\nFish More!";

    private void Awake()
    {
        AutoWire();
        DisableUpgradeVisuals();
    }

    private void AutoWire()
    {
        // LeftBlock/Icon
        if (!iconImage)
        {
            var t = transform.Find("LeftBlock/Icon");
            if (t) iconImage = t.GetComponent<Image>();
        }

        // TextBlock/TitleText, DescriptionText
        if (!titleText)
        {
            var t = transform.Find("TextBlock/TitleText");
            if (t) titleText = t.GetComponent<TMP_Text>();
        }

        if (!descriptionText)
        {
            var t = transform.Find("TextBlock/DescriptionText");
            if (t) descriptionText = t.GetComponent<TMP_Text>();
        }

        // RightBlock/PriceText
        if (!rightText)
        {
            var t = transform.Find("RightBlock/PriceText");
            if (t) rightText = t.GetComponent<TMP_Text>();
        }

        // Optional upgrade-only objects
        if (!progressBarBackground)
        {
            var t = transform.Find("TextBlock/ProgressBarBackground");
            if (t) progressBarBackground = t.gameObject;
        }

        if (!levelText)
        {
            var t = transform.Find("TextBlock/LevelText");
            if (t) levelText = t.gameObject;
        }
    }

    private void DisableUpgradeVisuals()
    {
        if (progressBarBackground) progressBarBackground.SetActive(false);
        if (levelText) levelText.SetActive(false);
    }

    public void Bind(Fish fish, int registryId, bool discovered, float pbWkg, float pbLcm, float wrWkg, float wrLcm)
    {
        string fishName = fish ? (string.IsNullOrWhiteSpace(fish.displayName) ? fish.name : fish.displayName) : "Unknown Fish";

        if (titleText) titleText.text = fishName;

        if (iconImage)
        {
            if (discovered && fish && fish.sprite)
            {
                iconImage.sprite = fish.sprite;
                iconImage.enabled = true;
                iconImage.gameObject.SetActive(true);
            }
            else
            {
                iconImage.sprite = unknownSprite;
                iconImage.enabled = unknownSprite != null;
                iconImage.gameObject.SetActive(unknownSprite != null);
            }
        }

        if (descriptionText)
        {
            if (!discovered)
                descriptionText.text = undiscoveredMessage;
            else
                descriptionText.text = $"Your PB: {pbWkg:0.00} kg / {pbLcm:0.#} cm";
        }

        if (rightText)
        {
            rightText.text = $"World Record\n{wrWkg:0.00} kg / {wrLcm:0.#} cm";
        }
    }
}
