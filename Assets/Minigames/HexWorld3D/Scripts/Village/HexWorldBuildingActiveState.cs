// Assets/Minigames/HexWorld3D/Scripts/Village/HexWorldBuildingActiveState.cs
using System;
using UnityEngine;

namespace GalacticFishing.Minigames.HexWorld
{
    /// <summary>
    /// Tracks whether a building is Active (produces) or Dormant.
    /// MVP visual: dims material color if a common color property exists.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HexWorldBuildingActiveState : MonoBehaviour
    {
        [SerializeField] private bool isActive = true;

        public event Action<bool> Changed;

        public bool IsActive => isActive;

        public void SetActive(bool active)
        {
            if (isActive == active) return;
            isActive = active;
            ApplyVisual();
            Changed?.Invoke(isActive);
        }

        private void Awake() => ApplyVisual();

        private void ApplyVisual()
        {
            // Best-effort dim.
            var r = GetComponentInChildren<Renderer>();
            if (!r) return;

            var mats = r.materials;
            if (mats == null) return;

            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (!m) continue;

                float mult = isActive ? 1f : 0.55f;

                if (m.HasProperty("_BaseColor"))
                {
                    var c = m.GetColor("_BaseColor");
                    m.SetColor("_BaseColor", c * mult);
                }
                else if (m.HasProperty("_Color"))
                {
                    var c = m.GetColor("_Color");
                    m.SetColor("_Color", c * mult);
                }
            }
        }
    }
}
