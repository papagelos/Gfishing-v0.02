using System;
using System.Reflection;
using UnityEngine;

namespace GalacticFishing
{
    [DisallowMultipleComponent]
    public sealed class WorldFishResetter : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private WorldManager worldManager;
        [SerializeField] private GFishSpawner spawner;

        [Header("Event hookup (preferred)")]
        [Tooltip("Common event names to try on WorldManager.")]
        [SerializeField] private string[] eventNameCandidates =
        {
            "WorldChanged",
            "OnWorldChanged",
            "ContextChanged",
            "OnContextChanged",
            "LakeChanged",
            "OnLakeChanged",
        };

        [Header("Fallback poll (ONLY used if no event exists AND pollSeconds > 0)")]
        [SerializeField] private float pollSeconds = 0f;

        [Header("Behaviour")]
        [SerializeField] private bool refillImmediately = true;
        [SerializeField] private bool resetRespawnDelay = true;
        [SerializeField] private bool logs = false;

        private EventInfo _hookedEvent;
        private Delegate _hookedDelegate;

        private float _nextPoll;
        private bool _hasKey;
        private int _lastKey;

        // Ignore startup events until Start() (WorldManager often fires initial events while booting)
        private bool _ready;

        private void Awake()
        {
            if (!worldManager) worldManager = FindFirstObjectByType<WorldManager>();
            if (!spawner) spawner = FindFirstObjectByType<GFishSpawner>();

            TryHookWorldEvent();
            CacheKey(); // cache whatever exists right now
        }

        private void Start()
        {
            // Now we consider the world "stable"
            CacheKey();
            _ready = true;

            if (logs) Debug.Log("[WorldFishResetter] Ready (Start).", this);
        }

        private void OnEnable()
        {
            if (!worldManager) worldManager = FindFirstObjectByType<WorldManager>();
            if (!spawner) spawner = FindFirstObjectByType<GFishSpawner>();

            TryHookWorldEvent();
            CacheKey();
        }

        private void OnDisable()
        {
            UnhookWorldEvent();
        }

        private void Update()
        {
            // If we successfully hooked an event, we do NOT poll.
            if (_hookedEvent != null) return;

            // Poll is opt-in only.
            if (pollSeconds <= 0f) return;

            if (Time.unscaledTime < _nextPoll) return;
            _nextPoll = Time.unscaledTime + Mathf.Max(0.05f, pollSeconds);

            if (!_ready) return;

            int key = ComputeWorldLakeKey();

            if (!_hasKey)
            {
                _lastKey = key;
                _hasKey = true;
                return;
            }

            if (key != _lastKey)
            {
                _lastKey = key;
                if (logs) Debug.Log("[WorldFishResetter] Detected world/lake change (poll). Clearing fish.", this);
                ClearFishNow();
            }
        }

        private void ClearFishNow()
        {
            if (spawner != null)
                spawner.ClearAllSpawnedFish(resetRespawnDelay, refillImmediately);
        }

        // ------------------------------------------------------------
        // Event hookup via reflection
        // ------------------------------------------------------------
        private void TryHookWorldEvent()
        {
            if (worldManager == null || _hookedEvent != null) return;

            var t = worldManager.GetType();
            const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            EventInfo evt = null;
            foreach (var name in eventNameCandidates)
            {
                evt = t.GetEvent(name, BF);
                if (evt != null) break;
            }

            if (evt == null) return;

            var handlerType = evt.EventHandlerType;
            if (handlerType == null) return;

            var invoke = handlerType.GetMethod("Invoke");
            if (invoke == null) return;

            var pars = invoke.GetParameters();
            MethodInfo mi;

            if (pars.Length == 0) mi = GetType().GetMethod(nameof(OnWorldChanged0), BF);
            else if (pars.Length == 1) mi = GetType().GetMethod(nameof(OnWorldChanged1), BF);
            else mi = GetType().GetMethod(nameof(OnWorldChanged2), BF);

            if (mi == null) return;

            try
            {
                var del = Delegate.CreateDelegate(handlerType, this, mi);
                evt.AddEventHandler(worldManager, del);

                _hookedEvent = evt;
                _hookedDelegate = del;

                if (logs) Debug.Log($"[WorldFishResetter] Hooked WorldManager event '{evt.Name}'. (No polling)", this);
            }
            catch (Exception e)
            {
                _hookedEvent = null;
                _hookedDelegate = null;
                if (logs) Debug.LogWarning($"[WorldFishResetter] Failed to hook event: {e.Message}", this);
            }
        }

        private void UnhookWorldEvent()
        {
            if (worldManager == null || _hookedEvent == null || _hookedDelegate == null) return;

            try { _hookedEvent.RemoveEventHandler(worldManager, _hookedDelegate); }
            catch { }

            _hookedEvent = null;
            _hookedDelegate = null;
        }

        // event handlers
        private void OnWorldChanged0() => HandleWorldChanged();
        private void OnWorldChanged1(object _) => HandleWorldChanged();
        private void OnWorldChanged2(object _, object __) => HandleWorldChanged();

        private void HandleWorldChanged()
        {
            // Ignore boot-time events
            if (!_ready)
            {
                CacheKey();
                if (logs) Debug.Log("[WorldFishResetter] Ignored event (not ready yet).", this);
                return;
            }

            int key = ComputeWorldLakeKey();

            if (!_hasKey)
            {
                _lastKey = key;
                _hasKey = true;
                if (logs) Debug.Log("[WorldFishResetter] First key observed. Not clearing.", this);
                return;
            }

            // IMPORTANT: only clear if the key actually changed
            if (key == _lastKey)
            {
                if (logs) Debug.Log("[WorldFishResetter] Event fired but key unchanged. Ignoring.", this);
                return;
            }

            _lastKey = key;

            if (logs) Debug.Log("[WorldFishResetter] World/lake changed. Clearing fish.", this);
            ClearFishNow();
        }

        // ------------------------------------------------------------
        // Stable key
        // ------------------------------------------------------------
        private void CacheKey()
        {
            _lastKey = ComputeWorldLakeKey();
            _hasKey = true;
        }

        private int ComputeWorldLakeKey()
        {
            if (worldManager == null) return 0;

            int lake = TryGetInt(worldManager, "lakeIndex")
                   ?? TryGetInt(worldManager, "LakeIndex")
                   ?? TryGetInt(worldManager, "currentLakeIndex")
                   ?? TryGetInt(worldManager, "CurrentLakeIndex")
                   ?? 0;

            object worldObj =
                TryGetObj(worldManager, "currentWorld")
                ?? TryGetObj(worldManager, "CurrentWorld")
                ?? TryGetObj(worldManager, "activeWorld")
                ?? TryGetObj(worldManager, "ActiveWorld")
                ?? TryGetObj(worldManager, "world")
                ?? TryGetObj(worldManager, "World");

            int worldHash = worldObj != null ? worldObj.GetHashCode() : 0;

            if (worldHash == 0)
            {
                string worldId = TryGetString(worldManager, "worldId")
                             ?? TryGetString(worldManager, "WorldId")
                             ?? TryGetString(worldManager, "currentWorldId")
                             ?? TryGetString(worldManager, "CurrentWorldId")
                             ?? "";
                worldHash = !string.IsNullOrEmpty(worldId) ? worldId.GetHashCode() : 0;
            }

            return (worldHash * 397) ^ lake;
        }

        private static int? TryGetInt(object obj, string name)
        {
            const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var t = obj.GetType();

            var f = t.GetField(name, BF);
            if (f != null && f.FieldType == typeof(int))
                return (int)f.GetValue(obj);

            var p = t.GetProperty(name, BF);
            if (p != null && p.PropertyType == typeof(int) && p.GetIndexParameters().Length == 0)
                return (int)p.GetValue(obj);

            return null;
        }

        private static string TryGetString(object obj, string name)
        {
            const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var t = obj.GetType();

            var f = t.GetField(name, BF);
            if (f != null && f.FieldType == typeof(string))
                return (string)f.GetValue(obj);

            var p = t.GetProperty(name, BF);
            if (p != null && p.PropertyType == typeof(string) && p.GetIndexParameters().Length == 0)
                return (string)p.GetValue(obj);

            return null;
        }

        private static object TryGetObj(object obj, string name)
        {
            const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var t = obj.GetType();

            var f = t.GetField(name, BF);
            if (f != null)
                return f.GetValue(obj);

            var p = t.GetProperty(name, BF);
            if (p != null && p.GetIndexParameters().Length == 0)
                return p.GetValue(obj);

            return null;
        }
    }
}
