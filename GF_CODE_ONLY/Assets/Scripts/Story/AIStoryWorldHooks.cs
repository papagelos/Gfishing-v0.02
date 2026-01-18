using System.Collections;
using UnityEngine;

namespace GalacticFishing
{
    /// <summary>
    /// Raises story triggers when entering specific worlds/lakes.
    /// Uses AIStoryDirector's showOnce persistence (PlayerPrefs) so repeated calls are safe.
    /// </summary>
    public sealed class AIStoryWorldHooks : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private WorldManager worldManager;

        [Header("Welcome (Lake 1 first time)")]
        [SerializeField] private WorldDefinition lake1World;   // drag your World 1 asset here
        [SerializeField] private int lake1Index = 0;           // lake index 0 = first lake
        [SerializeField] private string welcomeStoryId = "Welcome_Lake1";

        [Tooltip("Also check once on Start, in case we miss the first WorldChanged event due to script order.")]
        [SerializeField] private bool triggerOnStart = true;

        private void OnEnable()
        {
            if (worldManager != null)
                worldManager.WorldChanged += OnWorldChanged;
        }

        private void OnDisable()
        {
            if (worldManager != null)
                worldManager.WorldChanged -= OnWorldChanged;
        }

        private void Start()
        {
            if (!triggerOnStart || worldManager == null) return;
            StartCoroutine(TriggerIfMatchNextFrame(worldManager.world, worldManager.lakeIndex));
        }

        private void OnWorldChanged(WorldDefinition w, int idx)
        {
            StartCoroutine(TriggerIfMatchNextFrame(w, idx));
        }

        private IEnumerator TriggerIfMatchNextFrame(WorldDefinition w, int idx)
        {
            // wait 1 frame so UI/controllers are fully awake
            yield return null;

            if (w == null) yield break;
            if (lake1World != null && w != lake1World) yield break;
            if (idx != lake1Index) yield break;

            if (AIStoryDirector.Instance == null) yield break;
            AIStoryDirector.Instance.Trigger(welcomeStoryId);
        }

        [ContextMenu("DEBUG: Reset Welcome Seen Flag")]
        private void DebugResetWelcomeSeenFlag()
        {
            PlayerPrefs.DeleteKey("AIStory_Seen_" + welcomeStoryId);
            PlayerPrefs.Save();
            Debug.Log($"Reset seen flag: AIStory_Seen_{welcomeStoryId}");
        }
    }
}
