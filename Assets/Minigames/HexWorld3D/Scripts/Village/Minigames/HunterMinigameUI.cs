// Assets/Minigames/HexWorld3D/Scripts/Village/Minigames/HunterMinigameUI.cs
using UnityEngine;
using UnityEngine.UIElements;
using GalacticFishing.UI;

namespace GalacticFishing.Minigames.HexWorld
{
    /// <summary>
    /// UI Controller for the Hunter Lodge "Trail Board".
    /// Mirrors the Forestry UI pattern with RMB blocker scope.
    /// </summary>
    public sealed class HunterMinigameUI : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;

        private HunterMinigameController _controller;
        private VisualElement _root;
        private ProgressBar _progressBar;
        private Label _hideOutputLabel;
        private Label _featherOutputLabel;
        private Button _focusBalancedButton;
        private Button _focusHideButton;
        private Button _focusFeatherButton;
        private Button _closeButton;

        private bool _visible;
        private bool _isShown;

        private void Awake()
        {
            if (!uiDocument)
                uiDocument = GetComponent<UIDocument>();

            if (uiDocument != null)
            {
                var root = uiDocument.rootVisualElement;
                _root = root.Q<VisualElement>("HunterTrailRoot");
                if (_root != null)
                    _root.pickingMode = PickingMode.Position;
                _progressBar = root.Q<ProgressBar>("CaptureProgressBar");
                if (_progressBar != null)
                {
                    _progressBar.lowValue = 0f;
                    _progressBar.highValue = 1f;
                }
                _hideOutputLabel = root.Q<Label>("HideYieldLabel");
                _featherOutputLabel = root.Q<Label>("FeatherYieldLabel");
                _focusBalancedButton = root.Q<Button>("Btn_FocusBalanced");
                _focusHideButton = root.Q<Button>("Btn_FocusHides");
                _focusFeatherButton = root.Q<Button>("Btn_FocusFeathers");
                _closeButton = root.Q<Button>("Btn_Close");
            }

            if (_closeButton != null)
                _closeButton.clicked += Hide;

            if (_focusBalancedButton != null)
                _focusBalancedButton.clicked += () => SetFocus(0);
            if (_focusHideButton != null)
                _focusHideButton.clicked += () => SetFocus(1);
            if (_focusFeatherButton != null)
                _focusFeatherButton.clicked += () => SetFocus(2);

            HideImmediate();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void Show(HunterMinigameController controller)
        {
            if (controller == null || _root == null)
                return;

            Unsubscribe();

            _controller = controller;
            _controller.ProgressChanged += OnProgressChanged;
            _controller.FocusChanged += OnFocusChanged;

            _visible = true;
            _root.style.display = DisplayStyle.Flex;
            if (!_isShown)
            {
                RMBBlocker.Push();
                _isShown = true;
            }

            RefreshUI();
        }

        public void Hide()
        {
            HideImmediate();
        }

        private void HideImmediate()
        {
            if (_isShown)
            {
                RMBBlocker.Pop();
                _isShown = false;
            }

            _visible = false;
            if (_root != null)
                _root.style.display = DisplayStyle.None;

            Unsubscribe();
            _controller = null;
        }

        private void OnProgressChanged(float progress)
        {
            if (_progressBar != null)
                _progressBar.value = Mathf.Clamp01(progress);
        }

        private void SetFocus(int mode)
        {
            if (_controller == null) return;

            _controller.SetTrailFocus(mode);
            RefreshUI();
        }

        private void RefreshUI()
        {
            if (_controller == null) return;

            if (_progressBar != null)
                _progressBar.value = Mathf.Clamp01(_controller.GetCaptureProgress());

            int hideOutput = _controller.GetProjectedRawHide();
            int featherOutput = _controller.GetProjectedFeathers();

            if (_hideOutputLabel != null)
                _hideOutputLabel.text = $"Raw Hide: {hideOutput}";
            if (_featherOutputLabel != null)
                _featherOutputLabel.text = $"Feathers: {featherOutput}";
        }

        private void OnFocusChanged(int hide, int feathers)
        {
            if (_hideOutputLabel != null)
                _hideOutputLabel.text = $"Raw Hide: {hide}";
            if (_featherOutputLabel != null)
                _featherOutputLabel.text = $"Feathers: {feathers}";
        }

        private void Unsubscribe()
        {
            if (_controller != null)
            {
                _controller.ProgressChanged -= OnProgressChanged;
                _controller.FocusChanged -= OnFocusChanged;
            }
        }
    }
}
