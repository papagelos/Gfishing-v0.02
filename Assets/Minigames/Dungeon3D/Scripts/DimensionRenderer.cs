using System;
using System.Collections.Generic;
using GalacticFishing.Minigames.HexWorld;
using UnityEngine;
using UnityEngine.Serialization;

namespace GalacticFishing.Minigames.Dungeon3D
{
    public sealed class DimensionRenderer : MonoBehaviour
    {
        [Serializable]
        private sealed class EnemyArchetypeEntry
        {
            public string archetypeId;
            public GameObject prefab;
        }

        [Header("Refs")]
        [SerializeField] private DimensionGenerator generator;
        [SerializeField] private DimensionGenProfile profile;
        [SerializeField] private GameObject ownedPrefab;
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private GameObject voidGuardPrefab;
        [SerializeField] private List<EnemyArchetypeEntry> enemyArchetypes = new();
        [SerializeField, HideInInspector, FormerlySerializedAs("enemyChaserPrefab")] private GameObject legacyEnemyChaserPrefab;
        [SerializeField] private Transform tilesRoot;
        [SerializeField] private Transform propsRoot;
        [SerializeField] private Transform boundariesRoot;
        [SerializeField] private PropRegistry registry;

        [Header("Layout")]
        [SerializeField, Min(0.05f)] private float hexSize = 1f;
        [SerializeField] private bool regenerateOnEnable = true;
        [SerializeField] private bool renderCurrentOnEnable = true;
        [SerializeField] private bool clearOnDisable;
        [SerializeField] private bool deterministicStylePick = true;
        [SerializeField] private bool verboseLogging;

        [Header("Spawning (Legacy/Disabled)")]
        [SerializeField, Min(0.1f)] private float enemySpawnInterval = 5f;
        [SerializeField, Min(0)] private int maxLiveEnemies = 8;
        [SerializeField, Min(0f)] private float spawnRingRadiusInner = 18.0f;
        [SerializeField, Min(0f)] private float spawnRingRadiusOuter = 22.0f;
        [SerializeField, Min(1)] private int maxSpawnRetries = 8;

        private readonly List<GameObject> _spawnedTiles = new();
        private readonly List<GameObject> _spawnedProps = new();
        private readonly List<GameObject> _spawnedEnemies = new();
        private readonly Dictionary<string, List<HexWorldTileStyle>> _stylesByBiome = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, HexWorldTileStyle> _stylesById = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, HexWorldPropDefinition> _propsByKey = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, GameObject> _enemyPrefabMap = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _missingPropIdsLogged = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _missingPropPrefabLogged = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _missingEnemyArchetypesLogged = new(StringComparer.OrdinalIgnoreCase);
        private bool _missingRegistryWarned;
        private bool _missingEnemyArchetypeConfigWarned;
        private GameObject _defaultEnemyPrefab;
        private string _defaultEnemyArchetypeId;
        private GameObject _spawnedPlayer;
        private DimensionLayout _spawnLookupLayout;
        private int _spawnLookupTileCount;
        private HashSet<HexCoord> _spawnWalkableSetCache;
        private readonly Dictionary<HexCoord, DimensionTileData> _spawnTileByCoord = new();
        public PropRegistry Registry => registry;
        public int CurrentAliveCount
        {
            get
            {
                PruneDestroyedEnemies();
                return _spawnedEnemies.Count;
            }
        }

        private void OnEnable()
        {
            if (!generator)
                generator = GetComponent<DimensionGenerator>();

            EnsureRegistryReference();
            EnsureRoots();
            RebuildPropCache();
            RebuildEnemyArchetypeCache();

            if (generator)
                generator.OnGenerated += HandleGenerated;

            if (generator && regenerateOnEnable)
            {
                generator.Regenerate();
            }
            else if (renderCurrentOnEnable && generator && generator.Layout != null && generator.Layout.tiles != null && generator.Layout.tiles.Count > 0)
            {
                RenderLayout(generator.Layout);
            }
        }

        private void OnDisable()
        {
            if (generator)
                generator.OnGenerated -= HandleGenerated;

            if (clearOnDisable)
                Clear();
        }

        private void Update()
        {
            if (!Application.isPlaying)
                return;

            PruneDestroyedEnemies();
            PruneDistantEnemies();
        }

        [ContextMenu("Render Current Layout")]
        public void RenderCurrentLayout()
        {
            if (!generator)
            {
                Debug.LogWarning($"[{nameof(DimensionRenderer)}] Missing generator reference.", this);
                return;
            }

            RenderLayout(generator.Layout);
        }

        [ContextMenu("Clear")]
        public void Clear()
        {
            EnsureRoots();
            ClearRootChildren(tilesRoot);
            ClearRootChildren(propsRoot);
            ClearRootChildren(boundariesRoot);
            _spawnedTiles.Clear();
            _spawnedProps.Clear();
            _spawnedEnemies.Clear();
            _spawnedPlayer = null;
            _spawnLookupLayout = null;
            _spawnLookupTileCount = 0;
            _spawnWalkableSetCache = null;
            _spawnTileByCoord.Clear();
        }

        private void HandleGenerated(DimensionLayout layout)
        {
            RenderLayout(layout);
        }

        public void RenderLayout(DimensionLayout layout)
        {
            if (layout == null || layout.tiles == null)
            {
                Debug.LogWarning($"[{nameof(DimensionRenderer)}] Layout is null or empty.", this);
                return;
            }

            if (!ownedPrefab)
            {
                Debug.LogError($"[{nameof(DimensionRenderer)}] Missing ownedPrefab reference.", this);
                return;
            }

            EnsureRoots();
            RebuildBiomeStyleCache();
            RebuildPropCache();
            RebuildEnemyArchetypeCache();
            Clear();

            for (int i = 0; i < layout.tiles.Count; i++)
            {
                DimensionTileData tile = layout.tiles[i];
                Vector3 tilePos = AxialToWorld(tile.coord);

                GameObject tileGo = Instantiate(ownedPrefab, tilePos, Quaternion.identity, tilesRoot);
                tileGo.name = $"Tile_{tile.coord.q}_{tile.coord.r}_{tile.biomeGroup}";
                _spawnedTiles.Add(tileGo);

                HexWorldTileStyle style = null;
                if (!string.IsNullOrWhiteSpace(tile.styleId))
                    style = ResolveStyleById(tile.styleId);
                if (style == null)
                    style = ResolveStyle(tile.biomeGroup, tile.coord, layout);
                if (style != null)
                {
                    var visual = tileGo.GetComponent<HexTileVisual>() ?? tileGo.GetComponentInChildren<HexTileVisual>(true);
                    if (visual != null)
                        visual.ApplyStyle(style);
                }

                if (!tile.hasProp || tile.propIds == null || tile.propIds.Count == 0)
                    continue;

                for (int p = 0; p < tile.propIds.Count; p++)
                {
                    string propId = tile.propIds[p];
                    if (!TryResolveProp(propId, out HexWorldPropDefinition propDef) || !propDef)
                        continue;

                    if (!propDef.prefab)
                    {
                        if (_missingPropPrefabLogged.Add(propDef.name))
                        {
                            Debug.LogWarning(
                                $"[{nameof(DimensionRenderer)}] Prop '{propId}' resolved to '{propDef.name}' but has no prefab assigned.",
                                this);
                        }
                        continue;
                    }

                    Vector2 circle = UnityEngine.Random.insideUnitCircle * Mathf.Max(0f, propDef.jitterRadius);
                    Vector3 propPos = tilePos + new Vector3(circle.x, 0f, circle.y);

                    float finalScale = Mathf.Max(0.001f, propDef.masterScale);
                    if (tile.propScales != null && p < tile.propScales.Count)
                    {
                        float exportedScale = tile.propScales[p];
                        if (!float.IsNaN(exportedScale) && !float.IsInfinity(exportedScale))
                            finalScale = Mathf.Max(0.001f, exportedScale);
                    }

                    GameObject propGo = Instantiate(propDef.prefab, propPos, Quaternion.identity, propsRoot);
                    propGo.transform.localScale = Vector3.one * finalScale;
                    propGo.name = $"Prop_{propDef.name}_{tile.coord.q}_{tile.coord.r}_{p}";

                    var miningNode = propGo.GetComponent<GalacticFishing.Minigames.Dungeon3D.DungeonMiningNode>();
                    if (miningNode != null && generator != null &&
                        generator.TryGetResourceDefinitionForPropId(propId, out DungeonResourceDefinition resourceDef) &&
                        resourceDef != null)
                    {
                        miningNode.Initialize(resourceDef);
                    }

                    _spawnedProps.Add(propGo);
                }
            }

            SpawnPerimeterGuards(layout);
            SpawnPlayerAt(layout.startCoord);
            FocusMainCameraOnTile(layout.startCoord);

            if (verboseLogging)
            {
                Debug.Log(
                    $"[{nameof(DimensionRenderer)}] Rendered {layout.tiles.Count} tiles, " +
                    $"{_spawnedProps.Count} props, {_spawnedEnemies.Count} enemies (seed {layout.seedUsed}).",
                    this);
            }
        }

        private void SpawnEnemies(DimensionLayout layout)
        {
            if (layout == null || layout.packSeeds == null || layout.packSeeds.Count == 0)
                return;

            if (_enemyPrefabMap.Count == 0 && _defaultEnemyPrefab == null)
                RebuildEnemyArchetypeCache();

            if (_defaultEnemyPrefab == null)
            {
                if (!_missingEnemyArchetypeConfigWarned)
                {
                    Debug.LogWarning(
                        $"[{nameof(DimensionRenderer)}] No valid enemy archetype prefabs are configured; skipping pack seed spawns.",
                        this);
                    _missingEnemyArchetypeConfigWarned = true;
                }
                return;
            }

            for (int i = 0; i < layout.packSeeds.Count; i++)
            {
                PackSeedSpawn seed = layout.packSeeds[i];
                if (!TryResolveEnemyPrefab(seed.archetypeId, out GameObject enemyPrefab, out string resolvedArchetypeId))
                    continue;

                Vector3 pos = AxialToWorld(seed.coord);
                GameObject enemy = Instantiate(enemyPrefab, pos, Quaternion.identity, propsRoot);
                string spawnNameId = string.IsNullOrEmpty(resolvedArchetypeId) ? "Enemy" : resolvedArchetypeId;
                enemy.name = $"Enemy_{spawnNameId}_{seed.coord.q}_{seed.coord.r}_{i}";
                _spawnedEnemies.Add(enemy);
            }
        }

        private void PruneDestroyedEnemies()
        {
            for (int i = _spawnedEnemies.Count - 1; i >= 0; i--)
            {
                if (_spawnedEnemies[i] == null)
                    _spawnedEnemies.RemoveAt(i);
            }
        }

        public bool SpawnSingleRandomEnemy()
        {
            DimensionLayout activeLayout = generator ? generator.Layout : null;
            return SpawnSingleRandomEnemy(activeLayout);
        }

        private bool SpawnSingleRandomEnemy(DimensionLayout layout)
        {
            if (layout == null || layout.tiles == null || layout.tiles.Count == 0)
                return false;

            if (_enemyPrefabMap.Count == 0 && _defaultEnemyPrefab == null)
                RebuildEnemyArchetypeCache();

            if (_defaultEnemyPrefab == null)
            {
                if (!_missingEnemyArchetypeConfigWarned)
                {
                    Debug.LogWarning(
                        $"[{nameof(DimensionRenderer)}] No valid enemy archetype prefabs are configured; enemy spawning is disabled.",
                        this);
                    _missingEnemyArchetypeConfigWarned = true;
                }
                return false;
            }

            if (!TryPickRandomWalkableTile(layout, out DimensionTileData tile))
                return false;

            if (!TryPickRandomEnemyArchetype(out GameObject enemyPrefab, out string resolvedArchetypeId))
                return false;

            EnsureRoots();
            Vector3 pos = AxialToWorld(tile.coord);
            GameObject enemy = Instantiate(enemyPrefab, pos, Quaternion.identity, propsRoot);
            string spawnNameId = string.IsNullOrEmpty(resolvedArchetypeId) ? "Enemy" : resolvedArchetypeId;
            enemy.name = $"Enemy_{spawnNameId}_{tile.coord.q}_{tile.coord.r}_{Time.frameCount}";
            _spawnedEnemies.Add(enemy);
            return true;
        }

        private bool TryPickRandomWalkableTile(DimensionLayout layout, out DimensionTileData result)
        {
            result = default;
            if (layout == null || layout.tiles == null || layout.tiles.Count == 0)
                return false;

            if (!TryGetPlayerSpawnOrigin(out Vector3 origin))
                return false;

            if (!TryEnsureSpawnLookupCache(layout))
                return false;

            float inner = Mathf.Max(0f, spawnRingRadiusInner);
            float outer = Mathf.Max(inner, spawnRingRadiusOuter);
            int retries = Mathf.Max(1, maxSpawnRetries);

            for (int attempt = 0; attempt < retries; attempt++)
            {
                float angleRad = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                float distance = UnityEngine.Random.Range(inner, outer);
                Vector3 dir = new Vector3(Mathf.Cos(angleRad), 0f, Mathf.Sin(angleRad));
                Vector3 targetPos = origin + (dir * distance);

                HexCoord coord = WorldToAxial(targetPos);
                if (_spawnWalkableSetCache == null || !_spawnWalkableSetCache.Contains(coord))
                    continue;

                if (_spawnTileByCoord.TryGetValue(coord, out result))
                    return true;
            }

            return false;
        }

        private void PruneDistantEnemies()
        {
            if (_spawnedEnemies.Count == 0)
                return;
            if (!TryGetPlayerSpawnOrigin(out Vector3 origin))
                return;

            const float MaxDistance = 35f;
            float maxDistanceSqr = MaxDistance * MaxDistance;

            for (int i = _spawnedEnemies.Count - 1; i >= 0; i--)
            {
                GameObject enemy = _spawnedEnemies[i];
                if (enemy == null)
                    continue;

                Vector3 delta = enemy.transform.position - origin;
                delta.y = 0f;
                if (delta.sqrMagnitude <= maxDistanceSqr)
                    continue;

                if (Application.isPlaying)
                    Destroy(enemy);
                else
                    DestroyImmediate(enemy);

                _spawnedEnemies.RemoveAt(i);
            }
        }

        private static bool IsTimedSpawnTile(DimensionLayout layout, DimensionTileData tile)
        {
            if (tile.kind != DimensionTileKind.Spine && tile.kind != DimensionTileKind.Pocket)
                return false;

            if (tile.coord.Equals(layout.startCoord) || tile.coord.Equals(layout.bossCoord))
                return false;

            return true;
        }

        private bool TryPickRandomEnemyArchetype(out GameObject prefab, out string resolvedArchetypeId)
        {
            prefab = null;
            resolvedArchetypeId = string.Empty;

            if (enemyArchetypes != null && enemyArchetypes.Count > 0)
            {
                int validCount = 0;
                for (int i = 0; i < enemyArchetypes.Count; i++)
                {
                    EnemyArchetypeEntry entry = enemyArchetypes[i];
                    if (entry != null && entry.prefab != null)
                        validCount++;
                }

                if (validCount > 0)
                {
                    int pick = UnityEngine.Random.Range(0, validCount);
                    for (int i = 0; i < enemyArchetypes.Count; i++)
                    {
                        EnemyArchetypeEntry entry = enemyArchetypes[i];
                        if (entry == null || entry.prefab == null)
                            continue;

                        if (pick-- != 0)
                            continue;

                        prefab = entry.prefab;
                        resolvedArchetypeId = Normalize(entry.archetypeId);
                        if (string.IsNullOrEmpty(resolvedArchetypeId))
                            resolvedArchetypeId = Normalize(_defaultEnemyArchetypeId);
                        return true;
                    }
                }
            }

            if (_defaultEnemyPrefab == null)
                return false;

            prefab = _defaultEnemyPrefab;
            resolvedArchetypeId = Normalize(_defaultEnemyArchetypeId);
            return true;
        }

        private void EnsureRoots()
        {
            if (!tilesRoot)
            {
                Transform found = transform.Find("Tiles");
                if (!found)
                {
                    var go = new GameObject("Tiles");
                    go.transform.SetParent(transform, false);
                    found = go.transform;
                }
                tilesRoot = found;
            }

            if (!propsRoot)
            {
                Transform found = transform.Find("Props");
                if (!found)
                {
                    var go = new GameObject("Props");
                    go.transform.SetParent(transform, false);
                    found = go.transform;
                }
                propsRoot = found;
            }

            if (!boundariesRoot)
            {
                Transform found = transform.Find("Boundaries");
                if (!found)
                {
                    var go = new GameObject("Boundaries");
                    go.transform.SetParent(transform, false);
                    found = go.transform;
                }
                boundariesRoot = found;
            }
        }

        private void ClearRootChildren(Transform root)
        {
            if (!root)
                return;

            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform child = root.GetChild(i);
                if (!child)
                    continue;

                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }
        }

        private void RebuildBiomeStyleCache()
        {
            _stylesByBiome.Clear();
            _stylesById.Clear();

            DimensionGenProfile activeProfile = profile ? profile : (generator ? generator.Profile : null);
            if (!activeProfile || activeProfile.biomeStyleGroups == null)
                return;

            for (int i = 0; i < activeProfile.biomeStyleGroups.Count; i++)
            {
                BiomeTileStyleGroup group = activeProfile.biomeStyleGroups[i];
                if (group == null || string.IsNullOrWhiteSpace(group.biomeGroup) || group.tileStyles == null || group.tileStyles.Count == 0)
                    continue;

                string key = Normalize(group.biomeGroup);
                if (!_stylesByBiome.TryGetValue(key, out List<HexWorldTileStyle> list))
                {
                    list = new List<HexWorldTileStyle>();
                    _stylesByBiome.Add(key, list);
                }

                for (int t = 0; t < group.tileStyles.Count; t++)
                {
                    HexWorldTileStyle style = group.tileStyles[t];
                    if (style && !list.Contains(style))
                    {
                        list.Add(style);
                        AddStyleAlias(style.name, style);
                        AddStyleAlias(style.displayName, style);
                    }
                }
            }
        }

        private void AddStyleAlias(string alias, HexWorldTileStyle style)
        {
            if (style == null)
                return;

            string key = Normalize(alias);
            if (string.IsNullOrEmpty(key))
                return;

            if (!_stylesById.ContainsKey(key))
                _stylesById.Add(key, style);
        }

        private void RebuildPropCache()
        {
            _propsByKey.Clear();
            _missingPropIdsLogged.Clear();
            _missingPropPrefabLogged.Clear();
            EnsureRegistryReference();

            if (registry == null || registry.allProps == null || registry.allProps.Count == 0)
                return;

            for (int i = 0; i < registry.allProps.Count; i++)
            {
                HexWorldPropDefinition def = registry.allProps[i];
                if (!def)
                    continue;

                AddPropAlias(def.id, def);
                AddPropAlias(def.displayName, def);
                AddPropAlias(def.name, def);
            }
        }

        private void AddPropAlias(string alias, HexWorldPropDefinition def)
        {
            string key = Normalize(alias);
            if (string.IsNullOrEmpty(key))
                return;

            if (!_propsByKey.ContainsKey(key))
                _propsByKey.Add(key, def);
        }

        private void RebuildEnemyArchetypeCache()
        {
            _enemyPrefabMap.Clear();
            _missingEnemyArchetypesLogged.Clear();
            _defaultEnemyPrefab = null;
            _defaultEnemyArchetypeId = string.Empty;
            _missingEnemyArchetypeConfigWarned = false;

            if (enemyArchetypes == null || enemyArchetypes.Count == 0)
            {
                if (legacyEnemyChaserPrefab != null)
                {
                    const string LegacyChaserArchetypeId = "CHASER";
                    _defaultEnemyPrefab = legacyEnemyChaserPrefab;
                    _defaultEnemyArchetypeId = LegacyChaserArchetypeId;
                    _enemyPrefabMap[Normalize(LegacyChaserArchetypeId)] = legacyEnemyChaserPrefab;
                }
                return;
            }

            for (int i = 0; i < enemyArchetypes.Count; i++)
            {
                EnemyArchetypeEntry entry = enemyArchetypes[i];
                if (entry == null)
                    continue;

                if (entry.prefab == null)
                    continue;

                if (_defaultEnemyPrefab == null)
                {
                    _defaultEnemyPrefab = entry.prefab;
                    _defaultEnemyArchetypeId = entry.archetypeId;
                }

                string key = Normalize(entry.archetypeId);
                if (string.IsNullOrEmpty(key))
                    continue;

                if (!_enemyPrefabMap.ContainsKey(key))
                    _enemyPrefabMap.Add(key, entry.prefab);
            }

            if (_defaultEnemyPrefab == null && legacyEnemyChaserPrefab != null)
            {
                const string LegacyChaserArchetypeId = "CHASER";
                _defaultEnemyPrefab = legacyEnemyChaserPrefab;
                _defaultEnemyArchetypeId = LegacyChaserArchetypeId;
                if (!_enemyPrefabMap.ContainsKey(Normalize(LegacyChaserArchetypeId)))
                    _enemyPrefabMap.Add(Normalize(LegacyChaserArchetypeId), legacyEnemyChaserPrefab);
            }
        }

        private bool TryResolveEnemyPrefab(string archetypeId, out GameObject prefab, out string resolvedArchetypeId)
        {
            prefab = null;
            resolvedArchetypeId = string.Empty;

            string requestedKey = Normalize(archetypeId);
            if (!string.IsNullOrEmpty(requestedKey) &&
                _enemyPrefabMap.TryGetValue(requestedKey, out prefab) &&
                prefab != null)
            {
                resolvedArchetypeId = requestedKey;
                return true;
            }

            if (_defaultEnemyPrefab == null)
                return false;

            prefab = _defaultEnemyPrefab;
            resolvedArchetypeId = Normalize(_defaultEnemyArchetypeId);

            string warnKey = string.IsNullOrWhiteSpace(archetypeId) ? "<empty>" : archetypeId;
            if (_missingEnemyArchetypesLogged.Add(warnKey))
            {
                string fallbackKey = string.IsNullOrEmpty(resolvedArchetypeId) ? "<first-entry>" : resolvedArchetypeId;
                Debug.LogWarning(
                    $"[{nameof(DimensionRenderer)}] Unknown enemy archetype '{warnKey}'. Falling back to '{fallbackKey}'.",
                    this);
            }

            return true;
        }

        private bool TryResolveProp(string propId, out HexWorldPropDefinition def)
        {
            def = null;
            if (string.IsNullOrWhiteSpace(propId))
                return false;

            string key = Normalize(propId);
            if (_propsByKey.TryGetValue(key, out def))
                return def != null;

            // Extra hardening: direct case-insensitive comparisons against serialized fields.
            if (registry != null && registry.allProps != null)
            {
                for (int i = 0; i < registry.allProps.Count; i++)
                {
                    HexWorldPropDefinition candidate = registry.allProps[i];
                    if (!candidate)
                        continue;

                    if (string.Equals(candidate.id, propId, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(candidate.displayName, propId, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(candidate.name, propId, StringComparison.OrdinalIgnoreCase))
                    {
                        def = candidate;
                        return true;
                    }
                }
            }

            foreach (var kv in _propsByKey)
            {
                if (kv.Key.Contains(key) || key.Contains(kv.Key))
                {
                    def = kv.Value;
                    return def != null;
                }
            }

            if (_missingPropIdsLogged.Add(propId))
                Debug.LogWarning($"[{nameof(DimensionRenderer)}] No prop definition/prefab found for propId '{propId}'.", this);

            return false;
        }

        private void EnsureRegistryReference()
        {
            if (registry != null)
                return;

#if UNITY_EDITOR
            registry = UnityEditor.AssetDatabase.LoadAssetAtPath<PropRegistry>(
                "Assets/Minigames/HexWorld3D/Definitions/PropRegistry_Main.asset");
#endif

            if (registry == null && !_missingRegistryWarned)
            {
                Debug.LogWarning($"[{nameof(DimensionRenderer)}] Missing PropRegistry reference; prop spawning will be disabled.");
                _missingRegistryWarned = true;
            }
        }

        private HexWorldTileStyle ResolveStyle(string biomeGroup, HexCoord coord, DimensionLayout layout)
        {
            if (_stylesByBiome.Count == 0)
                return null;

            string key = Normalize(biomeGroup);
            if (!_stylesByBiome.TryGetValue(key, out List<HexWorldTileStyle> list) || list == null || list.Count == 0)
            {
                foreach (var kv in _stylesByBiome)
                {
                    if (kv.Value != null && kv.Value.Count > 0)
                    {
                        list = kv.Value;
                        break;
                    }
                }
            }

            if (list == null || list.Count == 0)
                return null;

            if (list.Count == 1)
                return list[0];

            float maxDist = 1f;
            float currentDist = 0f;
            if (layout != null)
            {
                maxDist = Mathf.Max(1f, layout.startCoord.DistanceTo(layout.bossCoord));
                currentDist = coord.DistanceTo(layout.startCoord);
            }

            float ratio = Mathf.Clamp01(currentDist / maxDist);
            int index = Mathf.Min(list.Count - 1, Mathf.FloorToInt(ratio * list.Count));

            return list[index];
        }

        private HexWorldTileStyle ResolveStyleById(string styleId)
        {
            if (string.IsNullOrWhiteSpace(styleId))
                return null;

            string key = Normalize(styleId);
            if (_stylesById.TryGetValue(key, out HexWorldTileStyle style) && style != null)
                return style;

            return null;
        }

        private Vector3 AxialToWorld(HexCoord c)
        {
            float x = hexSize * (1.5f * c.q);
            float z = hexSize * (Mathf.Sqrt(3f) * (c.r + c.q * 0.5f));
            return new Vector3(x, 0f, z);
        }

        private HexCoord WorldToAxial(Vector3 worldPos)
        {
            float size = Mathf.Max(0.0001f, hexSize);
            float q = (2f / 3f * worldPos.x) / size;
            float r = ((-1f / 3f * worldPos.x) + (Mathf.Sqrt(3f) / 3f * worldPos.z)) / size;
            return RoundAxial(q, r);
        }

        private static HexCoord RoundAxial(float q, float r)
        {
            float cubeX = q;
            float cubeZ = r;
            float cubeY = -cubeX - cubeZ;

            int roundedX = Mathf.RoundToInt(cubeX);
            int roundedY = Mathf.RoundToInt(cubeY);
            int roundedZ = Mathf.RoundToInt(cubeZ);

            float xDiff = Mathf.Abs(roundedX - cubeX);
            float yDiff = Mathf.Abs(roundedY - cubeY);
            float zDiff = Mathf.Abs(roundedZ - cubeZ);

            if (xDiff > yDiff && xDiff > zDiff)
                roundedX = -roundedY - roundedZ;
            else if (yDiff > zDiff)
                roundedY = -roundedX - roundedZ;
            else
                roundedZ = -roundedX - roundedY;

            return new HexCoord(roundedX, roundedZ);
        }

        private bool TryGetPlayerSpawnOrigin(out Vector3 origin)
        {
            origin = Vector3.zero;
            Transform playerTransform = null;

            if (_spawnedPlayer != null)
                playerTransform = _spawnedPlayer.transform;

            if (playerTransform == null)
            {
                GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
                if (taggedPlayer != null)
                {
                    _spawnedPlayer = taggedPlayer;
                    playerTransform = taggedPlayer.transform;
                }
            }

            if (playerTransform == null)
                return false;

            origin = playerTransform.position;
            origin.y = 0f;
            return true;
        }

        private static bool TryResolveTileByCoord(DimensionLayout layout, HexCoord coord, out DimensionTileData result)
        {
            result = default;
            if (layout == null || layout.tiles == null)
                return false;

            for (int i = 0; i < layout.tiles.Count; i++)
            {
                DimensionTileData tile = layout.tiles[i];
                if (tile.coord.Equals(coord))
                {
                    result = tile;
                    return true;
                }
            }

            return false;
        }

        private bool TryEnsureSpawnLookupCache(DimensionLayout layout)
        {
            if (layout == null || layout.tiles == null || layout.tiles.Count == 0)
                return false;

            if (ReferenceEquals(_spawnLookupLayout, layout) &&
                _spawnWalkableSetCache != null &&
                _spawnLookupTileCount == layout.tiles.Count &&
                _spawnTileByCoord.Count > 0)
            {
                return true;
            }

            _spawnLookupLayout = layout;
            _spawnLookupTileCount = layout.tiles.Count;
            _spawnWalkableSetCache = layout.BuildWalkableSet();
            _spawnTileByCoord.Clear();

            for (int i = 0; i < layout.tiles.Count; i++)
            {
                DimensionTileData tile = layout.tiles[i];
                if (!_spawnTileByCoord.ContainsKey(tile.coord))
                    _spawnTileByCoord.Add(tile.coord, tile);
            }

            return _spawnWalkableSetCache != null && _spawnWalkableSetCache.Count > 0 && _spawnTileByCoord.Count > 0;
        }

        private void FocusMainCameraOnTile(HexCoord coord)
        {
            Camera cam = Camera.main;
            if (cam == null)
                cam = FindFirstSceneCamera();

            if (cam == null)
                return;

            Vector3 target = AxialToWorld(coord);
            Transform tr = cam.transform;
            Vector3 forward = tr.forward;

            Vector3 nextPosition;
            if (Mathf.Abs(forward.y) > 0.0001f)
            {
                float rayDistance = (tr.position.y - target.y) / -forward.y;
                nextPosition = target - forward * rayDistance;
            }
            else
            {
                nextPosition = tr.position;
                nextPosition.x = target.x;
                nextPosition.z = target.z;
            }

            tr.position = new Vector3(nextPosition.x, tr.position.y, nextPosition.z);

            GameObject focus = GameObject.Find("CameraFocus_Origin");
            if (focus != null)
                focus.transform.position = target;
        }

        private void SpawnPlayerAt(HexCoord coord)
        {
            if (playerPrefab == null)
                return;

            Vector3 spawnPos = AxialToWorld(coord);

            if (_spawnedPlayer != null)
            {
                if (Application.isPlaying)
                    Destroy(_spawnedPlayer);
                else
                    DestroyImmediate(_spawnedPlayer);
            }

            _spawnedPlayer = Instantiate(playerPrefab, spawnPos, Quaternion.identity, propsRoot);
            _spawnedPlayer.name = "DungeonPlayer";

            // Health foundation for extraction reset + future enemy damage hooks.
            if (_spawnedPlayer.GetComponent<PlayerHealth>() == null)
                _spawnedPlayer.AddComponent<PlayerHealth>();

            Camera cam = Camera.main;
            if (cam == null)
                cam = FindFirstSceneCamera();

            if (cam != null)
            {
                HexCameraPanZoom3D orbitCam = cam.GetComponent<HexCameraPanZoom3D>();
                if (orbitCam != null)
                {
                    orbitCam.SetOrbitTarget(_spawnedPlayer.transform, true);
                }
                else
                {
                    GameObject focus = GameObject.Find("CameraFocus_Origin");
                    if (focus != null)
                        focus.transform.position = spawnPos;
                }
            }
        }

        private void SpawnPerimeterGuards(DimensionLayout layout)
        {
            if (layout == null || voidGuardPrefab == null)
                return;

            EnsureRoots();

            HashSet<HexCoord> walkableSet = layout.BuildWalkableSet();
            if (walkableSet == null || walkableSet.Count == 0)
                return;

            var boundaryCoords = new HashSet<HexCoord>();
            foreach (HexCoord coord in walkableSet)
            {
                for (int i = 0; i < HexCoord.NeighborDirs.Length; i++)
                {
                    HexCoord neighbor = coord.Neighbor(i);
                    if (!walkableSet.Contains(neighbor))
                        boundaryCoords.Add(neighbor);
                }
            }

            foreach (HexCoord coord in boundaryCoords)
            {
                Vector3 pos = AxialToWorld(coord);
                GameObject guard = Instantiate(voidGuardPrefab, pos, Quaternion.identity, boundariesRoot);
                guard.name = $"VoidGuard_{coord.q}_{coord.r}";
            }
        }

        private static Camera FindFirstSceneCamera()
        {
            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera cam = cameras[i];
                if (cam != null && cam.gameObject.scene.IsValid())
                    return cam;
            }

            return null;
        }

        private static float HashTo01(HexCoord coord, int seed)
        {
            uint h = (uint)seed;
            h = (h * 16777619u) ^ (uint)(coord.q * 73856093);
            h = (h * 16777619u) ^ (uint)(coord.r * 19349663);
            return (h & 0x00FFFFFF) / 16777215f;
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            char[] chars = value.Trim().ToUpperInvariant().ToCharArray();
            int j = 0;
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (char.IsLetterOrDigit(c))
                    chars[j++] = c;
            }
            return new string(chars, 0, j);
        }
    }
}
