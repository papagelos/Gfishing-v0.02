using GalacticFishing;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class WorldRecordRowView : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] private Image    iconImage;
    [SerializeField] private TMP_Text fishNameText;
    [SerializeField] private TMP_Text personalBestText;
    [SerializeField] private TMP_Text worldRecordText;   // can be on a Button or plain TMP
    [SerializeField] private Button   worldRecordButton; // optional

    [Header("Unknown / Undiscovered")]
    [SerializeField] private Sprite   unknownSprite;
    [TextArea]
    [SerializeField] private string   undiscoveredMessage = "Not Found Yet!\nFish More!";
    [Tooltip("If true: hide the icon visually, but KEEP its layout space (do not deactivate GameObject).")]
    [SerializeField] private bool     hideIconWhenUndiscovered = false;

    public struct RowData
    {
        public Fish fish;
        public int  registryId;

        public bool discovered;

        public bool  hasPBWeight;
        public float pbWeightKg;

        public bool  hasPBLength;
        public float pbLengthCm;

        public float wrWeightKg;
        public float wrLengthCm;

        public string worldRecordHolder; // optional (can be empty)
    }

    public void Bind(in RowData d)
    {
        bool discovered = d.discovered;

        // --- Fish name ---
        string fishName;
        if (!discovered)
        {
            fishName = "UNKNOWN NAME";
        }
        else
        {
            fishName = d.fish
                ? (string.IsNullOrWhiteSpace(d.fish.displayName) ? d.fish.name : d.fish.displayName)
                : "Unknown Fish";
        }

        if (fishNameText)
            fishNameText.text = fishName;

        // --- Icon ---
        // IMPORTANT: Never SetActive(false) here, because that collapses the layout
        // and makes later rows "jump left" when icon is missing.
        if (iconImage)
        {
            iconImage.gameObject.SetActive(true);

            if (discovered && d.fish && d.fish.sprite)
            {
                iconImage.sprite  = d.fish.sprite;
                iconImage.enabled = true;
            }
            else
            {
                if (hideIconWhenUndiscovered)
                {
                    // Hide visually but keep layout space.
                    iconImage.sprite  = null;
                    iconImage.enabled = false;
                }
                else
                {
                    // Show unknown sprite if provided; otherwise keep space with disabled image.
                    iconImage.sprite  = unknownSprite;
                    iconImage.enabled = (unknownSprite != null);
                }
            }
        }

        // --- Personal best text ---
        if (personalBestText)
        {
            if (!discovered)
            {
                personalBestText.text = undiscoveredMessage;
            }
            else
            {
                string w = d.hasPBWeight ? $"{d.pbWeightKg:0.00} kg" : "--";
                string l = d.hasPBLength ? $"{d.pbLengthCm:0.#} cm" : "--";
                personalBestText.text = $"Your PB: {w} / {l}";
            }
        }

        // --- World record block (right side) ---
        // Format: 3 lines always.
        if (worldRecordText)
        {
            worldRecordText.enableWordWrapping = false;

            if (!discovered)
            {
                worldRecordText.text =
                    "World Record\n" +
                    "? Kg\n" +
                    "? cm";
            }
            else
            {
                // If holder exists, keep it on the FIRST line so it stays 3 lines total.
                string header = "World Record";
                if (!string.IsNullOrWhiteSpace(d.worldRecordHolder))
                    header += $" - {d.worldRecordHolder}";

                worldRecordText.text =
                    header + "\n" +
                    $"{d.wrWeightKg:0.00} kg\n" +
                    $"{d.wrLengthCm:0.#} cm";
            }
        }

        // Button is optional; we don’t force any click behavior here.
        if (worldRecordButton)
            worldRecordButton.interactable = true;
    }
}
