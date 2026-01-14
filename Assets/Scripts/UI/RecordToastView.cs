// Assets/Scripts/UI/RecordToastView.cs
// Fancy “NEW RECORD” toast with diploma frame, supports
// any combination of weight / length records,
// plus optional world-record info. Quality is ignored here.

using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public sealed class RecordToastView : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private CanvasGroup group;
    [SerializeField] private TMP_Text headerText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private Button closeButton;

    [Header("Colors & Style")]
    [SerializeField] private Color headerColor = new Color(1f, 0.9f, 0.5f);     // gold-ish
    [SerializeField] private Color numberColor = Color.green;
    [SerializeField] private Color worldRecordColor = new Color(1f, 0.8f, 0.2f);

    [Header("Timing")]
    [SerializeField] private float fadeDuration = 0.15f;
    [SerializeField] private float autoHideSeconds = 15f;

    [Header("Debug")]
    [SerializeField] private bool debugClicks = false;

    private Coroutine _fadeRoutine;
    private float _autoHideAt = -1f;

    private void Awake()
    {
        if (!group)
            group = GetComponent<CanvasGroup>();

        // If someone forgot to wire it, try to find it safely.
        if (!closeButton)
            closeButton = GetComponentInChildren<Button>(true);

        // IMPORTANT: don't wipe inspector wiring. Just add ours.
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(OnCloseClicked);
        }
        else if (debugClicks)
        {
            Debug.LogWarning("[RecordToastView] closeButton is NULL. Click will never close the toast.", this);
        }

        HideImmediate();
    }

    private void OnDestroy()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(OnCloseClicked);
    }

    private void OnCloseClicked()
    {
        if (debugClicks)
            Debug.Log("[RecordToastView] Close button CLICKED → HideImmediate()", this);

        HideImmediate();
    }

    private void Update()
    {
        if (_autoHideAt > 0f && Time.unscaledTime >= _autoHideAt)
        {
            _autoHideAt = -1f;
            Hide();
        }
    }

    /// <summary>
    /// Show a new-record toast.
    /// Any of weight/length can be null, which means
    /// "this stat was not a new personal record".
    ///
    /// worldWeightKg/worldLengthCm are the *theoretical* world records
    /// for this species (optional). isWorldWeight/isWorldLength flag
    /// if this particular catch now matches or beats them.
    ///
    /// NOTE: Quality is intentionally ignored here and not displayed.
    /// </summary>
    public void ShowNewRecord(
        string fishName,
        float? weightKg,
        float? lengthCm,
        float? worldWeightKg = null,
        float? worldLengthCm = null,
        bool isWorldWeight = false,
        bool isWorldLength = false)
    {
        if (!group) return;

        if (debugClicks)
            Debug.Log($"[RecordToastView] ShowNewRecord('{fishName}') called. weight={weightKg?.ToString() ?? "null"}, length={lengthCm?.ToString() ?? "null"}", this);

        // ----- HEADER -----
        if (headerText != null)
        {
            string hdrCol = ColorUtility.ToHtmlStringRGB(headerColor);
            headerText.text = $"<b><color=#{hdrCol}>NEW RECORD!</color></b>";
        }

        // ----- BODY -----
        if (bodyText != null)
        {
            var sb = new StringBuilder(256);
            var parts = new System.Collections.Generic.List<string>();

            if (weightKg.HasValue) parts.Add("heaviest");
            if (lengthCm.HasValue) parts.Add("longest");

            if (parts.Count > 0)
            {
                string joined = parts.Count == 1 ? parts[0] : $"{parts[0]} and {parts[1]}";
                sb.AppendLine($"This is the {joined} {fishName} you have ever caught!");
            }
            else
            {
                sb.AppendLine($"New record for {fishName}!");
            }

            sb.AppendLine();

            string numCol = ColorUtility.ToHtmlStringRGB(numberColor);
            string worldCol = ColorUtility.ToHtmlStringRGB(worldRecordColor);

            if (weightKg.HasValue)
                sb.AppendLine($"<b><color=#{numCol}>Weight: {weightKg.Value:0.##} kg</color></b>");

            if (lengthCm.HasValue)
                sb.AppendLine($"<b><color=#{numCol}>Length: {lengthCm.Value:0.#} cm</color></b>");

            if (worldWeightKg.HasValue || worldLengthCm.HasValue)
            {
                sb.AppendLine();
                sb.AppendLine($"<b><color=#{worldCol}>World record for this species:</color></b>");

                if (worldWeightKg.HasValue)
                {
                    sb.Append($"  <color=#{worldCol}>Weight: {worldWeightKg.Value:0.##} kg</color>");
                    if (isWorldWeight)
                        sb.Append("  <b><color=#FFFFFF>YOU now hold the world record!</color></b>");
                    sb.AppendLine();
                }

                if (worldLengthCm.HasValue)
                {
                    sb.Append($"  <color=#{worldCol}>Length: {worldLengthCm.Value:0.#} cm</color>");
                    if (isWorldLength)
                        sb.Append("  <b><color=#FFFFFF>YOU now hold the world record!</color></b>");
                    sb.AppendLine();
                }
            }

            bodyText.text = sb.ToString();
        }

        // ----- FADE IN -----
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);

        gameObject.SetActive(true);

        group.interactable = true;
        group.blocksRaycasts = true;

        // Make sure the actual button is interactable too.
        if (closeButton != null)
            closeButton.interactable = true;

        _fadeRoutine = StartCoroutine(FadeCanvas(group, group.alpha, 1f, fadeDuration));

        _autoHideAt = autoHideSeconds > 0f
            ? Time.unscaledTime + autoHideSeconds
            : -1f;
    }

    public void Hide()
    {
        if (!group) return;
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(FadeOutAndDisable());
    }

    public void HideImmediate()
    {
        if (!group) return;

        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);

        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        gameObject.SetActive(false);
    }

    private System.Collections.IEnumerator FadeOutAndDisable()
    {
        yield return FadeCanvas(group, group.alpha, 0f, fadeDuration);
        group.interactable = false;
        group.blocksRaycasts = false;
        gameObject.SetActive(false);
    }

    private static System.Collections.IEnumerator FadeCanvas(CanvasGroup cg, float from, float to, float duration)
    {
        if (!cg) yield break;

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float u = duration > 0f ? Mathf.Clamp01(t / duration) : 1f;
            cg.alpha = Mathf.Lerp(from, to, u);
            yield return null;
        }

        cg.alpha = to;
    }
}
