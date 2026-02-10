using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GalacticFishing
{
    [AddComponentMenu("Galactic Fishing/World Manager")]
    public sealed class WorldManager : MonoBehaviour
    {
        [Header("Active Selection")]
        public WorldDefinition world;
        public int lakeIndex = 0;

        [Header("Scene Hooks")]
        [Tooltip("SpriteRenderer that shows the current world / lake backdrop.")]
        public SpriteRenderer backdropRenderer;

        [Tooltip("Boat rig transform whose Y position can differ per lake & boat.")]
        public Transform boatRig;

        [Header("Water")]
        [Tooltip("Water surface marker in the scene. If left empty, WorldManager will try to auto-find one.")]
        public WaterSurfaceMarker waterSurface;

        [Header("Progress (stub)")]
        [SerializeField] private int maxUnlockedLake = 0;

        [Header("Boat selection")]
        [Tooltip("Simple ID for the currently equipped boat (e.g. 'default', 'big_boat').")]
        [SerializeField] private string currentBoatId = "default";

        [Header("Boat height per lake & boat (optional)")]
        [SerializeField] private List<BoatHeightOverride> boatHeights = new List<BoatHeightOverride>();

        [Header("Waterline per lake (optional)")]
        [Tooltip("Overrides WaterSurfaceMarker.localPosition.y per world + lakeIndex. Uses the marker's initial local Y if no override is found.")]
        [SerializeField] private List<WaterlineOverride> waterlines = new List<WaterlineOverride>();

        /// <summary>
        /// Fired whenever the active world context changes (world or lake index).
        /// Args: (currentWorld, currentLakeIndex).
        /// </summary>
        public event Action<WorldDefinition, int> WorldChanged;

        [Serializable]
        public struct BoatHeightOverride
        {
            [Tooltip("World this override belongs to.")]
            public WorldDefinition world;

            [Tooltip("Index in that World's Lakes list.")]
            public int lakeIndex;

            [Tooltip("Boat ID this override is for. Leave empty to match ANY boat in this lake.")]
            public string boatId;

            [Tooltip("BoatRig.position.y when this world/lake/boat is active.")]
            public float boatY;
        }

        [Serializable]
        public struct WaterlineOverride
        {
            [Tooltip("World this override belongs to.")]
            public WorldDefinition world;

            [Tooltip("Index in that World's Lakes list.")]
            public int lakeIndex;

            [Tooltip("WaterSurfaceMarker.localPosition.y when this world/lake is active.")]
            public float waterLocalY;
        }

        // Cached starting Y so "no override" = original scene value.
        private float _initialBoatY;

        // Cached starting waterline local Y so "no override" = original scene value.
        private float _initialWaterLocalY;
        private bool _hasInitialWaterLocalY;

        void Awake()
        {
            if (boatRig != null)
                _initialBoatY = boatRig.position.y;

            // If user didn't wire it, try to auto-find one marker in the scene.
            if (waterSurface == null)
                waterSurface = FindFirstObjectByType<WaterSurfaceMarker>();

            if (waterSurface != null)
            {
                _initialWaterLocalY = waterSurface.transform.localPosition.y;
                _hasInitialWaterLocalY = true;
            }
        }

        void Start()
        {
            ApplyWorldContext();
            // Initial notification so listeners can spawn initial fish, etc.
            WorldChanged?.Invoke(world, lakeIndex);
        }

        private void OnDestroy()
        {
            Debug.LogWarning($"[Diagnostic] WorldManager on {gameObject.name} is being DESTROYED. StackTrace: {System.Environment.StackTrace}");
        }

        /// <summary>
        /// External systems (boat upgrades etc.) can call this to change the active boat.
        /// For now you can just set the initial value in the inspector.
        /// </summary>
        public void SetCurrentBoat(string boatId, bool applyNow = true)
        {
            currentBoatId = boatId;
            if (applyNow)
                ApplyWorldContext();
        }

        public string CurrentBoatId => currentBoatId;

        public void ApplyWorldContext()
        {
            if (!world) return;
            var lake = GetLake(lakeIndex);

            // ---------------- Backdrop ----------------
            if (backdropRenderer)
            {
                var sprite = (lake != null && lake.backdropOverride)
                    ? lake.backdropOverride
                    : world.backdrop;

                if (sprite)
                    backdropRenderer.sprite = sprite;

                var fitter = backdropRenderer.GetComponent<FitSpriteToCamera>();
                if (fitter) fitter.ApplyNow();
            }

            // ---------------- Waterline (per world + lake) ----------------
            ApplyWaterlineOverride();

            // ---------------- Boat height (per world + lake + boat) ----------------
            if (boatRig != null)
            {
                float targetY = _initialBoatY;
                string activeBoat = string.IsNullOrEmpty(currentBoatId) ? string.Empty : currentBoatId;

                if (boatHeights != null && world != null)
                {
                    // bestMatch: null = no override found yet
                    float? bestMatch = null;

                    for (int i = 0; i < boatHeights.Count; i++)
                    {
                        var entry = boatHeights[i];
                        if (entry.world != world || entry.lakeIndex != lakeIndex)
                            continue;

                        bool entryHasBoat = !string.IsNullOrEmpty(entry.boatId);

                        // Exact boat match wins immediately.
                        if (entryHasBoat && entry.boatId == activeBoat)
                        {
                            bestMatch = entry.boatY;
                            break;
                        }

                        // Fallback: world+lake with empty boatId = "any boat",
                        // only if we don't already have a better match.
                        if (!entryHasBoat && bestMatch == null)
                        {
                            bestMatch = entry.boatY;
                        }
                    }

                    if (bestMatch.HasValue)
                        targetY = bestMatch.Value;
                }

                var pos = boatRig.position;
                pos.y = targetY;
                boatRig.position = pos;
            }

            // ---------------- Quality settings ----------------
            var qs = QualitySettings.Active;
            if (qs)
            {
                float up = Mathf.Clamp01(world.upwardBonus + (lake != null ? lake.addUpwardBonus : 0f));
                float tig = Mathf.Clamp01(world.tightenBonus + (lake != null ? lake.addTightenBonus : 0f));
                qs.upwardBonus = up;
                qs.tightenBonus = tig;
            }
        }

        private void ApplyWaterlineOverride()
        {
            if (waterSurface == null)
                waterSurface = FindFirstObjectByType<WaterSurfaceMarker>();

            if (waterSurface == null)
                return;

            if (!_hasInitialWaterLocalY)
            {
                _initialWaterLocalY = waterSurface.transform.localPosition.y;
                _hasInitialWaterLocalY = true;
            }

            float targetLocalY = _initialWaterLocalY;

            if (waterlines != null && world != null)
            {
                for (int i = 0; i < waterlines.Count; i++)
                {
                    var entry = waterlines[i];
                    if (entry.world != world || entry.lakeIndex != lakeIndex)
                        continue;

                    targetLocalY = entry.waterLocalY;
                    break;
                }
            }

            var lp = waterSurface.transform.localPosition;
            lp.y = targetLocalY;
            waterSurface.transform.localPosition = lp;
        }

        public void SetLake(int index)
        {
            lakeIndex = Mathf.Clamp(index, 0, world != null ? world.lakes.Count - 1 : 0);
            ApplyWorldContext();
            WorldChanged?.Invoke(world, lakeIndex);
        }

        public bool IsLakeUnlocked(int index) => index <= Mathf.Max(maxUnlockedLake, 0);

        public bool TryUnlockNextLake()
        {
            if (!world) return false;
            int next = maxUnlockedLake + 1;
            if (next < world.lakes.Count)
            {
                maxUnlockedLake = next;
                return true;
            }
            return false;
        }

        public void SetWorld(WorldDefinition newWorld, int newLakeIndex = 0)
        {
            world = newWorld;
            lakeIndex = Mathf.Max(0, newLakeIndex);
            ApplyWorldContext();
            WorldChanged?.Invoke(world, lakeIndex);
        }

#if UNITY_EDITOR
        [ContextMenu("Apply Now (Editor)")]
        void EditorApplyNow()
        {
            if (!Application.isPlaying) ApplyWorldContext();
        }
#endif

        public Lake GetLake(int index)
        {
            if (!world || world.lakes == null || index < 0 || index >= world.lakes.Count) return null;
            return world.lakes[index];
        }

        public IReadOnlyList<FishWeight> GetActivePool()
        {
            if (!world) return Array.Empty<FishWeight>();
            var lake = GetLake(lakeIndex);

            if (lake != null && lake.usePoolOverride && lake.poolOverride != null && lake.poolOverride.Count > 0)
                return lake.poolOverride;

            if (world.defaultPool != null && world.defaultPool.Count > 0)
                return world.defaultPool;

#if UNITY_EDITOR
            return _editorDefaultPool ??= BuildEditorDefaultPool();
#else
            return Array.Empty<FishWeight>();
#endif
        }

#if UNITY_EDITOR
        List<FishWeight> _editorDefaultPool;
        List<FishWeight> BuildEditorDefaultPool()
        {
            var list = new List<FishWeight>();
            var guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/Data/Fish" });
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var obj  = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (!obj) continue;
                var name = System.IO.Path.GetFileNameWithoutExtension(path);
                if (!name.StartsWith("Fish_")) continue;
                list.Add(new FishWeight { fish = obj, weight = 1 });
            }
            return list;
        }
#endif

        public ScriptableObject PickRandomFish(System.Random rng = null)
        {
            var pool = GetActivePool();
            int total = 0;
            foreach (var e in pool) total += Mathf.Max(0, e.weight);
            if (total <= 0) return null;

            int roll = rng != null ? rng.Next(total) : UnityEngine.Random.Range(0, total);
            int acc = 0;

            foreach (var e in pool)
            {
                int w = Mathf.Max(0, e.weight);
                if (roll < acc + w) return e.fish;
                acc += w;
            }
            return null;
        }
    }
}
