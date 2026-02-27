using UnityEngine;

namespace GalacticFishing.Minigames.Dungeon3D
{
    [DisallowMultipleComponent]
    public sealed class DungeonSpawnDirector : MonoBehaviour
    {
        private const int DefaultGlobalAliveCap = 300;
        private const int DefaultSpawnBudgetPerFrame = 5;

        private static readonly SpawnSegment FallbackSegment = new()
        {
            segmentStartSec = 0,
            enemyMinimum = 20,
            quotaMultiplier = 1f,
            maintenanceSpawnCount = 1
        };

        [Header("Refs")]
        [SerializeField] private DimensionRenderer renderer;
        [SerializeField] private DimensionGenerator generator;
        [SerializeField] private DungeonSpawnSegmentProfile segmentProfile;

        [Header("Intensity")]
        [SerializeField, Min(0f)] private float intensityBaseline = 1.0f;
        [SerializeField, Min(0f)] private float intensitySlopePerSecond = 0.05f;
        [SerializeField, Min(0f)] private float intensityCap = 10.0f;

        private float _elapsedSeconds;
        private bool _isRunning;

        private void Awake()
        {
            if (renderer == null)
                renderer = GetComponent<DimensionRenderer>();
            if (generator == null)
                generator = GetComponent<DimensionGenerator>();
        }

        private void OnEnable()
        {
            if (generator != null)
                generator.OnGenerated += HandleGenerated;

            TryStartFromCurrentLayout();
        }

        private void OnDisable()
        {
            if (generator != null)
                generator.OnGenerated -= HandleGenerated;
        }

        private void OnValidate()
        {
            intensityBaseline = Mathf.Max(0f, intensityBaseline);
            intensitySlopePerSecond = Mathf.Max(0f, intensitySlopePerSecond);
            intensityCap = Mathf.Max(intensityBaseline, intensityCap);
        }

        private void Update()
        {
            if (!Application.isPlaying)
                return;
            if (renderer == null || generator == null)
                return;

            if (!_isRunning)
            {
                TryStartFromCurrentLayout();
                if (!_isRunning)
                    return;
            }

            _elapsedSeconds += Time.deltaTime;
            SpawnTick();
        }

        private void HandleGenerated(DimensionLayout layout)
        {
            if (layout == null || layout.tiles == null || layout.tiles.Count == 0)
            {
                _isRunning = false;
                return;
            }

            _elapsedSeconds = 0f;
            _isRunning = true;
        }

        private void TryStartFromCurrentLayout()
        {
            DimensionLayout layout = generator != null ? generator.Layout : null;
            if (layout == null || layout.tiles == null || layout.tiles.Count == 0)
                return;

            _elapsedSeconds = 0f;
            _isRunning = true;
        }

        private void SpawnTick()
        {
            int globalAliveCap = segmentProfile != null ? Mathf.Max(1, segmentProfile.globalAliveCap) : DefaultGlobalAliveCap;
            int spawnBudgetPerFrame = segmentProfile != null ? Mathf.Max(1, segmentProfile.spawnBudgetPerFrame) : DefaultSpawnBudgetPerFrame;

            int aliveCount = renderer.CurrentAliveCount;
            if (aliveCount >= globalAliveCap)
                return;

            SpawnSegment segment = ResolveActiveSegment();
            float intensity = Mathf.Clamp(
                intensityBaseline + intensitySlopePerSecond * _elapsedSeconds,
                intensityBaseline,
                Mathf.Max(intensityBaseline, intensityCap));

            float rawQuota = segment.enemyMinimum * segment.quotaMultiplier * intensity;
            int quota = Mathf.Max(0, Mathf.CeilToInt(rawQuota));

            int availableSlots = globalAliveCap - aliveCount;
            if (availableSlots <= 0)
                return;

            if (aliveCount < quota)
            {
                int needed = quota - aliveCount;
                int spawnCount = Mathf.Min(spawnBudgetPerFrame, needed, availableSlots);
                ExecuteSpawnBudget(spawnCount);
                return;
            }

            int maintenance = Mathf.Max(0, segment.maintenanceSpawnCount);
            if (maintenance <= 0)
                return;

            int maintenanceCount = Mathf.Min(spawnBudgetPerFrame, maintenance, availableSlots);
            ExecuteSpawnBudget(maintenanceCount);
        }

        private void ExecuteSpawnBudget(int count)
        {
            for (int i = 0; i < count; i++)
            {
                // Bail immediately on failed spawn attempts (invalid map/tile or missing prefab setup).
                if (!renderer.SpawnSingleRandomEnemy())
                    break;
            }
        }

        private SpawnSegment ResolveActiveSegment()
        {
            if (segmentProfile == null)
                return FallbackSegment;

            if (!segmentProfile.TryGetSegmentForElapsedSeconds(Mathf.FloorToInt(_elapsedSeconds), out SpawnSegment segment) ||
                segment == null)
            {
                return FallbackSegment;
            }

            return segment;
        }
    }
}
