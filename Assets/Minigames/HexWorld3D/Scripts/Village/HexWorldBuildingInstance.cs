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

        /// <summary>
        /// Gathers serialized state from all IHexWorldBuildingStateProvider components on this building.
        /// Returns null if no state providers exist or none have state to save.
        /// </summary>
        public string GatherSerializedState()
        {
            var providers = GetComponentsInChildren<IHexWorldBuildingStateProvider>(true);
            if (providers == null || providers.Length == 0)
                return extractorState; // Return existing state if no providers

            // For single provider (most common case), just return its state
            if (providers.Length == 1)
            {
                string state = providers[0].GetSerializedState();
                extractorState = state;
                return state;
            }

            // For multiple providers, we'd need a more complex format
            // For now, just use the first provider's state
            string firstState = providers[0].GetSerializedState();
            extractorState = firstState;
            return firstState;
        }

        /// <summary>
        /// Restores serialized state to all IHexWorldBuildingStateProvider components on this building.
        /// </summary>
        public void RestoreSerializedState(string state)
        {
            extractorState = state;

            if (string.IsNullOrEmpty(state))
                return;

            var providers = GetComponentsInChildren<IHexWorldBuildingStateProvider>(true);
            if (providers == null || providers.Length == 0)
                return;

            // Distribute state to all providers
            for (int i = 0; i < providers.Length; i++)
            {
                providers[i].LoadSerializedState(state);
            }
        }
    }
}