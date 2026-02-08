// Assets/Minigames/HexWorld3D/Scripts/Village/HexWorldPlacementHoverCardUI.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace GalacticFishing.Minigames.HexWorld
{
    /// <summary>
    /// Placement-time hover card that previews production base value and synergy bonuses.
    /// Uses ticker evaluation logic without mutating world state.
    /// </summary>
    public sealed class HexWorldPlacementHoverCardUI : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private HexWorld3DController controller;
        [SerializeField] private HexWorldProductionTicker productionTicker;

        [Header("Layout")]
        [SerializeField] private Vector2 cursorOffset = new Vector2(16f, 16f);
        [SerializeField, Min(2)] private int rowPoolSize = 10;

        private VisualElement _overlayRoot;
        private VisualElement _hoverCardRoot;
        private Label _titleLabel;
        private Label _baseValueLabel;
        private VisualElement _bonusList;
        private Label _totalLabel;

        private readonly List<RowRefs> _rowPool = new List<RowRefs>();
        private bool _wired;
        private bool _visible;
        private bool _previewDirty = true;
        private bool _hasLastCoord;
        private HexCoord _lastCoord;
        private string _lastBuildingId = string.Empty;

        private struct RowRefs
        {
            public VisualElement row;
            public Label name;
            public Label value;
        }

        private void OnEnable()
        {
            EnsureWired();
            SetVisible(false);
        }

        private void OnDisable()
        {
            Unwire();
        }

        private void Update()
        {
            if (!_wired || controller == null || _hoverCardRoot == null)
                return;

            HexWorldBuildingDefinition selected = controller.SelectedBuilding;
            if (selected == null || controller.CurrentPaletteMode != HexWorld3DController.PaletteMode.Buildings)
            {
                SetVisible(false);
                return;
            }

            if (!controller.TryGetCursorPlacementState(out var hoverCoord, out var isValid))
            {
                SetVisible(false);
                return;
            }

            if (!isValid)
            {
                SetVisible(false);
                return;
            }

            SetVisible(true);
            UpdateCardPosition();

            string buildingId = GetCanonicalBuildingId(selected);
            bool buildingChanged = !string.Equals(_lastBuildingId, buildingId, StringComparison.Ordinal);
            bool coordChanged = !_hasLastCoord || !_lastCoord.Equals(hoverCoord);

            if (_previewDirty || buildingChanged || coordChanged)
            {
                RebuildPreview(hoverCoord, selected);
                _previewDirty = false;
                _hasLastCoord = true;
                _lastCoord = hoverCoord;
                _lastBuildingId = buildingId;
            }
        }

        private void EnsureWired()
        {
            if (_wired) return;

            if (uiDocument == null)
                uiDocument = GetComponent<UIDocument>();
            if (uiDocument == null)
                return;

            if (controller == null)
                controller = UnityEngine.Object.FindObjectOfType<HexWorld3DController>(true);
            if (productionTicker == null)
                productionTicker = UnityEngine.Object.FindObjectOfType<HexWorldProductionTicker>(true);
            if (controller == null)
                return;

            var root = uiDocument.rootVisualElement;
            if (root == null)
                return;

            _overlayRoot = root.Q<VisualElement>("OverlayRoot") ?? root;
            _hoverCardRoot = root.Q<VisualElement>("HoverCardRoot") ?? root.Q<VisualElement>("PlacementHoverCard");
            _titleLabel = root.Q<Label>("TitleLabel") ?? root.Q<Label>("HoverNameLabel");
            _baseValueLabel = root.Q<Label>("BaseValueLabel");
            _bonusList = root.Q<VisualElement>("BonusList") ?? root.Q<VisualElement>("HoverBreakdownList");
            _totalLabel = root.Q<Label>("TotalLabel") ?? root.Q<Label>("HoverDeltaLabel");

            if (_hoverCardRoot == null || _bonusList == null || _titleLabel == null || _totalLabel == null)
                return;

            EnsureOverlayLayout();
            EnsureBaseValueLabel();
            BuildRowPool();
            ClearRows();

            controller.SelectedBuildingChanged += OnSelectedBuildingChanged;
            controller.RoadNetworkRecomputed += OnRoadNetworkRecomputed;

            _wired = true;
        }

        private void Unwire()
        {
            if (!_wired) return;

            if (controller != null)
            {
                controller.SelectedBuildingChanged -= OnSelectedBuildingChanged;
                controller.RoadNetworkRecomputed -= OnRoadNetworkRecomputed;
            }

            _wired = false;
        }

        private void EnsureOverlayLayout()
        {
            _overlayRoot.style.position = Position.Absolute;
            _overlayRoot.style.left = 0f;
            _overlayRoot.style.top = 0f;
            _overlayRoot.style.right = 0f;
            _overlayRoot.style.bottom = 0f;
            _overlayRoot.pickingMode = PickingMode.Ignore;

            _hoverCardRoot.style.position = Position.Absolute;
            _hoverCardRoot.pickingMode = PickingMode.Ignore;
        }

        private void EnsureBaseValueLabel()
        {
            if (_baseValueLabel != null)
                return;

            _baseValueLabel = new Label();
            _baseValueLabel.name = "BaseValueLabel";
            _baseValueLabel.AddToClassList("ti-small-muted");
            _hoverCardRoot.Insert(1, _baseValueLabel);
        }

        private void BuildRowPool()
        {
            _bonusList.Clear();
            _rowPool.Clear();

            int poolSize = Mathf.Max(2, rowPoolSize);
            for (int i = 0; i < poolSize; i++)
            {
                var row = new VisualElement();
                row.AddToClassList("ti-break-row");

                var nameLabel = new Label();
                nameLabel.AddToClassList("ti-break-name");

                var valueLabel = new Label();
                valueLabel.AddToClassList("ti-break-value");

                row.Add(nameLabel);
                row.Add(valueLabel);
                _bonusList.Add(row);

                _rowPool.Add(new RowRefs
                {
                    row = row,
                    name = nameLabel,
                    value = valueLabel
                });
            }
        }

        private void RebuildPreview(HexCoord hoverCoord, HexWorldBuildingDefinition selectedDef)
        {
            if (selectedDef == null)
            {
                ClearRows();
                return;
            }

            _titleLabel.text = GetDisplayName(selectedDef);
            _baseValueLabel.text = $"Base: {FormatBaseValue(selectedDef)}";

            var results = productionTicker != null
                ? productionTicker.EvaluateSynergyRulesDetailed(hoverCoord, selectedDef)
                : new List<HexWorldProductionTicker.SynergyRuleResult>();

            PopulateRows(results);

            float totalBonus = productionTicker != null
                ? productionTicker.CalculateSynergyBonus(hoverCoord, selectedDef)
                : 0f;
            int totalPct = Mathf.RoundToInt(totalBonus * 100f);
            _totalLabel.text = $"Total: +{totalPct}%";
            ApplyStateClasses(_totalLabel, _totalLabel, true);
        }

        private void PopulateRows(List<HexWorldProductionTicker.SynergyRuleResult> results)
        {
            int visibleCount = Mathf.Min(results != null ? results.Count : 0, _rowPool.Count);

            for (int i = 0; i < _rowPool.Count; i++)
            {
                var row = _rowPool[i];
                if (i >= visibleCount)
                {
                    row.row.style.display = DisplayStyle.None;
                    continue;
                }

                var result = results[i];
                string label = GetSynergyLabel(result.rule);
                bool isPositive = result.isSatisfied && result.bonusValue >= 0f;

                row.row.style.display = DisplayStyle.Flex;
                row.name.text = $"{(result.isSatisfied ? "✓" : "✗")} {label}";
                row.value.text = $"({FormatPercent(result.rule != null ? result.rule.amountPct : 0f)})";
                ApplyStateClasses(row.row, row.name, isPositive);
                ApplyStateClasses(row.row, row.value, isPositive);
            }
        }

        private void ClearRows()
        {
            for (int i = 0; i < _rowPool.Count; i++)
            {
                _rowPool[i].row.style.display = DisplayStyle.None;
            }
        }

        private void ApplyStateClasses(VisualElement row, VisualElement element, bool isPositive)
        {
            row.RemoveFromClassList("is-pos");
            row.RemoveFromClassList("is-neg");
            element.RemoveFromClassList("is-pos");
            element.RemoveFromClassList("is-neg");

            string stateClass = isPositive ? "is-pos" : "is-neg";
            row.AddToClassList(stateClass);
            element.AddToClassList(stateClass);
        }

        private static string GetCanonicalBuildingId(HexWorldBuildingDefinition def)
        {
            if (def == null) return string.Empty;
            return string.IsNullOrWhiteSpace(def.buildingName) ? def.name : def.buildingName;
        }

        private static string GetDisplayName(HexWorldBuildingDefinition def)
        {
            if (def == null) return "Building";
            return string.IsNullOrWhiteSpace(def.displayName) ? def.name : def.displayName;
        }

        private string FormatBaseValue(HexWorldBuildingDefinition def)
        {
            if (TryGetBaseOutput(def, out var baseOutput))
                return $"{baseOutput.amount} {baseOutput.id} / tick";

            return "- / tick";
        }

        private static string FormatPercent(float value)
        {
            int pct = Mathf.RoundToInt(value * 100f);
            return pct >= 0 ? $"+{pct}%" : $"{pct}%";
        }

        private static string GetSynergyLabel(SynergyRule rule)
        {
            if (rule == null)
                return "Synergy";

            if (rule.type == SynergyType.RoadAdjacent)
                return "Road Adjacent";

            if (rule.type == SynergyType.RoadConnectedToTownHall)
                return "Road -> Town Hall";

            return string.IsNullOrWhiteSpace(rule.label) ? rule.type.ToString() : rule.label;
        }

        private bool TryGetBaseOutput(HexWorldBuildingDefinition def, out HexWorldResourceStack baseOutput)
        {
            baseOutput = default;
            if (def == null)
                return false;

            var profile = def.prefab != null ? def.prefab.GetComponent<HexWorldBuildingProductionProfile>() : null;
            if (profile != null && profile.baseOutputPerTick != null)
            {
                for (int i = 0; i < profile.baseOutputPerTick.Count; i++)
                {
                    var entry = profile.baseOutputPerTick[i];
                    if (entry.id == HexWorldResourceId.None || entry.amount <= 0)
                        continue;

                    baseOutput = entry;
                    return true;
                }
            }

            if (def.kind != HexWorldBuildingDefinition.BuildingKind.Producer)
                return false;

            string id = GetCanonicalBuildingId(def);
            string lower = id.ToLowerInvariant();

            if (string.Equals(id, "Building_Lumberyard", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(id, "ForestryStation", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(id, "Lumberyard", StringComparison.OrdinalIgnoreCase) ||
                lower.Contains("lumber") || lower.Contains("wood"))
            {
                baseOutput = new HexWorldResourceStack(HexWorldResourceId.Wood, 6);
                return true;
            }

            if (lower.Contains("quarry") || lower.Contains("stone"))
            {
                baseOutput = new HexWorldResourceStack(HexWorldResourceId.Stone, 4);
                return true;
            }

            if (lower.Contains("forage") || lower.Contains("fiber") || lower.Contains("meadow"))
            {
                baseOutput = new HexWorldResourceStack(HexWorldResourceId.Fiber, 3);
                return true;
            }

            if (lower.Contains("bog") || lower.Contains("bait") || lower.Contains("fishery"))
            {
                baseOutput = new HexWorldResourceStack(HexWorldResourceId.BaitIngredients, 3);
                return true;
            }

            return false;
        }

        private void UpdateCardPosition()
        {
            if (_overlayRoot == null || _hoverCardRoot == null || _overlayRoot.panel == null)
                return;

            Vector2 screenPos;
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse == null) return;
            screenPos = mouse.position.ReadValue();
#else
            screenPos = Input.mousePosition;
#endif
            Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(_overlayRoot.panel, screenPos);
            _hoverCardRoot.style.left = panelPos.x + cursorOffset.x;
            _hoverCardRoot.style.top = panelPos.y + cursorOffset.y;
        }

        private void SetVisible(bool visible)
        {
            _visible = visible;
            if (_hoverCardRoot != null)
                _hoverCardRoot.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void OnSelectedBuildingChanged(HexWorldBuildingDefinition _)
        {
            _previewDirty = true;
            _hasLastCoord = false;
        }

        private void OnRoadNetworkRecomputed()
        {
            _previewDirty = true;
        }
    }
}
