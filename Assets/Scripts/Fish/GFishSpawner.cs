using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using GalacticFishing.Progress;

namespace GalacticFishing
{
    public sealed class GFishSpawner : MonoBehaviour
    {
        [Header("Data")]
        public FishRegistry registry;
        public GameObject defaultFishPrefab;
        public FishMetaIndex metaIndex; // optional: drives rarity + per-species meta

        [Header("World Binding")]
        [Tooltip("Which world/lake are we in right now? Spawner will follow this.")]
        public WorldManager worldManager;

        [Header("Water")]
        public WaterSurfaceMarker water;

        [Header("Spawn Area (world units)")]
        public Transform areaMin; // bottom-left
        public Transform areaMax; // top-right

        [Header("Spawn Timing")]
        [Min(0.05f)] public float spawnIntervalSeconds = 0.75f;
        [Min(1)] public int burst = 1;

        [Header("Global Cap (Fallback)")]
        [Tooltip("Fallback global cap if the current Lake.maxAlive is 0 or no WorldManager/Lake is available.")]
        [Min(1)] public int maxAlive = 30;

        [Header("Per-Lake MaxAlive (NEW)")]
        [Tooltip("If true, uses Lake.maxAlive (+ per-lake upgrades) when WorldManager is available.")]
        [SerializeField] private bool usePerLakeMaxAlive = true;

        [Tooltip("Workshop upgrade id prefix for per-lake max alive. Final id becomes prefix + lakeId (e.g. max_alive_inc_lake_01).")]
        [SerializeField] private string maxAliveUpgradePrefix = "max_alive_inc_";

        [Header("Rarity Weights (0 disables)")]
        public float wCommon = 60;
        public float wUncommon = 25;
        public float wRare = 10;
        public float wEpic = 4;
        public float wLegendary = 1;
        public float wUberLegendary = 0;
        public float wOneOfAKind = 0;

        [Header("Movement")]
        public Vector2 speedRange = new Vector2(0.6f, 1.8f);
        public Vector2 driftYRange = new Vector2(-0.15f, 0.15f);
        public bool flipToDirection = true;

        [Header("U-Turn Pattern (optional)")]
        [Tooltip("If enabled, some fish will do a hammerhead/U-turn mid-swim.")]
        [SerializeField] private bool uTurnEnabled = true;

        [Range(0f, 1f)]
        [SerializeField] private float uTurnChance = 0.18f;
        [Tooltip("Multiplier applied to fish speed ONLY while performing the U-turn phases (climb/vertical/return-diagonal). 1 = no change.")]
        [SerializeField] private float uTurnSpeedMultiplier = 2f;

        [Tooltip("How far (world units) the fish travels on the initial 45° climb before going vertical.")]
        [SerializeField] private Vector2 uTurnDiagonalDistanceRange = new Vector2(0.75f, 2.5f);

        [Tooltip("How far (world units) the fish travels straight up during the 'vertical' phase.")]
        [SerializeField] private float uTurnVerticalDistance = 0.35f;

        [Tooltip("Padding from edges when selecting the random trigger X (world units).")]
        [SerializeField] private float uTurnTriggerEdgePadding = 1.0f;
        

        // ---------------- NEW: S-Turn settings ----------------
        [Header("S-Turn Pattern (optional)")]
        [Tooltip("Chance (0..1) that a fish WHICH ALREADY rolled a U-turn will also roll an S-turn later (during StraightReturn). Total S-turn chance = uTurnChance * this.")]
        [Range(0f, 1f)]
        [SerializeField] private float sTurnChanceAfterUTurn = 0.35f;

        [Tooltip("Minimum X separation (world units) between the U-turn triggerX and the S-turn triggerX2.")]
        [SerializeField] private float sTurnTriggerMinSeparation = 1.0f;
        // ------------------------------------------------------

        [Header("Rendering")]
        public string sortingLayer = "Characters";
        public int orderInLayer = 5;

        float _t;

        [Header("Lifetime / Despawn")]
        [Tooltip("World-units margin outside the spawn rect before a fish is culled.")]
        public float despawnMargin = 2f;
        [Tooltip("Extra headroom above the water surface before a fish is culled (prevents surface-clamp conflicts).")]
public float surfaceDespawnHeadroom = 10f;
        [Tooltip("If true, refills to maxAlive instantly after any despawn (subject to per-species cap).")]
        public bool respawnImmediately = true;

        [Header("Lake Dynamics")]
        [Tooltip("Max simultaneous instances of a given species in this lake. 0 = unlimited.")]
        [SerializeField] private int maxAlivePerSpecies = 1;

        [Tooltip("Seconds to wait after any fish disappears (caught/missed/despawned) before allowing new spawns.")]
        [SerializeField] private float respawnDelaySeconds = 8f;

        [Header("Debug")]
        [Tooltip("Enable to see what the spawner + rod-power filter are doing.")]
        public bool logs = false;

        // --- runtime state for respawn delay ---
        int _lastFishCount;
        float _lastDespawnTime = float.NegativeInfinity;

        // When we clear via resetter, we DON'T want that to trigger respawn delay on the next frame
        bool _ignoreNextDespawnDrop;

        // When we clear, we can request an instant refill next Update (even if respawnImmediately is off)
        bool _forceRefillNextUpdate;

        // Public getters so Lake Info UI can read current rules
        public int MaxAlivePerSpecies => maxAlivePerSpecies;
        public float RespawnDelaySeconds => respawnDelaySeconds;

        /// <summary>
        /// The computed global cap for the CURRENT lake, including upgrades.
        /// Safe to call from UI.
        /// </summary>
        public int CurrentMaxAlive
        {
            get { return ComputeFinalMaxAlive(); }
        }

        /// <summary>
        /// IMPORTANT:
        /// Call this when switching world/lake to remove old fish.
        /// This will ONLY destroy spawned fish (objects with GFishSpawnTag),
        /// so it will NOT delete SpawnMin/SpawnMax or other helper children.
        /// </summary>
        public void ClearAllSpawnedFish(bool resetRespawnDelay = true, bool refillImmediately = true)
        {
            int killed = 0;

            // Destroy ONLY tagged spawned fish, never other children (SpawnMin/SpawnMax, helpers, etc)
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (!child) continue;

                var tag = child.GetComponent<GFishSpawnTag>();
                if (!tag) continue;

                Destroy(child.gameObject);
                killed++;
            }

            // Reset internal timers so spawning doesn't get stuck behind respawnDelaySeconds
            if (resetRespawnDelay)
                _lastDespawnTime = float.NegativeInfinity;

            _t = 0f;

            // Prevent the "drop" caused by our clear from starting a respawn delay
            _ignoreNextDespawnDrop = true;

            // Request an instant refill next Update (safe timing, avoids ordering issues)
            _forceRefillNextUpdate = refillImmediately;

            if (logs)
                Debug.Log($"[GFishSpawner] ClearAllSpawnedFish(): destroyed {killed} spawned fish (tagged only).", this);
        }

        void OnDrawGizmosSelected()
        {
            var rect = GetArea();
            Gizmos.color = new Color(0, 1, 0, 0.5f);
            Gizmos.DrawWireCube(rect.center, rect.size);
        }

        void Update()
        {
            if (!registry || registry.fishes == null || registry.fishes.Count == 0) return;
            if (!defaultFishPrefab && !registry.fishes.Any(f => f && f.prefab)) return;
            if (!areaMin || !areaMax) return;

            int finalMaxAlive = ComputeFinalMaxAlive();

            // Count ONLY spawned fish (tagged), not helper children
            int fishCount = GetAliveFishCount();

            // Detect despawns (caught/missed/offscreen) by watching fish count drop
            if (fishCount < _lastFishCount)
            {
                if (_ignoreNextDespawnDrop)
                {
                    _ignoreNextDespawnDrop = false;
                    if (logs)
                        Debug.Log($"[GFishSpawner] Fish count dropped {_lastFishCount}->{fishCount} (ignored: clear/reset).", this);
                }
                else
                {
                    _lastDespawnTime = Time.time;
                    if (logs)
                        Debug.Log($"[GFishSpawner] Fish count dropped {_lastFishCount}->{fishCount}, respawn delay {respawnDelaySeconds:0.0}s", this);
                }
            }
            _lastFishCount = fishCount;

            ReapOffscreen();

            // Recompute fishCount after reap (Destroy completes end-of-frame, but this keeps logic consistent)
            fishCount = GetAliveFishCount();

            // Global cap (fish only)
            if (fishCount >= finalMaxAlive)
                return;

            // Global respawn delay (unless we are forcing an instant refill)
            if (!_forceRefillNextUpdate && respawnDelaySeconds > 0f && _lastDespawnTime > float.NegativeInfinity)
            {
                float since = Time.time - _lastDespawnTime;
                if (since < respawnDelaySeconds)
                    return;
            }

            // Instant fill if enabled OR requested by ClearAllSpawnedFish
            if (respawnImmediately || _forceRefillNextUpdate)
            {
                _forceRefillNextUpdate = false;

                int safety = 0;
                int target = finalMaxAlive;

                while (GetAliveFishCount() < target && safety++ < target * 5)
                {
                    var fish = PickSpeciesByWeight();
                    if (fish == null) break;

                    SpawnOne(fish);
                }

                _lastFishCount = GetAliveFishCount();
                return;
            }

            // Timed spawning
            _t += Time.deltaTime;
            if (_t < spawnIntervalSeconds) return;
            _t = 0f;

            fishCount = GetAliveFishCount();
            if (fishCount >= finalMaxAlive) return;

            for (int i = 0; i < burst; i++)
            {
                var fish = PickSpeciesByWeight();
                if (fish == null) break;

                SpawnOne(fish);

                fishCount = GetAliveFishCount();
                if (fishCount >= finalMaxAlive)
                    break;
            }

            _lastFishCount = GetAliveFishCount();
        }

        int ComputeFinalMaxAlive()
        {
            // Default fallback: the spawner's own inspector value.
            int fallback = Mathf.Max(1, maxAlive);

            if (!usePerLakeMaxAlive)
                return fallback;

            if (!worldManager || worldManager.world == null)
                return fallback;

            var lake = worldManager.GetLake(worldManager.lakeIndex);
            if (lake == null)
                return fallback;

            // Base from lake (0 means "not configured yet", so use fallback to avoid breaking old assets)
            int baseMax = lake.maxAlive > 0 ? lake.maxAlive : fallback;

            // Bonus from per-lake workshop upgrade levels
            int bonus = 0;
            try
            {
                var ppm = PlayerProgressManager.Instance;
                if (ppm != null && !string.IsNullOrWhiteSpace(lake.lakeId))
                {
                    string upgradeId = (maxAliveUpgradePrefix ?? "max_alive_inc_") + lake.lakeId;
                    bonus = Mathf.Max(0, ppm.GetWorkshopUpgradeLevel(upgradeId));
                }
            }
            catch
            {
                bonus = 0;
            }

            return Mathf.Max(1, baseMax + bonus);
        }

        int GetAliveFishCount()
        {
            int count = 0;
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (!child) continue;
                if (child.GetComponent<GFishSpawnTag>() != null)
                    count++;
            }
            return count;
        }

        // ------------------------------------------------------------
        // WORLD-AWARE SPECIES PICKING
        // ------------------------------------------------------------

        Fish PickSpeciesByWeight()
        {
            // 1) Try to respect the current world + lake via WorldManager
            var worldWeights = BuildWorldWeightedSpecies();
            if (worldWeights != null && worldWeights.Count > 0)
            {
                // Apply rod-power filtering for world-bound pools only.
                var filtered = FilterWorldByRodPower(worldWeights);

                if (filtered != null)
                {
                    if (filtered.Count > 0)
                    {
                        if (logs)
                            Debug.Log(
                                $"[GFishSpawner] Using {filtered.Count}/{worldWeights.Count} world species " +
                                $"after rod-power filter (rod={GetCurrentRodPower():0}).");

                        return PickFromWeightedDict(filtered);
                    }

                    if (logs)
                        Debug.Log(
                            $"[GFishSpawner] Rod-power filter removed all {worldWeights.Count} world species " +
                            $"for rod={GetCurrentRodPower():0}. No spawn this tick.");

                    return null;
                }

                // filter not applied
                return PickFromWeightedDict(worldWeights);
            }

            // 2) Fallback: old behaviour – use ALL registry fishes filtered by rarity
            var weights = new Dictionary<FishRarity, float>
            {
                { FishRarity.Common,        Mathf.Max(0, wCommon) },
                { FishRarity.Uncommon,      Mathf.Max(0, wUncommon) },
                { FishRarity.Rare,          Mathf.Max(0, wRare) },
                { FishRarity.Epic,          Mathf.Max(0, wEpic) },
                { FishRarity.Legendary,     Mathf.Max(0, wLegendary) },
                { FishRarity.UberLegendary, Mathf.Max(0, wUberLegendary) },
                { FishRarity.OneOfAKind,    Mathf.Max(0, wOneOfAKind) },
            };

            var groups = registry.fishes
                .Where(f =>
                {
                    if (f == null) return false;
                    var rarity = ResolveRarity(f);
                    return weights.TryGetValue(rarity, out var w) && w > 0f;
                })
                .GroupBy(f => ResolveRarity(f))
                .ToList();

            if (groups.Count == 0) return null;

            float totalFallback = groups.Sum(g => weights[g.Key]);
            float r = UnityEngine.Random.value * totalFallback;
            FishRarity chosen = groups[0].Key;
            foreach (var g in groups)
            {
                r -= weights[g.Key];
                if (r <= 0f) { chosen = g.Key; break; }
            }

            var bucket = groups.First(g => g.Key == chosen).ToList();
            return bucket[UnityEngine.Random.Range(0, bucket.Count)];
        }

        Fish PickFromWeightedDict(Dictionary<Fish, float> dict)
        {
            if (dict == null || dict.Count == 0)
                return null;

            // Enforce per-species concurrent cap by excluding species already at cap.
            var eligible = new Dictionary<Fish, float>();
            foreach (var kvp in dict)
            {
                var species = kvp.Key;
                if (!species) continue;

                if (maxAlivePerSpecies > 0 && IsSpeciesAtCap(species))
                    continue;

                float w = kvp.Value;
                if (w <= 0f) continue;

                eligible[species] = w;
            }

            if (eligible.Count == 0)
            {
                if (logs)
                    Debug.Log("[GFishSpawner] All candidate species are at per-species cap; no spawn this tick.");
                return null;
            }

            float total = 0f;
            foreach (var kvp in eligible)
                total += kvp.Value;

            if (total <= 0f)
                return null;

            float roll = UnityEngine.Random.value * total;
            foreach (var kvp in eligible)
            {
                roll -= kvp.Value;
                if (roll <= 0f)
                    return kvp.Key;
            }

            foreach (var kvp in eligible)
                return kvp.Key;

            return null;
        }

        Dictionary<Fish, float> FilterWorldByRodPower(Dictionary<Fish, float> source)
        {
            if (source == null || source.Count == 0)
                return null;

            float rodPower = GetCurrentRodPower();
            if (rodPower <= 0f || metaIndex == null)
                return null; // cannot filter – treat as "off"

            var filtered = new Dictionary<Fish, float>();
            foreach (var kvp in source)
            {
                var species = kvp.Key;
                if (!species) continue;

                float fishPower = GetFishPower(species);
                if (fishPower <= rodPower)
                    filtered[species] = kvp.Value;
            }

            return filtered;
        }

        float GetCurrentRodPower()
        {
            try
            {
                var mgr = PlayerProgressManager.Instance;
                return mgr != null ? mgr.CurrentRodPower : 0f;
            }
            catch
            {
                return 0f;
            }
        }

        float GetFishPower(Fish species)
        {
            if (!species || metaIndex == null)
                return 0f;

            var meta = metaIndex.FindByFish(species);
            if (!meta) return 0f;

            try
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                var t = meta.GetType();

                var field = t.GetField("Power", flags) ?? t.GetField("power", flags);
                if (field != null)
                {
                    var raw = field.GetValue(meta);
                    if (raw is int i) return i;
                    if (raw is float f) return f;
                    return Convert.ToSingle(raw);
                }

                var prop = t.GetProperty("Power", flags) ?? t.GetProperty("power", flags);
                if (prop != null && prop.CanRead)
                {
                    var raw = prop.GetValue(meta, null);
                    if (raw is int i) return i;
                    if (raw is float f) return f;
                    return Convert.ToSingle(raw);
                }
            }
            catch { }

            return 0f;
        }

        Dictionary<Fish, float> BuildWorldWeightedSpecies()
        {
            var result = new Dictionary<Fish, float>();

            if (!worldManager) return result;

            var pool = worldManager.GetActivePool();
            if (pool == null || pool.Count == 0) return result;

            foreach (var fw in pool)
            {
                if (!fw.fish) continue;

                var species = ResolveSpeciesFromScriptable(fw.fish);
                if (!species) continue;

                var rarity = ResolveRarity(species);
                float rarityWeight = GetRarityBaseWeight(rarity);
                if (rarityWeight <= 0f) continue;

                float poolWeight = Mathf.Max(1, fw.weight); // 0 == "use default"
                float totalWeight = rarityWeight * poolWeight;

                if (result.TryGetValue(species, out var existing))
                    result[species] = existing + totalWeight;
                else
                    result[species] = totalWeight;
            }

            return result;
        }

        float GetRarityBaseWeight(FishRarity rarity)
        {
            switch (rarity)
            {
                case FishRarity.Common:        return Mathf.Max(0f, wCommon);
                case FishRarity.Uncommon:      return Mathf.Max(0f, wUncommon);
                case FishRarity.Rare:          return Mathf.Max(0f, wRare);
                case FishRarity.Epic:          return Mathf.Max(0f, wEpic);
                case FishRarity.Legendary:     return Mathf.Max(0f, wLegendary);
                case FishRarity.UberLegendary: return Mathf.Max(0f, wUberLegendary);
                case FishRarity.OneOfAKind:    return Mathf.Max(0f, wOneOfAKind);
                default:                       return 0f;
            }
        }

        Fish ResolveSpeciesFromScriptable(ScriptableObject obj)
        {
            if (!obj) return null;

            if (obj is Fish directFish)
                return directFish;

            if (obj is FishMeta fm && metaIndex != null)
            {
                var mapped = metaIndex.FindFishByMeta(fm);
                if (mapped) return mapped;
            }

            var type = obj.GetType();
            const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            foreach (var field in type.GetFields(BF))
            {
                if (!typeof(Fish).IsAssignableFrom(field.FieldType)) continue;
                var value = field.GetValue(obj) as Fish;
                if (value) return value;
            }

            foreach (var prop in type.GetProperties(BF))
            {
                if (!prop.CanRead) continue;
                if (!typeof(Fish).IsAssignableFrom(prop.PropertyType)) continue;
                try
                {
                    var value = prop.GetValue(obj, null) as Fish;
                    if (value) return value;
                }
                catch { }
            }

            return null;
        }

        FishRarity ResolveRarity(Fish species)
        {
            if (metaIndex != null)
            {
                var meta = metaIndex.FindByFish(species);
                if (meta != null) return meta.rarity;
            }
            return species != null ? species.rarity : FishRarity.Common;
        }

        bool IsSpeciesAtCap(Fish species)
        {
            if (maxAlivePerSpecies <= 0 || !species)
                return false;

            int count = 0;
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (!child) continue;

                var tag = child.GetComponent<GFishSpawnTag>();
                if (tag && tag.species == species)
                    count++;
            }

            return count >= maxAlivePerSpecies;
        }

        void SpawnOne(Fish species)
        {
            var rect = GetArea();

            // 1) Decide direction FIRST (so spawn position can depend on it)
            float dirX = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            float dirY = UnityEngine.Random.Range(driftYRange.x, driftYRange.y);
            Vector2 dir = new Vector2(dirX, dirY).normalized;

            // 2) Midpoint of the horizontal spawn area
            float midX = rect.xMin + (rect.width * 0.5f);

            // 3) Constrain spawn X based on direction:
            //    - swimming RIGHT => spawn in LEFT half
            //    - swimming LEFT  => spawn in RIGHT half
            float x = (dir.x > 0f)
                ? UnityEngine.Random.Range(rect.xMin, midX)
                : UnityEngine.Random.Range(midX, rect.xMax);

            // Y stays as before
            float y = UnityEngine.Random.Range(rect.yMin, rect.yMax);

            float surf = water ? water.SurfaceY : 0f;
            float pad = Mathf.Max(0.01f, species.surfacePadding);
            y = Mathf.Min(y, surf - pad);

            GameObject prefab = species.prefab ? species.prefab : defaultFishPrefab;
            if (!prefab) return;

            var go = Instantiate(prefab, new Vector3(x, y, 0f), Quaternion.identity, transform);

            var tag = go.GetComponent<GFishSpawnTag>() ?? go.AddComponent<GFishSpawnTag>();
            tag.species = species;

            FishMeta meta = null;
            if (metaIndex != null)
                meta = metaIndex.FindByFish(species);

            var fid = go.GetComponent<FishIdentity>();
            string instanceName = GetSpeciesName(species);
            if (fid)
            {
                fid.SetDisplayName(instanceName);

                if (meta != null)
                {
                    if (!TrySetPropOrField(fid, "meta", meta))
                        TrySetPropOrField(fid, "Meta", meta);

                    // copy per-species bullseye threshold from meta to the spawned instance
                    TryCopyBullseyeThresholdFromMeta(fid, meta);
                }
            }
            else
            {
                go.name = instanceName;
            }

            if (meta != null)
            {
                var blur = go.GetComponent<FishBlurController>();
                if (blur != null)
                    blur.InitFromMeta(meta);
            }

            var sr = go.GetComponentInChildren<SpriteRenderer>();
            if (sr)
            {
                if (!species.prefab && species.sprite) sr.sprite = species.sprite;
                sr.sortingLayerName = sortingLayer;
                sr.sortingOrder = orderInLayer;
            }

            float speed = UnityEngine.Random.Range(speedRange.x, speedRange.y);

            float meters = FishSizing.DrawLogNormalSizeMeters(species.baselineMeters, species.sigmaLogSize);
            float densityK = FishSizing.DrawLogNormalDensityK(species.baselineDensityK, species.sigmaLogDensity);

            float scale = FishSizing.ComputeScaleFromSizeMeters(species, sr, meters);
            FishSizing.ApplyTransformScale(go.transform, scale);

            
                        var ctrl = go.GetComponent<FishController>();
            if (!ctrl) ctrl = go.AddComponent<FishController>();
            ctrl.spriteRenderer = sr;
            ctrl.water = water;
            ctrl.direction = dir; // <-- use the direction we rolled FIRST
            ctrl.speed = speed;
            ctrl.surfacePadding = species.surfacePadding;
            ctrl.Initialize(species, meters);

            // Decide turn up/down based on the *actual* vertical spawn band (clamped to water surface)
            float spawnMaxY = rect.yMax;
            if (water)
            {
                float surfY = water.SurfaceY;
                float padY = Mathf.Max(0.01f, species.surfacePadding);
                spawnMaxY = Mathf.Min(spawnMaxY, surfY - padY);
            }
            float decisionMidY = rect.yMin + (spawnMaxY - rect.yMin) * 0.5f;
            ctrl.ConfigureTurnDecisionMidY(decisionMidY);


            // Optional U-turn “stunt pilot” brain
            ConfigureUTurnIfRolled(ctrl, rect, x, dir.x);

            if (flipToDirection && sr) sr.flipX = (dir.x > 0f) && species.allowFlipX;

            var lenRt = go.GetComponent<FishLengthRuntime>() ?? go.AddComponent<FishLengthRuntime>();
            lenRt.SetFromMeters(meters);

            var wtRt = go.GetComponent<FishWeightRuntime>() ?? go.AddComponent<FishWeightRuntime>();
            wtRt.SetFromMeters(meters, densityK);

            TrySetRuntimeStats(go, lenRt.ValueCm, wtRt.ValueKg);
        }

        private void ConfigureUTurnIfRolled(FishController ctrl, Rect rect, float spawnX, float dirX)
        {
            if (!uTurnEnabled) return;
            if (ctrl == null) return;
            if (uTurnChance <= 0f) return;

            if (UnityEngine.Random.value > Mathf.Clamp01(uTurnChance))
                return;

            // Choose a trigger X that lies ahead of the fish (so it can actually hit it)
            float triggerX = ChooseUTurnTriggerX(rect, spawnX, dirX);
            if (float.IsNaN(triggerX))
                return;

            float diagDist = 1.92f;
            float vertDist = Mathf.Max(0.01f, uTurnVerticalDistance);

            // Return diagonal uses the same distance as the first climb
            ctrl.ConfigureUTurn(true, triggerX, diagDist, vertDist, diagDist, uTurnSpeedMultiplier);
            

            // NEW: roll optional S-turn only if U-turn was rolled
            if (sTurnChanceAfterUTurn > 0f && UnityEngine.Random.value <= Mathf.Clamp01(sTurnChanceAfterUTurn))
            {
                float triggerX2 = ChooseSTurnTriggerX(rect, triggerX, dirX);
                if (!float.IsNaN(triggerX2))
                {
                    ctrl.ConfigureSTurn(true, triggerX2);

                    if (logs)
                        Debug.Log($"[GFishSpawner] S-Turn enabled: triggerX2={triggerX2:0.###} (minSep={sTurnTriggerMinSeparation:0.###})", this);
                }
            }

            if (logs)
                Debug.Log($"[GFishSpawner] U-Turn enabled: triggerX={triggerX:0.###}, diag={diagDist:0.###}, vert={vertDist:0.###}", this);
        }

        private float ChooseUTurnTriggerX(Rect rect, float spawnX, float dirX)
        {
            float midX = rect.xMin + rect.width * 0.5f;
            float pad = Mathf.Max(0f, uTurnTriggerEdgePadding);

            // Moving right: pick trigger somewhere in the RIGHT half, ahead of spawn
            if (dirX > 0f)
            {
                float min = Mathf.Max(midX, spawnX + 0.25f);
                float max = rect.xMax - pad;
                if (max <= min) return float.NaN;
                return UnityEngine.Random.Range(min, max);
            }
            else
            {
                // Moving left: pick trigger somewhere in the LEFT half, ahead of spawn
                float min = rect.xMin + pad;
                float max = Mathf.Min(midX, spawnX - 0.25f);
                if (max <= min) return float.NaN;
                return UnityEngine.Random.Range(min, max);
            }
        }

        // NEW helper: choose an S-turn triggerX2 that will be crossed during StraightReturn,
        // with at least sTurnTriggerMinSeparation distance from triggerX.
        private float ChooseSTurnTriggerX(Rect rect, float triggerX, float dirX)
        {
            float pad = Mathf.Max(0f, uTurnTriggerEdgePadding);
            float minSep = Mathf.Max(0f, sTurnTriggerMinSeparation);

            // If original was moving RIGHT, StraightReturn moves LEFT, so X decreases:
            // choose triggerX2 somewhere LEFT of triggerX by at least minSep.
            if (dirX > 0f)
            {
                float min = rect.xMin + pad;
                float max = triggerX - minSep;
                if (max <= min) return float.NaN;
                return UnityEngine.Random.Range(min, max);
            }
            else
            {
                // If original was moving LEFT, StraightReturn moves RIGHT, so X increases:
                // choose triggerX2 somewhere RIGHT of triggerX by at least minSep.
                float min = triggerX + minSep;
                float max = rect.xMax - pad;
                if (max <= min) return float.NaN;
                return UnityEngine.Random.Range(min, max);
            }
        }

        // ✅ NEW helper: reads BullseyeThreshold from meta (field/property) and assigns it to FishIdentity.bullseyeThreshold
        static void TryCopyBullseyeThresholdFromMeta(FishIdentity fid, FishMeta meta)
        {
            if (!fid || meta == null) return;

            try
            {
                const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                var t = meta.GetType();

                // Prefer exact names first
                var f = t.GetField("BullseyeThreshold", BF) ?? t.GetField("bullseyeThreshold", BF);
                if (f != null)
                {
                    var raw = f.GetValue(meta);
                    fid.bullseyeThreshold = Convert.ToSingle(raw);
                    return;
                }

                var p = t.GetProperty("BullseyeThreshold", BF) ?? t.GetProperty("bullseyeThreshold", BF);
                if (p != null && p.CanRead && p.GetIndexParameters().Length == 0)
                {
                    var raw = p.GetValue(meta, null);
                    fid.bullseyeThreshold = Convert.ToSingle(raw);
                    return;
                }

                // As a last resort: accept some common variants
                f = t.GetField("bullseye", BF) ?? t.GetField("Bullseye", BF);
                if (f != null)
                {
                    var raw = f.GetValue(meta);
                    fid.bullseyeThreshold = Convert.ToSingle(raw);
                    return;
                }

                p = t.GetProperty("bullseye", BF) ?? t.GetProperty("Bullseye", BF);
                if (p != null && p.CanRead && p.GetIndexParameters().Length == 0)
                {
                    var raw = p.GetValue(meta, null);
                    fid.bullseyeThreshold = Convert.ToSingle(raw);
                    return;
                }
            }
            catch
            {
                // Swallow to avoid breaking spawns if meta doesn't have the member or conversion fails.
            }
        }

        Rect GetArea()
        {
            if (!areaMin || !areaMax) return new Rect(0, 0, 0, 0);
            Vector2 a = areaMin.position, b = areaMax.position;
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        static string GetSpeciesName(Fish species)
        {
            if (species == null) return "Fish";

            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var type = species.GetType();

            string value = null;

            var field = type.GetField("displayName", flags);
            if (field != null)
                value = field.GetValue(species) as string;

            if (string.IsNullOrWhiteSpace(value))
            {
                var prop = type.GetProperty("DisplayName", flags) ?? type.GetProperty("displayName", flags);
                if (prop != null && prop.CanRead)
                    value = prop.GetValue(species) as string;
            }

            if (string.IsNullOrWhiteSpace(value))
                value = species.name;

            return value;
        }

        void ReapOffscreen()
{
    var rect = GetArea();
    rect.xMin -= despawnMargin;
    rect.xMax += despawnMargin;
    rect.yMin -= despawnMargin;
    rect.yMax += despawnMargin;

    // Use a forgiving ceiling above the water so fish that get clamped to the surface
    // (or overshoot for 1 frame due to script order) don't get destroyed.
    float upperY = rect.yMax;
    if (water)
    {
        float headroom = Mathf.Max(despawnMargin, surfaceDespawnHeadroom);
        upperY = Mathf.Max(upperY, water.SurfaceY + headroom);
    }

    var toKill = new List<GameObject>();

    for (int i = 0; i < transform.childCount; i++)
    {
        var child = transform.GetChild(i);
        if (!child) continue;

        if (child.GetComponent<GFishSpawnTag>() == null)
            continue;

        var p = child.position;

        if (p.x < rect.xMin || p.x > rect.xMax || p.y < rect.yMin || p.y > upperY)
            toKill.Add(child.gameObject);
    }

    for (int i = 0; i < toKill.Count; i++)
        Destroy(toKill[i]);
}

        // ------------------------------------------------------------
        // NEXT UNLOCK POWER (PUBLIC API FOR UI)
        // ------------------------------------------------------------

        public int GetCurrentLakeCandidateSpeciesCount()
        {
            var worldWeights = BuildWorldWeightedSpecies();
            return worldWeights != null ? worldWeights.Count : 0;
        }

        public bool TryGetNextUnlockPower(out float nextPower)
        {
            nextPower = 0f;

            if (!worldManager)
                return false;

            var worldWeights = BuildWorldWeightedSpecies();
            if (worldWeights == null || worldWeights.Count == 0)
                return false;

            float rodPower = GetCurrentRodPower();

            bool found = false;
            float best = float.PositiveInfinity;

            foreach (var kvp in worldWeights)
            {
                var species = kvp.Key;
                if (!species) continue;

                float fishPower = GetFishPower(species);
                if (fishPower <= 0f) continue;

                if (fishPower > rodPower && fishPower < best)
                {
                    best = fishPower;
                    found = true;
                }
            }

            if (!found)
                return false;

            nextPower = best;
            return true;
        }

        // ------------------------------------------------------------
        // RUNTIME STAT WRITER (robust against different component APIs)
        // ------------------------------------------------------------
        static void TrySetRuntimeStats(GameObject go, float lengthCm, float weightKg)
        {
            if (!go) return;

            var fi = FindComp(go, "FishInstance");
            if (fi != null)
            {
                if (!TryInvoke(fi, "SetLengthCm", lengthCm))
                {
                    if (!TrySetPropOrField(fi, "LengthCm", lengthCm))
                        TrySetPropOrField(fi, "lengthCm", lengthCm);
                }
                TrySetPropOrField(fi, "HasLength", true);

                if (!TryInvoke(fi, "SetWeightKg", weightKg))
                {
                    if (!TrySetPropOrField(fi, "WeightKg", weightKg))
                        TrySetPropOrField(fi, "weightKg", weightKg);
                }
                TrySetPropOrField(fi, "HasWeight", true);
            }

            var lenRt = FindComp(go, "FishLengthRuntime");
            if (lenRt != null)
            {
                TrySetPropOrField(lenRt, "Value", lengthCm);
                TrySetPropOrField(lenRt, "HasValue", true);
            }

            var wtRt = FindComp(go, "FishWeightRuntime");
            if (wtRt != null)
            {
                TrySetPropOrField(wtRt, "Value", weightKg);
                TrySetPropOrField(wtRt, "HasValue", true);
            }

            var id = FindComp(go, "FishIdentity");
            if (id != null)
            {
                TrySetPropOrField(id, "LengthCm", lengthCm);
                TrySetPropOrField(id, "WeightKg", weightKg);
            }
        }

        static Component FindComp(GameObject go, string typeNameContains)
        {
            return go.GetComponentsInChildren<Component>(true)
                     .FirstOrDefault(c => c && c.GetType().Name.IndexOf(typeNameContains, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        static bool TrySetPropOrField(object obj, string name, object value)
        {
            if (obj == null) return false;
            var t = obj.GetType();

            var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (p != null && p.CanWrite)
            {
                try { p.SetValue(obj, ConvertTo(value, p.PropertyType)); return true; } catch { }
            }

            var f = t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null && !f.IsInitOnly)
            {
                try { f.SetValue(obj, ConvertTo(value, f.FieldType)); return true; } catch { }
            }

            return false;
        }

        static bool TryInvoke(object obj, string method, params object[] args)
        {
            if (obj == null) return false;
            var t = obj.GetType();
            var m = t.GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (m == null) return false;
            try { m.Invoke(obj, args); return true; } catch { return false; }
        }

        static object ConvertTo(object val, Type target)
        {
            if (val == null) return null;
            if (target.IsInstanceOfType(val)) return val;
            try { return System.Convert.ChangeType(val, target); } catch { return val; }
        }
    }

    /// <summary>
    /// Lightweight runtime tag so the spawner can track which Fish species
    /// each spawned instance belongs to (for per-species caps).
    /// </summary>
    public sealed class GFishSpawnTag : MonoBehaviour
    {
        public Fish species;
    }
}
