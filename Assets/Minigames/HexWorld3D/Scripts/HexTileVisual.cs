using UnityEngine;

namespace GalacticFishing.Minigames.HexWorld
{
    /// <summary>
    /// Apply a HexWorldTileStyle's materials to a tile renderer.
    /// Attach to your OWNED tile prefab.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HexTileVisual : MonoBehaviour
    {
        [Header("Refs (optional)")]
        [SerializeField] private Renderer targetRenderer;

        private Material[] _defaultShared;

        private void Awake()
        {
            if (!targetRenderer) targetRenderer = GetComponentInChildren<Renderer>(true);
            if (targetRenderer) _defaultShared = targetRenderer.sharedMaterials;
        }

        public void ApplyStyle(HexWorldTileStyle style)
        {
            if (!targetRenderer) return;

            if (style == null || style.materials == null || style.materials.Length == 0)
            {
                if (_defaultShared != null && _defaultShared.Length > 0)
                    targetRenderer.sharedMaterials = _defaultShared;
                return;
            }

            var current = targetRenderer.sharedMaterials;
            if (current == null || current.Length == 0)
            {
                targetRenderer.sharedMaterials = style.materials;
                return;
            }

            var next = new Material[current.Length];
            for (int i = 0; i < next.Length; i++)
                next[i] = current[i];

            int count = Mathf.Min(next.Length, style.materials.Length);
            for (int i = 0; i < count; i++)
                next[i] = style.materials[i];

            targetRenderer.sharedMaterials = next;
        }
    }
}
