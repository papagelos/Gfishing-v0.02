using UnityEngine;
using TMPro;

/// <summary>
/// Shows THIS FISH weight/length on the HookCard.
///
/// It does NOT read from InventoryStatsService or any other global state.
/// CatchToInventory is responsible for calling SetFromThisFish(...) after a
/// successful catch. This binder just stores those values and formats them.
///
/// Attach this to the same GameObject as HookCardUI (your HookCard root),
/// and wire:
///   Weight Text -> WR_Weightholder
///   Length Text -> WR_Lengthholder
/// </summary>
public sealed class HookCardThisFishBinder : MonoBehaviour
{
    /// <summary>
    /// Global instance used by systems like CatchToInventory.
    /// There should be exactly one of these in your UI.
    /// </summary>
    public static HookCardThisFishBinder Instance { get; private set; }

    [Header("UI (assign in inspector)")]
    [Tooltip("Text that should show: <weight> kg")]
    [SerializeField] private TMP_Text weightText;

    [Tooltip("Text that should show: <length> cm")]
    [SerializeField] private TMP_Text lengthText;

    [Header("Formatting")]
    [SerializeField] private string weightFormat = "{0:0.##} kg";
    [SerializeField] private string lengthFormat = "{0:0.#} cm";

    [Tooltip("Used when there is no valid value yet.")]
    [SerializeField] private string placeholderText = "—";

    [Header("Debug")]
    [SerializeField] private bool logs = true;

    // internal state
    float _weightKg;
    float _lengthCm;
    bool _hasWeight;
    bool _hasLength;

    private void Awake()
    {
        // Simple singleton – we only ever want one binder.
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning(
                $"[HookCardThisFishBinder] Multiple instances detected. " +
                $"Keeping Instance on '{Instance.gameObject.name}', disabling this one on '{gameObject.name}'.",
                this
            );
            enabled = false;
            return;
        }

        Instance = this;
    }

    private void OnEnable()
    {
        // When the HookCard becomes visible, push whatever we currently have.
        // On the very first show this will normally be the placeholder.
        Apply();
    }

    /// <summary>
    /// Called by CatchToInventory.OnFishHooked(...) with the *current* fish stats.
    /// </summary>
    public void SetFromThisFish(float weightKg, float lengthCm)
    {
        _weightKg = weightKg;
        _lengthCm = lengthCm;
        _hasWeight = weightKg > 0f;
        _hasLength = lengthCm > 0f;

        if (logs)
        {
            Debug.Log(
                $"[HookCardThisFishBinder] SetFromThisFish → " +
                $"W={_weightKg:0.###} (hasW={_hasWeight}), " +
                $"L={_lengthCm:0.#} (hasL={_hasLength})",
                this
            );
        }

        Apply();
    }

    /// <summary>
    /// Pushes stored values into the TMP_Text fields.
    /// </summary>
    private void Apply()
    {
        if (weightText != null)
        {
            if (_hasWeight)
                weightText.text = string.Format(weightFormat, _weightKg);
            else
                weightText.text = placeholderText;
        }

        if (lengthText != null)
        {
            if (_hasLength)
                lengthText.text = string.Format(lengthFormat, _lengthCm);
            else
                lengthText.text = placeholderText;
        }
    }
}
