using UnityEngine;

namespace GalacticFishing.UI
{
    public class ReactionBarController : MonoBehaviour
    {
        [Header("Assign the parent GameObject that contains the bar UI")]
        [SerializeField] private GameObject reactionBarRoot;

        [Header("Optional fade (CanvasGroup on the same root)")]
        [SerializeField] private float fadeDuration = 0.12f;

        private CanvasGroup _cg;
        private bool _isVisible;

        private void Awake()
        {
            if (reactionBarRoot == null)
            {
                Debug.LogError("[ReactionBarController] ReactionBarRoot is not assigned.");
                enabled = false;
                return;
            }

            _cg = reactionBarRoot.GetComponent<CanvasGroup>();
            if (_cg == null)
            {
                // Add one for quick fades (safe if you forgot)
                _cg = reactionBarRoot.AddComponent<CanvasGroup>();
            }

            // Start hidden
            reactionBarRoot.SetActive(false);
            _cg.alpha = 0f;
            _isVisible = false;
        }

        private void Update()
        {
            // Show on left-click or Space
            if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space))
            {
                ShowBar();
            }

            // Press Escape to hide (handy while testing)
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                HideBar();
            }
        }

        public void ShowBar()
        {
            if (_isVisible) return;
            reactionBarRoot.SetActive(true);
            _isVisible = true;
            StopAllCoroutines();
            StartCoroutine(FadeTo(1f, fadeDuration));
        }

        public void HideBar()
        {
            if (!_isVisible) return;
            _isVisible = false;
            StopAllCoroutines();
            StartCoroutine(FadeOutAndDeactivate());
        }

        private System.Collections.IEnumerator FadeTo(float target, float duration)
        {
            float start = _cg.alpha;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                _cg.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(t / duration));
                yield return null;
            }
            _cg.alpha = target;
        }

        private System.Collections.IEnumerator FadeOutAndDeactivate()
        {
            yield return FadeTo(0f, fadeDuration);
            reactionBarRoot.SetActive(false);
        }
    }
}
