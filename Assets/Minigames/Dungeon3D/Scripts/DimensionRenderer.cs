using System;
using System.Collections.Generic;
using GalacticFishing.Minigames.HexWorld;
using UnityEngine;

namespace GalacticFishing.Minigames.Dungeon3D
{
    public sealed class DimensionRenderer : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private DimensionGenerator generator;
        [SerializeField] private DimensionGenProfile profile;
        [SerializeField] private GameObject ownedPrefab;
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private Transform tilesRoot;
        [SerializeField] private Transform propsRoot;
        [SerializeField] private HexWorldPropDefinition[] propDefinitions;

        [Header("Layout")]
        [SerializeField, Min(0.05f)] private float hexSize = 1f;
        [SerializeField] private bool regenerateOnEnable;
        [SerializeField] private bool renderCurrentOnEnable = true;
        [SerializeField] private bool clearOnDisable;
        [SerializeField] private bool deterministicStylePick = true;
        [SerializeField] private bool verboseLogging;

        private readonly List<GameObject> _spawnedTiles = new();
        private readonly List<GameObject> _spawnedProps = new();
        private readonly Dictionary<string, List<HexWorldTileStyle>> _stylesByBiome = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, HexWorldPropDefinition> _propsByKey = new(StringComparer.OrdinalIgnoreCase);
        private GameObject _spawnedPlayer;

        private void OnEnable()
        {
            if (!generator)
                generator = GetComponent<DimensionGenerator>();

            EnsureRoots();
            RebuildPropCache();

            if (generator)
                generator.OnGenerated += HandleGenerated;

            if (renderCurrentOnEnable && generator && generator.Layout != null && generator.Layout.tiles != null && generator.Layout.tiles.Count > 0)
                RenderLayout(generator.Layout);

            if (regenerateOnEnable && generator && (generator.Layout == null || generator.Layout.tiles == null || generator.Layout.tiles.Count == 0))
                generator.Regenerate();
        }

        private void OnDisable()
        {
            if (generator)
                generator.OnGenerated -= HandleGenerated;

            if (clearOnDisable)
                Clear();
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
            _spawnedTiles.Clear();
            _spawnedProps.Clear();
            _spawnedPlayer = null;
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
            Clear();

            for (int i = 0; i < layout.tiles.Count; i++)
            {
                DimensionTileData tile = layout.tiles[i];
                Vector3 tilePos = AxialToWorld(tile.coord);

                GameObject tileGo = Instantiate(ownedPrefab, tilePos, Quaternion.identity, tilesRoot);
                tileGo.name = $"Tile_{tile.coord.q}_{tile.coord.r}_{tile.biomeGroup}";
                _spawnedTiles.Add(tileGo);

                HexWorldTileStyle style = ResolveStyle(tile.biomeGroup, tile.coord, layout.seedUsed);
                if (style != null)
                {
                    var visual = tileGo.GetComponent<HexTileVisual>() ?? tileGo.GetComponentInChildren<HexTileVisual>(true);
                    if (visual != null)
                        visual.ApplyStyle(style);
                }

                if (!tile.hasProp)
                    continue;

                if (!TryResolveProp(tile.propId, out HexWorldPropDefinition propDef) || !propDef || !propDef.prefab)
                    continue;

                float angle = HashTo01(tile.coord, layout.seedUsed) * 360f;
                GameObject propGo = Instantiate(propDef.prefab, tilePos, Quaternion.Euler(0f, angle, 0f), propsRoot);
                propGo.transform.localScale = Vector3.one * Mathf.Max(0.001f, propDef.scale);
                propGo.name = $"Prop_{propDef.name}_{tile.coord.q}_{tile.coord.r}";
                _spawnedProps.Add(propGo);
            }

            SpawnPlayerAt(layout.startCoord);
            FocusMainCameraOnTile(layout.startCoord);

            if (verboseLogging)
            {
                Debug.Log(
                    $"[{nameof(DimensionRenderer)}] Rendered {layout.tiles.Count} tiles, " +
                    $"{_spawnedProps.Count} props (seed {layout.seedUsed}).",
                    this);
            }
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
                        list.Add(style);
                }
            }
        }

        private void RebuildPropCache()
        {
            _propsByKey.Clear();
            if (propDefinitions == null)
                return;

            for (int i = 0; i < propDefinitions.Length; i++)
            {
                HexWorldPropDefinition def = propDefinitions[i];
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

        private bool TryResolveProp(string propId, out HexWorldPropDefinition def)
        {
            def = null;
            if (string.IsNullOrWhiteSpace(propId))
                return false;

            string key = Normalize(propId);
            if (_propsByKey.TryGetValue(key, out def))
                return def;

            foreach (var kv in _propsByKey)
            {
                if (kv.Key.Contains(key) || key.Contains(kv.Key))
                {
                    def = kv.Value;
                    return def;
                }
            }

            return false;
        }

        private HexWorldTileStyle ResolveStyle(string biomeGroup, HexCoord coord, int seed)
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

            int index;
            if (deterministicStylePick)
            {
                int hash = seed;
                hash = unchecked(hash * 397) ^ coord.q;
                hash = unchecked(hash * 397) ^ coord.r;
                index = Mathf.Abs(hash) % list.Count;
            }
            else
            {
                index = UnityEngine.Random.Range(0, list.Count);
            }

            return list[index];
        }

        private Vector3 AxialToWorld(HexCoord c)
        {
            float x = hexSize * (1.5f * c.q);
            float z = hexSize * (Mathf.Sqrt(3f) * (c.r + c.q * 0.5f));
            return new Vector3(x, 0f, z);
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
