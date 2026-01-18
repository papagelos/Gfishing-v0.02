using UnityEngine;
using TMPro;

namespace GalacticFishing.UI
{
    [RequireComponent(typeof(TMP_Dropdown))]
    public sealed class FishEncyclopediaSortDropdown : MonoBehaviour
    {
        [SerializeField] private FishEncyclopediaGridController targetGrid;

        private TMP_Dropdown _dropdown;

        void Awake()
        {
            _dropdown = GetComponent<TMP_Dropdown>();
        }

        void OnEnable()
        {
            if (_dropdown == null)
                _dropdown = GetComponent<TMP_Dropdown>();

            _dropdown.onValueChanged.AddListener(HandleChanged);

            // Sync UI to current sort mode
            if (targetGrid != null)
                _dropdown.value = (int)targetGrid.SortMode;
        }

        void OnDisable()
        {
            if (_dropdown != null)
                _dropdown.onValueChanged.RemoveListener(HandleChanged);
        }

        private void HandleChanged(int index)
        {
            if (targetGrid == null)
                return;

            // Map dropdown index → InventorySortMode (enum order matches options)
            var mode = InventorySortMode.OwnedFirst;
            if (index >= 0 && index < System.Enum.GetValues(typeof(InventorySortMode)).Length)
                mode = (InventorySortMode)index;

            targetGrid.SetSortMode(mode);
        }
    }
}
