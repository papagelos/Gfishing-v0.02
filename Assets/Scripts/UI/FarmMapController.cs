using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace GalacticFishing
{
    public class FarmMapController : MonoBehaviour
    {
        [Header("Scene refs")]
        [SerializeField] private Camera cam;

        [System.Serializable]
        private class QuadrantMaps
        {
            public Grid grid;

            [Header("Tilemaps (children of this Grid)")]
            public Tilemap unlockMap;      // locked overlay
            public Tilemap unlockedMap;    // optional placeholder layer
            public Tilemap terrainMap;     // grass/industrial
            public Tilemap buildingMap;    // lumbermill etc
            public Tilemap highlightMap;   // selection
        }

        [Header("Quadrant grids + tilemaps")]
        [SerializeField] private QuadrantMaps mapsBL = new QuadrantMaps();
        [SerializeField] private QuadrantMaps mapsBR = new QuadrantMaps();
        [SerializeField] private QuadrantMaps mapsTL = new QuadrantMaps();
        [SerializeField] private QuadrantMaps mapsTR = new QuadrantMaps();

[Header("Fixed tile counts (override background->cell bounds)")]
[SerializeField] private bool useFixedQuadrantTileCounts = true;
[SerializeField] private int quadrantTilesX = 15;
[SerializeField] private int quadrantTilesY = 10;
[SerializeField] private int quadrantExtraMarginTiles = 2; // extra cells around the edge


        [Header("Tiles")]
        [SerializeField] private TileBase tileLockedOverlay;
        [SerializeField] private TileBase tileUnlockedPlaceholder; // OPTIONAL
        [SerializeField] private TileBase tileHighlight;
        [SerializeField] private TileBase tileGrass;
        [SerializeField] private TileBase tileIndustrial;
        [SerializeField] private TileBase tileLumbermill;

        [Header("Background (four quadrants)")]
        [SerializeField] private SpriteRenderer bgBL;
        [SerializeField] private SpriteRenderer bgBR;
        [SerializeField] private SpriteRenderer bgTL;
        [SerializeField] private SpriteRenderer bgTR;

        [Header("UI")]
        [SerializeField] private TMP_Text infoText;

        [Header("Quadrant navigation UI")]
        [SerializeField] private Button navUp;
        [SerializeField] private Button navDown;
        [SerializeField] private Button navLeft;
        [SerializeField] private Button navRight;

        [SerializeField] private TMP_Text quadrantLockedText;
        [SerializeField] private string lockedMessageFormat = "-LOCKED-\nREACH WORLD {0} TO UNLOCK";

        [Header("Locked message requirements (by quadrant)")]
        [SerializeField] private int worldReqBL = 2;
        [SerializeField] private int worldReqBR = 3;
        [SerializeField] private int worldReqTL = 1;
        [SerializeField] private int worldReqTR = 4;

        [Header("Costs (debug for now)")]
        [SerializeField] private int unlockCost = 1000;
        [SerializeField] private int grassCost = 500;
        [SerializeField] private int industrialCost = 800;
        [SerializeField] private int lumbermillCost = 2500;
        [SerializeField] private bool debugFreeBuild = true; // ignores costs

        [Header("Camera controls")]
        [SerializeField] private float keyPanSpeed = 12f;
        [SerializeField] private float dragPanSpeed = 1.0f;
        [SerializeField] private float zoomSpeed = 3.0f;
        [SerializeField] private float minOrthoSize = 2f;
        [SerializeField] private bool clampCameraToMap = true;
        [SerializeField] private float focusLerpSpeed = 10f;

        [Header("Zoom gating")]
        [Tooltip("Zoom is enabled only when at least this many quadrants are unlocked.")]
        [SerializeField] private int zoomUnlockAtUnlockedQuadrants = 2;

        [Tooltip("When zoom is locked, camera is forced to the current quadrant center and free pan/drag is disabled.")]
        [SerializeField] private bool lockCameraToQuadrantWhenZoomLocked = true;

        [Tooltip("If true, even after zoom unlock, we still use paging (buttons/arrow keys) and keep free pan/drag off.")]
        [SerializeField] private bool keepPagingNavigationEvenWhenZoomUnlocked = true;

        [Tooltip("Tiny padding used when fitting camera to bounds. Keep near 1.0 to avoid seeing beyond the background.")]
        [SerializeField] private float fitPadding = 1.001f;

        [System.Serializable]
        private class QuadrantMargins
        {
            public int left = 1;
            public int right = 1;
            public int top = 1;
            public int bottom = 1;
        }

        [Header("Per-quadrant margins (cells)")]
        [SerializeField] private QuadrantMargins marginsBL = new QuadrantMargins();
        [SerializeField] private QuadrantMargins marginsBR = new QuadrantMargins();
        [SerializeField] private QuadrantMargins marginsTL = new QuadrantMargins();
        [SerializeField] private QuadrantMargins marginsTR = new QuadrantMargins();

        [Header("Click behavior")]
        [Tooltip("If true, clicks outside the quadrant's inner bounds will clamp to the nearest valid cell inside that same quadrant.")]
        [SerializeField] private bool clampClicksToMapBounds = false;

        [Header("Quadrant unlock (visibility + clicks)")]
        [SerializeField] private bool unlockBL = false;
        [SerializeField] private bool unlockBR = false;
        [SerializeField] private bool unlockTL = true;
        [SerializeField] private bool unlockTR = false;

        [Tooltip("When a quadrant is locked, its BG sortingOrder is set to this (to cover grid).")]
        [SerializeField] private int bgOrderWhenLocked = 10;

        [Tooltip("When a quadrant is unlocked, its BG sortingOrder is set to this (behind grid).")]
        [SerializeField] private int bgOrderWhenUnlocked = -10;

        [Header("Click filtering")]
        [SerializeField] private bool blockClicksWhenOverUI = true;
        [SerializeField] private string uiPassThroughRootName = "farmMapClickCatcher";

        [Header("Debug")]
        [SerializeField] private bool debugLogs = true;

        private Bounds _worldBounds;

        private BoundsInt _rawBL, _rawBR, _rawTL, _rawTR;
        private BoundsInt _innerBL, _innerBR, _innerTL, _innerTR;

        private enum Quadrant { BL, BR, TL, TR }

        [SerializeField] private Quadrant currentQuadrant = Quadrant.TL;

        private Quadrant? _selectedQuadrant;
        private Vector3Int? _selectedCell;

        private Quadrant? _prevHighlightQuadrant;
        private Vector3Int? _prevHighlightCell;

        private bool _isDragging;
        private Vector2 _dragStartMouse;
        private Vector3 _dragStartCamPos;

        private Vector3 _focusTargetPos;
        private bool _hasFocusTarget;

        private float _maxOrthoFitWholeMap;
        private float _lockedOrthoFitQuadrant;

        private readonly List<RaycastResult> _uiHits = new List<RaycastResult>(16);

        private void Start()
        {
            if (!cam)
            {
                Debug.LogError("[FarmMap] Camera reference is missing on FarmMapController.");
                enabled = false;
                return;
            }

            if (!bgBL || !bgBR || !bgTL || !bgTR)
            {
                Debug.LogError("[FarmMap] One or more background SpriteRenderers are missing (bgBL/bgBR/bgTL/bgTR).");
                enabled = false;
                return;
            }

            if (!ValidateQuadrant(Quadrant.BL, mapsBL) ||
                !ValidateQuadrant(Quadrant.BR, mapsBR) ||
                !ValidateQuadrant(Quadrant.TL, mapsTL) ||
                !ValidateQuadrant(Quadrant.TR, mapsTR))
            {
                enabled = false;
                return;
            }

            ComputeWorldBoundsFromBackground();
            ComputeAllQuadrantCellBounds();

            ApplyQuadrantVisuals();
            SetupNavButtons();

            LockAllCells();

            FocusQuadrant(currentQuadrant, instant: true);
            RecomputeZoomLimits();
            ApplyZoomPolicy(forceImmediate: true);

            UpdateNavButtons();
            UpdateLockedOverlayText();
            UpdateInfoText();
        }

        private bool ValidateQuadrant(Quadrant q, QuadrantMaps m)
        {
            if (m == null || m.grid == null)
            {
                Debug.LogError($"[FarmMap] Missing Grid reference for quadrant {q}.");
                return false;
            }

            if (m.unlockMap == null)
                Debug.LogWarning($"[FarmMap] Missing Unlock Tilemap for quadrant {q} (locked overlay will not work).");

            if (m.terrainMap == null)
                Debug.LogWarning($"[FarmMap] Missing Terrain Tilemap for quadrant {q}.");

            if (m.buildingMap == null)
                Debug.LogWarning($"[FarmMap] Missing Building Tilemap for quadrant {q}.");

            if (m.highlightMap == null)
                Debug.LogWarning($"[FarmMap] Missing Highlight Tilemap for quadrant {q}.");

            return true;
        }

        private void Update()
        {
            HandleCameraInput();
            HandleTileInteraction();
            ApplyFocusLerpIfNeeded();

            if (lockCameraToQuadrantWhenZoomLocked && !IsZoomUnlocked())
            {
                ApplyZoomPolicy(forceImmediate: true);
                ForceCameraToCurrentQuadrantCenter();
            }
        }

        // -------------------------
        // Navigation UI
        // -------------------------

        private void SetupNavButtons()
        {
            if (navUp) navUp.onClick.AddListener(() => Navigate(0, +1));
            if (navDown) navDown.onClick.AddListener(() => Navigate(0, -1));
            if (navLeft) navLeft.onClick.AddListener(() => Navigate(-1, 0));
            if (navRight) navRight.onClick.AddListener(() => Navigate(+1, 0));
        }

        private void UpdateNavButtons()
        {
            bool isTL = currentQuadrant == Quadrant.TL;
            bool isTR = currentQuadrant == Quadrant.TR;
            bool isBL = currentQuadrant == Quadrant.BL;
            bool isBR = currentQuadrant == Quadrant.BR;

            if (navUp) navUp.gameObject.SetActive(isBL || isBR);
            if (navDown) navDown.gameObject.SetActive(isTL || isTR);
            if (navLeft) navLeft.gameObject.SetActive(isTR || isBR);
            if (navRight) navRight.gameObject.SetActive(isTL || isBL);
        }

        private void Navigate(int dx, int dy)
        {
            Quadrant target = currentQuadrant;

            if (dx != 0)
            {
                if ((currentQuadrant == Quadrant.TL || currentQuadrant == Quadrant.BL) && dx > 0) target = (currentQuadrant == Quadrant.TL) ? Quadrant.TR : Quadrant.BR;
                else if ((currentQuadrant == Quadrant.TR || currentQuadrant == Quadrant.BR) && dx < 0) target = (currentQuadrant == Quadrant.TR) ? Quadrant.TL : Quadrant.BL;
                else return;
            }

            if (dy != 0)
            {
                if ((currentQuadrant == Quadrant.TL || currentQuadrant == Quadrant.TR) && dy < 0) target = (currentQuadrant == Quadrant.TL) ? Quadrant.BL : Quadrant.BR;
                else if ((currentQuadrant == Quadrant.BL || currentQuadrant == Quadrant.BR) && dy > 0) target = (currentQuadrant == Quadrant.BL) ? Quadrant.TL : Quadrant.TR;
                else return;
            }

            FocusQuadrant(target, instant: true);
            RecomputeZoomLimits();
            ApplyZoomPolicy(forceImmediate: true);
            UpdateNavButtons();
            UpdateLockedOverlayText();
            UpdateInfoText();
        }

        // -------------------------
        // Zoom policy
        // -------------------------

        private void RecomputeZoomLimits()
        {
            _maxOrthoFitWholeMap = ComputeOrthoToFitBounds(_worldBounds);
            _lockedOrthoFitQuadrant = ComputeOrthoToFitBounds(GetBgRenderer(currentQuadrant).bounds);
        }

        private void ApplyZoomPolicy(bool forceImmediate)
        {
            if (!cam || !cam.orthographic) return;

            float maxAllowed = _maxOrthoFitWholeMap;
            float minAllowed = minOrthoSize;

            if (!IsZoomUnlocked())
            {
                cam.orthographicSize = Mathf.Clamp(_lockedOrthoFitQuadrant, minAllowed, maxAllowed);
                return;
            }

            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize, minAllowed, maxAllowed);
        }

        private bool IsZoomUnlocked()
        {
            return GetUnlockedQuadrantCount() >= zoomUnlockAtUnlockedQuadrants;
        }

        private int GetUnlockedQuadrantCount()
        {
            int c = 0;
            if (unlockBL) c++;
            if (unlockBR) c++;
            if (unlockTL) c++;
            if (unlockTR) c++;
            return c;
        }

        private float ComputeOrthoToFitBounds(Bounds b)
        {
            float halfW = b.extents.x;
            float halfH = b.extents.y;

            float sizeByHeight = halfH;
            float sizeByWidth = halfW / cam.aspect;

            return Mathf.Max(sizeByHeight, sizeByWidth) * Mathf.Max(1f, fitPadding);
        }

        private void ForceCameraToCurrentQuadrantCenter()
{
    if (!cam) return;

    Vector3 target = GetInnerBoundsWorldCenter(currentQuadrant);
    cam.transform.position = new Vector3(target.x, target.y, cam.transform.position.z);
}

        // -------------------------
        // Camera input
        // -------------------------

        private void HandleCameraInput()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;

            bool zoomUnlocked = IsZoomUnlocked();
            bool allowFreePan = zoomUnlocked && !keepPagingNavigationEvenWhenZoomUnlocked;

            if (kb != null)
            {
                if (kb.leftArrowKey.wasPressedThisFrame) Navigate(-1, 0);
                if (kb.rightArrowKey.wasPressedThisFrame) Navigate(+1, 0);
                if (kb.upArrowKey.wasPressedThisFrame) Navigate(0, +1);
                if (kb.downArrowKey.wasPressedThisFrame) Navigate(0, -1);

                if (kb.zKey.wasPressedThisFrame)
                {
                    if (zoomUnlocked)
                    {
                        cam.orthographicSize = _maxOrthoFitWholeMap;
                        ClampCameraIfNeeded();
                    }
                    else
                    {
                        RecomputeZoomLimits();
                        ApplyZoomPolicy(forceImmediate: true);
                        ForceCameraToCurrentQuadrantCenter();
                    }
                }

                if (allowFreePan)
                {
                    Vector3 delta = Vector3.zero;
                    if (kb.wKey.isPressed) delta.y += 1;
                    if (kb.sKey.isPressed) delta.y -= 1;
                    if (kb.aKey.isPressed) delta.x -= 1;
                    if (kb.dKey.isPressed) delta.x += 1;

                    if (delta.sqrMagnitude > 0f)
                    {
                        _hasFocusTarget = false;
                        delta.Normalize();
                        cam.transform.position += delta * (keyPanSpeed * Time.deltaTime);
                        ClampCameraIfNeeded();
                    }
                }
            }

            if (mouse != null)
            {
                if (zoomUnlocked)
                {
                    float scroll = mouse.scroll.ReadValue().y;
                    if (Mathf.Abs(scroll) > 0.01f)
                    {
                        _hasFocusTarget = false;
                        float zoomDelta = -scroll * 0.01f * zoomSpeed * cam.orthographicSize;
                        cam.orthographicSize = Mathf.Clamp(cam.orthographicSize + zoomDelta, minOrthoSize, _maxOrthoFitWholeMap);
                        ClampCameraIfNeeded();
                    }
                }

                if (allowFreePan)
                {
                    if (mouse.middleButton.wasPressedThisFrame)
                    {
                        _isDragging = true;
                        _dragStartMouse = mouse.position.ReadValue();
                        _dragStartCamPos = cam.transform.position;
                        _hasFocusTarget = false;
                    }
                    else if (mouse.middleButton.wasReleasedThisFrame)
                    {
                        _isDragging = false;
                    }

                    if (_isDragging && mouse.middleButton.isPressed)
                    {
                        Vector2 now = mouse.position.ReadValue();
                        Vector2 deltaPx = now - _dragStartMouse;

                        float unitsPerPixelY = (cam.orthographicSize * 2f) / Screen.height;
                        float unitsPerPixelX = unitsPerPixelY * cam.aspect;

                        Vector3 worldDelta = new Vector3(-deltaPx.x * unitsPerPixelX, -deltaPx.y * unitsPerPixelY, 0f);
                        cam.transform.position = _dragStartCamPos + (worldDelta * dragPanSpeed);

                        ClampCameraIfNeeded();
                    }
                }
            }
        }

        private void ApplyFocusLerpIfNeeded()
        {
            if (!_hasFocusTarget) return;

            Vector3 p = cam.transform.position;
            Vector3 target = _focusTargetPos;
            target.z = p.z;

            cam.transform.position = Vector3.Lerp(p, target, 1f - Mathf.Exp(-focusLerpSpeed * Time.deltaTime));
            ClampCameraIfNeeded();

            if ((cam.transform.position - target).sqrMagnitude < 0.0004f)
            {
                cam.transform.position = target;
                _hasFocusTarget = false;
            }
        }

        private void FocusQuadrant(Quadrant q, bool instant = false)
        {
            currentQuadrant = q;

            SpriteRenderer bg = GetBgRenderer(q);
            Vector3 target = bg ? bg.bounds.center : _worldBounds.center;

            _focusTargetPos = new Vector3(target.x, target.y, cam.transform.position.z);

            if (instant)
            {
                cam.transform.position = _focusTargetPos;
                _hasFocusTarget = false;
                ClampCameraIfNeeded();
            }
            else
            {
                _hasFocusTarget = true;
            }
        }

        private void ClampCameraIfNeeded()
        {
            if (!clampCameraToMap || cam == null || !cam.orthographic) return;

            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;

            Vector3 p = cam.transform.position;

            float minX = _worldBounds.min.x + halfW;
            float maxX = _worldBounds.max.x - halfW;
            float minY = _worldBounds.min.y + halfH;
            float maxY = _worldBounds.max.y - halfH;

            if (minX > maxX) { minX = maxX = _worldBounds.center.x; }
            if (minY > maxY) { minY = maxY = _worldBounds.center.y; }

            p.x = Mathf.Clamp(p.x, minX, maxX);
            p.y = Mathf.Clamp(p.y, minY, maxY);

            cam.transform.position = p;
        }

        // -------------------------
        // Tile interaction
        // -------------------------

        private void HandleTileInteraction()
{
    var mouse = Mouse.current;
    var kb = Keyboard.current;

    // -----------------
    // CLICK: select cell
    // -----------------
    if (mouse != null && mouse.leftButton.wasPressedThisFrame)
    {
        Vector2 screen = mouse.position.ReadValue();

        // Ignore clicks outside this camera's viewport (IMPORTANT if you use multiple cameras)
        if (cam != null && !cam.pixelRect.Contains(screen))
            return;

        if (blockClicksWhenOverUI && IsPointerOverBlockingUI(screen, out _))
            return;

        Vector3 world = ScreenToWorld(screen);

        Quadrant q = GetQuadrantForWorld(world);
        var maps = GetMaps(q);
        var inner = GetInnerBounds(q);

        Vector3Int cell = maps.grid.WorldToCell(world);

        bool insideInner = inner.size.x > 0 && inner.size.y > 0 && inner.Contains(cell);

        if (!insideInner)
        {
            if (!clampClicksToMapBounds)
                return;

            cell = ClampToBounds(cell, inner);
        }

        if (!IsQuadrantUnlocked(q))
        {
            if (infoText) infoText.text = "That quadrant is locked.";
            return;
        }

        // >>> THIS IS THE KEY FIX: store quadrant + cell so keyboard actions work <<<
        _selectedQuadrant = q;
        _selectedCell = cell;

        SelectCell(q, cell);
    }

    // -----------------
    // KEYBOARD: act on selected cell
    // -----------------
    if (kb == null || _selectedCell == null)
        return;

    // If something ever cleared it, recover a reasonable default:
    if (_selectedQuadrant == null)
        _selectedQuadrant = currentQuadrant;

    Quadrant selQ = _selectedQuadrant.Value;
    Vector3Int c = _selectedCell.Value;

    var mapsSel = GetMaps(selQ);
    BoundsInt innerSel = GetInnerBounds(selQ);

    if (!innerSel.Contains(c) || !IsQuadrantUnlocked(selQ))
    {
        if (infoText) infoText.text = "That quadrant is locked.";
        return;
    }

    bool locked = IsLocked(selQ, c);
    bool hasTerrain = mapsSel.terrainMap != null && mapsSel.terrainMap.GetTile(c) != null;
    bool hasBuilding = mapsSel.buildingMap != null && mapsSel.buildingMap.GetTile(c) != null;

    if (kb.uKey.wasPressedThisFrame)
    {
        if (locked && TrySpend(unlockCost))
        {
            SetLocked(selQ, c, false);
            EnsureUnlockedPlaceholder(selQ, c);
            UpdateInfoText();
        }
        return;
    }

    if (kb.deleteKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame)
    {
        if (!locked)
        {
            if (hasBuilding && mapsSel.buildingMap) mapsSel.buildingMap.SetTile(c, null);
            else if (hasTerrain && mapsSel.terrainMap) mapsSel.terrainMap.SetTile(c, null);

            EnsureUnlockedPlaceholder(selQ, c);
            UpdateInfoText();
        }
        return;
    }

    if (locked) return;

    if (!hasTerrain)
    {
        if (kb.digit1Key.wasPressedThisFrame)
        {
            if (tileGrass != null && TrySpend(grassCost) && mapsSel.terrainMap)
            {
                mapsSel.terrainMap.SetTile(c, tileGrass);
                ClearUnlockedPlaceholder(selQ, c);
            }
            UpdateInfoText();
        }
        else if (kb.digit2Key.wasPressedThisFrame)
        {
            if (tileIndustrial != null && TrySpend(industrialCost) && mapsSel.terrainMap)
            {
                mapsSel.terrainMap.SetTile(c, tileIndustrial);
                ClearUnlockedPlaceholder(selQ, c);
            }
            UpdateInfoText();
        }
        return;
    }

    if (hasTerrain && !hasBuilding)
    {
        if (kb.bKey.wasPressedThisFrame)
        {
            if (tileLumbermill != null && TrySpend(lumbermillCost) && mapsSel.buildingMap)
            {
                mapsSel.buildingMap.SetTile(c, tileLumbermill);
                ClearUnlockedPlaceholder(selQ, c);
            }
            UpdateInfoText();
        }
    }
}


        private void SelectCell(Quadrant q, Vector3Int cell)
        {
            _selectedQuadrant = q;
            _selectedCell = cell;

            if (_prevHighlightCell != null && _prevHighlightQuadrant != null)
            {
                var prevMaps = GetMaps(_prevHighlightQuadrant.Value);
                if (prevMaps.highlightMap != null)
                    prevMaps.highlightMap.SetTile(_prevHighlightCell.Value, null);
            }

            var maps = GetMaps(q);
            if (maps.highlightMap != null && tileHighlight != null)
                maps.highlightMap.SetTile(cell, tileHighlight);

            _prevHighlightQuadrant = q;
            _prevHighlightCell = cell;

            UpdateInfoText();
        }

        // -------------------------
        // Placeholder (optional)
        // -------------------------

        private void EnsureUnlockedPlaceholder(Quadrant q, Vector3Int cell)
        {
            var maps = GetMaps(q);
            if (!maps.unlockedMap || !tileUnlockedPlaceholder) return;

            if (IsLocked(q, cell))
            {
                maps.unlockedMap.SetTile(cell, null);
                return;
            }

            bool hasTerrain = maps.terrainMap != null && maps.terrainMap.GetTile(cell) != null;
            bool hasBuilding = maps.buildingMap != null && maps.buildingMap.GetTile(cell) != null;

            maps.unlockedMap.SetTile(cell, (!hasTerrain && !hasBuilding) ? tileUnlockedPlaceholder : null);
        }

        private void ClearUnlockedPlaceholder(Quadrant q, Vector3Int cell)
        {
            var maps = GetMaps(q);
            if (!maps.unlockedMap) return;
            maps.unlockedMap.SetTile(cell, null);
        }

        // -------------------------
        // Quadrants: unlock + visuals
        // -------------------------

        private void ApplyQuadrantVisuals()
        {
            bgBL.sortingOrder = unlockBL ? bgOrderWhenUnlocked : bgOrderWhenLocked;
            bgBR.sortingOrder = unlockBR ? bgOrderWhenUnlocked : bgOrderWhenLocked;
            bgTL.sortingOrder = unlockTL ? bgOrderWhenUnlocked : bgOrderWhenLocked;
            bgTR.sortingOrder = unlockTR ? bgOrderWhenUnlocked : bgOrderWhenLocked;
        }

        private bool IsQuadrantUnlocked(Quadrant q)
        {
            switch (q)
            {
                case Quadrant.BL: return unlockBL;
                case Quadrant.BR: return unlockBR;
                case Quadrant.TL: return unlockTL;
                case Quadrant.TR: return unlockTR;
                default: return false;
            }
        }

        public void SetQuadrantBL(bool unlocked) { unlockBL = unlocked; OnQuadrantUnlockChanged(); }
        public void SetQuadrantBR(bool unlocked) { unlockBR = unlocked; OnQuadrantUnlockChanged(); }
        public void SetQuadrantTL(bool unlocked) { unlockTL = unlocked; OnQuadrantUnlockChanged(); }
        public void SetQuadrantTR(bool unlocked) { unlockTR = unlocked; OnQuadrantUnlockChanged(); }

        private void OnQuadrantUnlockChanged()
        {
            ApplyQuadrantVisuals();
            RecomputeZoomLimits();
            ApplyZoomPolicy(forceImmediate: true);
            UpdateLockedOverlayText();
            UpdateNavButtons();
        }

        private void UpdateLockedOverlayText()
        {
            bool locked = !IsQuadrantUnlocked(currentQuadrant);

            if (quadrantLockedText)
            {
                quadrantLockedText.gameObject.SetActive(locked);
                if (locked)
                {
                    int req = GetWorldRequirementForQuadrant(currentQuadrant);
                    quadrantLockedText.text = string.Format(lockedMessageFormat, req);
                }
            }
            else if (locked && infoText)
            {
                int req = GetWorldRequirementForQuadrant(currentQuadrant);
                infoText.text = string.Format(lockedMessageFormat, req);
            }
        }

        private int GetWorldRequirementForQuadrant(Quadrant q)
        {
            switch (q)
            {
                case Quadrant.BL: return worldReqBL;
                case Quadrant.BR: return worldReqBR;
                case Quadrant.TL: return worldReqTL;
                case Quadrant.TR: return worldReqTR;
                default: return 0;
            }
        }

        private SpriteRenderer GetBgRenderer(Quadrant q)
        {
            switch (q)
            {
                case Quadrant.BL: return bgBL;
                case Quadrant.BR: return bgBR;
                case Quadrant.TL: return bgTL;
                case Quadrant.TR: return bgTR;
                default: return null;
            }
        }

        private QuadrantMaps GetMaps(Quadrant q)
        {
            switch (q)
            {
                case Quadrant.BL: return mapsBL;
                case Quadrant.BR: return mapsBR;
                case Quadrant.TL: return mapsTL;
                case Quadrant.TR: return mapsTR;
                default: return mapsTL;
            }
        }

private Vector3 GetInnerBoundsWorldCenter(Quadrant q)
{
    var maps = GetMaps(q);
    BoundsInt b = GetInnerBounds(q);

    // Fallback if bounds aren't ready
    var bg = GetBgRenderer(q);
    if (maps == null || maps.grid == null || b.size.x <= 0 || b.size.y <= 0)
        return bg ? bg.bounds.center : _worldBounds.center;

    int x0 = b.xMin;
    int x1 = b.xMin + b.size.x - 1;
    int y0 = b.yMin;
    int y1 = b.yMin + b.size.y - 1;

    // Average the 4 corner cell centers -> stable true center in world space (works great for isometric)
    Vector3 p0 = maps.grid.GetCellCenterWorld(new Vector3Int(x0, y0, 0));
    Vector3 p1 = maps.grid.GetCellCenterWorld(new Vector3Int(x0, y1, 0));
    Vector3 p2 = maps.grid.GetCellCenterWorld(new Vector3Int(x1, y0, 0));
    Vector3 p3 = maps.grid.GetCellCenterWorld(new Vector3Int(x1, y1, 0));

    Vector3 c = (p0 + p1 + p2 + p3) * 0.25f;
    c.z = cam.transform.position.z;
    return c;
}


        private BoundsInt GetInnerBounds(Quadrant q)
        {
            switch (q)
            {
                case Quadrant.BL: return _innerBL;
                case Quadrant.BR: return _innerBR;
                case Quadrant.TL: return _innerTL;
                case Quadrant.TR: return _innerTR;
                default: return new BoundsInt();
            }
        }

        private QuadrantMargins GetMargins(Quadrant q)
        {
            switch (q)
            {
                case Quadrant.BL: return marginsBL;
                case Quadrant.BR: return marginsBR;
                case Quadrant.TL: return marginsTL;
                case Quadrant.TR: return marginsTR;
                default: return marginsTL;
            }
        }

        private Quadrant GetQuadrantForWorld(Vector3 world)
        {
            // Prefer "contains" checks (works even if quadrants aren't perfectly symmetrical).
            if (bgBL.bounds.Contains(world)) return Quadrant.BL;
            if (bgBR.bounds.Contains(world)) return Quadrant.BR;
            if (bgTL.bounds.Contains(world)) return Quadrant.TL;
            if (bgTR.bounds.Contains(world)) return Quadrant.TR;

            // Fallback: seam split
            Vector3 seam = _worldBounds.center;
            bool right = world.x >= seam.x;
            bool top = world.y >= seam.y;

            if (!right && !top) return Quadrant.BL;
            if (right && !top) return Quadrant.BR;
            if (!right && top) return Quadrant.TL;
            return Quadrant.TR;
        }

        // -------------------------
        // Locked overlay
        // -------------------------

        private void LockAllCells()
        {
            LockQuadrantCells(Quadrant.BL);
            LockQuadrantCells(Quadrant.BR);
            LockQuadrantCells(Quadrant.TL);
            LockQuadrantCells(Quadrant.TR);
        }

        private void LockQuadrantCells(Quadrant q)
        {
            var maps = GetMaps(q);
            if (maps.unlockMap == null || tileLockedOverlay == null) return;

            maps.unlockMap.ClearAllTiles();
            if (maps.unlockedMap) maps.unlockedMap.ClearAllTiles();

            BoundsInt inner = GetInnerBounds(q);
            FillBoundsWithTile(maps.unlockMap, inner, tileLockedOverlay);
        }

        private void FillBoundsWithTile(Tilemap target, BoundsInt b, TileBase tile)
{
    if (target == null || tile == null) return;
    if (b.size.x <= 0 || b.size.y <= 0) return;

    int count = b.size.x * b.size.y;

    // Safety guard so Unity doesn’t die if bounds are accidentally huge again
    if (count > 250000)
    {
        Debug.LogError($"[FarmMap] Refusing to fill {count:n0} tiles on {target.name}. Bounds={b}. Something is wrong with bounds.");
        return;
    }

    var tiles = new TileBase[count];
    for (int i = 0; i < count; i++) tiles[i] = tile;

    target.SetTilesBlock(b, tiles);
}

        private bool IsLocked(Quadrant q, Vector3Int cell)
        {
            var maps = GetMaps(q);
            if (maps.unlockMap == null) return false;
            return maps.unlockMap.GetTile(cell) != null;
        }

        private void SetLocked(Quadrant q, Vector3Int cell, bool locked)
        {
            var maps = GetMaps(q);
            if (maps.unlockMap == null) return;

            maps.unlockMap.SetTile(cell, locked ? tileLockedOverlay : null);

            if (locked)
                ClearUnlockedPlaceholder(q, cell);
        }

        // -------------------------
        // Bounds calculations
        // -------------------------

        private void ComputeWorldBoundsFromBackground()
        {
            Bounds b = bgBL.bounds;
            b.Encapsulate(bgBR.bounds);
            b.Encapsulate(bgTL.bounds);
            b.Encapsulate(bgTR.bounds);
            _worldBounds = b;
        }

private void ComputeAllQuadrantCellBounds()
{
    ComputeQuadrantCellBounds(Quadrant.BL, out _rawBL, out _innerBL);
    ComputeQuadrantCellBounds(Quadrant.BR, out _rawBR, out _innerBR);
    ComputeQuadrantCellBounds(Quadrant.TL, out _rawTL, out _innerTL);
    ComputeQuadrantCellBounds(Quadrant.TR, out _rawTR, out _innerTR);
}

private BoundsInt ApplyMarginsOrFallback(BoundsInt raw, QuadrantMargins m, Quadrant q)
{
    int left = Mathf.Max(0, m.left);
    int right = Mathf.Max(0, m.right);
    int top = Mathf.Max(0, m.top);
    int bottom = Mathf.Max(0, m.bottom);

    int newW = raw.size.x - left - right;
    int newH = raw.size.y - top - bottom;

    if (newW <= 0 || newH <= 0)
    {
        Debug.LogWarning(
            $"[FarmMap] Margins too large for quadrant {q}. raw={raw} " +
            $"margins L/R/T/B={left}/{right}/{top}/{bottom}. Using raw bounds."
        );
        return raw;
    }

    return new BoundsInt(raw.xMin + left, raw.yMin + bottom, 0, newW, newH, 1);
}

private Vector3Int ClampToBounds(Vector3Int cell, BoundsInt b)
{
    if (b.size.x <= 0 || b.size.y <= 0) return cell;

    int x = Mathf.Clamp(cell.x, b.xMin, b.xMax - 1);
    int y = Mathf.Clamp(cell.y, b.yMin, b.yMax - 1);
    return new Vector3Int(x, y, 0);
}



       private void ComputeQuadrantCellBounds(Quadrant q, out BoundsInt raw, out BoundsInt inner)
{
    var maps = GetMaps(q);
    var bg = GetBgRenderer(q);

    raw = new BoundsInt();
    inner = new BoundsInt();

    if (maps == null || maps.grid == null || bg == null)
        return;

    // ============================
    // FIXED OVERRIDE (your inspector settings)
    // ============================
    if (useFixedQuadrantTileCounts)
    {
        int innerW = Mathf.Clamp(quadrantTilesX, 1, 500);
        int innerH = Mathf.Clamp(quadrantTilesY, 1, 500);
        int margin = Mathf.Clamp(quadrantExtraMarginTiles, 0, 500);

        // Anchor at the background's center cell
        Vector3Int centerCell = maps.grid.WorldToCell(bg.bounds.center);

        int rawW = innerW + (margin * 2);
        int rawH = innerH + (margin * 2);

        // Center the rectangle around the center cell
        int rawXMin = centerCell.x - (rawW / 2);
        int rawYMin = centerCell.y - (rawH / 2);

        raw = new BoundsInt(rawXMin, rawYMin, 0, rawW, rawH, 1);

        // Inner/playable area sits inside the raw bounds
        inner = new BoundsInt(rawXMin + margin, rawYMin + margin, 0, innerW, innerH, 1);

        if (debugLogs)
        {
            Debug.Log($"[FarmMap] {q} FIXED raw={raw} inner={inner} " +
                      $"tilesRaw={rawW * rawH} tilesInner={innerW * innerH} " +
                      $"(X={innerW}, Y={innerH}, margin={margin})");
        }

        return;
    }

    // ============================
    // BACKGROUND-DERIVED (old behaviour)
    // ============================
    Bounds wb = bg.bounds;

    const float eps = 0.001f;
    Vector3 bmin = wb.min + new Vector3(eps, eps, 0f);
    Vector3 bmax = wb.max - new Vector3(eps, eps, 0f);

    Vector3 w0 = new Vector3(bmin.x, bmin.y, 0f);
    Vector3 w1 = new Vector3(bmin.x, bmax.y, 0f);
    Vector3 w2 = new Vector3(bmax.x, bmin.y, 0f);
    Vector3 w3 = new Vector3(bmax.x, bmax.y, 0f);

    Vector3Int c0 = maps.grid.WorldToCell(w0);
    Vector3Int c1 = maps.grid.WorldToCell(w1);
    Vector3Int c2 = maps.grid.WorldToCell(w2);
    Vector3Int c3 = maps.grid.WorldToCell(w3);

    int xMin = Mathf.Min(c0.x, c1.x, c2.x, c3.x);
    int xMax = Mathf.Max(c0.x, c1.x, c2.x, c3.x);
    int yMin = Mathf.Min(c0.y, c1.y, c2.y, c3.y);
    int yMax = Mathf.Max(c0.y, c1.y, c2.y, c3.y);

    int w = (xMax - xMin) + 1;
    int h = (yMax - yMin) + 1;

    raw = new BoundsInt(xMin, yMin, 0, w, h, 1);
    inner = ApplyMarginsOrFallback(raw, GetMargins(q), q);

    if (debugLogs)
        Debug.Log($"[FarmMap] {q} raw={raw} inner={inner}  tilesRaw={w * h}  tilesInner={inner.size.x * inner.size.y}");
}



        // -------------------------
        // UI
        // -------------------------

        private void UpdateInfoText()
        {
            if (!infoText) return;

            if (!IsQuadrantUnlocked(currentQuadrant))
            {
                infoText.text = "";
                return;
            }

            if (_selectedCell == null || _selectedQuadrant == null)
            {
                infoText.text = "Click a tile to select it.";
                return;
            }

            Quadrant q = _selectedQuadrant.Value;
            Vector3Int c = _selectedCell.Value;

            BoundsInt inner = GetInnerBounds(q);

            if (!inner.Contains(c))
            {
                infoText.text = "Click a tile to select it.";
                return;
            }

            if (!IsQuadrantUnlocked(q))
            {
                infoText.text = "That quadrant is locked.";
                return;
            }

            var maps = GetMaps(q);

            bool locked = IsLocked(q, c);
            var terrain = maps.terrainMap ? maps.terrainMap.GetTile(c) : null;
            var building = maps.buildingMap ? maps.buildingMap.GetTile(c) : null;

            string cellPrefix = $"Cell ({c.x},{c.y}) — ";

            if (locked)
            {
                infoText.text = cellPrefix + $"LOCKED — Unlock: {unlockCost:n0} (press U)";
                return;
            }

            if (building != null)
            {
                infoText.text = cellPrefix + $"Building: {building.name} — Delete removes building (Del)";
                return;
            }

            if (terrain != null)
            {
                infoText.text = cellPrefix + $"Terrain: {terrain.name} — Place Lumbermill: {lumbermillCost:n0} (press B) — Delete removes terrain (Del)";
                return;
            }

            infoText.text = cellPrefix + $"UNLOCKED — Place Terrain: 1=Grass ({grassCost:n0})  2=Industrial ({industrialCost:n0})";
        }

        private bool TrySpend(int cost)
        {
            if (debugFreeBuild) return true;
            return true; // hook into currency later
        }

        // -------------------------
        // UI raycast helpers
        // -------------------------

        private bool IsPointerOverBlockingUI(Vector2 screenPos, out string topHitPath)
        {
            topHitPath = null;

            var es = EventSystem.current;
            if (es == null) return false;

            _uiHits.Clear();

            var ped = new PointerEventData(es) { position = screenPos };
            es.RaycastAll(ped, _uiHits);

            if (_uiHits.Count == 0)
                return false;

            var top = _uiHits[0].gameObject;
            if (top == null) return false;

            if (!string.IsNullOrEmpty(uiPassThroughRootName))
            {
                if (IsSelfOrAncestorNamed(top.transform, uiPassThroughRootName))
                    return false;
            }

            topHitPath = GetTransformPath(top.transform);
            return true;
        }

        private static bool IsSelfOrAncestorNamed(Transform t, string name)
        {
            while (t != null)
            {
                if (t.name == name) return true;
                t = t.parent;
            }
            return false;
        }

        private static string GetTransformPath(Transform t)
        {
            if (t == null) return "<null>";

            string path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }

        // -------------------------
        // Utilities
        // -------------------------

        private Vector3 ScreenToWorld(Vector2 screen)
        {
            Vector3 w = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 0f));
            w.z = 0f;
            return w;
        }
    }
}
