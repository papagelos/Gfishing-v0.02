using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI; // Graphic

public class BullseyeRainController : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] private RectTransform spawnBounds;       // e.g. SafeFrame_16x9
    [SerializeField] private RectTransform targetPrefab;      // a prefab of ONE bullseye target
    [SerializeField] private CanvasGroup rootGroup;           // optional show/hide + optional parent for spawned targets

    [Header("Spawn/Despawn")]
    [SerializeField] private float spawnAbovePadding = 80f;   // UI units above top (for down movers)
    [SerializeField] private float spawnBelowPadding = 80f;   // UI units below bottom (for up movers)
    [SerializeField] private float despawnBelowPadding = 80f; // UI units below bottom (for down movers)
    [SerializeField] private float despawnAbovePadding = 80f; // UI units above top (for up movers)

    [Header("Direction")]
    [Range(0f, 1f)]
    [SerializeField] private float reverseDirectionChance = 0.25f; // 25% go bottom->top

    [Tooltip("Trajectory tilt away from straight vertical, in degrees. Example: 30 means -30..+30.")]
    [SerializeField] private float maxTiltDegrees = 30f;

    [Header("Hit Test")]
    [SerializeField] private bool circleHitTest = true;       // good for bullseyes

    [Header("Safety")]
    [SerializeField] private bool pauseWhileAIStoryOpen = true;

    public Action<bool> OnFinished; // true=finished (no excess escapes), false=failed by escapes

    private sealed class LiveTarget
    {
        public RectTransform rt;
        public Vector2 velLocal; // velocity in spawnBounds-local space (units/sec)
        public int dirY;         // -1 = down, +1 = up
    }

    private readonly List<LiveTarget> _live = new();

    private bool _running;
    private int _totalToSpawn;
    private int _spawned;
    private int _hits;
    private int _escaped;
    private int _allowedEscapes;

    private float _fallSpeed;
    private float _spawnInterval;
    private float _nextSpawnAt;

    public bool IsRunning => _running;
    public int TotalToSpawn => _totalToSpawn;
    public int Spawned => _spawned;
    public int Hits => _hits;
    public int Escaped => _escaped;

    // “Left to resolve” (not “alive on screen”)
    public int TargetsLeft => Mathf.Max(0, _totalToSpawn - (_hits + _escaped));

    private void Awake()
    {
        HideRootImmediate();
        ClearAll();
        _running = false;
    }

    private void OnDisable()
    {
        Stop();
    }

    public void Begin(int totalTargets, float fallSpeed, float spawnInterval, int allowedEscapes)
    {
        if (!spawnBounds || !targetPrefab)
        {
            Debug.LogWarning("[BullseyeRain] Missing spawnBounds or targetPrefab.");
            Finish(false);
            return;
        }

        ClearAll();

        _totalToSpawn = Mathf.Max(0, totalTargets);
        _fallSpeed = Mathf.Max(0f, fallSpeed);
        _spawnInterval = Mathf.Max(0.01f, spawnInterval);
        _allowedEscapes = Mathf.Max(0, allowedEscapes);

        _spawned = 0;
        _hits = 0;
        _escaped = 0;

        _running = true;
        _nextSpawnAt = Time.unscaledTime; // spawn immediately

        ShowRootImmediate();

        // Edge case: if asked to spawn 0, finish immediately as success.
        if (_totalToSpawn == 0)
            Finish(true);
    }

    public void Stop()
    {
        _running = false;
        ClearAll();
        HideRootImmediate();
    }

    private void Update()
    {
        if (!_running) return;

        if (pauseWhileAIStoryOpen && AIStoryDirector.Instance != null && AIStoryDirector.Instance.IsOpen)
            return;

        TickSpawning();
        TickMovement();

        // Done when we've spawned all, and all are resolved (hit or escaped)
        if (_spawned >= _totalToSpawn && (_hits + _escaped) >= _totalToSpawn)
        {
            bool ok = _escaped <= _allowedEscapes;
            Finish(ok);
        }
    }

    public bool TryHit(Vector2 screenPos, out RectTransform hitTarget, out Vector2 localPointInTarget)
    {
        hitTarget = null;
        localPointInTarget = default;

        if (!_running) return false;
        if (_live.Count == 0) return false;

        Camera cam = null;
        var canvas = spawnBounds.GetComponentInParent<Canvas>();
        if (canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            cam = canvas.worldCamera;

        // topmost preference = newest last
        for (int i = _live.Count - 1; i >= 0; i--)
        {
            var t = _live[i];
            if (t == null || !t.rt) { _live.RemoveAt(i); continue; }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(t.rt, screenPos, cam, out var local))
                continue;

            if (HitTestLocal(t.rt, local))
            {
                hitTarget = t.rt;
                localPointInTarget = local;

                _hits++;
                Destroy(t.rt.gameObject);
                _live.RemoveAt(i);
                return true;
            }
        }

        return false;
    }

    private bool HitTestLocal(RectTransform rt, Vector2 local)
    {
        Vector2 size = rt.rect.size;
        Vector2 center = rt.rect.center;

        if (!circleHitTest)
        {
            Vector2 d = local - center;
            return Mathf.Abs(d.x) <= size.x * 0.5f && Mathf.Abs(d.y) <= size.y * 0.5f;
        }

        float r = Mathf.Min(size.x, size.y) * 0.5f;
        return (local - center).sqrMagnitude <= (r * r);
    }

    private void TickSpawning()
    {
        if (_spawned >= _totalToSpawn) return;
        if (Time.unscaledTime < _nextSpawnAt) return;

        SpawnOne();
        _spawned++;
        _nextSpawnAt = Time.unscaledTime + _spawnInterval;
    }

    private void TickMovement()
    {
        if (!spawnBounds) return;

        Rect b = spawnBounds.rect;

        float dt = Time.unscaledDeltaTime;
        if (dt <= 0f) return;

        for (int i = _live.Count - 1; i >= 0; i--)
        {
            var t = _live[i];
            if (t == null || !t.rt) { _live.RemoveAt(i); continue; }

            Vector2 p = GetLocalPosInBounds(t.rt);
            p += t.velLocal * dt;

            // Bounce at LEFT/RIGHT edges only (never allow side escapes)
            GetHalfExtentsInBounds(t.rt, out float halfW, out float halfH);

            float minX = b.xMin + halfW;
            float maxX = b.xMax - halfW;

            if (p.x < minX)
            {
                p.x = minX;
                t.velLocal.x = Mathf.Abs(t.velLocal.x);
            }
            else if (p.x > maxX)
            {
                p.x = maxX;
                t.velLocal.x = -Mathf.Abs(t.velLocal.x);
            }

            SetLocalPosInBounds(t.rt, p);

            // Escape only on TOP/BOTTOM depending on direction
            if (t.dirY < 0)
            {
                // moving DOWN: escape below bottom (fully past + padding)
                if (p.y < (b.yMin - halfH - despawnBelowPadding))
                {
                    RegisterEscape(i);
                    if (!_running) return;
                }
            }
            else
            {
                // moving UP: escape above top (fully past + padding)
                if (p.y > (b.yMax + halfH + despawnAbovePadding))
                {
                    RegisterEscape(i);
                    if (!_running) return;
                }
            }
        }
    }

    private void RegisterEscape(int index)
    {
        var t = _live[index];

        _escaped++;

        if (t != null && t.rt) Destroy(t.rt.gameObject);
        _live.RemoveAt(index);

        if (_escaped > _allowedEscapes)
        {
            Finish(false);
        }
    }

    private void SpawnOne()
    {
        if (!spawnBounds || !targetPrefab)
        {
            Finish(false);
            return;
        }

        Rect b = spawnBounds.rect;

        // Parent spawned targets under rootGroup if provided, otherwise under spawnBounds.
        Transform parent = rootGroup ? rootGroup.transform : spawnBounds.transform;

        RectTransform rt = Instantiate(targetPrefab, parent);
        ConfigureSpawnedTarget(rt);
        rt.gameObject.SetActive(true);

        // Compute correct half extents in spawnBounds space AFTER instantiation
        GetHalfExtentsInBounds(rt, out float halfW, out float halfH);

        float minX = b.xMin + halfW;
        float maxX = b.xMax - halfW;

        if (maxX <= minX + 10f)
        {
            Debug.LogWarning("[BullseyeRain] Spawn bounds too small for target size.");
            Destroy(rt.gameObject);
            Finish(false);
            return;
        }

        // Direction: default top->bottom; reverse chance bottom->top
        bool reverse = UnityEngine.Random.value < Mathf.Clamp01(reverseDirectionChance);
        int dirY = reverse ? +1 : -1;

        // Tilt: angle relative to vertical axis (-max..+max)
        float tilt = UnityEngine.Random.Range(-Mathf.Abs(maxTiltDegrees), Mathf.Abs(maxTiltDegrees));
        float rad = tilt * Mathf.Deg2Rad;

        // Build direction vector in local space:
        // x = sin(tilt), y magnitude = cos(tilt), sign = dirY
        float vx = Mathf.Sin(rad);
        float vy = Mathf.Cos(rad) * dirY;

        Vector2 dir = new Vector2(vx, vy);
        if (dir.sqrMagnitude < 0.0001f) dir = new Vector2(0f, dirY);

        Vector2 velLocal = dir.normalized * _fallSpeed;

        // Spawn position
        float x = UnityEngine.Random.Range(minX, maxX);

        float y;
        if (dirY < 0)
        {
            // spawn above top, move down
            y = b.yMax + halfH + spawnAbovePadding;
        }
        else
        {
            // spawn below bottom, move up
            y = b.yMin - halfH - spawnBelowPadding;
        }

        SetLocalPosInBounds(rt, new Vector2(x, y));
        _live.Add(new LiveTarget { rt = rt, velLocal = velLocal, dirY = dirY });
    }

    private void ConfigureSpawnedTarget(RectTransform rt)
    {
        if (!rt) return;

        // Ensure it’s visible even if the prefab had CanvasGroup alpha=0.
        var cg = rt.GetComponent<CanvasGroup>();
        if (cg)
        {
            cg.alpha = 1f;
            cg.interactable = false;
            cg.blocksRaycasts = false;
            cg.ignoreParentGroups = false;
        }

        // IMPORTANT: Don’t let these UI images block UI / RMB menu interaction.
        // Our hit detection is manual, not via UI raycasts.
        var graphics = rt.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
            graphics[i].raycastTarget = false;
    }

    private void GetHalfExtentsInBounds(RectTransform rt, out float halfW, out float halfH)
    {
        halfW = 0f;
        halfH = 0f;

        if (!rt || !spawnBounds) return;

        Vector2 size = rt.rect.size;

        Vector3 rtScale = rt.lossyScale;
        Vector3 bScale = spawnBounds.lossyScale;

        float sx = (Mathf.Abs(bScale.x) > 0.0001f) ? (rtScale.x / bScale.x) : rtScale.x;
        float sy = (Mathf.Abs(bScale.y) > 0.0001f) ? (rtScale.y / bScale.y) : rtScale.y;

        halfW = size.x * Mathf.Abs(sx) * 0.5f;
        halfH = size.y * Mathf.Abs(sy) * 0.5f;
    }

    private Vector2 GetLocalPosInBounds(RectTransform rt)
    {
        Vector3 local3 = spawnBounds.InverseTransformPoint(rt.position);
        return new Vector2(local3.x, local3.y);
    }

    private void SetLocalPosInBounds(RectTransform rt, Vector2 localPos)
    {
        Vector3 world = spawnBounds.TransformPoint(new Vector3(localPos.x, localPos.y, 0f));
        world.z = rt.position.z;
        rt.position = world;
    }

    private void Finish(bool success)
    {
        if (!_running) return;

        _running = false;

        // Clean up visuals so nothing stays on screen or blocks input after finish.
        ClearAll();
        HideRootImmediate();

        OnFinished?.Invoke(success);
    }

    private void ClearAll()
    {
        for (int i = _live.Count - 1; i >= 0; i--)
        {
            if (_live[i] != null && _live[i].rt) Destroy(_live[i].rt.gameObject);
        }
        _live.Clear();
    }

    private void ShowRootImmediate()
    {
        if (!rootGroup) return;

        rootGroup.gameObject.SetActive(true);
        rootGroup.alpha = 1f;

        rootGroup.interactable = false;
        rootGroup.blocksRaycasts = false;
    }

    private void HideRootImmediate()
    {
        if (!rootGroup) return;

        rootGroup.alpha = 0f;
        rootGroup.interactable = false;
        rootGroup.blocksRaycasts = false;
        rootGroup.gameObject.SetActive(false);
    }
}
