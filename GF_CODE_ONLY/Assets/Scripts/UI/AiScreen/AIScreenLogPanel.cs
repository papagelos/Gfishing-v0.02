using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GalacticFishing.UI
{
    public sealed class AIScreenLogPanel : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text logText;
        [SerializeField] private ScrollRect scrollRect;

        [Header("Optional Search")]
        [Tooltip("Optional TMP_InputField used to filter the log. Leave null to disable search.")]
        [SerializeField] private TMP_InputField searchInput;

        private bool _subscribedToService;

        private void Awake()
        {
            HideImmediate();
        }

        private void OnEnable()
        {
            if (searchInput)
                searchInput.onValueChanged.AddListener(OnSearchChanged);

            TrySubscribeToService();
        }

        private void OnDisable()
        {
            if (searchInput)
                searchInput.onValueChanged.RemoveListener(OnSearchChanged);

            UnsubscribeFromService();
        }

        public void Open()
        {
            Show(true);
            TrySubscribeToService();
            Refresh();
        }

        public void Close()
        {
            Show(false);
        }

        public void ClearHistory()
        {
            AIMessageLogService.Instance?.Clear();
            Refresh();
        }

        public void Refresh()
        {
            TrySubscribeToService();

            string filter = searchInput ? searchInput.text : null;

            if (logText)
                logText.text = AIMessageLogService.Instance ? AIMessageLogService.Instance.GetFilteredText(filter) : "";

            // Scroll to bottom (latest)
            if (scrollRect)
            {
                Canvas.ForceUpdateCanvases();
                scrollRect.verticalNormalizedPosition = 0f;
                Canvas.ForceUpdateCanvases();
            }
        }

        private void OnSearchChanged(string _)
        {
            Refresh();
        }

        private void TrySubscribeToService()
        {
            if (_subscribedToService) return;

            var svc = AIMessageLogService.Instance;
            if (svc == null) return;

            svc.Changed += OnServiceChanged;
            _subscribedToService = true;
        }

        private void UnsubscribeFromService()
        {
            if (!_subscribedToService) return;

            var svc = AIMessageLogService.Instance;
            if (svc != null)
                svc.Changed -= OnServiceChanged;

            _subscribedToService = false;
        }

        private void OnServiceChanged()
        {
            // Only refresh if we’re actually visible/open
            if (group && group.alpha > 0.01f)
                Refresh();
        }

        private void HideImmediate()
        {
            Show(false);
            if (logText) logText.text = "";
        }

        private void Show(bool on)
        {
            if (!group) return;

            group.gameObject.SetActive(true);
            group.alpha = on ? 1f : 0f;
            group.interactable = on;
            group.blocksRaycasts = on;
        }
    }
}
