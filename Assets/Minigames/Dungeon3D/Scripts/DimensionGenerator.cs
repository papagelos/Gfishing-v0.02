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
        [SerializeField] private bool useFixedSeed = true;
        [SerializeField] private int fixedSeed = 1337;

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
            latestLayout = GenerateWithRetries(seed, profile, registry);

            Debug.Log(
                $"[{nameof(DimensionGenerator)}] Seed={latestLayout.seedUsed} " +
                $"Tiles={latestLayout.WalkableCount} Spine={latestLayout.spineCoords.Count} " +
                $"Pockets={latestLayout.pocketCoords.Count} Reachable={latestLayout.bossReachable}",
                this);

            OnGenerated?.Invoke(latestLayout);
        }

        private static DimensionLayout GenerateWithRetries(int seed, DimensionGenProfile genProfile, PropRegistry propRegistry)
        {
            const int MaxAttempts = 4;
            DimensionLayout best = null;

            for (int i = 0; i < MaxAttempts; i++)
            {
                int attemptSeed = seed + i * 7919;
                var attempt = GenerateOnce(attemptSeed, genProfile, propRegistry);
                if (best == null || attempt.WalkableCount > best.WalkableCount)
                    best = attempt;

                if (attempt.bossReachable)
                    return attempt;
            }

            Debug.LogWarning($"[{nameof(DimensionGenerator)}] Generated layout failed connectivity after retries.");
            return best ?? new DimensionLayout();
        }

        private static DimensionLayout GenerateOnce(int seed, DimensionGenProfile genProfile, PropRegistry propRegistry)
        {
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
            ExpandToTarget(rng, genProfile.EffectiveTargetTileCount, walkable, walkableList);

            if (!IsReachable(layout.startCoord, layout.bossCoord, walkable))
                ForceConnectStartToBoss(layout.startCoord, layout.bossCoord, walkable, walkableList);

            var biomeByCoord = AssignBiomes(rng, genProfile, walkableList);

            var sortedWalkable = new List<HexCoord>(walkableList);
            sortedWalkable.Sort(CompareCoords);
            List<string> resolvedPropPool = ResolvePropPool(genProfile, propRegistry);

            layout.tiles.Clear();
            for (int i = 0; i < sortedWalkable.Count; i++)
            {
                HexCoord coord = sortedWalkable[i];
                var kind = spineSet.Contains(coord)
                    ? DimensionTileKind.Spine
                    : pocketSet.Contains(coord) ? DimensionTileKind.Pocket : DimensionTileKind.Filler;

                bool hasProp = rng.NextDouble() < genProfile.propChance;
                string prop = hasProp ? PickRandomProp(rng, resolvedPropPool) : string.Empty;
                string biome = biomeByCoord.TryGetValue(coord, out string b) ? b : "DEFAULT";

                layout.tiles.Add(new DimensionTileData
                {
                    coord = coord,
                    biomeGroup = biome,
                    hasProp = hasProp,
                    propId = prop,
                    kind = kind,
                });
            }

            var sortedPockets = new List<HexCoord>(pocketSet);
            sortedPockets.Sort(CompareCoords);
            layout.pocketCoords = sortedPockets;
            layout.bossReachable = IsReachable(layout.startCoord, layout.bossCoord, walkable);

            return layout;
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
            }
        }

        private static Dictionary<HexCoord, string> AssignBiomes(
            System.Random rng,
            DimensionGenProfile genProfile,
            List<HexCoord> walkableList)
        {
            var result = new Dictionary<HexCoord, string>();
            if (walkableList == null || walkableList.Count == 0)
                return result;

            var pool = genProfile.biomeGroups;
            if (pool == null || pool.Count == 0)
                pool = new List<string> { "DEFAULT" };

            int patchSize = Mathf.Max(1, genProfile.biomePatchSize);
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
                centerBiomes.Add(pool[rng.Next(pool.Count)]);
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

        private static List<string> ResolvePropPool(DimensionGenProfile genProfile, PropRegistry propRegistry)
        {
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (genProfile != null && genProfile.randomPropPool != null)
            {
                for (int i = 0; i < genProfile.randomPropPool.Count; i++)
                {
                    string id = genProfile.randomPropPool[i]?.Trim();
                    if (!string.IsNullOrWhiteSpace(id) && seen.Add(id))
                        result.Add(id);
                }
            }

            if (result.Count == 0 && propRegistry != null && propRegistry.allProps != null)
            {
                for (int i = 0; i < propRegistry.allProps.Count; i++)
                {
                    HexWorldPropDefinition def = propRegistry.allProps[i];
                    if (!def)
                        continue;

                    // Registry fallback uses IDs as authoritative keys.
                    string id = def.id;
                    if (string.IsNullOrWhiteSpace(id))
                        id = def.name;

                    id = id?.Trim();
                    if (!string.IsNullOrWhiteSpace(id) && seen.Add(id))
                        result.Add(id);
                }
            }

            if (result.Count == 0)
                result.Add("RandomProp");

            return result;
        }

        private static string PickRandomProp(System.Random rng, List<string> pool)
        {
            if (pool == null || pool.Count == 0)
                return "RandomProp";

            return pool[rng.Next(pool.Count)];
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
