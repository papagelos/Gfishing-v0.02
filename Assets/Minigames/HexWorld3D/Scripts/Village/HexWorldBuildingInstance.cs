using UnityEngine;

namespace GalacticFishing.Minigames.HexWorld
{
    /// <summary>
    /// Interface for minigame controllers attached to buildings that need to persist custom state.
    /// Implement this on any MonoBehaviour that needs to save/load state with the building.
    /// </summary>
    public interface IHexWorldBuildingStateProvider
    {
        /// <summary>
        /// Returns the serialized state as a JSON string.
        /// Called during save to capture minigame-specific data (e.g., drill depth, seeds planted).
        /// </summary>
        string GetSerializedState();

        /// <summary>
        /// Restores the state from a JSON string.
        /// Called during load to restore minigame-specific data.
        /// </summary>
        void LoadSerializedState(string state);
    }

    public sealed class HexWorldBuildingInstance : MonoBehaviour
    {
        [Header("Runtime (set by controller)")]
        public int Q;
        public int R;
        public string buildingName;

        [Header("State")]
        public int Level = 1;
        public bool IsActive = true;

        [Header("Minigame State")]
        [Tooltip("JSON blob storing minigame-specific state (e.g., drill depth, seeds planted).")]
        [TextArea(2, 5)]
        public string extractorState;

        [Header("Production")]
        [SerializeField] private int productQuality = 1;
        [SerializeField] private float relocationCooldown;

        public int ProductQuality
        {
            get => productQuality;
            set => productQuality = Mathf.Max(1, value);
        }

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
            // Get all renderers in the building prefab
            var renderers = GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0) return;

            // Shader property IDs for performance
            int propBaseColor = Shader.PropertyToID("_BaseColor");
            int propColor = Shader.PropertyToID("_Color");

            var mpb = new MaterialPropertyBlock();

            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (!r) continue;

                r.GetPropertyBlock(mpb);

                Color c = Color.white;
                bool hasProperty = false;

                // Check for standard shader color properties
                if (r.sharedMaterial && r.sharedMaterial.HasProperty(propBaseColor))
                {
                    c = r.sharedMaterial.GetColor(propBaseColor);
                    hasProperty = true;

                    // Calculate dimmed version: Multiply RGB by 0.3 but force Alpha to 1.0 (opaque)
                    Color dimmedColor = c * 0.3f; 
                    dimmedColor.a = c.a; 
                    
                    mpb.SetColor(propBaseColor, IsActive ? c : dimmedColor);
                }
                else if (r.sharedMaterial && r.sharedMaterial.HasProperty(propColor))
                {
                    c = r.sharedMaterial.GetColor(propColor);
                    hasProperty = true;

                    Color dimmedColor = c * 0.3f;
                    dimmedColor.a = c.a;

                    mpb.SetColor(propColor, IsActive ? c : dimmedColor);
                }

                if (hasProperty)
                {
                    r.SetPropertyBlock(mpb);
                }
            }
        }

        [System.Serializable]
        private struct BuildingStateContainer
        {
            public string payload;
            public float cooldown;
        }

        private string WrapState(string payload)
        {
            var container = new BuildingStateContainer
            {
                payload = payload,
                cooldown = Mathf.Max(0f, relocationCooldown)
            };
            return JsonUtility.ToJson(container);
        }

        private string ExtractPayload(string wrappedState, out float savedCooldown)
        {
            savedCooldown = 0f;
            if (string.IsNullOrEmpty(wrappedState))
                return extractorState;

            try
            {
                var container = JsonUtility.FromJson<BuildingStateContainer>(wrappedState);
                savedCooldown = Mathf.Max(0f, container.cooldown);
                return container.payload;
            }
            catch
            {
                return wrappedState;
            }
        }

        public string GatherSerializedState()
        {
            var providers = GetComponentsInChildren<IHexWorldBuildingStateProvider>(true);
            string payload;

            if (providers == null || providers.Length == 0)
            {
                payload = extractorState;
            }
            else if (providers.Length == 1)
            {
                payload = providers[0].GetSerializedState();
            }
            else
            {
                payload = providers[0].GetSerializedState();
            }

            extractorState = payload;
            return WrapState(payload);
        }

        public void RestoreSerializedState(string state)
        {
            float savedCooldown;
            string payload = ExtractPayload(state, out savedCooldown);

            relocationCooldown = Mathf.Max(0f, savedCooldown);
            extractorState = payload;

            if (string.IsNullOrEmpty(payload))
                return;

            var providers = GetComponentsInChildren<IHexWorldBuildingStateProvider>(true);
            if (providers == null || providers.Length == 0)
                return;

            for (int i = 0; i < providers.Length; i++)
            {
                providers[i].LoadSerializedState(payload);
            }
        }

        public float GetRelocationCooldown() => relocationCooldown;
        public void SetRelocationCooldown(float seconds) => relocationCooldown = Mathf.Max(0f, seconds);
    }
}
