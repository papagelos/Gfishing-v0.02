using UnityEngine;

namespace GalacticFishing.UI
{
    [AddComponentMenu("Galactic Fishing/UI/RMB Blocker Scope")]
    public sealed class RMBBlockerScope : MonoBehaviour
    {
        [Header("Modal detection (recommended)")]
        [Tooltip("If set, we block RMB only while this CanvasGroup is modal (active + blocksRaycasts + alpha > threshold). If null, we auto-find.")]
        [SerializeField] private CanvasGroup targetGroup;

        [Tooltip("Alpha must be above this to count as visible.")]
        [SerializeField] private float alphaThreshold = 0.01f;

        [Tooltip("If true, requires blocksRaycasts to be enabled to block RMB.")]
        [SerializeField] private bool requireBlocksRaycasts = true;

        [Header("Fallback (only if no CanvasGroup exists)")]
        [Tooltip("If no CanvasGroup is found, should we block while this GameObject is active? Leave OFF to avoid accidental global blocking.")]
        [SerializeField] private bool blockWhenNoCanvasGroup = false;

        private bool _pushed;
        private bool _lastShouldBlock;

        void Awake()
        {
            AutoFindCanvasGroup();
            _lastShouldBlock = ShouldBlock();
        }

        void OnEnable()
        {
            AutoFindCanvasGroup();
            EvaluateAndApply(force: true);
        }

        void Update()
        {
            // Lightweight polling so you can hide/show by tweaking CanvasGroup without SetActive().
            EvaluateAndApply(force: false);
        }

        void OnDisable()
        {
            ReleaseIfHeld();
        }

        void OnDestroy()
        {
            ReleaseIfHeld();
        }

        private void AutoFindCanvasGroup()
        {
            if (targetGroup) return;

            // Prefer self first
            targetGroup = GetComponent<CanvasGroup>();
            if (targetGroup) return;

            // Then children (common pattern: root GO has script, child GO has CanvasGroup)
            targetGroup = GetComponentInChildren<CanvasGroup>(true);
        }

        private bool ShouldBlock()
        {
            if (targetGroup != null)
            {
                if (!targetGroup.gameObject.activeInHierarchy) return false;
                if (targetGroup.alpha <= alphaThreshold) return false;
                if (requireBlocksRaycasts && !targetGroup.blocksRaycasts) return false;
                return true;
            }

            // No CanvasGroup found -> optional fallback
            return blockWhenNoCanvasGroup && gameObject.activeInHierarchy;
        }

        private void EvaluateAndApply(bool force)
        {
            bool should = ShouldBlock();

            if (!force && should == _lastShouldBlock)
                return;

            _lastShouldBlock = should;

            if (should)
            {
                if (!_pushed)
                {
                    RMBBlocker.Push();
                    _pushed = true;
                }
            }
            else
            {
                ReleaseIfHeld();
            }
        }

        private void ReleaseIfHeld()
        {
            if (!_pushed) return;
            RMBBlocker.Pop();
            _pushed = false;
        }
    }
}
