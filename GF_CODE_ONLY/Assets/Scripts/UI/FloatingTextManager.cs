// Assets/Scripts/UI/FloatingTextManager.cs
using UnityEngine;

namespace GalacticFishing.UI
{
    /// <summary>
    /// Singleton that spawns floating text on a UI canvas.
    /// Call:
    ///   FloatingTextManager.Instance.SpawnWorld("Upgrade your rod!", worldPos, Color.cyan);
    /// or:
    ///   FloatingTextManager.Instance.SpawnAtScreenCenter("12.3 kg  •  145.6 cm", Color.yellow);
    /// </summary>
    public class FloatingTextManager : MonoBehaviour
    {
        public static FloatingTextManager Instance { get; private set; }

        [Header("Setup")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private FloatingText floatingTextPrefab;
        [SerializeField] private Camera worldCamera;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (canvas == null)
                canvas = GetComponentInParent<Canvas>();

            if (worldCamera == null)
                worldCamera = Camera.main;
        }

        /// <summary>
        /// Spawn a popup above a world position.
        /// </summary>
        public void SpawnWorld(string message, Vector3 worldPosition, Color color)
        {
            if (floatingTextPrefab == null || canvas == null)
            {
                Debug.LogWarning("[FloatingTextManager] Missing prefab or canvas.");
                return;
            }

            var instance = Instantiate(floatingTextPrefab, canvas.transform);
            instance.Init(message, worldPosition, worldCamera, color);
        }

        /// <summary>
        /// Overload with default white color.
        /// </summary>
        public void SpawnWorld(string message, Vector3 worldPosition)
        {
            SpawnWorld(message, worldPosition, Color.white);
        }

        /// <summary>
        /// Spawn text in the center of the screen (in front of the world camera).
        /// </summary>
        public void SpawnAtScreenCenter(string message, Color color)
        {
            if (worldCamera == null)
                worldCamera = Camera.main;

            if (worldCamera == null)
            {
                Debug.LogWarning("[FloatingTextManager] No worldCamera assigned.");
                return;
            }

            // Put the world position in front of the camera so the projection lands at screen center.
            // Distance can be anything positive; in a 2D setup Z usually doesn't matter for screen point.
            const float distanceInFront = 5f;
            Vector3 worldPos = worldCamera.transform.position + worldCamera.transform.forward * distanceInFront;

            SpawnWorld(message, worldPos, color);
        }

        /// <summary>
        /// Center spawn with default white color.
        /// </summary>
        public void SpawnAtScreenCenter(string message)
        {
            SpawnAtScreenCenter(message, Color.white);
        }
    }
}
