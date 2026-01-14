using TMPro;
using UnityEngine;

namespace GalacticFishing.UI
{
    [AddComponentMenu("Galactic Fishing/UI/Hub Hover Hint")]
    public sealed class HubHoverHint : MonoBehaviour
    {
        [SerializeField] TMP_Text label;
        [SerializeField] string idleText = "";

        public void Set(string text) { if (label) label.text = text; }
        public void Clear()          { if (label) label.text = idleText; }
    }
}
