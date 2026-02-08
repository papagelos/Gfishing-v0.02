// Assets/Minigames/HexWorld3D/Scripts/Village/Minigames/HerbalistMinigameController.cs
using System;
using UnityEngine;

namespace GalacticFishing.Minigames.HexWorld
{
    /// <summary>
    /// Controls the Herbalist Greenhouse 3x3 grid of herb plots.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HerbalistMinigameController : MonoBehaviour, IHexWorldBuildingStateProvider
    {
        private const int GridSize = 3;
        private const int PlotCount = GridSize * GridSize;

        [SerializeField, Range(0.01f, 0.5f)] private float baseGrowthPerTick = 0.12f;
        [SerializeField] private string fertileMeadowTag = "meadow.fertile";
        [SerializeField] private string herbMarshTag = "wetland.herbmarsh";
        [SerializeField] private float meadowBonus = 0.10f;
        [SerializeField] private float marshBonus = 0.15f;

        private HexWorld3DController _controller;
        private HexWorldWarehouseInventory _warehouse;
        private HexWorldBuildingInstance _buildingInstance;

        [SerializeField] private float[] _plotProgress = new float[PlotCount];
        [SerializeField] private bool[] _plotOccupied = new bool[PlotCount];

        private bool _loggedMissingTag;
        private float _cachedBonus = -1f;

        public event Action<int, float> PlotGrowthChanged;
        public event Action<float> GrowthBonusChanged;

        private void Awake()
        {
            _buildingInstance = GetComponent<HexWorldBuildingInstance>();
            EnsureGridInitialized();
        }

        private void Start()
        {
            _controller = UnityEngine.Object.FindObjectOfType<HexWorld3DController>(true);
            _warehouse = UnityEngine.Object.FindObjectOfType<HexWorldWarehouseInventory>(true);

            var ticker = UnityEngine.Object.FindObjectOfType<HexWorldProductionTicker>(true);
            if (ticker != null)
                ticker.TickCompleted += OnProductionTick;
        }

        private void OnDestroy()
        {
            var ticker = UnityEngine.Object.FindObjectOfType<HexWorldProductionTicker>(true);
            if (ticker != null)
                ticker.TickCompleted -= OnProductionTick;
        }

        private void EnsureGridInitialized()
        {
            if (_plotOccupied == null || _plotOccupied.Length != PlotCount)
            {
                _plotOccupied = new bool[PlotCount];
                _plotProgress = new float[PlotCount];
            }

            bool anyOccupied = false;
            for (int i = 0; i < PlotCount; i++)
            {
                if (_plotOccupied[i])
                {
                    anyOccupied = true;
                    break;
                }
            }

            if (!anyOccupied)
            {
                for (int i = 0; i < PlotCount; i++)
                {
                    _plotOccupied[i] = true;
                    _plotProgress[i] = 0f;
                }
            }
            _cachedBonus = -1f;
        }

        private void OnProductionTick()
        {
            if (_buildingInstance != null && !_buildingInstance.IsActive)
                return;
            if (_buildingInstance != null && _buildingInstance.GetRelocationCooldown() > 0f)
                return;

            float growthPerTick = GetEffectiveGrowth();

            for (int i = 0; i < PlotCount; i++)
            {
                if (!_plotOccupied[i]) continue;

                float next = Mathf.Clamp01(_plotProgress[i] + growthPerTick);
                if (!Mathf.Approximately(next, _plotProgress[i]))
                {
                    _plotProgress[i] = next;
                    PlotGrowthChanged?.Invoke(i, _plotProgress[i]);
                }

                if (_plotProgress[i] >= 1f)
                {
                    HarvestPlot(i);
                }
            }
        }

        private float GetEffectiveGrowth()
        {
            float bonus = 0f;

            if (_controller != null && _buildingInstance != null)
            {
                int meadowTiles = _controller.CountAdjacentTilesWithTag(_buildingInstance.Coord, fertileMeadowTag);
                int marshTiles = _controller.CountAdjacentTilesWithTag(_buildingInstance.Coord, herbMarshTag);

                bonus += meadowTiles * meadowBonus;
                bonus += marshTiles * marshBonus;

                if (meadowTiles == 0 && marshTiles == 0 && !_loggedMissingTag)
                {
                    Debug.LogWarning($"[HerbalistMinigame] No tagged tiles found near {_buildingInstance.buildingName}. Bonuses will be 0.");
                    _loggedMissingTag = true;
                }
            }

            if (_cachedBonus < 0f || !Mathf.Approximately(_cachedBonus, bonus))
            {
                _cachedBonus = bonus;
                GrowthBonusChanged?.Invoke(_cachedBonus);
            }

            return baseGrowthPerTick * (1f + bonus);
        }

        private void HarvestPlot(int index)
        {
            _plotProgress[index] = 0f;
            PlotGrowthChanged?.Invoke(index, 0f);

            if (_warehouse == null)
                _warehouse = UnityEngine.Object.FindObjectOfType<HexWorldWarehouseInventory>(true);

            if (_warehouse == null) return;

            _warehouse.TryAdd(HexWorldResourceId.Herbs, 2);
            _warehouse.TryAdd(HexWorldResourceId.Fiber, 1);
        }

        public int HarvestAllReady()
        {
            int harvested = 0;
            for (int i = 0; i < PlotCount; i++)
            {
                if (!_plotOccupied[i]) continue;
                if (_plotProgress[i] < 1f) continue;
                HarvestPlot(i);
                harvested++;
            }
            return harvested;
        }

        public float GetGrowthBonusPercent() => _cachedBonus < 0f ? 0f : _cachedBonus;

        public float GetPlotProgress(int index)
        {
            if (index < 0 || index >= PlotCount) return 0f;
            return _plotProgress[index];
        }

        public int GetPlotCount() => PlotCount;

        public void ForceRefreshEvents()
        {
            GrowthBonusChanged?.Invoke(GetGrowthBonusPercent());
            for (int i = 0; i < PlotCount; i++)
            {
                PlotGrowthChanged?.Invoke(i, _plotProgress[i]);
            }
        }

        [Serializable]
        private struct HerbalistState
        {
            public float[] plotProgress;
            public bool[] occupied;
        }

        public string GetSerializedState()
        {
            var state = new HerbalistState
            {
                plotProgress = _plotProgress,
                occupied = _plotOccupied
            };
            return JsonUtility.ToJson(state);
        }

        public void LoadSerializedState(string state)
        {
            if (string.IsNullOrEmpty(state))
            {
                EnsureGridInitialized();
                return;
            }

            try
            {
                var parsed = JsonUtility.FromJson<HerbalistState>(state);
                _plotProgress = parsed.plotProgress != null && parsed.plotProgress.Length == PlotCount
                    ? parsed.plotProgress
                    : new float[PlotCount];

                _plotOccupied = parsed.occupied != null && parsed.occupied.Length == PlotCount
                    ? parsed.occupied
                    : new bool[PlotCount];

                EnsureGridInitialized();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HerbalistMinigame] Failed to parse state: {e.Message}");
                EnsureGridInitialized();
            }
        }
    }
}
