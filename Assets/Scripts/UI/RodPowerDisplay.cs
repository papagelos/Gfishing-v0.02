using UnityEngine;
using TMPro;
using GalacticFishing.Progress;
using GalacticFishing; // GFishSpawner
using GalacticFishing.UI; // FullscreenHubController, UIBlocksGameplay

[DisallowMultipleComponent]
public class RodPowerDisplay : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] private TMP_Text label;
    [SerializeField] private PlayerProgressManager progressManager;

    [Tooltip("If not assigned, we auto-find one in the scene.")]
    [SerializeField] private GFishSpawner fishSpawner;

    [Tooltip("Optional. If assigned, we also treat Hub-open as 'in a menu'.")]
    [SerializeField] private FullscreenHubController hubController;

    [Header("Visibility")]
    [Tooltip("Hide the display while any menu is open (including Hub).")]
    [SerializeField] private bool hideWhenInMenu = true;

    [Header("Formatting")]
    [SerializeField] private string rodPowerFormat = "Rod Power: {0:0}";
    [SerializeField] private string nextUnlockFormat = "Next Fish Unlocked At Rodpower: {0:0}";
    [SerializeField] private string unknownText = "Next Fish Unlocked At Rodpower: ?";
    [SerializeField] private string allUnlockedText = "All fish unlocked in this lake!";

    [Header("Refresh")]
    [SerializeField] private float refreshSeconds = 0.25f;

    private float _nextRefresh;
    private bool _lastVisible = true;

    private void Awake()
    {
        if (!progressManager)
            progressManager = PlayerProgressManager.Instance;

        if (!fishSpawner)
            fishSpawner = FindFirstObjectByType<GFishSpawner>();

        // hubController is optional; you can drag it in, or leave it null.
    }

    private void Update()
    {
        if (!label || !progressManager) return;

        // --- Visibility gate ---
        if (hideWhenInMenu)
        {
            bool hubOpen = (hubController != null && hubController.IsOpen);
            bool inMenu = hubOpen || UIBlocksGameplay.GameplayBlocked;

            bool shouldBeVisible = !inMenu;

            if (shouldBeVisible != _lastVisible)
            {
                _lastVisible = shouldBeVisible;
                label.enabled = shouldBeVisible;
            }

            if (!shouldBeVisible)
                return; // don't waste cycles updating text while hidden
        }
        else
        {
            if (!_lastVisible)
            {
                _lastVisible = true;
                label.enabled = true;
            }
        }

        // --- Refresh text on timer ---
        if (Time.unscaledTime < _nextRefresh) return;
        _nextRefresh = Time.unscaledTime + refreshSeconds;

        float power = progressManager.CurrentRodPower;

        string line1 = string.Format(rodPowerFormat, power);
        string line2 = BuildNextUnlockLine();

        label.text = line1 + "\n" + line2;
    }

    private string BuildNextUnlockLine()
    {
        if (!fishSpawner)
            return unknownText;

        int candidates = fishSpawner.GetCurrentLakeCandidateSpeciesCount();
        if (candidates <= 0)
            return unknownText;

        if (fishSpawner.TryGetNextUnlockPower(out float nextPower))
            return string.Format(nextUnlockFormat, nextPower);

        return allUnlockedText;
    }
}
