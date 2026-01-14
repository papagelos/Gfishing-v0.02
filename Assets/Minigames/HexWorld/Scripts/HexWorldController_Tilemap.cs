using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

namespace GalacticFishing.Minigames.HexWorld
{
    public sealed class HexWorldController_Tilemap : MonoBehaviour
    {
        [Header("References")]
        public Camera worldCamera;
        public GridLayout grid;

        [Header("Tilemaps")]
        public Tilemap placedTilemap;
        public Tilemap frontierTilemap;
        public Tilemap highlightTilemap;

        [Header("Catalog / Selection")]
        public TileCatalog catalog;
        public BuildPaletteUI paletteUI;
        public bool allowRandomIfNothingSelected = true;

        [Header("Coordinate Mode (Flat-top hex offset)")]
        public HexOffset.OffsetMode offsetMode = HexOffset.OffsetMode.OddQ;

        [Header("Frontier Visuals")]
        public Sprite frontierOutlineSprite;
        public Color frontierAffordableColor = new Color(1f, 1f, 1f, 0.60f);
        public Color frontierUnaffordableColor = new Color(1f, 0.35f, 0.35f, 0.60f);

        [Header("Highlight Visuals (optional)")]
        public Sprite highlightSprite;
        public Color highlightColor = new Color(1f, 1f, 1f, 0.35f);

        [Header("Economy")]
        public double startCredits = 250;
        public int baseTileCost = 50;
        public float costGrowth = 1.08f;
        public int distanceCost = 3;

        [Header("HUD")]
        public TMP_Text creditsText;
        public TMP_Text toastText;
        public float toastSeconds = 1.25f;

        private sealed class TileInstance
        {
            public TileDefinition def;
            public TileContribution contrib;
        }

        private readonly Dictionary<HexCoord, TileInstance> _placed = new();
        private readonly HashSet<HexCoord> _frontier = new();

        private readonly Dictionary<TileDefinition, TileBase> _runtimeTiles = new();
        private TileBase _frontierTile;
        private TileBase _highlightTile;

        private readonly HexWorldEconomy _economy = new();

        private float _toastTimer;
        private HexCoord? _lastHover;

        private static readonly HexCoord ORIGIN = new HexCoord(0, 0);

        private void Awake()
        {
            if (!worldCamera) worldCamera = Camera.main;
        }

        private void Start()
        {
            // sanity
            if (!grid) grid = FindFirstObjectByType<GridLayout>();

            // create runtime tiles
            _frontierTile = MakeRuntimeTile(frontierOutlineSprite);
            _highlightTile = MakeRuntimeTile(highlightSprite ? highlightSprite : frontierOutlineSprite);

            _economy.OnChanged += UpdateHud;
            _economy.Init(startCredits);

            // Start tile at origin
            var startDef = GetSelectedOrDefaultTile();
            PlaceTileInternal(ORIGIN, startDef);

            // Create frontier around origin
            EnsureFrontierAround(ORIGIN);
            RefreshFrontierColors();

            UpdateHud();
        }

        private void Update()
        {
            _economy.Tick(Time.deltaTime);

            HandleHoverHighlight();
            HandleClick();

            if (_toastTimer > 0f)
            {
                _toastTimer -= Time.deltaTime;
                if (_toastTimer <= 0f && toastText) toastText.text = "";
            }
        }

        // ---------------- Input ----------------

        private void HandleClick()
        {
            if (Mouse.current == null) return;
            if (!Mouse.current.leftButton.wasPressedThisFrame) return;

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            var world = worldCamera.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            var cell = grid.WorldToCell(world);
            var axial = CellToAxial(cell);

            if (_frontier.Contains(axial))
            {
                TryPurchase(axial);
                return;
            }

            if (_placed.TryGetValue(axial, out var inst) && inst != null && inst.def != null)
            {
                Toast($"{inst.def.displayName} @ {axial}");
                return;
            }
        }

        private void HandleHoverHighlight()
        {
            if (!highlightTilemap || Mouse.current == null) return;

            var world = worldCamera.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            var cell = grid.WorldToCell(world);
            var axial = CellToAxial(cell);

            if (_lastHover.HasValue && _lastHover.Value == axial)
                return;

            // clear old
            if (_lastHover.HasValue)
            {
                var oldCell = AxialToCell(_lastHover.Value);
                highlightTilemap.SetTile(oldCell, null);
            }

            _lastHover = axial;

            // only show highlight if it's a placed tile or frontier
            if (_placed.ContainsKey(axial) || _frontier.Contains(axial))
            {
                var newCell = AxialToCell(axial);
                highlightTilemap.SetTile(newCell, _highlightTile);
                highlightTilemap.SetTileFlags(newCell, TileFlags.None);
                highlightTilemap.SetColor(newCell, highlightColor);
            }
        }

        // ---------------- Core gameplay ----------------

        private void TryPurchase(HexCoord at)
        {
            if (_placed.ContainsKey(at)) return;
            if (!_frontier.Contains(at)) return;

            double cost = GetTileCost(at);
            if (!_economy.CanAfford(cost))
            {
                Toast($"Need {cost:0} credits");
                RefreshFrontierColors();
                return;
            }

            var def = GetSelectedOrDefaultTile();
            if (!def)
            {
                Toast("No tile selected (and random disabled / empty catalog).");
                return;
            }

            if (!_economy.SpendCredits(cost))
            {
                Toast($"Need {cost:0} credits");
                RefreshFrontierColors();
                return;
            }

            // Remove frontier tile visually + logically
            RemoveFrontier(at);

            // Place tile
            PlaceTileInternal(at, def);

            // Add new frontier around this tile
            EnsureFrontierAround(at);

            // Only recompute contributions for affected tiles (tile + its placed neighbors)
            RecomputeContribFor(at);
            for (int i = 0; i < 6; i++)
            {
                var n = at.Neighbor(i);
                if (_placed.ContainsKey(n))
                    RecomputeContribFor(n);
            }

            RefreshFrontierColors();
        }

        private void PlaceTileInternal(HexCoord at, TileDefinition def)
        {
            if (!def)
            {
                Debug.LogError("PlaceTileInternal called with null TileDefinition.");
                return;
            }

            // create instance
            var inst = new TileInstance
            {
                def = def,
                contrib = TileContribution.CreateEmpty()
            };
            _placed[at] = inst;

            // paint tile
            var cell = AxialToCell(at);
            placedTilemap.SetTile(cell, GetOrCreateTile(def));
            placedTilemap.SetColor(cell, Color.white);
        }

        private void EnsureFrontierAround(HexCoord placedCoord)
        {
            for (int i = 0; i < 6; i++)
            {
                var n = placedCoord.Neighbor(i);
                if (_placed.ContainsKey(n)) continue;
                if (_frontier.Contains(n)) continue;

                AddFrontier(n);
            }
        }

        private void AddFrontier(HexCoord at)
        {
            _frontier.Add(at);

            var cell = AxialToCell(at);
            frontierTilemap.SetTile(cell, _frontierTile);
            frontierTilemap.SetTileFlags(cell, TileFlags.None);
            // color set by RefreshFrontierColors
        }

        private void RemoveFrontier(HexCoord at)
        {
            _frontier.Remove(at);

            var cell = AxialToCell(at);
            frontierTilemap.SetTile(cell, null);
        }

        private void RefreshFrontierColors()
        {
            foreach (var f in _frontier)
            {
                var cell = AxialToCell(f);
                frontierTilemap.SetTileFlags(cell, TileFlags.None);

                double cost = GetTileCost(f);
                frontierTilemap.SetColor(cell, _economy.CanAfford(cost) ? frontierAffordableColor : frontierUnaffordableColor);
            }
        }

        // ---------------- Economy / contributions (incremental) ----------------

        private void RecomputeContribFor(HexCoord at)
        {
            if (!_placed.TryGetValue(at, out var inst) || inst == null || inst.def == null) return;

            // remove old
            _economy.SubtractContribution(inst.contrib);

            // recompute
            inst.contrib.Clear();

            // base production
            if (inst.def.baseProductionPerSecond != null)
            {
                foreach (var r in inst.def.baseProductionPerSecond)
                    inst.contrib.prodPerSec[(int)r.type] += r.perSecond;
            }

            // base stats
            if (inst.def.statBonuses != null)
            {
                foreach (var s in inst.def.statBonuses)
                    inst.contrib.stats[(int)s.type] += s.amount;
            }

            // adjacency
            if (inst.def.neighborBonuses != null && inst.def.neighborBonuses.Count > 0)
            {
                for (int i = 0; i < 6; i++)
                {
                    var n = at.Neighbor(i);
                    if (!_placed.TryGetValue(n, out var nInst) || nInst == null || nInst.def == null)
                        continue;

                    var nCat = nInst.def.category;

                    foreach (var rule in inst.def.neighborBonuses)
                    {
                        if (rule == null) continue;
                        if (rule.requiredNeighborCategory != nCat) continue;

                        if (rule.bonusProductionPerNeighborPerSecond != null)
                        {
                            foreach (var r in rule.bonusProductionPerNeighborPerSecond)
                                inst.contrib.prodPerSec[(int)r.type] += r.perSecond;
                        }

                        if (rule.bonusStatsPerNeighbor != null)
                        {
                            foreach (var s in rule.bonusStatsPerNeighbor)
                                inst.contrib.stats[(int)s.type] += s.amount;
                        }
                    }
                }
            }

            // add new
            _economy.AddContribution(inst.contrib);
        }

        private double GetTileCost(HexCoord at)
        {
            int owned = _placed.Count;
            double growthCost = baseTileCost * System.Math.Pow(costGrowth, owned);
            int dist = at.DistanceTo(ORIGIN);
            return System.Math.Round(growthCost + dist * distanceCost);
        }

        // ---------------- Tiles / selection ----------------

      private TileDefinition GetSelectedOrDefaultTile()
{
    if (paletteUI && paletteUI.Selected) return paletteUI.Selected;

    if (catalog)
    {
        if (allowRandomIfNothingSelected)
        {
            var r = catalog.GetRandomWeighted();
            if (r) return r;
        }

        if (catalog.tiles != null && catalog.tiles.Count > 0)
            return catalog.tiles[0];
    }

    return null;
}

        private TileBase GetOrCreateTile(TileDefinition def)
        {
            if (!def) return null;
            if (_runtimeTiles.TryGetValue(def, out var t) && t != null) return t;

            var created = MakeRuntimeTile(def.sprite);
            _runtimeTiles[def] = created;
            return created;
        }

        private static TileBase MakeRuntimeTile(Sprite sprite)
        {
            if (!sprite) return null;
            var t = ScriptableObject.CreateInstance<Tile>();
            t.sprite = sprite;
            t.color = Color.white;
            t.colliderType = Tile.ColliderType.None;
            return t;
        }

        // ---------------- Axial <-> Cell ----------------

        private HexCoord CellToAxial(Vector3Int cell)
        {
            return HexOffset.OffsetToAxial(cell.x, cell.y, offsetMode);
        }

        private Vector3Int AxialToCell(HexCoord a)
        {
            var off = HexOffset.AxialToOffset(a, offsetMode);
            return new Vector3Int(off.x, off.y, 0);
        }

        // ---------------- HUD ----------------

        private void UpdateHud()
        {
            if (creditsText)
                creditsText.text = $"Credits: {_economy.Get(ResourceType.Credits):0}  (+{_economy.GetProduction(ResourceType.Credits):0.00}/s)";
        }

        private void Toast(string msg)
        {
            if (!toastText) return;
            toastText.text = msg;
            _toastTimer = toastSeconds;
        }
    }
}
