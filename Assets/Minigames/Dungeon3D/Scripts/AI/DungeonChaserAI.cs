using UnityEngine;
using UnityEngine.AI;

namespace GalacticFishing.Minigames.Dungeon3D
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(DungeonSpriteHitboxAutoSizer))]
    public sealed class DungeonChaserAI : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField, Min(0.05f)] private float repathInterval = 0.2f;
        [SerializeField, Min(0.1f)] private float fallbackMoveSpeed = 2.5f;

        [Header("Combat")]
        [SerializeField, Min(1)] private int contactDamage = 1;
        [SerializeField, Min(0.1f)] private float damageInterval = 1f;

        [Header("Refs")]
        [SerializeField] private NavMeshAgent agent;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private DungeonSpriteHitboxAutoSizer hitboxAutoSizer;
        [SerializeField] private Rigidbody bodyRigidbody;
        [SerializeField] private Collider bodyCollider;

        [Header("Directional Sprites (Optional)")]
        [SerializeField] private Sprite north;
        [SerializeField] private Sprite northEast;
        [SerializeField] private Sprite east;
        [SerializeField] private Sprite southEast;
        [SerializeField] private Sprite south;
        [SerializeField] private Sprite southWest;
        [SerializeField] private Sprite west;
        [SerializeField] private Sprite northWest;

        private Transform _target;
        private float _repathTimer;
        private float _damageCooldown;
        private Vector3 _lastPosition;
        private Vector3 _targetOffset;
        private int _swarmSlotIndex;
        private int _enemyLayerMask;
        private bool _warnedEnemyLayerFallback;
        private Transform _ignoredPlayerCollisionTarget;
        private PlayerController3D _cachedPlayer;
        private readonly Collider[] _separationHits = new Collider[24];
        private static int s_nextSwarmSlotIndex;

        private void Awake()
        {
            if (agent == null)
                agent = GetComponent<NavMeshAgent>();
            if (agent == null)
                agent = gameObject.AddComponent<NavMeshAgent>();

            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
            if (hitboxAutoSizer == null)
                hitboxAutoSizer = GetComponent<DungeonSpriteHitboxAutoSizer>();
            if (bodyRigidbody == null)
                bodyRigidbody = GetComponent<Rigidbody>();
            if (bodyCollider == null)
                bodyCollider = GetComponent<Collider>();
            if (bodyCollider != null)
                bodyCollider.isTrigger = false;

            _swarmSlotIndex = s_nextSwarmSlotIndex++;
            CacheEnemyLayerMask();
            ConfigureBodyRigidbody();
            ConfigureAgent();
            RefreshSwarmOffsetFromSlot();
        }

        private void OnEnable()
        {
            _repathTimer = 0f;
            _damageCooldown = 0f;
            _lastPosition = transform.position;
            if (bodyCollider != null)
                bodyCollider.isTrigger = false;
            ResolveTarget();
        }

        private void OnValidate()
        {
            repathInterval = Mathf.Max(0.05f, repathInterval);
            fallbackMoveSpeed = Mathf.Max(0.1f, fallbackMoveSpeed);
            contactDamage = Mathf.Max(1, contactDamage);
            damageInterval = Mathf.Max(0.1f, damageInterval);
        }

        private void Update()
        {
            if (_target == null)
                ResolveTarget();
            if (_cachedPlayer == null)
                _cachedPlayer = FindAnyObjectByType<PlayerController3D>(FindObjectsInactive.Include);

            if (_target != null)
            {
                _repathTimer -= Time.deltaTime;
                if (_repathTimer <= 0f)
                {
                    _repathTimer = repathInterval;
                    SyncAgentRadiusFromVisuals();
                    RefreshSwarmOffsetFromSlot();
                    RepathTarget(_target.position);
                }

                // Keep motion continuous even when path recalculation is throttled.
                MoveFallbackEachFrame(_target.position);
            }

            ApplyDirectionalSpeed();
            ResolveEnemyOverlapNoForces();

            if (_damageCooldown > 0f)
                _damageCooldown -= Time.deltaTime;

            UpdateSpriteFacing();
            _lastPosition = transform.position;
        }

        private void OnTriggerStay(Collider other)
        {
            if (_damageCooldown > 0f)
                return;

            if (!TryResolvePlayerHealth(other, out PlayerHealth health))
                return;

            health.TakeDamage(Mathf.Max(1, contactDamage));
            _damageCooldown = damageInterval;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision == null)
                return;

            bool hitPlayer =
                collision.gameObject.CompareTag("Player") ||
                (collision.rigidbody != null && collision.rigidbody.CompareTag("Player")) ||
                collision.transform.root.CompareTag("Player");

            if (!hitPlayer)
                return;

            Vector3 pushDir = transform.position - collision.transform.position;
            pushDir.y = 0f;
            if (pushDir.sqrMagnitude <= 0.0001f)
                return;

            if (bodyRigidbody != null)
                bodyRigidbody.AddForce(pushDir.normalized * 2.0f, ForceMode.Impulse);
        }

        private void RepathTarget(Vector3 targetPos)
        {
            if (agent == null || !agent.enabled || !agent.gameObject.activeInHierarchy || !agent.isOnNavMesh)
                return;

            Vector3 offsetTarget = targetPos + _targetOffset;
            Vector3 destination = new Vector3(offsetTarget.x, transform.position.y, offsetTarget.z);
            agent.SetDestination(destination);
        }

        private void MoveFallbackEachFrame(Vector3 targetPos)
        {
            if (agent != null && agent.enabled && agent.gameObject.activeInHierarchy && agent.isOnNavMesh)
                return;

            Vector3 destination = new Vector3(targetPos.x, transform.position.y, targetPos.z);
            transform.position = Vector3.MoveTowards(
                transform.position,
                destination,
                fallbackMoveSpeed * Time.deltaTime);
        }

        private void UpdateSpriteFacing()
        {
            if (spriteRenderer == null)
                return;

            Vector3 planarVelocity = GetPlanarVelocity();
            if (HasDirectionalSpritesConfigured())
            {
                if (planarVelocity.sqrMagnitude <= 0.0001f)
                {
                    if (south != null)
                        spriteRenderer.sprite = south;
                    spriteRenderer.flipX = false;
                    return;
                }

                if (TryResolveDirectionalSprite(planarVelocity, out Sprite resolvedSprite, out bool flipX))
                {
                    if (resolvedSprite != null)
                        spriteRenderer.sprite = resolvedSprite;
                    spriteRenderer.flipX = flipX;
                    return;
                }
            }

            float vx = 0f;
            if (agent != null && agent.enabled)
                vx = agent.velocity.x;
            else
                vx = (transform.position.x - _lastPosition.x) / Mathf.Max(Time.deltaTime, 0.0001f);

            if (Mathf.Abs(vx) > 0.001f)
                spriteRenderer.flipX = false;
        }

        private Vector3 GetPlanarVelocity()
        {
            Vector3 v;
            if (agent != null && agent.enabled)
                v = agent.velocity;
            else
                v = (transform.position - _lastPosition) / Mathf.Max(Time.deltaTime, 0.0001f);

            v.y = 0f;
            return v;
        }

        private bool HasDirectionalSpritesConfigured()
        {
            return north != null || northEast != null || east != null || southEast != null ||
                   south != null || southWest != null || west != null || northWest != null;
        }

        private bool TryResolveDirectionalSprite(Vector3 planarVelocity, out Sprite resolvedSprite, out bool flipX)
        {
            resolvedSprite = null;
            flipX = false;

            if (planarVelocity.sqrMagnitude <= 0.0001f)
                return false;

            int octant = DirectionToOctant(planarVelocity);
            resolvedSprite = ResolveSpriteForOctant(octant, ref flipX);
            return resolvedSprite != null;
        }

        // Octant layout:
        // 0=E, 1=NE, 2=N, 3=NW, 4=W, 5=SW, 6=S, 7=SE
        private static int DirectionToOctant(Vector3 planarVelocity)
        {
            float angle = Mathf.Atan2(planarVelocity.z, planarVelocity.x) * Mathf.Rad2Deg;
            if (angle < 0f)
                angle += 360f;

            return Mathf.RoundToInt(angle / 45f) % 8;
        }

        private Sprite ResolveSpriteForOctant(int octant, ref bool flipX)
        {
            flipX = false;

            switch (octant & 7)
            {
                case 0: // East
                    if (east != null) return east;
                    Sprite eastFallback = FirstSprite(southEast, northEast, south, north);
                    if (eastFallback != null) return eastFallback;
                    if (west != null) { flipX = true; return west; }
                    return FirstSprite(northWest, southWest);

                case 1: // North-East
                    if (northEast != null) return northEast;
                    Sprite northEastFallback = FirstSprite(north, east, south, southEast);
                    if (northEastFallback != null) return northEastFallback;
                    if (northWest != null) { flipX = true; return northWest; }
                    return FirstSprite(west, southWest);

                case 2: // North
                    return FirstSprite(north, northEast, northWest, east, west, south, southEast, southWest);

                case 3: // North-West
                    if (northWest != null) return northWest;
                    Sprite northWestFallback = FirstSprite(north, west, south, southWest);
                    if (northWestFallback != null) return northWestFallback;
                    if (northEast != null) { flipX = true; return northEast; }
                    return FirstSprite(east, southEast);

                case 4: // West
                    if (west != null) return west;
                    Sprite westFallback = FirstSprite(southWest, northWest, south, north);
                    if (westFallback != null) return westFallback;
                    if (east != null) { flipX = true; return east; }
                    return FirstSprite(northEast, southEast);

                case 5: // South-West
                    if (southWest != null) return southWest;
                    Sprite southWestFallback = FirstSprite(south, west, east, north);
                    if (southWestFallback != null) return southWestFallback;
                    if (southEast != null) { flipX = true; return southEast; }
                    return FirstSprite(northWest, northEast);

                case 6: // South
                    return FirstSprite(south, southEast, southWest, east, west, north, northEast, northWest);

                case 7: // South-East
                    if (southEast != null) return southEast;
                    Sprite southEastFallback = FirstSprite(south, east, west, north);
                    if (southEastFallback != null) return southEastFallback;
                    if (southWest != null) { flipX = true; return southWest; }
                    return FirstSprite(northEast, northWest);
            }

            // Unexpected octant: prefer the forward-facing pose over side-view bias.
            return FirstSprite(south, southEast, southWest, east, west, north, northEast, northWest);
        }

        private static Sprite FirstSprite(params Sprite[] candidates)
        {
            if (candidates == null)
                return null;

            for (int i = 0; i < candidates.Length; i++)
            {
                if (candidates[i] != null)
                    return candidates[i];
            }

            return null;
        }

        private void ResolveTarget()
        {
            GameObject tagged = GameObject.FindGameObjectWithTag("Player");
            if (tagged != null)
            {
                _target = tagged.transform;
                if (_cachedPlayer == null)
                    _cachedPlayer = tagged.GetComponent<PlayerController3D>() ?? tagged.GetComponentInParent<PlayerController3D>();
                IgnoreBodyCollisionWithPlayer();
                return;
            }

            PlayerController3D controller = FindAnyObjectByType<PlayerController3D>(FindObjectsInactive.Include);
            _cachedPlayer = controller;
            _target = controller != null ? controller.transform : null;
            IgnoreBodyCollisionWithPlayer();
        }

        private void ConfigureAgent()
        {
            if (agent == null)
                return;

            agent.speed = fallbackMoveSpeed;
            agent.angularSpeed = 0f;
            agent.acceleration = 100f;
            agent.stoppingDistance = 0f;
            agent.autoBraking = false;
            agent.updatePosition = true;
            agent.updateRotation = false;
            agent.updateUpAxis = false;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
            agent.avoidancePriority = Random.Range(0, 100);
            agent.radius = ResolveAgentRadius();
            agent.height = 1f;
            agent.baseOffset = 0f;
        }

        private void ApplyDirectionalSpeed()
        {
            if (agent == null || !agent.enabled)
                return;

            if (_cachedPlayer == null)
            {
                agent.speed = fallbackMoveSpeed;
                return;
            }

            Vector3 moveDir = agent.velocity;
            moveDir.y = 0f;
            if (moveDir.sqrMagnitude < 0.01f)
            {
                agent.speed = fallbackMoveSpeed;
                return;
            }

            Camera cam = Camera.main;
            if (cam == null)
            {
                agent.speed = fallbackMoveSpeed;
                return;
            }

            moveDir.Normalize();
            Vector3 camForward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
            Vector3 camRight = Vector3.ProjectOnPlane(cam.transform.right, Vector3.up).normalized;
            if (camForward.sqrMagnitude < 0.0001f || camRight.sqrMagnitude < 0.0001f)
            {
                agent.speed = fallbackMoveSpeed;
                return;
            }

            float forwardDot = Vector3.Dot(moveDir, camForward);
            float rightDot = Vector3.Dot(moveDir, camRight);
            float compensation = Mathf.Max(1f, _cachedPlayer.VerticalCompensation);

            Vector3 stretchedMove =
                (camRight * rightDot) +
                (camForward * (forwardDot * compensation));

            float directionalScale = stretchedMove.magnitude;
            if (directionalScale <= 0.0001f)
                directionalScale = 1f;

            agent.speed = fallbackMoveSpeed * directionalScale;
        }

        private void ConfigureBodyRigidbody()
        {
            if (bodyRigidbody == null)
                return;

            bodyRigidbody.useGravity = false;
            bodyRigidbody.isKinematic = false;
            bodyRigidbody.constraints =
                RigidbodyConstraints.FreezePositionY |
                RigidbodyConstraints.FreezeRotation;
        }

        private void SyncAgentRadiusFromVisuals()
        {
            if (agent == null)
                return;

            float nextRadius = ResolveAgentRadius();
            if (Mathf.Abs(agent.radius - nextRadius) > 0.001f)
                agent.radius = nextRadius;
        }

        private float ResolveAgentRadius()
        {
            const float MinRadius = 0.12f;
            const float MaxRadius = 5.0f;
            const float FallbackRadius = 0.3f;

            BoxCollider box = GetComponent<Collider>() as BoxCollider;
            if (box != null && box.size.x > 0f)
            {
                float colliderRadius = Mathf.Abs(box.size.x * transform.lossyScale.x) * 0.5f;
                if (colliderRadius > 0.001f)
                    return Mathf.Clamp(colliderRadius, MinRadius, MaxRadius);
            }

            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);

            if (spriteRenderer != null)
            {
                Bounds localBounds = spriteRenderer.localBounds;
                if (localBounds.size.x > 0.001f)
                {
                    float spriteRadius = Mathf.Abs(localBounds.extents.x * spriteRenderer.transform.lossyScale.x);
                    if (spriteRadius > 0.001f)
                        return Mathf.Clamp(spriteRadius, MinRadius, MaxRadius);
                }

                Bounds worldBounds = spriteRenderer.bounds;
                if (worldBounds.size.x > 0.001f)
                {
                    float spriteRadius = Mathf.Abs(worldBounds.extents.x);
                    if (spriteRadius > 0.001f)
                        return Mathf.Clamp(spriteRadius, MinRadius, MaxRadius);
                }
            }

            return FallbackRadius;
        }

        private void RefreshSwarmOffsetFromSlot()
        {
            float bodyRadius = 0.35f;
            if (agent != null)
                bodyRadius = Mathf.Max(0.2f, agent.radius);

            // Ring spacing tracks body size so dense groups pack into concentric layers instead of a line.
            float ringSpacing = Mathf.Max(0.75f, bodyRadius * 2.4f);
            int slot = Mathf.Max(0, _swarmSlotIndex);

            // Let a small number of enemies occupy the player center.
            if (slot < 3)
            {
                _targetOffset = Vector3.zero;
                return;
            }

            int remaining = slot - 3;
            int ring = 1;
            int slotsInRing = 6;

            while (remaining >= slotsInRing)
            {
                remaining -= slotsInRing;
                ring++;
                slotsInRing = Mathf.Max(6, ring * 6);
            }

            float angle = (remaining / (float)slotsInRing) * Mathf.PI * 2f;
            angle += ring * 0.37f; // slight phase offset to avoid straight radial lanes

            float radius = ring * ringSpacing;
            _targetOffset = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
        }

        private void CacheEnemyLayerMask()
        {
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (enemyLayer >= 0)
            {
                _enemyLayerMask = 1 << enemyLayer;
                return;
            }

            _enemyLayerMask = 1 << gameObject.layer;
            if (!_warnedEnemyLayerFallback)
            {
                Debug.LogWarning(
                    $"[{nameof(DungeonChaserAI)}] Layer 'Enemy' is missing; depenetration uses layer '{LayerMask.LayerToName(gameObject.layer)}'.",
                    this);
                _warnedEnemyLayerFallback = true;
            }
        }

        private void ResolveEnemyOverlapNoForces()
        {
            if (bodyCollider == null || !bodyCollider.enabled)
                return;
            if (_enemyLayerMask == 0)
                return;

            float queryRadius = agent != null ? Mathf.Max(0.1f, agent.radius * 1.1f) : 0.5f;
            int hitCount = Physics.OverlapSphereNonAlloc(
                transform.position,
                queryRadius,
                _separationHits,
                _enemyLayerMask,
                QueryTriggerInteraction.Ignore);

            if (hitCount <= 1)
                return;

            Vector3 correction = Vector3.zero;
            int contributors = 0;

            for (int i = 0; i < hitCount; i++)
            {
                Collider otherCollider = _separationHits[i];
                if (otherCollider == null || otherCollider == bodyCollider)
                    continue;

                DungeonChaserAI other = otherCollider.GetComponent<DungeonChaserAI>() ??
                                        otherCollider.GetComponentInParent<DungeonChaserAI>();
                if (other == null || other == this)
                    continue;

                if (Physics.ComputePenetration(
                        bodyCollider, transform.position, transform.rotation,
                        otherCollider, otherCollider.transform.position, otherCollider.transform.rotation,
                        out Vector3 direction, out float distance))
                {
                    if (distance <= 0.0001f)
                        continue;

                    direction.y = 0f;
                    if (direction.sqrMagnitude <= 0.000001f)
                        continue;

                    // Minimal planar depenetration only; no physics impulse/force.
                    correction += direction.normalized * distance;
                    contributors++;
                }
            }

            if (contributors <= 0 || correction.sqrMagnitude <= 0.000001f)
                return;

            Vector3 planarCorrection = correction / contributors;
            planarCorrection.y = 0f;
            planarCorrection = Vector3.ClampMagnitude(planarCorrection, queryRadius * 0.5f);

            if (planarCorrection.sqrMagnitude <= 0.0000001f)
                return;

            if (agent != null && agent.enabled && agent.gameObject.activeInHierarchy && agent.isOnNavMesh)
            {
                agent.Move(planarCorrection);
            }
            else
            {
                transform.position += planarCorrection;
            }
        }

        private void IgnoreBodyCollisionWithPlayer()
        {
            if (bodyCollider == null || _target == null)
                return;

            Transform root = _target.root != null ? _target.root : _target;
            if (_ignoredPlayerCollisionTarget == root)
                return;

            Collider[] playerColliders = root.GetComponentsInChildren<Collider>(true);
            if (playerColliders == null || playerColliders.Length == 0)
                return;

            for (int i = 0; i < playerColliders.Length; i++)
            {
                Collider playerCollider = playerColliders[i];
                if (playerCollider == null)
                    continue;

                Physics.IgnoreCollision(bodyCollider, playerCollider, false);
            }

            _ignoredPlayerCollisionTarget = root;
        }

        private static bool TryResolvePlayerHealth(Collider other, out PlayerHealth health)
        {
            health = null;
            if (other == null)
                return false;

            if (other.attachedRigidbody != null && other.attachedRigidbody.CompareTag("Player"))
            {
                health = other.attachedRigidbody.GetComponent<PlayerHealth>();
                if (health == null)
                    health = other.attachedRigidbody.GetComponentInChildren<PlayerHealth>(true);
                return health != null;
            }

            if (other.CompareTag("Player"))
            {
                health = other.GetComponentInParent<PlayerHealth>();
                if (health == null)
                    health = other.GetComponentInChildren<PlayerHealth>(true);
                return health != null;
            }

            return false;
        }
    }
}
