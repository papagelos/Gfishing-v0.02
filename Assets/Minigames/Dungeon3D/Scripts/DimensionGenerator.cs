using System;
using System.Collections.Generic;
using GalacticFishing.Minigames.HexWorld;
using UnityEngine;

namespace GalacticFishing.Minigames.Dungeon3D
{
    public sealed class DimensionGenerator : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private DimensionGenProfile profile;
        [SerializeField] private PropRegistry registry;
        [SerializeField, Min(1)] private int floorIndex = 1;
        [SerializeField] private List<DungeonResourceDefinition> resourceDefinitions = new();
        [SerializeField] private bool useFixedSeed = true;
        [SerializeField] private int fixedSeed = 1337;
        [SerializeField] private bool useHandpaintedMap = false;
        [SerializeField] private TextAsset mapJson;

        [Header("Output")]
        [SerializeField] private DimensionLayout latestLayout = new DimensionLayout();

        [Header("Gizmos")]
        [SerializeField] private bool drawGizmos = true;
        [SerializeField, Min(0.1f)] private float gizmoHexSize = 0.8f;
        [SerializeField] private float gizmoY = 0.05f;
        [SerializeField] private Color spineTileColor = new Color(0.98f, 0.62f, 0.20f, 0.90f);
        [SerializeField] private Color pocketTileColor = new Color(0.15f, 0.80f, 0.95f, 0.80f);
        [SerializeField] private Color fillerTileColor = new Color(0.40f, 0.40f, 0.40f, 0.30f);
        [SerializeField] private Color spinePathColor = new Color(1.00f, 0.75f, 0.20f, 1.00f);
        [SerializeField] private Color startColor = Color.green;
        [SerializeField] private Color bossColor = Color.red;

        public DimensionLayout Layout => latestLayout;
        public DimensionGenProfile Profile => profile;
        public event Action<DimensionLayout> OnGenerated;

        public bool TryGetResourceDefinitionForPropId(string propId, out DungeonResourceDefinition definition)
        {
            definition = null;
            if (resourceDefinitions == null || resourceDefinitions.Count == 0 || string.IsNullOrWhiteSpace(propId))
                return false;

            string target = NormalizeId(propId);
            string targetNoPrefix = StripResourcePrefix(target);

            for (int i = 0; i < resourceDefinitions.Count; i++)
            {
                DungeonResourceDefinition def = resourceDefinitions[i];
                if (def == null || string.IsNullOrWhiteSpace(def.resourceId))
                    continue;

                string candidate = NormalizeId(def.resourceId);
                if (string.Equals(candidate, target, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(StripResourcePrefix(candidate), targetNoPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    definition = def;
                    return true;
                }
            }

            return false;
        }

        private void OnValidate()
        {
            floorIndex = Mathf.Max(1, floorIndex);
        }

        [ContextMenu("Regenerate")]
        public void Regenerate()
        {
            if (profile == null)
            {
                Debug.LogError($"[{nameof(DimensionGenerator)}] Missing {nameof(DimensionGenProfile)} reference.", this);
                return;
            }

            EnsureRegistryReference();

            int seed = useFixedSeed ? fixedSeed : Environment.TickCount;
            latestLayout = GenerateWithRetries(
                seed,
                profile,
                registry,
                floorIndex,
                resourceDefinitions,
                useHandpaintedMap,
                mapJson);

            Debug.Log(
                $"[{nameof(DimensionGenerator)}] Seed={latestLayout.seedUsed} " +
                $"Tiles={latestLayout.WalkableCount} Spine={latestLayout.spineCoords.Count} " +
                $"Pockets={latestLayout.pocketCoords.Count} Reachable={latestLayout.bossReachable} Floor={floorIndex}",
                this);

            OnGenerated?.Invoke(latestLayout);
        }

        private static DimensionLayout GenerateWithRetries(
            int seed,
            DimensionGenProfile genProfile,
            PropRegistry propRegistry,
            int floorIndex,
            List<DungeonResourceDefinition> resourceDefinitions,
            bool useHandpaintedMap,
            TextAsset mapJson)
        {
            const int MaxAttempts = 4;
            DimensionLayout best = null;

            for (int i = 0; i < MaxAttempts; i++)
            {
                int attemptSeed = seed + i * 7919;
                var attempt = GenerateOnce(
                    attemptSeed,
                    genProfile,
                    propRegistry,
                    floorIndex,
                    resourceDefinitions,
                    useHandpaintedMap,
                    mapJson);
                if (best == null || attempt.WalkableCount > best.WalkableCount)
                    best = attempt;

                if (attempt.bossReachable)
                    return attempt;
            }

            Debug.LogWarning($"[{nameof(DimensionGenerator)}] Generated layout failed connectivity after retries.");
            return best ?? new DimensionLayout();
        }

        private static DimensionLayout GenerateOnce(
            int seed,
            DimensionGenProfile genProfile,
            PropRegistry propRegistry,
            int floorIndex,
            List<DungeonResourceDefinition> resourceDefinitions,
            bool useHandpaintedMap,
            TextAsset mapJson)
        {
            if (useHandpaintedMap && mapJson != null)
            {
                DimensionLayout loaded = JsonUtility.FromJson<DimensionLayout>(mapJson.text);
                if (loaded == null)
                    return new DimensionLayout();

                if (loaded.tiles == null)
                    loaded.tiles = new List<DimensionTileData>();
                if (loaded.spineCoords == null)
                    loaded.spineCoords = new List<HexCoord>();
                if (loaded.pocketCoords == null)
                    loaded.pocketCoords = new List<HexCoord>();
                if (loaded.packSeeds == null)
                    loaded.packSeeds = new List<PackSeedSpawn>();

                bool foundStartMarker = false;
                bool foundBossMarker = false;

                for (int i = 0; i < loaded.tiles.Count; i++)
                {
                    DimensionTileData tile = loaded.tiles[i];
                    if (tile.propIds == null)
                        tile.propIds = new List<string>();

                    bool hasStart = false;
                    bool hasBoss = false;
                    for (int p = tile.propIds.Count - 1; p >= 0; p--)
                    {
                        string id = tile.propIds[p];
                        if (string.IsNullOrWhiteSpace(id))
                            continue;

                        string trimmed = id.Trim();
                        bool isStart = string.Equals(trimmed, "Start_Marker", StringComparison.OrdinalIgnoreCase) ||
                                       string.Equals(trimmed, "Prop_Start_Marker", StringComparison.OrdinalIgnoreCase);
                        bool isBoss = string.Equals(trimmed, "Boss_Marker", StringComparison.OrdinalIgnoreCase) ||
                                      string.Equals(trimmed, "Prop_Boss_Marker", StringComparison.OrdinalIgnoreCase);

                        if (!isStart && !isBoss)
                            continue;

                        if (isStart)
                            hasStart = true;
                        if (isBoss)
                            hasBoss = true;

                        tile.propIds.RemoveAt(p);
                    }

                    if (hasStart)
                    {
                        loaded.startCoord = tile.coord;
                        foundStartMarker = true;
                    }

                    if (hasBoss)
                    {
                        loaded.bossCoord = tile.coord;
                        foundBossMarker = true;
                    }

                    tile.hasProp = tile.propIds.Count > 0;
                    loaded.tiles[i] = tile;
                }

                if (loaded.tiles.Count > 0)
                {
                    if (!foundStartMarker)
                        loaded.startCoord = loaded.tiles[0].coord;

                    if (!foundBossMarker)
                        loaded.bossCoord = loaded.tiles[loaded.tiles.Count - 1].coord;
                }

                loaded.bossReachable = IsReachable(
                    loaded.startCoord,
                    loaded.bossCoord,
                    loaded.BuildWalkableSet());

                if (loaded.packSeeds.Count == 0)
                    loaded.packSeeds = BuildPackSeeds(new System.Random(seed), loaded);

                return loaded;
            }

            var rng = new System.Random(seed);

            var layout = new DimensionLayout
            {
                seedUsed = seed,
                startCoord = new HexCoord(0, 0),
            };

            var walkable = new HashSet<HexCoord>();
            var walkableList = new List<HexCoord>();
            var spineSet = new HashSet<HexCoord>();
            var pocketSet = new HashSet<HexCoord>();

            int forwardDir;
            List<HexCoord> spinePath = GenerateSpine(rng, genProfile, walkable, walkableList, spineSet, out forwardDir);
            layout.spineCoords = spinePath;
            layout.startCoord = spinePath.Count > 0 ? spinePath[0] : new HexCoord(0, 0);
            layout.bossCoord = spinePath.Count > 0 ? spinePath[spinePath.Count - 1] : layout.startCoord;

            GeneratePockets(rng, genProfile, spinePath, walkable, walkableList, pocketSet);
            MarkSpineThickeningAsResourceValid(spinePath, spineSet, pocketSet);
            ExpandToTarget(rng, genProfile.EffectiveTargetTileCount, walkable, walkableList);
            FillIsolatedHoles(walkable, walkableList);

            if (!IsReachable(layout.startCoord, layout.bossCoord, walkable))
                ForceConnectStartToBoss(layout.startCoord, layout.bossCoord, walkable, walkableList);

            List<string> selectedBiomes = SelectBiomeSubset(rng, genProfile);
            var biomeByCoord = AssignBiomes(rng, selectedBiomes, genProfile.biomePatchSize, walkableList);
            var allResourceIds = BuildResourceIdSet(resourceDefinitions, includeOnlyEligible: false, floorIndex);
            var eligibleResourceIds = BuildResourceIdSet(resourceDefinitions, includeOnlyEligible: true, floorIndex);
            var resourceVeinCoords = GenerateResourceVeinCoords(rng, pocketSet);

            var sortedWalkable = new List<HexCoord>(walkableList);
            sortedWalkable.Sort(CompareCoords);

            layout.tiles.Clear();
            for (int i = 0; i < sortedWalkable.Count; i++)
            {
                HexCoord coord = sortedWalkable[i];
                var kind = spineSet.Contains(coord)
                    ? DimensionTileKind.Spine
                    : pocketSet.Contains(coord) ? DimensionTileKind.Pocket : DimensionTileKind.Filler;

                string biome = biomeByCoord.TryGetValue(coord, out string b) ? b : "DEFAULT";
                var tilePropIds = new List<string>();
                bool hasProp = rng.NextDouble() < genProfile.propChance;
                if (hasProp)
                {
                    bool isPocket = kind == DimensionTileKind.Pocket;
                    bool preferResource = isPocket && resourceVeinCoords.Contains(coord);
                    bool avoidResources = kind == DimensionTileKind.Spine;

                    string prop = PickRandomProp(
                        rng,
                        genProfile,
                        propRegistry,
                        biome,
                        allResourceIds,
                        eligibleResourceIds,
                        preferResource,
                        avoidResources);

                    // If a pocket vein had no matching resource candidates, gracefully fall back.
                    if (string.IsNullOrEmpty(prop) && preferResource)
                    {
                        prop = PickRandomProp(
                            rng,
                            genProfile,
                            propRegistry,
                            biome,
                            allResourceIds,
                            eligibleResourceIds,
                            preferResources: false,
                            avoidResources: false);
                    }

                    if (!string.IsNullOrEmpty(prop))
                    {
                        int spawnCount = 1;
                        HexWorldPropDefinition propDef = ResolvePropDefinitionById(propRegistry, prop);
                        if (propDef != null)
                        {
                            int min = Mathf.Max(1, propDef.minPerTile);
                            int max = Mathf.Max(min, propDef.maxPerTile);
                            spawnCount = rng.Next(min, max + 1);
                        }

                        for (int p = 0; p < spawnCount; p++)
                            tilePropIds.Add(prop);
                    }
                }

                hasProp = tilePropIds.Count > 0;

                layout.tiles.Add(new DimensionTileData
                {
                    coord = coord,
                    biomeGroup = biome,
                    hasProp = hasProp,
                    propIds = tilePropIds,
                    kind = kind,
                });
            }

            var sortedPockets = new List<HexCoord>(pocketSet);
            sortedPockets.Sort(CompareCoords);
            layout.pocketCoords = sortedPockets;
            layout.bossReachable = IsReachable(layout.startCoord, layout.bossCoord, walkable);
            layout.packSeeds = BuildPackSeeds(rng, layout);

            return layout;
        }

        private static List<PackSeedSpawn> BuildPackSeeds(System.Random rng, DimensionLayout layout)
        {
            var seeds = new List<PackSeedSpawn>();
            if (layout == null || layout.tiles == null || layout.tiles.Count == 0)
                return seeds;

            const string ChaserArchetypeId = "CHASER";

            int desired = Mathf.Clamp(layout.tiles.Count / 350, 3, 14);
            var chosen = new HashSet<HexCoord>();

            AddFromList(layout.pocketCoords, desired, preferRandom: true);
            AddFromList(layout.spineCoords, desired, preferRandom: false);

            if (seeds.Count < desired)
            {
                var fallback = new List<HexCoord>(layout.tiles.Count);
                for (int i = 0; i < layout.tiles.Count; i++)
                    fallback.Add(layout.tiles[i].coord);
                AddFromList(fallback, desired, preferRandom: true);
            }

            return seeds;

            void AddFromList(List<HexCoord> source, int targetCount, bool preferRandom)
            {
                if (source == null || source.Count == 0 || seeds.Count >= targetCount)
                    return;

                if (preferRandom)
                {
                    int guard = source.Count * 4;
                    while (seeds.Count < targetCount && guard-- > 0)
                    {
                        HexCoord coord = source[rng.Next(source.Count)];
                        TryAdd(coord);
                    }
                    return;
                }

                int stride = Mathf.Max(1, source.Count / Mathf.Max(1, targetCount));
                for (int i = 0; i < source.Count && seeds.Count < targetCount; i += stride)
                    TryAdd(source[i]);
            }

            void TryAdd(HexCoord coord)
            {
                if (coord.Equals(layout.startCoord) || coord.Equals(layout.bossCoord))
                    return;
                if (!chosen.Add(coord))
                    return;

                seeds.Add(new PackSeedSpawn
                {
                    coord = coord,
                    archetypeId = ChaserArchetypeId,
                });
            }
        }

        private static List<HexCoord> GenerateSpine(
            System.Random rng,
            DimensionGenProfile genProfile,
            HashSet<HexCoord> walkable,
            List<HexCoord> walkableList,
            HashSet<HexCoord> spineSet,
            out int forwardDir)
        {
            var start = new HexCoord(0, 0);
            AddWalkable(start, walkable, walkableList);
            spineSet.Add(start);

            var spine = new List<HexCoord> { start };

            int targetLength = rng.Next(genProfile.spineMinLength, genProfile.spineMaxLength + 1);
            forwardDir = rng.Next(0, HexCoord.NeighborDirs.Length);
            int leftDir = (forwardDir + 5) % HexCoord.NeighborDirs.Length;
            int rightDir = (forwardDir + 1) % HexCoord.NeighborDirs.Length;

            HexCoord current = start;
            HexCoord bossAnchor = StepRepeated(start, forwardDir, targetLength);

            int lateralMax = Mathf.Max(1, targetLength / 5);
            int lateralShift = rng.Next(-lateralMax, lateralMax + 1);
            if (lateralShift > 0)
                bossAnchor = StepRepeated(bossAnchor, rightDir, lateralShift);
            else if (lateralShift < 0)
                bossAnchor = StepRepeated(bossAnchor, leftDir, -lateralShift);

            for (int i = 0; i < targetLength; i++)
            {
                HexCoord next = PickSpineStep(
                    rng,
                    current,
                    start,
                    bossAnchor,
                    forwardDir,
                    leftDir,
                    rightDir,
                    spineSet,
                    genProfile);

                if (next == current)
                    break;

                current = next;
                if (spineSet.Add(current))
                {
                    spine.Add(current);
                    AddWalkable(current, walkable, walkableList);
                }
            }

            int guard = Mathf.Max(genProfile.spineMinLength, genProfile.spineMaxLength);
            while (start.DistanceTo(current) < genProfile.minBossDistance && guard-- > 0)
            {
                HexCoord next = PickSpineStep(
                    rng,
                    current,
                    start,
                    bossAnchor,
                    forwardDir,
                    leftDir,
                    rightDir,
                    spineSet,
                    genProfile);

                if (next == current)
                    break;

                current = next;
                if (spineSet.Add(current))
                {
                    spine.Add(current);
                    AddWalkable(current, walkable, walkableList);
                }
            }

            // Thicken the main route to prevent 1-tile chokepoints.
            var spineSnapshot = new HashSet<HexCoord>(spine);
            foreach (HexCoord spineCoord in spineSnapshot)
            {
                AddRadiusCluster(spineCoord, 2, walkable, walkableList);
            }

            return spine;
        }

        private static HexCoord PickSpineStep(
            System.Random rng,
            HexCoord current,
            HexCoord start,
            HexCoord bossAnchor,
            int forwardDir,
            int leftDir,
            int rightDir,
            HashSet<HexCoord> spineSet,
            DimensionGenProfile genProfile)
        {
            int currentBossDist = current.DistanceTo(bossAnchor);
            int currentStartDist = current.DistanceTo(start);

            var coords = new List<HexCoord>(6);
            var weights = new List<float>(6);

            for (int dir = 0; dir < HexCoord.NeighborDirs.Length; dir++)
            {
                HexCoord next = current.Neighbor(dir);
                if (spineSet.Contains(next))
                    continue;

                float weight = 0.25f;
                weight += (currentBossDist - next.DistanceTo(bossAnchor)) * genProfile.towardBossBias;
                weight += (next.DistanceTo(start) - currentStartDist) * genProfile.outwardBias;

                if (dir == forwardDir)
                    weight += genProfile.forwardDirectionBonus;
                else if (dir == leftDir || dir == rightDir)
                    weight += genProfile.sideDirectionBonus;

                if (next.DistanceTo(start) + 1 < currentStartDist)
                    weight *= 0.30f;

                if (weight < 0.05f)
                    weight = 0.05f;

                coords.Add(next);
                weights.Add(weight);
            }

            if (coords.Count == 0)
            {
                for (int dir = 0; dir < HexCoord.NeighborDirs.Length; dir++)
                {
                    HexCoord next = current.Neighbor(dir);
                    if (!spineSet.Contains(next))
                        return next;
                }

                return current;
            }

            return PickWeighted(rng, coords, weights);
        }

        private static void GeneratePockets(
            System.Random rng,
            DimensionGenProfile genProfile,
            List<HexCoord> spinePath,
            HashSet<HexCoord> walkable,
            List<HexCoord> walkableList,
            HashSet<HexCoord> pocketSet)
        {
            if (genProfile.pocketSeedCount <= 0 || spinePath == null || spinePath.Count == 0)
                return;

            int minIndex = Mathf.Clamp(genProfile.pocketStartPadding, 0, Mathf.Max(0, spinePath.Count - 1));
            int maxExclusive = Mathf.Clamp(spinePath.Count - genProfile.pocketEndPadding, minIndex + 1, spinePath.Count);
            int span = maxExclusive - minIndex;
            if (span <= 0)
                return;

            int seedCount = Mathf.Min(genProfile.pocketSeedCount, span);
            var usedIndices = new HashSet<int>();

            for (int i = 0; i < seedCount; i++)
            {
                int index = PickUniqueIndex(rng, minIndex, maxExclusive, usedIndices);
                HexCoord seed = spinePath[index];
                int budget = rng.Next(genProfile.pocketMinSize, genProfile.pocketMaxSize + 1);
                GrowPocketBlob(rng, seed, budget, walkable, walkableList, pocketSet);
            }
        }

        private static int PickUniqueIndex(System.Random rng, int minInclusive, int maxExclusive, HashSet<int> usedIndices)
        {
            int count = maxExclusive - minInclusive;
            if (count <= 1)
                return minInclusive;

            for (int i = 0; i < 24; i++)
            {
                int candidate = rng.Next(minInclusive, maxExclusive);
                if (usedIndices.Add(candidate))
                    return candidate;
            }

            for (int i = minInclusive; i < maxExclusive; i++)
            {
                if (usedIndices.Add(i))
                    return i;
            }

            return minInclusive;
        }

        private static void GrowPocketBlob(
            System.Random rng,
            HexCoord seed,
            int budget,
            HashSet<HexCoord> walkable,
            List<HexCoord> walkableList,
            HashSet<HexCoord> pocketSet)
        {
            if (budget <= 0)
                return;

            var frontier = new List<HexCoord> { seed };
            int grown = 0;
            int guard = budget * 20 + 20;

            while (grown < budget && frontier.Count > 0 && guard-- > 0)
            {
                HexCoord origin = frontier[rng.Next(frontier.Count)];
                int attempts = 1 + rng.Next(3);

                for (int i = 0; i < attempts && grown < budget; i++)
                {
                    HexCoord candidate = origin.Neighbor(rng.Next(0, HexCoord.NeighborDirs.Length));
                    if (!AddWalkable(candidate, walkable, walkableList))
                        continue;

                    pocketSet.Add(candidate);
                    frontier.Add(candidate);
                    grown++;
                }

                if (frontier.Count > budget * 3)
                    frontier.RemoveAt(rng.Next(frontier.Count));
            }
        }

        private static void ExpandToTarget(
            System.Random rng,
            int targetTileCount,
            HashSet<HexCoord> walkable,
            List<HexCoord> walkableList)
        {
            if (targetTileCount <= walkable.Count)
                return;

            int guard = Mathf.Max(2048, targetTileCount * 25);
            while (walkable.Count < targetTileCount && guard-- > 0)
            {
                HexCoord origin = walkableList[rng.Next(walkableList.Count)];
                HexCoord candidate = origin.Neighbor(rng.Next(0, HexCoord.NeighborDirs.Length));
                AddWalkable(candidate, walkable, walkableList);
                for (int dir = 0; dir < HexCoord.NeighborDirs.Length; dir++)
                {
                    AddWalkable(candidate.Neighbor(dir), walkable, walkableList);
                }
            }
        }

        private static void FillIsolatedHoles(HashSet<HexCoord> walkable, List<HexCoord> walkableList)
        {
            if (walkable == null || walkableList == null || walkableList.Count == 0)
                return;

            var toFill = new HashSet<HexCoord>();
            var scanSnapshot = new List<HexCoord>(walkableList);
            for (int i = 0; i < scanSnapshot.Count; i++)
            {
                HexCoord coord = scanSnapshot[i];
                for (int dir = 0; dir < HexCoord.NeighborDirs.Length; dir++)
                {
                    HexCoord neighbor = coord.Neighbor(dir);
                    if (walkable.Contains(neighbor))
                        continue;

                    int adjacentWalkableCount = 0;
                    for (int n = 0; n < HexCoord.NeighborDirs.Length; n++)
                    {
                        if (walkable.Contains(neighbor.Neighbor(n)))
                            adjacentWalkableCount++;
                    }

                    if (adjacentWalkableCount >= 5)
                        toFill.Add(neighbor);
                }
            }

            foreach (HexCoord hole in toFill)
            {
                AddWalkable(hole, walkable, walkableList);
            }
        }

        private static void AddRadiusCluster(
            HexCoord center,
            int radius,
            HashSet<HexCoord> walkable,
            List<HexCoord> walkableList)
        {
            if (radius < 0)
                return;

            for (int dq = -radius; dq <= radius; dq++)
            {
                int drMin = Mathf.Max(-radius, -dq - radius);
                int drMax = Mathf.Min(radius, -dq + radius);
                for (int dr = drMin; dr <= drMax; dr++)
                {
                    AddWalkable(new HexCoord(center.q + dq, center.r + dr), walkable, walkableList);
                }
            }
        }

        // Treat spine thickening tiles as "pocket-like" for resource generation so widened corridors can host veins.
        private static void MarkSpineThickeningAsResourceValid(
            List<HexCoord> spinePath,
            HashSet<HexCoord> spineSet,
            HashSet<HexCoord> pocketSet)
        {
            if (spinePath == null || spineSet == null || pocketSet == null)
                return;

            for (int i = 0; i < spinePath.Count; i++)
            {
                HexCoord center = spinePath[i];
                const int radius = 2;
                for (int dq = -radius; dq <= radius; dq++)
                {
                    int drMin = Mathf.Max(-radius, -dq - radius);
                    int drMax = Mathf.Min(radius, -dq + radius);
                    for (int dr = drMin; dr <= drMax; dr++)
                    {
                        HexCoord coord = new HexCoord(center.q + dq, center.r + dr);
                        if (spineSet.Contains(coord))
                            continue;

                        pocketSet.Add(coord);
                    }
                }
            }
        }

        private static Dictionary<HexCoord, string> AssignBiomes(
            System.Random rng,
            List<string> biomePool,
            int biomePatchSize,
            List<HexCoord> walkableList)
        {
            var result = new Dictionary<HexCoord, string>();
            if (walkableList == null || walkableList.Count == 0)
                return result;

            if (biomePool == null || biomePool.Count == 0)
                biomePool = new List<string> { "DEFAULT" };

            int patchSize = Mathf.Max(1, biomePatchSize);
            int centerCount = Mathf.Clamp(
                walkableList.Count / patchSize,
                1,
                Mathf.Min(256, walkableList.Count));

            var centers = new List<HexCoord>(centerCount);
            var centerBiomes = new List<string>(centerCount);
            var usedCenters = new HashSet<HexCoord>();

            while (centers.Count < centerCount)
            {
                HexCoord center = walkableList[rng.Next(walkableList.Count)];
                if (!usedCenters.Add(center))
                    continue;

                centers.Add(center);
                centerBiomes.Add(biomePool[rng.Next(biomePool.Count)]);
            }

            for (int i = 0; i < walkableList.Count; i++)
            {
                HexCoord coord = walkableList[i];
                int bestIndex = 0;
                int bestDist = int.MaxValue;

                for (int c = 0; c < centers.Count; c++)
                {
                    int dist = coord.DistanceTo(centers[c]);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestIndex = c;
                    }
                }

                result[coord] = centerBiomes[bestIndex];
            }

            return result;
        }

        private static List<string> SelectBiomeSubset(System.Random rng, DimensionGenProfile genProfile)
        {
            var available = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (genProfile != null && genProfile.biomeGroups != null)
            {
                for (int i = 0; i < genProfile.biomeGroups.Count; i++)
                {
                    string biome = NormalizeBiome(genProfile.biomeGroups[i]);
                    if (!string.IsNullOrWhiteSpace(biome) && seen.Add(biome))
                        available.Add(biome);
                }
            }

            if (available.Count == 0)
                available.Add("DEFAULT");

            int maxSelectable = Mathf.Min(3, available.Count);
            int selectCount = Mathf.Clamp(rng.Next(1, 4), 1, maxSelectable);
            var subset = new List<string>(selectCount);
            var used = new HashSet<int>();
            while (subset.Count < selectCount)
            {
                int idx = rng.Next(0, available.Count);
                if (!used.Add(idx))
                    continue;
                subset.Add(available[idx]);
            }

            return subset;
        }

        private static string PickRandomProp(
            System.Random rng,
            DimensionGenProfile genProfile,
            PropRegistry propRegistry,
            string tileBiome,
            HashSet<string> allResourceIds,
            HashSet<string> eligibleResourceIds,
            bool preferResources,
            bool avoidResources)
        {
            string biome = NormalizeBiome(tileBiome);
            var candidates = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var filteredIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var globalIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (genProfile != null && genProfile.randomPropPool != null)
            {
                for (int i = 0; i < genProfile.randomPropPool.Count; i++)
                {
                    string id = NormalizeId(genProfile.randomPropPool[i]);
                    if (!string.IsNullOrEmpty(id))
                        filteredIds.Add(id);
                }
            }

            if (genProfile != null && genProfile.globalPropPool != null)
            {
                for (int i = 0; i < genProfile.globalPropPool.Count; i++)
                {
                    string id = NormalizeId(genProfile.globalPropPool[i]);
                    if (!string.IsNullOrEmpty(id))
                        globalIds.Add(id);
                }
            }

            bool useFilteredPool = filteredIds.Count > 0;
            if (propRegistry != null && propRegistry.allProps != null)
            {
                for (int i = 0; i < propRegistry.allProps.Count; i++)
                {
                    HexWorldPropDefinition def = propRegistry.allProps[i];
                    if (!def)
                        continue;

                    string id = NormalizeId(def.id);
                    if (string.IsNullOrEmpty(id))
                        id = NormalizeId(def.name);
                    if (string.IsNullOrEmpty(id))
                        continue;

                    bool isResource = allResourceIds != null && allResourceIds.Contains(id);
                    if (isResource && (eligibleResourceIds == null || !eligibleResourceIds.Contains(id)))
                        continue;
                    if (preferResources && !isResource)
                        continue;
                    if (avoidResources && isResource)
                        continue;

                    if (useFilteredPool && !filteredIds.Contains(id) && !globalIds.Contains(id))
                        continue;

                    string propBiome = string.IsNullOrWhiteSpace(def.biomeGroup)
                        ? "ALL"
                        : NormalizeBiome(def.biomeGroup);
                    bool matchesBiome =
                        isResource || // Resources are floor content; don't block them on biome art tags.
                        string.Equals(propBiome, "ALL", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(propBiome, biome, StringComparison.OrdinalIgnoreCase) ||
                        globalIds.Contains(id);

                    if (!matchesBiome)
                        continue;

                    if (seen.Add(id))
                        candidates.Add(id);
                }
            }

            if (candidates.Count == 0)
                return string.Empty;

            return candidates[rng.Next(candidates.Count)];
        }

        private static HashSet<string> BuildResourceIdSet(
            List<DungeonResourceDefinition> definitions,
            bool includeOnlyEligible,
            int floorIndex)
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (definitions == null || definitions.Count == 0)
                return ids;

            int floor = Mathf.Max(1, floorIndex);
            for (int i = 0; i < definitions.Count; i++)
            {
                DungeonResourceDefinition def = definitions[i];
                if (def == null || string.IsNullOrWhiteSpace(def.resourceId))
                    continue;

                bool eligible =
                    def.minFloor <= floor &&
                    (def.maxFloor <= 0 || floor <= def.maxFloor);

                if (!includeOnlyEligible || eligible)
                    ids.Add(NormalizeId(def.resourceId));
            }

            return ids;
        }

        private static HashSet<HexCoord> GenerateResourceVeinCoords(System.Random rng, HashSet<HexCoord> pocketSet)
        {
            var result = new HashSet<HexCoord>();
            if (pocketSet == null || pocketSet.Count == 0)
                return result;

            var pocketList = new List<HexCoord>(pocketSet);
            int seedCount = Mathf.Clamp(pocketList.Count / 90, 1, 10);

            for (int i = 0; i < seedCount; i++)
            {
                HexCoord seed = pocketList[rng.Next(pocketList.Count)];
                int budget = rng.Next(6, 18);
                GrowResourceVeinBlob(rng, seed, budget, pocketSet, result);
            }

            return result;
        }

        private static void GrowResourceVeinBlob(
            System.Random rng,
            HexCoord seed,
            int budget,
            HashSet<HexCoord> pocketSet,
            HashSet<HexCoord> veinSet)
        {
            if (budget <= 0 || !pocketSet.Contains(seed))
                return;

            var frontier = new List<HexCoord> { seed };
            veinSet.Add(seed);

            int grown = 1;
            int guard = budget * 12 + 16;
            while (grown < budget && frontier.Count > 0 && guard-- > 0)
            {
                HexCoord origin = frontier[rng.Next(frontier.Count)];
                HexCoord next = origin.Neighbor(rng.Next(0, HexCoord.NeighborDirs.Length));

                if (!pocketSet.Contains(next))
                    continue;
                if (!veinSet.Add(next))
                    continue;

                frontier.Add(next);
                grown++;
            }
        }

        private void EnsureRegistryReference()
        {
            if (registry != null)
                return;

#if UNITY_EDITOR
            registry = UnityEditor.AssetDatabase.LoadAssetAtPath<PropRegistry>(
                "Assets/Minigames/HexWorld3D/Definitions/PropRegistry_Main.asset");
#endif
        }

        private static bool IsReachable(HexCoord start, HexCoord boss, HashSet<HexCoord> walkable)
        {
            if (!walkable.Contains(start) || !walkable.Contains(boss))
                return false;

            var visited = new HashSet<HexCoord> { start };
            var queue = new Queue<HexCoord>();
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                HexCoord current = queue.Dequeue();
                if (current == boss)
                    return true;

                for (int i = 0; i < HexCoord.NeighborDirs.Length; i++)
                {
                    HexCoord next = current.Neighbor(i);
                    if (!walkable.Contains(next) || !visited.Add(next))
                        continue;

                    queue.Enqueue(next);
                }
            }

            return false;
        }

        private static void ForceConnectStartToBoss(
            HexCoord start,
            HexCoord boss,
            HashSet<HexCoord> walkable,
            List<HexCoord> walkableList)
        {
            HexCoord current = start;
            int guard = 8192;

            while (current != boss && guard-- > 0)
            {
                HexCoord best = current;
                int bestDist = current.DistanceTo(boss);

                for (int i = 0; i < HexCoord.NeighborDirs.Length; i++)
                {
                    HexCoord next = current.Neighbor(i);
                    int dist = next.DistanceTo(boss);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        best = next;
                    }
                }

                if (best == current)
                    break;

                current = best;
                AddWalkable(current, walkable, walkableList);
            }
        }

        private static HexCoord StepRepeated(HexCoord start, int dir, int count)
        {
            HexCoord current = start;
            for (int i = 0; i < count; i++)
                current = current.Neighbor(dir);
            return current;
        }

        private static HexCoord PickWeighted(System.Random rng, List<HexCoord> coords, List<float> weights)
        {
            double total = 0d;
            for (int i = 0; i < weights.Count; i++)
                total += Math.Max(0.0001, weights[i]);

            double roll = rng.NextDouble() * total;
            for (int i = 0; i < coords.Count; i++)
            {
                roll -= Math.Max(0.0001, weights[i]);
                if (roll <= 0d)
                    return coords[i];
            }

            return coords[coords.Count - 1];
        }

        private static bool AddWalkable(HexCoord coord, HashSet<HexCoord> walkable, List<HexCoord> walkableList)
        {
            if (!walkable.Add(coord))
                return false;

            walkableList.Add(coord);
            return true;
        }

        private static int CompareCoords(HexCoord a, HexCoord b)
        {
            int q = a.q.CompareTo(b.q);
            return q != 0 ? q : a.r.CompareTo(b.r);
        }

        private static string NormalizeBiome(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "DEFAULT";
            return value.Trim().ToUpperInvariant();
        }

        private static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static string StripResourcePrefix(string value)
        {
            const string Prefix = "resource_";
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string normalized = NormalizeId(value);
            if (normalized.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
                return normalized.Substring(Prefix.Length);

            return normalized;
        }

        private static HexWorldPropDefinition ResolvePropDefinitionById(PropRegistry propRegistry, string propId)
        {
            if (propRegistry == null || propRegistry.allProps == null || string.IsNullOrWhiteSpace(propId))
                return null;

            string target = NormalizeId(propId);
            for (int i = 0; i < propRegistry.allProps.Count; i++)
            {
                HexWorldPropDefinition def = propRegistry.allProps[i];
                if (!def)
                    continue;

                if (string.Equals(NormalizeId(def.id), target, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(NormalizeId(def.displayName), target, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(NormalizeId(def.name), target, StringComparison.OrdinalIgnoreCase))
                {
                    return def;
                }
            }

            return null;
        }

        private Vector3 AxialToWorld(HexCoord c)
        {
            float x = gizmoHexSize * (1.5f * c.q);
            float z = gizmoHexSize * (Mathf.Sqrt(3f) * (c.r + c.q * 0.5f));
            return new Vector3(x, gizmoY, z);
        }

        private void OnDrawGizmos()
        {
            if (!drawGizmos || latestLayout == null || latestLayout.tiles == null || latestLayout.tiles.Count == 0)
                return;

            float radius = Mathf.Max(0.02f, gizmoHexSize * 0.22f);

            for (int i = 0; i < latestLayout.tiles.Count; i++)
            {
                DimensionTileData tile = latestLayout.tiles[i];
                Gizmos.color = tile.kind switch
                {
                    DimensionTileKind.Spine => spineTileColor,
                    DimensionTileKind.Pocket => pocketTileColor,
                    _ => fillerTileColor
                };
                Gizmos.DrawSphere(AxialToWorld(tile.coord), radius);
            }

            if (latestLayout.spineCoords != null && latestLayout.spineCoords.Count > 1)
            {
                Gizmos.color = spinePathColor;
                for (int i = 1; i < latestLayout.spineCoords.Count; i++)
                    Gizmos.DrawLine(AxialToWorld(latestLayout.spineCoords[i - 1]), AxialToWorld(latestLayout.spineCoords[i]));
            }

            Gizmos.color = startColor;
            Gizmos.DrawSphere(AxialToWorld(latestLayout.startCoord), radius * 1.8f);

            Gizmos.color = bossColor;
            Gizmos.DrawCube(AxialToWorld(latestLayout.bossCoord), Vector3.one * (radius * 2.2f));
        }
    }
}

#if UNITY_EDITOR
namespace GalacticFishing.Minigames.Dungeon3D
{
    using UnityEditor;

    [CustomEditor(typeof(DimensionGenerator))]
    public sealed class DimensionGeneratorEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var generator = (DimensionGenerator)target;
            EditorGUILayout.Space();

            if (GUILayout.Button("Regenerate", GUILayout.Height(28f)))
            {
                Undo.RecordObject(generator, "Regenerate Dimension Layout");
                generator.Regenerate();
                EditorUtility.SetDirty(generator);
            }

            DimensionLayout layout = generator.Layout;
            if (layout != null && layout.tiles != null && layout.tiles.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    $"Seed {layout.seedUsed} | Walkable {layout.WalkableCount} | " +
                    $"Spine {layout.spineCoords.Count} | Boss Reachable: {layout.bossReachable}",
                    MessageType.None);
            }
        }
    }
}
#endif
