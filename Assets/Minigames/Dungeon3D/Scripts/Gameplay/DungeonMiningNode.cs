using System;
using System.Collections;
using System.Reflection;
using GalacticFishing.Minigames.HexWorld;
using GalacticFishing.Upgrades;
using UnityEngine;

namespace GalacticFishing.Minigames.Dungeon3D
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class DungeonMiningNode : MonoBehaviour
    {
        private const float PlayerCapsuleRadiusFallback = 0.3f;
        private const float PlayerRadiusBuffer = 0.1f;

        [Header("Resource")]
        [SerializeField] private DungeonResourceDefinition definition;
        [SerializeField, Min(1)] private int playerMiningPower = 5;

        [Header("Mining Tick")]
        [SerializeField, Min(0.1f)] private float miningTickSeconds = 1f;
        [SerializeField, Min(0f)] private float proximityRadius = 1.2f;

        [Header("Vibration")]
        [SerializeField, Min(0f)] private float vibrationAmplitude = 0.05f;
        [SerializeField, Min(0.01f)] private float vibrationDuration = 0.12f;
        [SerializeField] private ParticleSystem miningParticles;

        [Header("Loot Sink")]
        [SerializeField] private MonoBehaviour dungeonRunInventory;

        [Header("Break Visual")]
        [SerializeField] private GameObject brokenVisual;
        [SerializeField] private bool destroyOnBreak = true;

        private Collider _trigger;
        private Transform _player;
        private Vector3 _baseLocalPosition;
        private float _tickTimer;
        private int _currentHp;
        private bool _isPlayerInRange;
        private bool _wasPlayerInRangeLastFrame;
        private bool _isBroken;
        private Coroutine _vibrateRoutine;
        private HexWorldWarehouseInventory _warehouseFallback;

        public int CurrentHp => _currentHp;
        public bool IsBroken => _isBroken;

        public void Initialize(DungeonResourceDefinition def)
        {
            definition = def;
            _isBroken = false;
            _tickTimer = 0f;
            _isPlayerInRange = false;
            _wasPlayerInRangeLastFrame = false;

            if (_vibrateRoutine != null)
            {
                StopCoroutine(_vibrateRoutine);
                _vibrateRoutine = null;
            }

            _currentHp = definition != null ? definition.maxHp : 1;
            if (_currentHp <= 0)
                _currentHp = 1;
            transform.localPosition = _baseLocalPosition;

            if (brokenVisual != null)
                brokenVisual.SetActive(false);

            if (!enabled)
                enabled = true;
        }

        private void Awake()
        {
            CacheAndPrepareTrigger();
            ResetRuntimeState();
        }

        private void OnEnable()
        {
            if (_isBroken)
                return;

            CacheAndPrepareTrigger();
            ResetRuntimeState();
        }

        private void OnDisable()
        {
            if (_vibrateRoutine != null)
            {
                StopCoroutine(_vibrateRoutine);
                _vibrateRoutine = null;
            }

            if (miningParticles != null && miningParticles.isPlaying)
                miningParticles.Stop();

            _wasPlayerInRangeLastFrame = false;
            transform.localPosition = _baseLocalPosition;
        }

        private void Reset()
        {
            CacheAndPrepareTrigger();
        }

        private void OnValidate()
        {
            playerMiningPower = Mathf.Max(1, playerMiningPower);
            miningTickSeconds = Mathf.Max(0.1f, miningTickSeconds);
            proximityRadius = Mathf.Max(0f, proximityRadius);
            vibrationAmplitude = Mathf.Max(0f, vibrationAmplitude);
            vibrationDuration = Mathf.Max(0.01f, vibrationDuration);
        }

        private void Update()
        {
            if (_isBroken)
            {
                if (miningParticles != null && miningParticles.isPlaying)
                    miningParticles.Stop();

                _wasPlayerInRangeLastFrame = false;
                return;
            }

            RefreshPlayerRangeFallback();

            if (_isPlayerInRange)
            {
                if (!_wasPlayerInRangeLastFrame)
                    TriggerVibration();

                if (miningParticles != null && !miningParticles.isPlaying)
                    miningParticles.Play();
            }
            else
            {
                if (miningParticles != null && miningParticles.isPlaying)
                    miningParticles.Stop();
            }

            if (_isPlayerInRange && definition != null)
            {
                _tickTimer += Time.deltaTime;
                while (_tickTimer >= miningTickSeconds)
                {
                    _tickTimer -= miningTickSeconds;
                    ProcessMiningTick();

                    if (_isBroken)
                        break;
                }
            }

            _wasPlayerInRangeLastFrame = _isPlayerInRange;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_isBroken)
                return;

            if (TryGetPlayerFromCollider(other, out Transform player))
            {
                _player = player;
                Debug.Log($"[{nameof(DungeonMiningNode)}] Player Detected at node '{name}'.", this);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (_isBroken)
                return;

            if (TryGetPlayerFromCollider(other, out _))
            {
                // Intentionally no range toggling here.
                // Distance check in RefreshPlayerRangeFallback() is the source of truth.
            }
        }

        private void ProcessMiningTick()
        {
            TriggerVibration();

            int dmgDealt = Mathf.Max(1, playerMiningPower);
            _currentHp -= dmgDealt;

            string msg = $"{dmgDealt} Dmg!\u00A0\u00A0{_currentHp} Hp Left";
            Vector3 stableWorldPos = transform.parent != null
                ? transform.parent.TransformPoint(_baseLocalPosition)
                : transform.position;
            var ftm = GalacticFishing.UI.FloatingTextManager.Instance;
            if (ftm != null)
                ftm.SpawnWorld(msg, stableWorldPos + (Vector3.up * 0.8f), Color.yellow);

            if (_currentHp > 0)
                return;

            BreakNode();
        }

        private void TriggerVibration()
        {
            if (vibrationAmplitude <= 0f || vibrationDuration <= 0f)
                return;

            if (_vibrateRoutine != null)
                StopCoroutine(_vibrateRoutine);

            _vibrateRoutine = StartCoroutine(VibrateOnce());
        }

        private IEnumerator VibrateOnce()
        {
            float elapsed = 0f;
            while (elapsed < vibrationDuration)
            {
                elapsed += Time.deltaTime;
                Vector2 jitter = UnityEngine.Random.insideUnitCircle * vibrationAmplitude;
                transform.localPosition = _baseLocalPosition + new Vector3(jitter.x, 0f, jitter.y);
                yield return null;
            }

            transform.localPosition = _baseLocalPosition;
            _vibrateRoutine = null;
        }

        private void BreakNode()
        {
            if (_isBroken)
                return;

            _isBroken = true;

            if (_vibrateRoutine != null)
            {
                StopCoroutine(_vibrateRoutine);
                _vibrateRoutine = null;
            }

            if (miningParticles != null && miningParticles.isPlaying)
                miningParticles.Stop();

            transform.localPosition = _baseLocalPosition;

            int lootAmount = RollLootAmount();
            if (lootAmount > 0 && definition != null)
                TrySendLoot(definition.lootId, lootAmount);

            if (destroyOnBreak && brokenVisual == null)
            {
                Destroy(gameObject);
                return;
            }

            if (brokenVisual != null)
                brokenVisual.SetActive(true);

            DisableUnbrokenVisualsAndColliders();
            enabled = false;
        }

        private int RollLootAmount()
        {
            if (definition == null)
                return 0;

            int min = Mathf.Min(definition.veinSizeRange.x, definition.veinSizeRange.y);
            int max = Mathf.Max(definition.veinSizeRange.x, definition.veinSizeRange.y);
            min = Mathf.Max(0, min);
            max = Mathf.Max(min, max);
            return UnityEngine.Random.Range(min, max + 1);
        }

        private bool TrySendLoot(HexWorldResourceId lootId, int amount)
        {
            if (lootId == HexWorldResourceId.None || amount <= 0)
                return false;

            if (TrySendLootToTarget(dungeonRunInventory, lootId, amount))
                return true;

            // Re-resolve each failed send in case the previous reference was stale/destroyed.
            dungeonRunInventory = FindDungeonRunInventory();
            if (TrySendLootToTarget(dungeonRunInventory, lootId, amount))
                return true;

            if (_warehouseFallback == null)
                _warehouseFallback = UnityEngine.Object.FindAnyObjectByType<HexWorldWarehouseInventory>(FindObjectsInactive.Include);

            if (_warehouseFallback != null)
                return _warehouseFallback.TryAdd(lootId, amount);

            Debug.LogWarning(
                $"[{nameof(DungeonMiningNode)}] Could not deliver loot {lootId} x{amount}. " +
                "Assign a DungeonRunInventory-compatible component.",
                this);
            return false;
        }

        private static bool TrySendLootToTarget(MonoBehaviour target, HexWorldResourceId lootId, int amount)
        {
            if (target == null)
                return false;

            bool accepted;

            // Strongly-typed signatures (preferred).
            if (TryInvokeInventoryMethod(target, "AddLoot", lootId, amount, out accepted))
                return accepted;
            if (TryInvokeInventoryMethod(target, "TryAdd", lootId, amount, out accepted))
                return accepted;
            if (TryInvokeInventoryMethod(target, "Add", lootId, amount, out accepted))
                return accepted;
            if (TryInvokeInventoryMethod(target, "AddResource", lootId, amount, out accepted))
                return accepted;

            // String fallback signatures.
            string idString = lootId.ToString();
            if (TryInvokeInventoryMethod(target, "AddLoot", idString, amount, out accepted))
                return accepted;
            if (TryInvokeInventoryMethod(target, "TryAdd", idString, amount, out accepted))
                return accepted;
            if (TryInvokeInventoryMethod(target, "Add", idString, amount, out accepted))
                return accepted;
            if (TryInvokeInventoryMethod(target, "AddResource", idString, amount, out accepted))
                return accepted;

            return false;
        }

        private static bool TryInvokeInventoryMethod(MonoBehaviour target, string methodName, object idArg, int amount, out bool accepted)
        {
            accepted = false;
            if (target == null || idArg == null)
                return false;

            Type targetType = target.GetType();
            MethodInfo method = targetType.GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { idArg.GetType(), typeof(int) },
                null);

            if (method == null)
                return false;

            object result = method.Invoke(target, new[] { idArg, (object)amount });
            accepted = method.ReturnType == typeof(bool) ? (bool)result : true;
            return true;
        }

        private MonoBehaviour FindDungeonRunInventory()
        {
            // 1) Prefer the current player transform if we already tracked one.
            if (_player != null)
            {
                var invOnTrackedPlayer = _player.GetComponentInParent<DungeonRunInventory>();
                if (invOnTrackedPlayer != null)
                    return invOnTrackedPlayer;

                invOnTrackedPlayer = _player.GetComponentInChildren<DungeonRunInventory>(true);
                if (invOnTrackedPlayer != null)
                    return invOnTrackedPlayer;
            }

            // 2) Resolve from PlayerController instance.
            PlayerController3D playerController = UnityEngine.Object.FindAnyObjectByType<PlayerController3D>(FindObjectsInactive.Include);
            if (playerController != null)
            {
                var invOnController = playerController.GetComponentInParent<DungeonRunInventory>();
                if (invOnController != null)
                    return invOnController;

                invOnController = playerController.GetComponentInChildren<DungeonRunInventory>(true);
                if (invOnController != null)
                    return invOnController;
            }

            // 3) Resolve from the tagged Player root as an explicit fallback.
            GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
            if (taggedPlayer != null)
            {
                var invOnTagged = taggedPlayer.GetComponentInParent<DungeonRunInventory>();
                if (invOnTagged != null)
                    return invOnTagged;

                invOnTagged = taggedPlayer.GetComponentInChildren<DungeonRunInventory>(true);
                if (invOnTagged != null)
                    return invOnTagged;
            }

            // 4) Strongly-typed global search.
            DungeonRunInventory typedAny = UnityEngine.Object.FindAnyObjectByType<DungeonRunInventory>(FindObjectsInactive.Include);
            if (typedAny != null)
                return typedAny;

            // 5) Last-resort legacy scan by type name.
            MonoBehaviour[] all = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                MonoBehaviour candidate = all[i];
                if (candidate == null)
                    continue;

                if (string.Equals(candidate.GetType().Name, "DungeonRunInventory", StringComparison.Ordinal))
                    return candidate;
            }

            return null;
        }

        private void RefreshPlayerRangeFallback()
        {
            if (_player == null)
            {
                PlayerController3D playerController = UnityEngine.Object.FindAnyObjectByType<PlayerController3D>(FindObjectsInactive.Exclude);
                if (playerController != null)
                    _player = playerController.transform;
            }

            if (_player == null)
            {
                _isPlayerInRange = false;
                _tickTimer = 0f;
                return;
            }

            float capsuleRadius = PlayerCapsuleRadiusFallback;
            CapsuleCollider playerCapsule = _player.GetComponentInChildren<CapsuleCollider>();
            if (playerCapsule != null)
                capsuleRadius = Mathf.Max(0f, playerCapsule.radius);

            float fallbackRange = capsuleRadius + PlayerRadiusBuffer;

            // Character-centric upgraded stat.
            float upgradedRange = UpgradeService.Evaluate("dungeon", "mining_reach", "dungeon_mining_radius", 0f);
            float effectiveRadius = upgradedRange > 0f ? upgradedRange : fallbackRange;

            Vector3 playerPos = _player.position;
            Vector3 stableWorldPos = transform.parent != null
                ? transform.parent.TransformPoint(_baseLocalPosition)
                : _baseLocalPosition;

            float distToSurface = Vector2.Distance(
                new Vector2(playerPos.x, playerPos.z),
                new Vector2(stableWorldPos.x, stableWorldPos.z));

            float rangeThreshold = _isPlayerInRange ? (effectiveRadius + 0.2f) : effectiveRadius;
            _isPlayerInRange = distToSurface <= rangeThreshold;
            if (!_isPlayerInRange)
                _tickTimer = 0f;
        }

        private static bool TryGetPlayerFromCollider(Collider other, out Transform player)
        {
            player = null;
            if (other == null)
                return false;

            // Primary gate: match the tagged Rigidbody bridge used by the player.
            if (other.attachedRigidbody != null && other.attachedRigidbody.CompareTag("Player"))
            {
                player = other.attachedRigidbody.transform;
                return true;
            }

            // Fallback: direct collider tagged as Player.
            if (other.CompareTag("Player"))
            {
                player = other.transform;
                return true;
            }

            return false;
        }

        private void CacheAndPrepareTrigger()
        {
            // Surface-aware range checks should use the root trigger volume first.
            _trigger = GetComponent<BoxCollider>();
            if (_trigger == null)
                _trigger = GetComponent<Collider>();
            if (_trigger != null && !_trigger.isTrigger)
                _trigger.isTrigger = true;
        }

        private void ResetRuntimeState()
        {
            _baseLocalPosition = transform.localPosition;
            _tickTimer = 0f;
            _isPlayerInRange = false;
            _wasPlayerInRangeLastFrame = false;
            _isBroken = false;
            _currentHp = definition != null ? Mathf.Max(1, definition.maxHp) : 1;

            if (brokenVisual != null)
                brokenVisual.SetActive(false);
        }

        private void DisableUnbrokenVisualsAndColliders()
        {
            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider col = colliders[i];
                if (col != null)
                    col.enabled = false;
            }

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null)
                    continue;

                if (brokenVisual != null && r.transform.IsChildOf(brokenVisual.transform))
                    continue;

                r.enabled = false;
            }
        }
    }
}
