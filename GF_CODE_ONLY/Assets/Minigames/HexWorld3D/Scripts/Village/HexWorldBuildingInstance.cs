// Assets/Minigames/HexWorld3D/Scripts/HexWorldBuildingInstance.cs
using UnityEngine;

namespace GalacticFishing.Minigames.HexWorld
{
    public sealed class HexWorldBuildingInstance : MonoBehaviour
    {
        [Header("Runtime (set by controller)")]
        public int Q;
        public int R;
        public string buildingName;

        [Header("State")]
        public int Level = 1;
        public bool IsActive = true;

        [Header("Cached")]
        public bool ConsumesActiveSlot = true;

        public HexCoord Coord => new HexCoord(Q, R);

        public void Set(HexCoord c, string defName)
        {
            Q = c.q;
            R = c.r;
            buildingName = defName;
        }

        public void Set(HexCoord c, string defName, bool consumesActiveSlot, bool isActive, int level = 1)
        {
            Q = c.q;
            R = c.r;
            buildingName = defName;
            ConsumesActiveSlot = consumesActiveSlot;
            Level = Mathf.Max(1, level);
            SetActive(isActive);
        }

        public void SetActive(bool active)
        {
            IsActive = active;
            ApplyActiveVisuals();
        }

        private void ApplyActiveVisuals()
        {
            // Keep it simple: dim renderers when dormant.
            // Only affects instance materials (MaterialPropertyBlock).
            var renderers = GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0) return;

            // Try both common property names.
            int propBaseColor = Shader.PropertyToID("_BaseColor");
            int propColor = Shader.PropertyToID("_Color");
            var mpb = new MaterialPropertyBlock();

            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (!r) continue;

                r.GetPropertyBlock(mpb);

                // We don't know the material; just multiply if possible.
                Color c = Color.white;
                bool hasAny = false;

                // Prefer BaseColor if present
                if (r.sharedMaterial && r.sharedMaterial.HasProperty(propBaseColor))
                {
                    c = r.sharedMaterial.GetColor(propBaseColor);
                    hasAny = true;
                    mpb.SetColor(propBaseColor, IsActive ? c : c * 0.5f);
                }
                else if (r.sharedMaterial && r.sharedMaterial.HasProperty(propColor))
                {
                    c = r.sharedMaterial.GetColor(propColor);
                    hasAny = true;
                    mpb.SetColor(propColor, IsActive ? c : c * 0.5f);
                }

                if (hasAny)
                    r.SetPropertyBlock(mpb);
            }
        }
    }
}
