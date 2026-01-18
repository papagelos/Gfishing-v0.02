using System.Collections;
using TMPro;
using UnityEngine;

namespace GalacticFishing.Minigames.HexWorld
{
    public sealed class HexWorldResourceLabelUI : MonoBehaviour
    {
        [Header("Optional (auto-found at runtime if empty)")]
        [SerializeField] private HexWorld3DController controller;
        [SerializeField] private HexWorldWarehouseInventory warehouse;

        [SerializeField] private TMP_Text label;

        [Header("What to show")]
        [SerializeField] private HexWorldResourceId resourceId = HexWorldResourceId.Wood;
        [SerializeField] private string prefixOverride = ""; // e.g. "Wood" (leave empty to use enum name)

        [Header("Binding")]
        [SerializeField, Min(0.05f)] private float retrySeconds = 0.25f;

        private Coroutine _bindRoutine;

        private void Awake()
        {
            if (!label) label = GetComponent<TMP_Text>();
        }

        private void OnEnable()
        {
            // Start (or restart) binding. We do NOT disable the component if not found yet.
            _bindRoutine = StartCoroutine(BindWhenReady());
        }

        private void OnDisable()
        {
            if (_bindRoutine != null) StopCoroutine(_bindRoutine);
            _bindRoutine = null;

            if (warehouse) warehouse.InventoryChanged -= Refresh;
        }

        private IEnumerator BindWhenReady()
        {
            // Show a placeholder while waiting
            SetLabelText(waiting: true);

            while (!warehouse)
            {
                if (!controller)
                    controller = FindObjectOfType<HexWorld3DController>(true);

                if (controller)
                    warehouse = controller.GetComponent<HexWorldWarehouseInventory>();

                // Fallback (in case warehouse exists elsewhere)
                if (!warehouse)
                    warehouse = FindObjectOfType<HexWorldWarehouseInventory>(true);

                if (warehouse) break;

                yield return new WaitForSeconds(retrySeconds);
            }

            warehouse.InventoryChanged -= Refresh; // safety
            warehouse.InventoryChanged += Refresh;

            Refresh();
        }

        private void Refresh()
        {
            if (!warehouse)
            {
                SetLabelText(waiting: true);
                return;
            }

            SetLabelText(waiting: false);
        }

        private void SetLabelText(bool waiting)
        {
            if (!label) return;

            string name = string.IsNullOrEmpty(prefixOverride) ? resourceId.ToString() : prefixOverride;

            if (waiting)
                label.text = $"{name}: ...";
            else
                label.text = $"{name}: {warehouse.Get(resourceId)}";
        }
    }
}
