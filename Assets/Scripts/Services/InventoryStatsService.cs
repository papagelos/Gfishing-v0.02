using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>
/// Persists per-species records + a small recent-catches FIFO.
/// Independent of counts-only InventoryService. JSON at Application.persistentDataPath.
/// Now also stores the *full* record entries for each #1 (weight/length/quality),
/// so we can show cross-stats and auto-build the breeding tank.
/// </summary>
public static class InventoryStatsService
{
    public const int RecentCapacity = 20;

    // Toggle for internal debug logging of record updates
    private const bool DebugLogs = true;

    // Bump this when you want to invalidate older stats on disk
    private const int CurrentVersion = 3;

    // ---- Public "entry" used for records/tank display ----
    [Serializable]
    public struct Record
    {
        public float weightKg;
        public float lengthCm;
        public int   quality;
        public long  unixMs;     // when this specimen was recorded (serves as an id)
    }

    [Serializable] public struct RuntimeStats
    {
        public bool  hasWeight; public float weightKg;
        public bool  hasLength; public float lengthCm;
        public bool  hasQuality; public int   quality;

        public static RuntimeStats From(float? kg, float? cm, int? q)
        {
            return new RuntimeStats
            {
                hasWeight  = kg.HasValue, weightKg = kg ?? 0f,
                hasLength  = cm.HasValue, lengthCm = cm ?? 0f,
                hasQuality = q.HasValue,  quality  = q ?? 0
            };
        }
    }

    // Kept for compatibility with any existing code
    [Serializable] public struct BestOf
    {
        public float maxWeightKg;
        public float maxLengthCm;
        public int   maxQuality;

        public void Accumulate(RuntimeStats s)
        {
            if (s.hasWeight)  maxWeightKg = Mathf.Max(maxWeightKg, s.weightKg);
            if (s.hasLength)  maxLengthCm = Mathf.Max(maxLengthCm, s.lengthCm);
            if (s.hasQuality) maxQuality  = Mathf.Max(maxQuality, s.quality);
        }
    }

    // Per-species pack of the #1 holders (stores the full triple that achieved each #1)
    [Serializable]
    public struct RecordPack
    {
        public Record weightRecord;   public bool hasWeight;
        public Record lengthRecord;   public bool hasLength;
        public Record qualityRecord;  public bool hasQuality;
    }

    [Serializable] struct BestKV { public int id; public BestOf best; }
    [Serializable] struct RecordPackKV { public int id; public RecordPack pack; }
    [Serializable] struct RecentEntry { public int id; public float weightKg; public float lengthCm; public int quality; public long unixMs; }

    [Serializable] class SaveBlob
    {
        public List<BestKV>       best   = new();
        public List<RecordPackKV> packs  = new();   // detailed record holders
        public List<RecentEntry>  recent = new();
        public bool breedingUnlocked = false;
        public int  version           = CurrentVersion;
    }

    static readonly Dictionary<int, BestOf>     _best   = new();
    static readonly Dictionary<int, RecordPack> _packs  = new();
    static readonly Queue<RecentEntry>          _recent = new();
    static bool _loaded;

    static bool _breedingUnlocked;

    public static bool BreedingUnlocked
    {
        get { EnsureLoaded(); return _breedingUnlocked; }
        set { EnsureLoaded(); if (_breedingUnlocked != value) { _breedingUnlocked = value; Save(); } }
    }

    static string SavePath => Path.Combine(Application.persistentDataPath, "inventory_stats_v2.json");

    public static void EnsureLoaded()
    {
        if (_loaded) return;
        Load();
        _loaded = true;
    }

    /// <summary>
    /// Call on every successful catch (species id + stat triple).
    /// This updates #1 records, recent feed, and persists to disk.
    /// </summary>
    public static void RecordCatch(int fishId, RuntimeStats stats)
    {
        EnsureLoaded();

        // update best-of (compat)
        _best.TryGetValue(fishId, out var b);
        b.Accumulate(stats);
        _best[fishId] = b;

        // update recent FIFO
        var e = new RecentEntry
        {
            id       = fishId,
            weightKg = stats.hasWeight ? stats.weightKg : 0f,
            lengthCm = stats.hasLength ? stats.lengthCm : 0f,
            quality  = stats.hasQuality ? stats.quality  : 0,
            unixMs   = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
        _recent.Enqueue(e);
        while (_recent.Count > RecentCapacity) _recent.Dequeue();

        // update detailed #1 holders (stores the full triple that achieved the #1)
        _packs.TryGetValue(fishId, out var pack);

        if (DebugLogs)
        {
            Debug.Log(
                $"[InventoryStatsService] RecordCatch candidate for id={fishId}: " +
                $"W={stats.weightKg:0.##} kg, L={stats.lengthCm:0.#} cm, Q={stats.quality}");

            if (pack.hasWeight)
            {
                Debug.Log(
                    $"[InventoryStatsService] Existing best (weight) for id={fishId}: " +
                    $"W={pack.weightRecord.weightKg:0.##} kg, L={pack.weightRecord.lengthCm:0.#} cm, Q={pack.weightRecord.quality}");
            }
            if (pack.hasLength)
            {
                Debug.Log(
                    $"[InventoryStatsService] Existing best (length) for id={fishId}: " +
                    $"W={pack.lengthRecord.weightKg:0.##} kg, L={pack.lengthRecord.lengthCm:0.#} cm, Q={pack.lengthRecord.quality}");
            }
            if (pack.hasQuality)
            {
                Debug.Log(
                    $"[InventoryStatsService] Existing best (quality) for id={fishId}: " +
                    $"W={pack.qualityRecord.weightKg:0.##} kg, L={pack.qualityRecord.lengthCm:0.#} cm, Q={pack.qualityRecord.quality}");
            }
        }

        // Weight
        if (stats.hasWeight)
        {
            if (!pack.hasWeight || stats.weightKg > pack.weightRecord.weightKg)
            {
                pack.weightRecord = new Record
                {
                    weightKg = stats.weightKg,
                    lengthCm = stats.lengthCm,
                    quality  = stats.quality,
                    unixMs   = e.unixMs
                };
                pack.hasWeight = true;
            }
        }
        // Length
        if (stats.hasLength)
        {
            if (!pack.hasLength || stats.lengthCm > pack.lengthRecord.lengthCm)
            {
                pack.lengthRecord = new Record
                {
                    weightKg = stats.weightKg,
                    lengthCm = stats.lengthCm,
                    quality  = stats.quality,
                    unixMs   = e.unixMs
                };
                pack.hasLength = true;
            }
        }
        // Quality
        if (stats.hasQuality)
        {
            if (!pack.hasQuality || stats.quality > pack.qualityRecord.quality)
            {
                pack.qualityRecord = new Record
                {
                    weightKg = stats.weightKg,
                    lengthCm = stats.lengthCm,
                    quality  = stats.quality,
                    unixMs   = e.unixMs
                };
                pack.hasQuality = true;
            }
        }

        _packs[fishId] = pack;

        if (DebugLogs && (pack.hasWeight || pack.hasLength || pack.hasQuality))
        {
            Debug.Log(
                $"[InventoryStatsService] Stored best for id={fishId}: " +
                $"W={pack.weightRecord.weightKg:0.##} kg, " +
                $"L={pack.lengthRecord.lengthCm:0.#} cm, " +
                $"Q={pack.qualityRecord.quality}");
        }

        Save();
    }

    // -------- Queries used by UI --------

    public static BestOf GetBestOf(int fishId)
    {
        EnsureLoaded();
        return _best.TryGetValue(fishId, out var v) ? v : default;
    }

    public static bool TryGetWeightRecord(int fishId, out Record r)
    {
        EnsureLoaded();
        if (_packs.TryGetValue(fishId, out var p) && p.hasWeight) { r = p.weightRecord; return true; }
        r = default; return false;
    }
    public static bool TryGetLengthRecord(int fishId, out Record r)
    {
        EnsureLoaded();
        if (_packs.TryGetValue(fishId, out var p) && p.hasLength) { r = p.lengthRecord; return true; }
        r = default; return false;
    }
    public static bool TryGetQualityRecord(int fishId, out Record r)
    {
        EnsureLoaded();
        if (_packs.TryGetValue(fishId, out var p) && p.hasQuality) { r = p.qualityRecord; return true; }
        r = default; return false;
    }

    /// <summary>
    /// Returns up to 3 unique specimens for the "breeding tank":
    /// the union of the current #1 holders (weight/length/quality), de-duplicated.
    /// This auto-updates as soon as a new #1 appears (no manual move required).
    /// </summary>
    public static List<Record> GetBreedingTank(int fishId, int capacity = 3)
    {
        EnsureLoaded();
        var list = new List<Record>(3);

        if (_packs.TryGetValue(fishId, out var p))
        {
            void TryAdd(Record rec)
            {
                if (rec.unixMs == 0) return;
                if (!list.Any(x => x.unixMs == rec.unixMs)) list.Add(rec);
            }

            if (p.hasWeight)  TryAdd(p.weightRecord);
            if (p.hasLength)  TryAdd(p.lengthRecord);
            if (p.hasQuality) TryAdd(p.qualityRecord);
        }

        // cap to capacity (stable)
        if (list.Count > capacity) list = list.Take(capacity).ToList();
        return list;
    }

    public static IReadOnlyList<(int id, float weightKg, float lengthCm, int quality, long unixMs)> GetRecent()
    {
        EnsureLoaded();
        return _recent.Select(r => (r.id, r.weightKg, r.lengthCm, r.quality, r.unixMs)).ToList();
    }

    // (Kept for older UI that expected value-only top lists)
    public static IReadOnlyList<float> TopWeights(int fishId, int k)   => TryGetWeightRecord(fishId, out var r) ? new List<float> { r.weightKg }   : new List<float>();
    public static IReadOnlyList<float> TopLengths(int fishId, int k)   => TryGetLengthRecord(fishId, out var r) ? new List<float> { r.lengthCm }   : new List<float>();
    public static IReadOnlyList<int>   TopQualities(int fishId, int k) => TryGetQualityRecord(fishId, out var r) ? new List<int>   { r.quality } : new List<int>();

    // -------- Save/Load --------

    static void Save()
    {
        try
        {
            var blob = new SaveBlob
            {
                best             = _best.Select(kv => new BestKV       { id = kv.Key, best = kv.Value }).ToList(),
                packs            = _packs.Select(kv => new RecordPackKV { id = kv.Key, pack = kv.Value }).ToList(),
                recent           = _recent.ToList(),
                breedingUnlocked = _breedingUnlocked,
                version          = CurrentVersion
            };
            File.WriteAllText(SavePath, JsonUtility.ToJson(blob, true));
        }
        catch (Exception ex)
        {
            Debug.LogError("[InventoryStatsService] Save failed: " + ex);
        }
    }

    static void Load()
    {
        _best.Clear(); _packs.Clear(); _recent.Clear();
        _breedingUnlocked = false;

        try
        {
            if (!File.Exists(SavePath)) return;
            var json = File.ReadAllText(SavePath);
            if (string.IsNullOrWhiteSpace(json)) return;

            var blob = JsonUtility.FromJson<SaveBlob>(json);
            if (blob == null) return;

            // Version gate: ignore older stats after baseline change
            if (blob.version < CurrentVersion)
            {
                if (DebugLogs)
                {
                    Debug.LogWarning(
                        $"[InventoryStatsService] Stats file version {blob.version} " +
                        $"is older than current {CurrentVersion}; ignoring stored records.");
                }

                // We still keep whatever breeding flag was saved.
                _breedingUnlocked = blob.breedingUnlocked;
                return;
            }

            foreach (var kv in blob.best)   _best[kv.id]   = kv.best;
            foreach (var kv in blob.packs)  _packs[kv.id]  = kv.pack;

            if (blob.recent != null)
            {
                foreach (var r in blob.recent)
                {
                    _recent.Enqueue(r);
                    if (_recent.Count > RecentCapacity) _recent.Dequeue();
                }
            }

            _breedingUnlocked = blob.breedingUnlocked;
        }
        catch (Exception ex)
        {
            Debug.LogError("[InventoryStatsService] Load failed: " + ex);
        }
    }
}
