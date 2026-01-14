using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GalacticFishing.UI
{
    [System.Serializable]
    public sealed class HubTile
    {
        public string label;
        public Sprite icon;
        public bool interactable = true;
        public UnityEngine.Events.UnityEvent onClick = new();
        public string hoverText;
    }

    [RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("Galactic Fishing/UI/Hub Grid Populator")]
    public sealed class HubGridPopulator : MonoBehaviour
    {
        public RectTransform gridRoot;
        public List<HubTile> tiles = new();
        public int columns = 3;
        public Vector2 spacing = new(24, 24);
        public Vector2 padding = new(24, 24);
        public Sprite tileSprite;
        public Color tileColor = new(0.08f, 0.14f, 0.2f, 0.92f);
        public Color tileDisabled = new(0.08f, 0.14f, 0.2f, 0.45f);
        public Color labelColor = Color.white;
        public Color labelDisabled = new(0.85f, 0.85f, 0.85f, 0.7f);
        public TMP_FontAsset font;
        public bool showLabels = false;

        HubHoverHint _hint;

        void Awake()
        {
            if (!gridRoot) gridRoot = (RectTransform)transform;
            _hint = GetComponentInParent<HubHoverHint>();
            Build();
        }

        public void Build()
        {
            for (int i = gridRoot.childCount - 1; i >= 0; i--)
                DestroyImmediate(gridRoot.GetChild(i).gameObject);

            var gl = gridRoot.GetComponent<GridLayoutGroup>() ?? gridRoot.gameObject.AddComponent<GridLayoutGroup>();
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gl.constraintCount = Mathf.Max(1, columns);
            gl.spacing = spacing;
            gl.padding = new RectOffset((int)padding.x, (int)padding.x, (int)padding.y, (int)padding.y);

            var size = gridRoot.rect.size;
            float availW = Mathf.Max(0f, size.x - padding.x * 2f - spacing.x * (columns - 1));
            float cell = columns > 0 ? Mathf.Floor(availW / columns) : 100f;
            gl.cellSize = new Vector2(cell, cell);

            foreach (var t in tiles) CreateTile(t, cell);
        }

        void CreateTile(HubTile tile, float cell)
        {
            var go = new GameObject($"Tile_{(string.IsNullOrEmpty(tile.label) ? "Item" : tile.label)}",
                typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform;
            rt.SetParent(gridRoot, false);
            rt.sizeDelta = new Vector2(cell, cell);

            var img = go.GetComponent<Image>();
            img.sprite = tileSprite;
            img.type = tileSprite ? Image.Type.Sliced : Image.Type.Simple;
            img.color = tile.interactable ? tileColor : tileDisabled;

            var btn = go.GetComponent<Button>();
            btn.interactable = tile.interactable;
            btn.onClick.AddListener(() => tile.onClick?.Invoke());

            var relay = go.AddComponent<TileHoverRelay>();
            relay.hint = _hint;
            relay.text = string.IsNullOrWhiteSpace(tile.hoverText) ? tile.label : tile.hoverText;

            if (tile.icon)
            {
                var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                var iconRT = (RectTransform)iconGO.transform;
                iconRT.SetParent(rt, false);
                iconRT.anchorMin = iconRT.anchorMax = new Vector2(0.5f, 0.65f);
                iconRT.sizeDelta = new Vector2(cell * 0.45f, cell * 0.45f);
                var iconImg = iconGO.GetComponent<Image>();
                iconImg.sprite = tile.icon;
                iconImg.raycastTarget = false;
                if (!tile.interactable) iconImg.color = new Color(1, 1, 1, 0.6f);
            }

            if (showLabels)
            {
                var labelGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                var labelRT = (RectTransform)labelGO.transform;
                labelRT.SetParent(rt, false);
                labelRT.anchorMin = new Vector2(0.12f, 0.08f);
                labelRT.anchorMax = new Vector2(0.88f, 0.35f);
                labelRT.offsetMin = labelRT.offsetMax = Vector2.zero;

                var tmp = labelGO.GetComponent<TextMeshProUGUI>();
                tmp.text = string.IsNullOrWhiteSpace(tile.label) ? " " : tile.label;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.enableAutoSizing = false;
                tmp.fontSizeMin = 12;
                tmp.fontSizeMax = 42;
                tmp.color = tile.interactable ? labelColor : labelDisabled;
                if (font) tmp.font = font;
                tmp.raycastTarget = false;
            }
        }
    }
}
