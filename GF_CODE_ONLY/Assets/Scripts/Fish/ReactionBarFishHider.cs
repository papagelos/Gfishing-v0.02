using UnityEngine;
using UnityEngine.UI;

/// Hides all fish as soon as the ReactionBar becomes visible.
/// Does NOT restore them when the bar hides; the panel will restore after linger.
/// This avoids fish popping back when the bar fades out and the ✓/✗ panel appears.
public class ReactionBarFishHider : MonoBehaviour
{
    [Header("How do we detect visibility? (assign if you have them)")]
    public CanvasGroup canvasGroup;      // assign if the bar fades with a CanvasGroup
    public Image barImage;               // assign your BarImage (Image) if you have one
    [Tooltip("Optional: additional objects that must be active for the bar to count as 'shown' (e.g., Marker).")]
    public GameObject[] alsoWatch;

    // internal: prevent re-hiding spam each frame
    bool _hiddenByThis;

    void Awake()
    {
        // Best-effort auto-wire to reduce setup mistakes
        if (!canvasGroup) canvasGroup = GetComponent<CanvasGroup>();
        if (!barImage)
        {
            var imgs = GetComponentsInChildren<Image>(true);
            for (int i = 0; i < imgs.Length; i++)
            {
                var n = imgs[i].name.ToLowerInvariant();
                if (n.Contains("barimage") || n.Contains("bar_image") || n.Contains("bar"))
                { barImage = imgs[i]; break; }
            }
        }
    }

    void OnEnable()
    {
        _hiddenByThis = false;
        if (IsBarVisible())
        {
            FishWorldVisibility.HideAll();
            _hiddenByThis = true;
        }
    }

    // IMPORTANT: do NOT restore here — the panel is about to show and should keep fish hidden
    void OnDisable()
    {
        _hiddenByThis = false;
    }

    void Update()
    {
        // If the bar becomes visible (e.g., alpha fades in while GO stays active), hide fish once.
        if (!_hiddenByThis && IsBarVisible())
        {
            FishWorldVisibility.HideAll();
            _hiddenByThis = true;
        }
        // When the bar becomes invisible we do nothing here — restoration happens after the panel’s linger.
    }

    bool IsBarVisible()
    {
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy) return false;

        // If a CanvasGroup is present and in use, require some alpha.
        if (canvasGroup && canvasGroup.gameObject.activeInHierarchy && canvasGroup.enabled)
            if (canvasGroup.alpha <= 0.01f) return false;

        // If a bar Image is provided, require it be enabled and not fully transparent.
        if (barImage)
            if (!barImage.enabled || barImage.color.a <= 0.01f) return false;

        // Optional extra objects that must be active
        if (alsoWatch != null)
            for (int i = 0; i < alsoWatch.Length; i++)
                if (alsoWatch[i] && !alsoWatch[i].activeInHierarchy) return false;

        return true;
    }
}
