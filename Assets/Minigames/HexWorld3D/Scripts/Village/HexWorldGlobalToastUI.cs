using System.Collections;
using TMPro;
using UnityEngine;

namespace GalacticFishing.Minigames.HexWorld
{
    /// <summary>
    /// Global UI component to handle toast messages from the HexWorld3DController.
    /// This should be placed on a persistent UI object that is not deactivated by tab switching.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HexWorldGlobalToastUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private HexWorld3DController controller;
        [SerializeField] private CanvasGroup toastGroup;
        [SerializeField] private TMP_Text toastText;

        [Header("Settings")]
        [SerializeField] private float toastFadeIn = 0.08f;
        [SerializeField] private float toastHold = 1.2f;
        [SerializeField] private float toastFadeOut = 0.25f;

        private Coroutine _toastCo;

        private void Awake()
        {
            // Initialise the toast group as invisible and non-blocking [1]
            if (toastGroup)
            {
                toastGroup.alpha = 0f;
                toastGroup.interactable = false;
                toastGroup.blocksRaycasts = false;
            }

            // Fallback: try to find the text in children if not assigned [1]
            if (!toastText && toastGroup) 
                toastText = toastGroup.GetComponentInChildren<TMP_Text>(true);
        }

        private void OnEnable()
        {
            if (controller)
            {
                // Subscribe to the controller's toast requests [2]
                controller.ToastRequested += ShowToast;
            }
        }

        private void OnDisable()
        {
            if (controller)
            {
                controller.ToastRequested -= ShowToast;
            }
        }

        private void ShowToast(string msg)
        {
            // Critical Activity Check: Prevent coroutine errors on hidden objects [Option 1]
            if (!gameObject.activeInHierarchy || !toastGroup || !toastText) 
                return;

            toastText.text = msg;

            // Restart the routine if a new message arrives [3]
            if (_toastCo != null) StopCoroutine(_toastCo);
            _toastCo = StartCoroutine(ToastRoutine());
        }

        private IEnumerator ToastRoutine()
        {
            // Fade In [3]
            float t = 0f;
            while (t < toastFadeIn)
            {
                t += Time.unscaledDeltaTime;
                toastGroup.alpha = toastFadeIn <= 0f ? 1f : Mathf.Clamp01(t / toastFadeIn);
                yield return null;
            }
            toastGroup.alpha = 1f;

            // Hold [3]
            float hold = 0f;
            while (hold < toastHold)
            {
                hold += Time.unscaledDeltaTime;
                yield return null;
            }

            // Fade Out [4]
            float o = 0f;
            while (o < toastFadeOut)
            {
                o += Time.unscaledDeltaTime;
                toastGroup.alpha = 1f - (toastFadeOut <= 0f ? 1f : Mathf.Clamp01(o / toastFadeOut));
                yield return null;
            }
            toastGroup.alpha = 0f;
            _toastCo = null;
        }
    }
}
