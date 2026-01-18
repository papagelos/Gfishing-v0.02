using UnityEngine;

namespace GalacticFishing.UI
{
    public sealed class UI_ToggleGameObject : MonoBehaviour
    {
        [SerializeField] private GameObject target;

        public void Toggle()
        {
            if (!target) return;
            target.SetActive(!target.activeSelf);
        }

        public void Show()
        {
            if (!target) return;
            target.SetActive(true);
        }

        public void Hide()
        {
            if (!target) return;
            target.SetActive(false);
        }
    }
}
