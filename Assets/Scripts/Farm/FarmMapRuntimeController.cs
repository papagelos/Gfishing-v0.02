using UnityEngine;
using UnityEngine.Tilemaps;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem; // NEW input system

public class FarmMapRuntimeController : MonoBehaviour
{
    [Header("Scene")]
    public Camera cam;
    public Grid grid;

    [Header("Tilemaps (same Grid)")]
    public Tilemap unlockMap;    // locked overlay lives here
    public Tilemap terrainMap;   // grass / industrial
    public Tilemap buildingMap;  // forestry station etc
    public Tilemap highlightMap; // selection outline/glow

    [Header("Tiles")]
    public TileBase tileLockedOverlay;
    public TileBase tileHighlight;

    public TileBase tileGrass;
    public TileBase tileIndustrial;
    public TileBase tileForestry;

    [Header("Background bounds (for auto sizing)")]
    public SpriteRenderer bgBL;
    public SpriteRenderer bgBR;
    public SpriteRenderer bgTL;
    public SpriteRenderer bgTR;

    [Header("UI")]
    public TMP_Text infoText;

    [Header("Costs (hook up later to real currency)")]
    public int unlockCost = 1000;
    public int grassCost = 500;
    public int industrialCost = 800;
    public int forestryCost = 2500;

    [Header("Debug")]
    public bool debugFreeBuild = true; // true = ignores costs

    BoundsInt _cellBounds;
    Vector3Int? _selected;
    Vector3Int? _prevHighlight;

    void Awake()
    {
        if (!cam) cam = Camera.main;
    }

    void Start()
    {
        ComputeCellBoundsFromBackground();
        LockAllCells();
        UpdateInfo();
    }

    void Update()
    {
        HandleClickSelect();
        HandleActions();
    }

    // ----------------- Core Logic -----------------

    void HandleClickSelect()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        if (!mouse.leftButton.wasPressedThisFrame) return;

        // ignore UI clicks
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        Vector3 world = ScreenToWorld(mouse.position.ReadValue());
        Vector3Int cell = grid.WorldToCell(world);

        if (!_cellBounds.Contains(cell))
            return;

        _selected = cell;

        // update highlight (clear only previous)
        if (_prevHighlight != null && highlightMap != null)
            highlightMap.SetTile(_prevHighlight.Value, null);

        if (highlightMap != null && tileHighlight != null)
            highlightMap.SetTile(cell, tileHighlight);

        _prevHighlight = cell;

        UpdateInfo();
    }

    void HandleActions()
    {
        if (_selected == null) return;

        var kb = Keyboard.current;
        if (kb == null) return;

        var cell = _selected.Value;

        bool locked = IsLocked(cell);
        bool hasTerrain = terrainMap != null && terrainMap.GetTile(cell) != null;
        bool hasBuilding = buildingMap != null && buildingMap.GetTile(cell) != null;

        // U = unlock
        if (kb.uKey.wasPressedThisFrame)
        {
            if (locked && TrySpend(unlockCost))
            {
                SetLocked(cell, false);
                UpdateInfo();
            }
            return;
        }

        // Delete / Backspace = remove topmost (building first, then terrain)
        if (kb.deleteKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame)
        {
            if (!locked)
            {
                if (hasBuilding)
                    buildingMap.SetTile(cell, null);
                else if (hasTerrain)
                    terrainMap.SetTile(cell, null);

                UpdateInfo();
            }
            return;
        }

        // if locked, nothing else allowed
        if (locked) return;

        // Terrain placement only when no terrain
        if (!hasTerrain)
        {
            if (kb.digit1Key.wasPressedThisFrame) // grass
            {
                if (tileGrass != null && TrySpend(grassCost))
                    terrainMap.SetTile(cell, tileGrass);
                UpdateInfo();
            }
            else if (kb.digit2Key.wasPressedThisFrame) // industrial
            {
                if (tileIndustrial != null && TrySpend(industrialCost))
                    terrainMap.SetTile(cell, tileIndustrial);
                UpdateInfo();
            }
            return;
        }

        // Building placement only when terrain exists and building empty
        if (hasTerrain && !hasBuilding)
        {
            if (kb.bKey.wasPressedThisFrame)
            {
                if (tileForestry != null && TrySpend(forestryCost))
                    buildingMap.SetTile(cell, tileForestry);

                UpdateInfo();
            }
        }
    }

    // ----------------- Locked overlay -----------------

    void LockAllCells()
    {
        if (unlockMap == null || tileLockedOverlay == null) return;

        unlockMap.ClearAllTiles();

        for (int x = _cellBounds.xMin; x < _cellBounds.xMax; x++)
        for (int y = _cellBounds.yMin; y < _cellBounds.yMax; y++)
            unlockMap.SetTile(new Vector3Int(x, y, 0), tileLockedOverlay);
    }

    bool IsLocked(Vector3Int cell)
    {
        if (unlockMap == null) return false;
        return unlockMap.GetTile(cell) != null; // locked if overlay exists
    }

    void SetLocked(Vector3Int cell, bool locked)
    {
        if (unlockMap == null) return;
        unlockMap.SetTile(cell, locked ? tileLockedOverlay : null);
    }

    // ----------------- Bounds -----------------

    void ComputeCellBoundsFromBackground()
    {
        Bounds b = bgBL.bounds;
        b.Encapsulate(bgBR.bounds);
        b.Encapsulate(bgTL.bounds);
        b.Encapsulate(bgTR.bounds);

        Vector3 minW = b.min + new Vector3(0.001f, 0.001f, 0f);
        Vector3 maxW = b.max - new Vector3(0.001f, 0.001f, 0f);

        Vector3Int minC = grid.WorldToCell(minW);
        Vector3Int maxC = grid.WorldToCell(maxW);

        int w = (maxC.x - minC.x) + 1;
        int h = (maxC.y - minC.y) + 1;

        _cellBounds = new BoundsInt(minC.x, minC.y, 0, w, h, 1);
    }

    // ----------------- UI -----------------

    void UpdateInfo()
    {
        if (!infoText) return;

        if (_selected == null)
        {
            infoText.text = "";
            return;
        }

        var cell = _selected.Value;
        bool locked = IsLocked(cell);

        var terrain = terrainMap != null ? terrainMap.GetTile(cell) : null;
        var building = buildingMap != null ? buildingMap.GetTile(cell) : null;

        if (locked)
        {
            infoText.text = $"LOCKED — Unlock: {unlockCost:n0} credits (press U)";
            return;
        }

        if (building != null)
        {
            infoText.text = $"Building: {building.name}  (Delete removes building)";
            return;
        }

        if (terrain != null)
        {
            infoText.text = $"Terrain: {terrain.name} — Place Building: Forestry Station ({forestryCost:n0}) (press B) — Delete removes terrain";
            return;
        }

        infoText.text = $"EMPTY — Place Terrain: 1=Grass ({grassCost:n0})  2=Industrial ({industrialCost:n0})";
    }

    bool TrySpend(int cost)
    {
        if (debugFreeBuild) return true;

        // TODO: hook into your real currency system.
        // Return true if you can afford and it deducts, false otherwise.
        return true;
    }

    Vector3 ScreenToWorld(Vector2 screen)
    {
        Vector3 w = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 0f));
        w.z = 0f;
        return w;
    }
}
