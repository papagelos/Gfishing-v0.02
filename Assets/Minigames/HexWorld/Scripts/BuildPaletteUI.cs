using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GalacticFishing.Minigames.HexWorld
{
    public class BuildPaletteUI : MonoBehaviour
    {
        public TileCatalog catalog;

        [Header("UI")]
        public Button buttonTemplate;   // disabled template
        public Transform buttonParent;  // layout group parent
        public Image selectedIcon;
        public TMP_Text selectedName;

        public TileDefinition Selected { get; private set; }

        private void Start()
        {
            if (!catalog || !buttonTemplate || !buttonParent) return;

            buttonTemplate.gameObject.SetActive(false);

            foreach (var def in catalog.tiles)
            {
                if (!def) continue;

                var btn = Instantiate(buttonTemplate, buttonParent);
                btn.gameObject.SetActive(true);

                // Expect: an Image + TMP_Text somewhere under the button
                var imgs = btn.GetComponentsInChildren<Image>(true);
                var texts = btn.GetComponentsInChildren<TMP_Text>(true);

                // choose the first non-template image (usually the child icon)
                Image icon = null;
                foreach (var im in imgs)
                {
                    if (im == btn.image) continue;
                    icon = im;
                    break;
                }

                TMP_Text label = texts != null && texts.Length > 0 ? texts[0] : null;

                if (icon) icon.sprite = def.sprite;
                if (label) label.text = def.displayName;

                btn.onClick.AddListener(() => Select(def));
            }

            if (catalog.tiles.Count > 0 && catalog.tiles[0])
                Select(catalog.tiles[0]);
        }

        private void Select(TileDefinition def)
        {
            Selected = def;

            if (selectedIcon) selectedIcon.sprite = def ? def.sprite : null;
            if (selectedName) selectedName.text = def ? def.displayName : "None";
        }
    }
}
