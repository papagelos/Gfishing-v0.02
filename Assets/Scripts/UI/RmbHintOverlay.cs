using UnityEngine;
using TMPro;
using UnityEngine.UI;
using GalacticFishing.UI;

public class RmbHintOverlay : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text hintText;
    [SerializeField] private Image mouseIcon;

    [Header("State sources")]
    [Tooltip("Assign the scene MenuRouter.")]
    [SerializeField] private MenuRouter menuRouter;

    [Tooltip("Assign the Hub controller (Panel_Hub object).")]
    [SerializeField] private FullscreenHubController hubController;

    [Header("Labels")]
    [SerializeField] private string labelBack = "BACK";
    [SerializeField] private string labelMainHub = "Main Hub";

    [Header("Optional")]
    [Tooltip("If true, hide the hint while the hub is open.")]
    [SerializeField] private bool hideWhenHubOpen = true;

    private string _lastText;
    private bool _lastVisible;

    private void Reset()
    {
        hintText = GetComponentInChildren<TMP_Text>(true);
        mouseIcon = GetComponentInChildren<Image>(true);
    }

    private void Awake()
    {
        Apply(true);
    }

    private void Update()
    {
        Apply(false);
    }

    private void Apply(bool force)
    {
        bool hubOpen = (hubController != null && hubController.IsOpen);

        if (hideWhenHubOpen && hubOpen)
        {
            SetVisible(false, force);
            return;
        }

        // If hub is open, RMB acts like BACK (closes hub -> returns to world).
        // If any other menu is open, RMB also acts like BACK.
        bool shouldShowBack = hubOpen || UIBlocksGameplay.GameplayBlocked;

        string wanted = shouldShowBack ? labelBack : labelMainHub;

        SetVisible(true, force);

        if (force || wanted != _lastText)
        {
            _lastText = wanted;
            if (hintText != null) hintText.text = wanted;
        }
    }

    private void SetVisible(bool on, bool force)
    {
        if (!force && on == _lastVisible) return;
        _lastVisible = on;

        if (hintText != null) hintText.enabled = on;
        if (mouseIcon != null) mouseIcon.enabled = on;
    }
}
