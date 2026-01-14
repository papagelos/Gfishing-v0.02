using UnityEngine;
using UnityEngine.EventSystems;

namespace GalacticFishing.UI
{
    [AddComponentMenu("Galactic Fishing/UI/Tile Hover Relay")]
    public sealed class TileHoverRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public HubHoverHint hint;
        [TextArea] public string text;

        public void OnPointerEnter(PointerEventData eventData) { if (hint) hint.Set(text); }
        public void OnPointerExit(PointerEventData eventData)  { if (hint) hint.Clear(); }
    }
}
