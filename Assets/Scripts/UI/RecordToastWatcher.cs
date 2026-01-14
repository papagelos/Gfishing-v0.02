using System.Collections.Generic;
using GalacticFishing;
using UnityEngine;

/// <summary>
/// Watches InventoryStatsService for changes in best Weight/Length per fish,
/// and triggers a RecordToastView when a new record is detected.
///
/// Updated:
/// - Still supports the old InventoryService.OnChanged snapshot flow.
/// - Adds NotifyCatch(...) so auto-sold fish can still toast (no count increase).
/// </summary>
public sealed class RecordToastWatcher : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private RecordToastView toastView;

    private struct BestTriple
    {
        public float weightKg;
        public float lengthCm;
        public int   quality;
        public bool  hasWeight;
        public bool  hasLength;
        public bool  hasQuality;

        public int   count;      // inventory count snapshot for this species
    }

    // registryId → cached best + last known count
    private readonly Dictionary<int, BestTriple> _bestById = new Dictionary<int, BestTriple>();

    // We don’t want to spam toasts on first load with all existing records.
    private bool _initialisedSnapshot;

    private void OnEnable()
    {
        InventoryService.OnChanged += HandleInventoryChanged;
    }

    private void OnDisable()
    {
        InventoryService.OnChanged -= HandleInventoryChanged;
    }

    private void Start()
    {
        // Take initial snapshot silently.
        RefreshSnapshot(initial: true);
        _initialisedSnapshot = true;
    }

    private void HandleInventoryChanged()
    {
        if (!_initialisedSnapshot)
        {
            RefreshSnapshot(initial: true);
            _initialisedSnapshot = true;
        }
        else
        {
            RefreshSnapshot(initial: false);
        }
    }

    /// <summary>
    /// Explicit notification that a catch of this species occurred.
    /// This bypasses the "inventory count must increase" rule, so it works with auto-sell.
    ///
    /// prevBestWeightKg/prevBestLengthCm should be the PERSONAL bests BEFORE the catch was recorded,
    /// or null if unknown.
    /// </summary>
    public void NotifyCatch(int registryId, Fish fish, float? prevBestWeightKg = null, float? prevBestLengthCm = null)
    {
        if (toastView == null) return;
        if (!InventoryService.IsInitialized) return;

        // Read current PERSONAL bests after RecordCatch.
        bool hasW = InventoryStatsService.TryGetWeightRecord(registryId, out var rw);
        bool hasL = InventoryStatsService.TryGetLengthRecord(registryId, out var rl);
        bool hasQ = InventoryStatsService.TryGetQualityRecord(registryId, out var rq); // cached only

        if (!(hasW || hasL || hasQ))
            return;

        // Fetch previous cached values (if any) for fallback comparisons.
        _bestById.TryGetValue(registryId, out var previous);
        bool hadPrevious = _bestById.ContainsKey(registryId);

        float newW = hasW ? rw.weightKg : previous.weightKg;
        float newL = hasL ? rl.lengthCm : previous.lengthCm;
        int   newQ = hasQ ? rq.quality : previous.quality;

        bool newHasW = hasW || previous.hasWeight;
        bool newHasL = hasL || previous.hasLength;
        bool newHasQ = hasQ || previous.hasQuality;

        // Determine whether THIS catch improved the PERSONAL record.
        bool improvedW = false;
        bool improvedL = false;

        const float EPS_W = 0.0001f;
        const float EPS_L = 0.0001f;

        if (hasW)
        {
            float compareW = prevBestWeightKg ?? (hadPrevious && previous.hasWeight ? previous.weightKg : float.NegativeInfinity);
            if (!float.IsNegativeInfinity(compareW) && newW > compareW + EPS_W) improvedW = true;
            if (float.IsNegativeInfinity(compareW)) improvedW = true; // first ever record
        }

        if (hasL)
        {
            float compareL = prevBestLengthCm ?? (hadPrevious && previous.hasLength ? previous.lengthCm : float.NegativeInfinity);
            if (!float.IsNegativeInfinity(compareL) && newL > compareL + EPS_L) improvedL = true;
            if (float.IsNegativeInfinity(compareL)) improvedL = true; // first ever record
        }

        // Update cache (do not change count here)
        _bestById[registryId] = new BestTriple
        {
            weightKg   = newW,
            lengthCm   = newL,
            quality    = newQ,
            hasWeight  = newHasW,
            hasLength  = newHasL,
            hasQuality = newHasQ,
            count      = previous.count
        };

        if (!(improvedW || improvedL))
            return;

        // World record info (theoretical)
        float? worldW = null;
        float? worldL = null;
        bool isWorldW = false;
        bool isWorldL = false;

        if (FishWorldRecords.Instance != null && fish != null)
        {
            var wr = FishWorldRecords.Instance.GetWorldRecord(fish);
            if (wr.IsValid)
            {
                worldW = wr.maxWeightKg;
                worldL = wr.maxLengthCm;

                const float EPS_WORLD_W = 0.0001f;
                const float EPS_WORLD_L = 0.01f;

                if (improvedW && worldW.HasValue && newW >= wr.maxWeightKg - EPS_WORLD_W)
                    isWorldW = true;

                if (improvedL && worldL.HasValue && newL >= wr.maxLengthCm - EPS_WORLD_L)
                    isWorldL = true;
            }
        }

        string fishName = fish != null ? fish.displayName : null;
        if (string.IsNullOrEmpty(fishName) && fish != null) fishName = fish.name;
        if (string.IsNullOrEmpty(fishName)) fishName = "Fish";

        toastView.ShowNewRecord(
            fishName,
            improvedW ? newW : (float?)null,
            improvedL ? newL : (float?)null,
            worldW,
            worldL,
            isWorldW,
            isWorldL
        );
    }

    private void RefreshSnapshot(bool initial)
    {
        if (toastView == null) return;
        if (!InventoryService.IsInitialized) return;

        foreach (var (registryId, fish, countNow) in InventoryService.All())
        {
            if (fish == null)
                continue;

            bool hasW = InventoryStatsService.TryGetWeightRecord(registryId, out var rw);
            bool hasL = InventoryStatsService.TryGetLengthRecord(registryId, out var rl);
            bool hasQ = InventoryStatsService.TryGetQualityRecord(registryId, out var rq);

            bool hasAnyStat = hasW || hasL || hasQ;
            if (!hasAnyStat)
            {
                if (_bestById.TryGetValue(registryId, out var prevEmpty))
                {
                    prevEmpty.count = countNow;
                    _bestById[registryId] = prevEmpty;
                }
                else
                {
                    _bestById[registryId] = new BestTriple { count = countNow };
                }
                continue;
            }

            bool hadPrevious = _bestById.TryGetValue(registryId, out var previous);

            // Gate by inventory count INCREASE for the automatic snapshot flow.
            bool countIncreased = !hadPrevious || countNow > previous.count;

            float newW = previous.weightKg;
            float newL = previous.lengthCm;
            int   newQ = previous.quality;

            bool newHasW = previous.hasWeight;
            bool newHasL = previous.hasLength;
            bool newHasQ = previous.hasQuality;

            bool improvedW = false;
            bool improvedL = false;

            if (hasW)
            {
                newHasW = true;
                newW    = rw.weightKg;

                if (!initial && countIncreased)
                {
                    if (!hadPrevious || !previous.hasWeight || newW > previous.weightKg + 0.0001f)
                        improvedW = true;
                }
            }

            if (hasL)
            {
                newHasL = true;
                newL    = rl.lengthCm;

                if (!initial && countIncreased)
                {
                    if (!hadPrevious || !previous.hasLength || newL > previous.lengthCm + 0.0001f)
                        improvedL = true;
                }
            }

            if (hasQ)
            {
                newHasQ = true;
                newQ    = rq.quality;
            }

            _bestById[registryId] = new BestTriple
            {
                weightKg   = newW,
                lengthCm   = newL,
                quality    = newQ,
                hasWeight  = newHasW,
                hasLength  = newHasL,
                hasQuality = newHasQ,
                count      = countNow
            };

            if (initial || !countIncreased || !(improvedW || improvedL))
                continue;

            // World record info (theoretical)
            float? worldW = null;
            float? worldL = null;
            bool   isWorldW = false;
            bool   isWorldL = false;

            if (FishWorldRecords.Instance != null)
            {
                var wr = FishWorldRecords.Instance.GetWorldRecord(fish);
                if (wr.IsValid)
                {
                    worldW = wr.maxWeightKg;
                    worldL = wr.maxLengthCm;

                    const float EPS_WORLD_W = 0.0001f;
                    const float EPS_WORLD_L = 0.01f;

                    if (improvedW && newW >= wr.maxWeightKg - EPS_WORLD_W)
                        isWorldW = true;

                    if (improvedL && newL >= wr.maxLengthCm - EPS_WORLD_L)
                        isWorldL = true;
                }
            }

            string fishName = fish.displayName;
            if (string.IsNullOrEmpty(fishName))
                fishName = fish.name;

            toastView.ShowNewRecord(
                fishName,
                improvedW ? newW : (float?)null,
                improvedL ? newL : (float?)null,
                worldW,
                worldL,
                isWorldW,
                isWorldL
            );
        }
    }
}
