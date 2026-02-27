using UnityEngine;

namespace GalacticFishing.Minigames.Dungeon3D
{
    [DisallowMultipleComponent]
    public sealed class PlayerHpBarController : MonoBehaviour
    {
        [SerializeField] private PlayerHealth health;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Transform barRootTransform;
        [SerializeField] private Transform fillTransform;
        [SerializeField, Min(0f)] private float headGapPixels = 5f;
        [SerializeField, Range(0, 255)] private byte alphaThreshold = 10;

        private Vector3 _fillBaseScale = Vector3.one;
        private Vector3 _fillBaseLocalPosition = Vector3.zero;
        private Sprite _lastSprite;
        private bool _lastFlipX;
        private bool _lastFlipY;
        private Transform _fillAnchorTransform;

        private void Awake()
        {
            if (health == null)
            {
                health = GetComponent<PlayerHealth>();
                if (health == null)
                    health = gameObject.AddComponent<PlayerHealth>();
            }

            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);

            if (barRootTransform == null)
                barRootTransform = transform.Find("HP_Bar_Root");

            if (_fillAnchorTransform == null && barRootTransform != null)
                _fillAnchorTransform = barRootTransform.Find("HP_Bar_Fill_Anchor");

            if (fillTransform != null)
            {
                if (_fillAnchorTransform != null && fillTransform.parent != _fillAnchorTransform)
                    fillTransform.SetParent(_fillAnchorTransform, false);

                _fillBaseScale = fillTransform.localScale;
                _fillBaseLocalPosition = fillTransform.localPosition;
                fillTransform.localPosition = _fillBaseLocalPosition;
            }
        }

        private void OnEnable()
        {
            if (_fillAnchorTransform == null && barRootTransform != null)
                _fillAnchorTransform = barRootTransform.Find("HP_Bar_Fill_Anchor");

            if (fillTransform != null)
            {
                if (_fillAnchorTransform != null && fillTransform.parent != _fillAnchorTransform)
                    fillTransform.SetParent(_fillAnchorTransform, false);

                _fillBaseScale = fillTransform.localScale;
                _fillBaseLocalPosition = fillTransform.localPosition;
                fillTransform.localPosition = _fillBaseLocalPosition;
            }

            if (health != null)
                health.OnDamaged += UpdateBar;

            UpdateBar();
            RefreshHeadAnchor(force: true);
        }

        private void OnDisable()
        {
            if (health != null)
                health.OnDamaged -= UpdateBar;
        }

        private void LateUpdate()
        {
            RefreshHeadAnchor(force: false);
        }

        private void UpdateBar()
        {
            if (health == null || fillTransform == null)
                return;

            float ratio = Mathf.Clamp01(health.CurrentHealth / (float)Mathf.Max(1, health.MaxHealth));
            float baseX = _fillBaseScale.x > 0f ? _fillBaseScale.x : 8f;
            float baseY = _fillBaseScale.y > 0f ? _fillBaseScale.y : 0.75f;
            float baseZ = _fillBaseScale.z > 0f ? _fillBaseScale.z : 1f;

            fillTransform.localScale = new Vector3(baseX * ratio, baseY, baseZ);
            fillTransform.localPosition = _fillBaseLocalPosition;
        }

        private void RefreshHeadAnchor(bool force)
        {
            if (barRootTransform == null || spriteRenderer == null)
                return;

            Sprite current = spriteRenderer.sprite;
            bool flipX = spriteRenderer.flipX;
            bool flipY = spriteRenderer.flipY;
            if (!force && current == _lastSprite && flipX == _lastFlipX && flipY == _lastFlipY)
                return;

            _lastSprite = current;
            _lastFlipX = flipX;
            _lastFlipY = flipY;
            if (current == null)
                return;

            float ppu = Mathf.Max(0.0001f, current.pixelsPerUnit);
            float topLocalY;
            Rect opaqueRect;
            if (TryGetOpaqueBounds(current, out opaqueRect))
                topLocalY = (opaqueRect.yMax - current.pivot.y) / ppu;
            else
                topLocalY = (current.rect.yMax - current.pivot.y) / ppu;

            float y = topLocalY + (headGapPixels / ppu);
            barRootTransform.localPosition = new Vector3(0f, y, 0f);
        }

        private bool TryGetOpaqueBounds(Sprite sprite, out Rect opaqueRect)
        {
            opaqueRect = default;

            Texture2D tex = sprite.texture;
            if (tex == null)
                return false;

            Rect textureRect = sprite.textureRect;
            int texWidth = tex.width;
            int texHeight = tex.height;

            int xMin = Mathf.Clamp(Mathf.FloorToInt(textureRect.xMin), 0, texWidth - 1);
            int xMax = Mathf.Clamp(Mathf.CeilToInt(textureRect.xMax) - 1, 0, texWidth - 1);
            int yMin = Mathf.Clamp(Mathf.FloorToInt(textureRect.yMin), 0, texHeight - 1);
            int yMax = Mathf.Clamp(Mathf.CeilToInt(textureRect.yMax) - 1, 0, texHeight - 1);
            if (xMax < xMin || yMax < yMin)
                return false;

            Color32[] pixels;
            try
            {
                pixels = tex.GetPixels32();
            }
            catch
            {
                return false;
            }

            bool found = false;
            int minX = int.MaxValue;
            int minY = int.MaxValue;
            int maxX = int.MinValue;
            int maxY = int.MinValue;

            for (int y = yMin; y <= yMax; y++)
            {
                int rowBase = y * texWidth;
                for (int x = xMin; x <= xMax; x++)
                {
                    if (pixels[rowBase + x].a <= alphaThreshold)
                        continue;

                    if (!found)
                    {
                        found = true;
                        minX = maxX = x;
                        minY = maxY = y;
                        continue;
                    }

                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }

            if (!found)
                return false;

            float localMinX = minX - xMin;
            float localMaxX = (maxX - xMin) + 1f;
            float localMinY = minY - yMin;
            float localMaxY = (maxY - yMin) + 1f;
            opaqueRect = Rect.MinMaxRect(localMinX, localMinY, localMaxX, localMaxY);
            return true;
        }
    }
}
