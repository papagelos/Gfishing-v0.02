// Assets/Minigames/HexWorld3D/Scripts/UI/HexWorldWarehouseResourcesDropdownUI.cs
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace GalacticFishing.Minigames.HexWorld
{
    /// <summary>
    /// Dropdown that shows resources as:
    ///   Name (left, ellipsis)    Value (right)
    ///
    /// Template requirements (recommended):
    /// - rowTemplate (inactive) is a RectTransform with two TMP_Text children:
    ///     - NameText (TMP_Text)
    ///     - ValueText (TMP_Text)
    ///
    /// Robust fallbacks:
    /// - If exact names not found, matches by heuristic (contains "name"/"value"/"amount"/"count")
    /// - If still not found, uses first TMP as name, second TMP as value
    /// - If only one TMP exists, it will display "Name: Value" in that TMP
    /// </summary>
    public sealed class HexWorldWarehouseResourcesDropdownUI : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private HexWorldWarehouseInventory warehouse;
        [SerializeField] private Transform rowsRoot;              // e.g. ResourcesDropdown/ResourcesRows
        [SerializeField] private RectTransform rowTemplate;       // inactive RowTemplate (root GO)

        [Header("Which resources to show (leave empty to show ALL enum values)")]
        [SerializeField] private HexWorldResourceId[] resources;

        [Header("Behavior")]
        [SerializeField] private bool hideZero = false;
        [SerializeField] private bool sortByEnumValue = true;

        [Header("Formatting")]
        [SerializeField] private string nameSuffix = ":";         // e.g. "Wood:"
        [SerializeField] private string valueFormat = "{0}";      // e.g. "{0}" or "{0:N0}"

        [Header("Binding / Robustness")]
        [SerializeField, Min(0.05f)] private float retrySeconds = 0.25f;

        private sealed class RowRefs
        {
            public RectTransform root;
            public TMP_Text nameText;
            public TMP_Text valueText;
            public TMP_Text singleLineFallback; // used only if we cannot resolve a clean pair
        }

        private readonly Dictionary<HexWorldResourceId, RowRefs> _rows = new();

        private Coroutine _bindRoutine;
        private bool _subscribed;

        // Rebuild detection
        private int _lastEnumHash = -1;
        private int _lastResourcesHash = 0;

        private void OnEnable()
        {
            ResolveRefsIfMissing();

            if (_bindRoutine != null)
                StopCoroutine(_bindRoutine);

            _bindRoutine = StartCoroutine(BindWhenReady());
        }

        private void OnDisable()
        {
            if (_bindRoutine != null)
            {
                StopCoroutine(_bindRoutine);
                _bindRoutine = null;
            }
            Unsubscribe();
        }

        private void ResolveRefsIfMissing()
        {
            if (!rowsRoot) rowsRoot = transform;

            if (!rowTemplate && rowsRoot)
            {
                // Prefer a direct child named RowTemplate under rowsRoot
                var t = rowsRoot.Find("RowTemplate");
                if (t) rowTemplate = t as RectTransform;
            }
        }

        private IEnumerator BindWhenReady()
        {
            if (!rowsRoot || !rowTemplate)
            {
                Debug.LogWarning("[HexWorldWarehouseResourcesDropdownUI] Missing rowsRoot or rowTemplate.");
                enabled = false;
                yield break;
            }

            TryFindWarehouse();
            while (!warehouse)
            {
                yield return new WaitForSecondsRealtime(retrySeconds);
                TryFindWarehouse();
            }

            Subscribe();

            RebuildIfNeeded(force: true);
            Refresh();
        }

        private void TryFindWarehouse()
        {
            if (warehouse) return;

            // 1) Try controller first (common in your project)
            var controller = FindObjectOfType<HexWorld3DController>(true);
            if (controller)
            {
                // inventory might be on the same GO or nested somewhere under it
                warehouse = controller.GetComponent<HexWorldWarehouseInventory>();
                if (!warehouse)
                    warehouse = controller.GetComponentInChildren<HexWorldWarehouseInventory>(true);
            }

            // 2) Fallback: any warehouse in scene (including inactive)
            if (!warehouse)
                warehouse = FindObjectOfType<HexWorldWarehouseInventory>(true);
        }

        private void Subscribe()
        {
            if (_subscribed || !warehouse) return;
            warehouse.InventoryChanged += OnWarehouseChanged;
            warehouse.Changed += OnWarehouseChanged; // back-compat
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            if (warehouse)
            {
                warehouse.InventoryChanged -= OnWarehouseChanged;
                warehouse.Changed -= OnWarehouseChanged;
            }
            _subscribed = false;
        }

        private void OnWarehouseChanged()
        {
            RebuildIfNeeded(force: false);
            Refresh();
        }

        private void RebuildIfNeeded(bool force)
        {
            var desired = GetDesiredResourceIds(out int enumHash, out int resourcesHash);

            bool autoListing = (resources == null || resources.Length == 0);

            if (!force)
            {
                // Auto listing: rebuild if enum changed (new resource added)
                if (autoListing && enumHash != _lastEnumHash) force = true;

                // Manual list: rebuild if list changed
                if (resourcesHash != _lastResourcesHash) force = true;

                // Safety: if our instantiated rows don't match the desired set
                if (!force && !RowsMatch(desired)) force = true;
            }

            _lastEnumHash = enumHash;
            _lastResourcesHash = resourcesHash;

            if (!force) return;
            BuildRows(desired);
        }

        private List<HexWorldResourceId> GetDesiredResourceIds(out int enumHash, out int resourcesHash)
        {
            // Hash the enum values list (more robust than count-only)
            unchecked
            {
                int h = 17;
                var all = (HexWorldResourceId[])Enum.GetValues(typeof(HexWorldResourceId));
                for (int i = 0; i < all.Length; i++)
                    h = h * 31 + (int)all[i];
                enumHash = h;
            }

            // Hash the explicit resources array (if provided)
            unchecked
            {
                int h = 17;
                if (resources != null)
                {
                    for (int i = 0; i < resources.Length; i++)
                        h = h * 31 + (int)resources[i];
                }
                resourcesHash = h;
            }

            var list = new List<HexWorldResourceId>();

            if (resources == null || resources.Length == 0)
            {
                var all = (HexWorldResourceId[])Enum.GetValues(typeof(HexWorldResourceId));
                for (int i = 0; i < all.Length; i++)
                {
                    var id = all[i];
                    if (IsSkippable(id)) continue;
                    list.Add(id);
                }
            }
            else
            {
                var seen = new HashSet<HexWorldResourceId>();
                for (int i = 0; i < resources.Length; i++)
                {
                    var id = resources[i];
                    if (IsSkippable(id)) continue;
                    if (seen.Add(id)) list.Add(id);
                }
            }

            if (sortByEnumValue)
                list.Sort((a, b) => ((int)a).CompareTo((int)b));

            return list;
        }

        private static bool IsSkippable(HexWorldResourceId id)
        {
            if (id == HexWorldResourceId.None) return true;
            if (id.ToString().Equals("None", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private bool RowsMatch(List<HexWorldResourceId> desired)
        {
            if (desired == null) return _rows.Count == 0;
            if (_rows.Count != desired.Count) return false;

            for (int i = 0; i < desired.Count; i++)
                if (!_rows.ContainsKey(desired[i])) return false;

            return true;
        }

        private void BuildRows(List<HexWorldResourceId> desired)
        {
            // Delete generated rows, keep template
            for (int i = rowsRoot.childCount - 1; i >= 0; i--)
            {
                var child = rowsRoot.GetChild(i);
                if (child == rowTemplate.transform) continue;
                if (child.name.StartsWith("ResRow_", StringComparison.Ordinal))
                    Destroy(child.gameObject);
            }

            _rows.Clear();

            for (int i = 0; i < desired.Count; i++)
            {
                var id = desired[i];

                var row = Instantiate(rowTemplate, rowsRoot);
                row.gameObject.SetActive(true);
                row.name = $"ResRow_{id}";

                // Robust lookup: find TMP children and match by name, fallback to first/second TMP.
                FindNameAndValueTexts(row, out TMP_Text nameText, out TMP_Text valueText, out TMP_Text singleLine);

                // Apply sensible defaults (so even if your template is messy, it behaves)
                if (nameText)
                {
                    nameText.enableWordWrapping = false;
                    nameText.overflowMode = TextOverflowModes.Ellipsis;
                    nameText.alignment = TextAlignmentOptions.Left;
                }

                if (valueText)
                {
                    valueText.enableWordWrapping = false;
                    valueText.overflowMode = TextOverflowModes.Overflow;
                    valueText.alignment = TextAlignmentOptions.Right;
                }

                if (!nameText || (!valueText && !singleLine))
                {
                    Debug.LogWarning(
                        "[HexWorldWarehouseResourcesDropdownUI] RowTemplate should have two TMP_Text children (NameText + ValueText). " +
                        "Fallbacks will be used, but check template wiring on: " + row.name);
                }

                _rows[id] = new RowRefs
                {
                    root = row,
                    nameText = nameText,
                    valueText = valueText,
                    singleLineFallback = singleLine
                };
            }

            // Keep template hidden
            rowTemplate.gameObject.SetActive(false);
        }

        private static void FindNameAndValueTexts(RectTransform row, out TMP_Text nameText, out TMP_Text valueText, out TMP_Text singleLine)
        {
            nameText = null;
            valueText = null;
            singleLine = null;

            var tmps = row.GetComponentsInChildren<TMP_Text>(true);

            // 1) Try exact child names anywhere under the row
            for (int i = 0; i < tmps.Length; i++)
            {
                var t = tmps[i];
                if (!t) continue;

                if (string.Equals(t.gameObject.name, "NameText", StringComparison.OrdinalIgnoreCase))
                    nameText = t;
                else if (string.Equals(t.gameObject.name, "ValueText", StringComparison.OrdinalIgnoreCase))
                    valueText = t;
            }

            // If we found both, great
            if (nameText && valueText) return;

            // 2) Heuristic match if you renamed
            for (int i = 0; i < tmps.Length; i++)
            {
                var t = tmps[i];
                if (!t) continue;

                var n = t.gameObject.name.ToLowerInvariant();
                if (!nameText && n.Contains("name")) nameText = t;
                if (!valueText && (n.Contains("value") || n.Contains("amount") || n.Contains("count"))) valueText = t;
            }

            if (nameText && valueText) return;

            // 3) Fallback: first TMP = name, second TMP = value
            if (tmps.Length >= 1 && !nameText) nameText = tmps[0];
            if (tmps.Length >= 2 && !valueText) valueText = tmps[1];

            // 4) If we still don't have a pair but we do have at least one TMP, use single-line display
            if (!valueText && tmps.Length >= 1)
                singleLine = tmps[0];
        }

        private void Refresh()
        {
            if (!warehouse) return;

            foreach (var kv in _rows)
            {
                var id = kv.Key;
                var row = kv.Value;

                int amount = warehouse.Get(id);

                bool show = !hideZero || amount != 0;
                if (row.root) row.root.gameObject.SetActive(show);
                if (!show) continue;

                string name = id.ToString() + nameSuffix;
                string value = string.Format(valueFormat, amount);

                // Preferred: separate name/value
                if (row.nameText && row.valueText)
                {
                    row.nameText.text = name;
                    row.valueText.text = value;
                }
                else if (row.singleLineFallback)
                {
                    // Fallback: single TMP gets "Name: Value"
                    row.singleLineFallback.enableWordWrapping = false;
                    row.singleLineFallback.overflowMode = TextOverflowModes.Ellipsis;
                    row.singleLineFallback.text = $"{name} {value}";
                }
                else
                {
                    // Nothing to render into (shouldn't happen, but safe)
                }
            }
        }
    }
}
