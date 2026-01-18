// Assets/Minigames/HexWorld3D/Scripts/Village/HexWorldWarehouseLabelUI.cs
using TMPro;
using UnityEngine;

namespace GalacticFishing.Minigames.HexWorld
{
    /// <summary>
    /// Simple HUD label: "Warehouse: 123/200 (L1)" and an optional full warning suffix.
    /// </summary>
    public sealed class HexWorldWarehouseLabelUI : MonoBehaviour
    {
        [SerializeField] private HexWorldWarehouseInventory warehouse;
        [SerializeField] private TMP_Text label;
        [SerializeField] private string prefix = "Warehouse: ";
        [SerializeField] private string fullSuffix = "  FULL";

        private void Awake()
        {
            if (!label) label = GetComponent<TMP_Text>();
        }

        private void Start()
        {
            if (!warehouse) warehouse = FindObjectOfType<HexWorldWarehouseInventory>();
            if (!warehouse || !label)
            {
                enabled = false;
                return;
            }

            warehouse.InventoryChanged += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            if (warehouse) warehouse.InventoryChanged -= Refresh;
        }

        private void Refresh()
        {
            int total = warehouse.TotalStored;
            int cap = warehouse.Capacity;
            int lvl = warehouse.WarehouseLevel;

            label.text = prefix + total + "/" + cap + " (L" + lvl + ")" + (warehouse.IsFull ? fullSuffix : "");
        }
    }
}
