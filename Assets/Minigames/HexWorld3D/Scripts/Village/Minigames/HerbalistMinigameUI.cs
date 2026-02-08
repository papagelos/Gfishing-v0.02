// Assets/Minigames/HexWorld3D/Scripts/Village/Minigames/HerbalistMinigameUI.cs
using UnityEngine;
using GalacticFishing.UI;
using UnityEngine.UIElements;

namespace GalacticFishing.Minigames.HexWorld
{
    public sealed class HerbalistMinigameUI : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;

        private HerbalistMinigameController _controller;
        private VisualElement _root;
        private Label _growthSpeedLabel;
        private ProgressBar[] _plotBars = new ProgressBar[9];
        private Button _harvestAllButton;
        private Button _closeButton;

        private bool _isShown;

        private void Awake()
        {
            if (!uiDocument)
                uiDocument = GetComponent<UIDocument>();

            if (uiDocument != null)
            {
                var root = uiDocument.rootVisualElement;
                _root = root?.Q<VisualElement>("HerbalistGridRoot");
                _growthSpeedLabel = root?.Q<Label>("GrowthSpeedLabel");
                _harvestAllButton = root?.Q<Button>("Btn_HarvestAll");
                _closeButton = root?.Q<Button>("Btn_Close");

                for (int i = 0; i < _plotBars.Length; i++)
                {
                    _plotBars[i] = root?.Q<ProgressBar>($"PlotProgress_{i}");
                }
            }

            if (_harvestAllButton != null)
                _harvestAllButton.clicked += OnHarvestAllClicked;
            if (_closeButton != null)
                _closeButton.clicked += Hide;

            HideImmediate();
        }

        private void OnDisable()
        {
            HideImmediate();
        }

        public void Show(HerbalistMinigameController controller)
        {
            if (controller == null || _root == null)
                return;

            UnbindController();
            _controller = controller;
            _controller.PlotGrowthChanged += OnPlotGrowthChanged;
            _controller.GrowthBonusChanged += OnGrowthBonusChanged;

            if (!_isShown)
            {
                RMBBlocker.Push();
                _isShown = true;
            }

            _root.style.display = DisplayStyle.Flex;

            RefreshAll();
        }

        public void Hide()
        {
            HideImmediate();
        }

        private void HideImmediate()
        {
            if (_isShown)
                RMBBlocker.Pop();

            _isShown = false;
            if (_root != null)
                _root.style.display = DisplayStyle.None;

            UnbindController();
        }

        private void UnbindController()
        {
            if (_controller != null)
            {
                _controller.PlotGrowthChanged -= OnPlotGrowthChanged;
                _controller.GrowthBonusChanged -= OnGrowthBonusChanged;
            }
            _controller = null;
        }

        private void RefreshAll()
        {
            if (_controller == null) return;

            for (int i = 0; i < _plotBars.Length; i++)
            {
                OnPlotGrowthChanged(i, _controller.GetPlotProgress(i));
            }

            UpdateGrowthSpeedLabel(_controller.GetGrowthBonusPercent());
        }

        private void OnPlotGrowthChanged(int index, float value)
        {
            if (index < 0 || index >= _plotBars.Length) return;
            if (_plotBars[index] != null)
                _plotBars[index].value = value * 100f;
        }

        private void OnGrowthBonusChanged(float bonus)
        {
            UpdateGrowthSpeedLabel(bonus);
        }

        private void UpdateGrowthSpeedLabel(float bonus)
        {
            if (_growthSpeedLabel == null) return;
            int pct = Mathf.RoundToInt(bonus * 100f);
            string sign = pct >= 0 ? "+" : string.Empty;
            _growthSpeedLabel.text = $"{sign}{pct}% Speed";
        }

        private void OnHarvestAllClicked()
        {
            if (_controller == null) return;
            _controller.HarvestAllReady();
        }
    }
}
