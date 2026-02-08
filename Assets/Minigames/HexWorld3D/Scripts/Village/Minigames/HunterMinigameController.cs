// Assets/Minigames/HexWorld3D/Scripts/Village/Minigames/HunterMinigameController.cs
using System;
using UnityEngine;

namespace GalacticFishing.Minigames.HexWorld
{
    /// <summary>
    /// Hunter Lodge extractor that captures animals over time and produces Raw Hide + Feathers.
    /// Mirrors the Forestry station pattern but uses a single capture progress meter.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HunterMinigameController : MonoBehaviour, IHexWorldBuildingStateProvider
    {
        [Header("Capture Settings")]
        [Tooltip("Base capture progress gained per production tick.")]
        [SerializeField, Range(0.01f, 0.5f)] private float baseCapturePerTick = 0.1f;

        [Tooltip("Additional capture speed per adjacent Fertile Meadow tile (meadow.fertile).")]
        [SerializeField, Range(0f, 0.2f)] private float fertileMeadowBonus = 0.08f;

        [Tooltip("Additional capture speed per adjacent Forest tile.")]
        [SerializeField, Range(0f, 0.2f)] private float forestAdjacencyBonus = 0.05f;

        [Tooltip("Gameplay tag used for Fertile Meadow tiles.")]
        [SerializeField] private string meadowTag = "meadow.fertile";

        [Tooltip("Gameplay tag used for Forest tiles.")]
        [SerializeField] private string forestTag = "forest";

        [Header("Outputs")]
        [SerializeField, Min(0)] private int rawHidePerCapture = 2;
        [SerializeField, Min(0)] private int feathersPerCapture = 1;

        [Header("Dependencies")]
        [SerializeField] private HexWorldBuildingProductionProfile productionProfile;

        private HexWorld3DController _controller;
        private HexWorldWarehouseInventory _warehouse;
        private HexWorldBuildingInstance _buildingInstance;

        [Range(0f, 1f)]
        [SerializeField] private float captureProgress;
        [SerializeField] private int trailFocusMode = 0;

        public event Action<float> ProgressChanged;
        public event Action<int, int> FocusChanged;

        private void Awake()
        {
            if (!productionProfile)
                productionProfile = GetComponent<HexWorldBuildingProductionProfile>();

            _buildingInstance = GetComponent<HexWorldBuildingInstance>();
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

        private void OnProductionTick()
        {
            if (_buildingInstance != null && !_buildingInstance.IsActive)
                return;
            if (_buildingInstance != null && _buildingInstance.GetRelocationCooldown() > 0f)
                return;

            float progressGain = CalculateCaptureGain();
            captureProgress = Mathf.Clamp01(captureProgress + progressGain);
            ProgressChanged?.Invoke(captureProgress);

            if (captureProgress >= 1f)
            {
                captureProgress = 0f;
                ProgressChanged?.Invoke(captureProgress);
                DeliverHarvest();
            }
        }

        private void DeliverHarvest()
        {
            if (_warehouse == null)
                _warehouse = UnityEngine.Object.FindObjectOfType<HexWorldWarehouseInventory>(true);

            if (_warehouse == null) return;

            (float hideMult, float featherMult) = GetFocusMultipliers();

            int hideAmount = Mathf.RoundToInt(rawHidePerCapture * hideMult);
            int feathersAmount = Mathf.RoundToInt(feathersPerCapture * featherMult);

            if (hideAmount > 0)
                _warehouse.TryAdd(HexWorldResourceId.RawHide, hideAmount);

            if (feathersAmount > 0)
                _warehouse.TryAdd(HexWorldResourceId.Feathers, feathersAmount);
        }

        private float CalculateCaptureGain()
        {
            float gain = baseCapturePerTick;

            if (_controller != null && _buildingInstance != null)
            {
                int meadowTiles = _controller.CountAdjacentTilesWithTag(_buildingInstance.Coord, meadowTag);
                int forestTiles = _controller.CountAdjacentTilesWithTag(_buildingInstance.Coord, forestTag);

                gain += meadowTiles * fertileMeadowBonus;
                gain += forestTiles * forestAdjacencyBonus;
            }

            return gain;
        }

        // ─────────────────────────────────────────────────────────────────
        // Save / Load
        // ─────────────────────────────────────────────────────────────────

        [Serializable]
        private struct HunterState
        {
            public float progress;
            public int focusMode;
        }

        public string GetSerializedState()
        {
            var state = new HunterState
            {
                progress = captureProgress,
                focusMode = trailFocusMode
            };
            return JsonUtility.ToJson(state);
        }

        public void LoadSerializedState(string state)
        {
            if (string.IsNullOrEmpty(state))
                return;

            try
            {
                var parsed = JsonUtility.FromJson<HunterState>(state);
                captureProgress = Mathf.Clamp01(parsed.progress);
                trailFocusMode = Mathf.Clamp(parsed.focusMode, 0, 2);
                ProgressChanged?.Invoke(captureProgress);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HunterMinigameController] Failed to parse state: {e.Message}");
            }
        }

        public void SetTrailFocus(int mode)
        {
            trailFocusMode = Mathf.Clamp(mode, 0, 2);
            FocusChanged?.Invoke(GetProjectedRawHide(), GetProjectedFeathers());
        }

        public int GetTrailFocus() => trailFocusMode;

        public float GetCaptureProgress() => captureProgress;

        public (float hideMultiplier, float featherMultiplier) GetFocusMultipliers()
        {
            return trailFocusMode switch
            {
                1 => (1.5f, 0.5f),
                2 => (0.5f, 1.5f),
                _ => (1f, 1f)
            };
        }

        public int GetProjectedRawHide()
        {
            var (hide, _) = GetFocusMultipliers();
            return Mathf.RoundToInt(rawHidePerCapture * hide);
        }

        public int GetProjectedFeathers()
        {
            var (_, feathers) = GetFocusMultipliers();
            return Mathf.RoundToInt(feathersPerCapture * feathers);
        }
    }
}
