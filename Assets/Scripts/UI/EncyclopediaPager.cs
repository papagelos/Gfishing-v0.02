using UnityEngine;
using TMPro;

namespace GalacticFishing.UI
{
    public sealed class EncyclopediaPager : MonoBehaviour
    {
        [Header("Target grid")]
        [SerializeField] private FishEncyclopediaGridController grid;

        [Header("UI")]
        [SerializeField] private TMP_Text pageLabel;

        void OnEnable()
        {
            RefreshLabel();
        }

        public void PrevPage()
        {
            if (grid == null) return;
            grid.PrevPage();
            RefreshLabel();
        }

        public void NextPage()
        {
            if (grid == null) return;
            grid.NextPage();
            RefreshLabel();
        }

        public void RefreshLabel()
        {
            if (pageLabel == null || grid == null)
                return;

            if (!grid.UsePaging)
            {
                pageLabel.text = string.Empty;
                return;
            }

            int current = grid.Page + 1;
            int total   = Mathf.Max(1, grid.TotalPages);
            pageLabel.text = $"{current}/{total}";
        }
    }
}
