using UnityEngine;

namespace GalacticFishing
{
    public sealed class FishController : MonoBehaviour
    {
        public SpriteRenderer spriteRenderer;
        public Rigidbody2D rb2d;
        public WaterSurfaceMarker water;

        public Vector2 direction = Vector2.right;
        public float speed = 1f;

        public float surfacePadding = 0.05f;

        float _halfHeight;

        // Track last non-zero horizontal movement so we don't jitter during vertical phases.
        float _lastMoveXSign = 1f;

        // -------- U-Turn state machine --------
        private enum MovementState
        {
            Normal = 0,
            Climbing45 = 1,
            RisingVertical = 2,
            Returning45 = 3,
            StraightReturn = 4,

            // -------- S-Turn (optional) --------
            STurnClimbing45 = 5,
            STurnRisingVertical = 6,
            STurnReturning45 = 7
        }

        private MovementState _state = MovementState.Normal;

        // U-turn config (set by spawner)
        private bool _uTurnEnabled = false;
        private bool _uTurnTriggered = false;
        private float _uTurnTriggerX = float.NaN;

        private float _diagOutDistance = 0f;
        private float _verticalDistance = 0f;
        private float _diagBackDistance = 0f;

        // speed multiplier applied during the U-turn maneuver
        private float _uTurnSpeedMultiplier = 1f;

        // -------- S-Turn config (optional; only valid after a U-turn completes) --------
        private bool _sTurnEnabled = false;
        private bool _sTurnTriggered = false;
        private float _sTurnTriggerX = float.NaN;

        // turn direction decision (up vs down)
        private float _turnDecisionMidY = float.NaN;
        private float _turnYSign = 1f; // +1 = up, -1 = down (set when a maneuver starts)

        // State progress
        private float _distanceRemaining = 0f;

        // Original motion
        private Vector2 _originalDirection;
        private float _origDirXSign = 1f;

        // Species flags we need
        private Fish _species;
        private bool _allowFlipX = true;

        public void Initialize(Fish species, float rolledSizeMeters)
        {
            _species = species;

            spriteRenderer ??= GetComponentInChildren<SpriteRenderer>();
            rb2d ??= GetComponent<Rigidbody2D>();
            surfacePadding = species ? species.surfacePadding : surfacePadding;

            _allowFlipX = species ? species.allowFlipX : true;

            if (spriteRenderer && spriteRenderer.sprite)
                _halfHeight = spriteRenderer.bounds.extents.y + surfacePadding;
            else
                _halfHeight = 0.1f + surfacePadding;

            if (rb2d)
            {
                rb2d.gravityScale = 0f;
                rb2d.linearDamping = 1f;     // v6 name (replaces rb2d.drag)
                rb2d.angularDamping = 0.05f; // v6 name (replaces rb2d.angularDrag)
            }

            // Capture original direction as spawned
            _originalDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            _origDirXSign = Mathf.Sign(_originalDirection.x);
            if (Mathf.Abs(_origDirXSign) < 0.0001f) _origDirXSign = 1f;

            _lastMoveXSign = _origDirXSign;

            // Start in Normal state
            _state = MovementState.Normal;
            _uTurnTriggered = false;

            // Reset S-turn runtime flag (config can still be present, but must not be "already used")
            _sTurnTriggered = false;

            // Ensure initial facing is correct.
            ApplyFacingForDirectionX(_lastMoveXSign);
        }

        /// <summary>
        /// Spawner passes the mid-Y of the spawn area so we can decide whether to turn up or down.
        /// If current Y >= midY => turn DOWN. If current Y < midY => turn UP.
        /// </summary>
        public void ConfigureTurnDecisionMidY(float midY)
        {
            _turnDecisionMidY = midY;
        }

        private float ComputeTurnYSignNow()
        {
            // If spawner never configured this, default to the original behavior: turn UP.
            if (float.IsNaN(_turnDecisionMidY))
                return 1f;

            return (transform.position.y >= _turnDecisionMidY) ? -1f : 1f;
        }

        public void ConfigureUTurn(
            bool enabled,
            float triggerX,
            float diagonalDistance,
            float verticalDistance,
            float returnDiagonalDistance = -1f,
            float speedMultiplier = 1f)
        {
            _uTurnEnabled = enabled;
            _uTurnTriggerX = triggerX;

            _diagOutDistance = Mathf.Max(0f, diagonalDistance);
            _verticalDistance = Mathf.Max(0f, verticalDistance);
            _diagBackDistance = (returnDiagonalDistance > 0f) ? returnDiagonalDistance : _diagOutDistance;

            _uTurnSpeedMultiplier = Mathf.Max(0.01f, speedMultiplier);
        }

        public void ConfigureSTurn(bool enabled, float triggerX2)
        {
            _sTurnEnabled = enabled;
            _sTurnTriggerX = triggerX2;

            // If reconfigured mid-life, allow it to trigger again (but still only once).
            _sTurnTriggered = false;
        }

        void Reset()
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            rb2d = GetComponent<Rigidbody2D>();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // Keep half-height updated if sprite loads late
            if (_halfHeight <= 0.0001f && spriteRenderer && spriteRenderer.sprite)
                _halfHeight = spriteRenderer.bounds.extents.y + surfacePadding;

            // Speed rules
            bool inUTurnManeuver =
                _uTurnTriggered &&
                (_state == MovementState.Climbing45 ||
                 _state == MovementState.RisingVertical ||
                 _state == MovementState.Returning45);

            bool inStraightReturn =
                _uTurnTriggered &&
                (_state == MovementState.StraightReturn);

            bool inSTurnManeuver =
                _sTurnTriggered &&
                (_state == MovementState.STurnClimbing45 ||
                 _state == MovementState.STurnRisingVertical ||
                 _state == MovementState.STurnReturning45);

            float mult = 1f;
            if (inUTurnManeuver || inSTurnManeuver)
                mult = _uTurnSpeedMultiplier;
            else if (inStraightReturn)
                mult = Mathf.Max(1f, _uTurnSpeedMultiplier * 0.5f);

            float speedThisFrame = speed * mult;

            // --- Move (attempted) ---
            Vector2 dirN = (direction.sqrMagnitude > 0.0001f) ? direction.normalized : Vector2.zero;
            Vector3 prev = transform.position;

            // Update facing EVERY frame based on the direction used for movement.
            // If we're moving vertically (x ~ 0), keep last horizontal facing.
            if (Mathf.Abs(dirN.x) > 0.0001f)
                _lastMoveXSign = Mathf.Sign(dirN.x);
            ApplyFacingForDirectionX(_lastMoveXSign);

            float attemptedDist = speedThisFrame * dt;
            Vector3 next = prev + (Vector3)dirN * attemptedDist;

            // Clamp to water surface if needed
            if (water)
            {
                float limit = water.SurfaceY - _halfHeight;
                if (next.y > limit) next.y = limit;
            }

            transform.position = next;

            // --- State machine ---
            switch (_state)
            {
                case MovementState.Normal:
                {
                    if (_uTurnEnabled && !_uTurnTriggered && !float.IsNaN(_uTurnTriggerX))
                    {
                        bool movingRight = _origDirXSign > 0f;

                        bool crossed =
                            (movingRight && prev.x < _uTurnTriggerX && next.x >= _uTurnTriggerX) ||
                            (!movingRight && prev.x > _uTurnTriggerX && next.x <= _uTurnTriggerX);

                        if (crossed)
                            StartUTurn();
                    }
                    break;
                }

                case MovementState.Climbing45:
                {
                    _distanceRemaining -= attemptedDist;
                    if (_distanceRemaining <= 0f)
                    {
                        _state = MovementState.RisingVertical;
                        _distanceRemaining = _verticalDistance;

                        direction = new Vector2(0f, _turnYSign);
                    }
                    break;
                }

                case MovementState.RisingVertical:
                {
                    _distanceRemaining -= attemptedDist;
                    if (_distanceRemaining <= 0f)
                    {
                        _state = MovementState.Returning45;
                        _distanceRemaining = _diagBackDistance;

                        direction = new Vector2(-_origDirXSign, _turnYSign).normalized;
                    }
                    break;
                }

                case MovementState.Returning45:
                {
                    _distanceRemaining -= attemptedDist;
                    if (_distanceRemaining <= 0f)
                    {
                        _state = MovementState.StraightReturn;

                        direction = new Vector2(-_origDirXSign, 0f);
                    }
                    break;
                }

                case MovementState.StraightReturn:
                {
                    if (_sTurnEnabled && !_sTurnTriggered && _uTurnTriggered && !float.IsNaN(_sTurnTriggerX))
                    {
                        bool movingRight = (-_origDirXSign) > 0f;

                        bool crossed =
                            (movingRight && prev.x < _sTurnTriggerX && next.x >= _sTurnTriggerX) ||
                            (!movingRight && prev.x > _sTurnTriggerX && next.x <= _sTurnTriggerX);

                        if (crossed)
                            StartSTurn();
                    }
                    break;
                }

                case MovementState.STurnClimbing45:
                {
                    _distanceRemaining -= attemptedDist;
                    if (_distanceRemaining <= 0f)
                    {
                        _state = MovementState.STurnRisingVertical;
                        _distanceRemaining = _verticalDistance;

                        direction = new Vector2(0f, _turnYSign);
                    }
                    break;
                }

                case MovementState.STurnRisingVertical:
                {
                    _distanceRemaining -= attemptedDist;
                    if (_distanceRemaining <= 0f)
                    {
                        _state = MovementState.STurnReturning45;
                        _distanceRemaining = _diagBackDistance;

                        direction = new Vector2(_origDirXSign, _turnYSign).normalized;
                    }
                    break;
                }

                case MovementState.STurnReturning45:
                {
                    _distanceRemaining -= attemptedDist;
                    if (_distanceRemaining <= 0f)
                    {
                        _state = MovementState.Normal;

                        direction = new Vector2(_origDirXSign, 0f);
                    }
                    break;
                }

                default:
                    break;
            }
        }

        private void StartUTurn()
        {
            _uTurnTriggered = true;

            _state = MovementState.Climbing45;
            _distanceRemaining = _diagOutDistance;

            _turnYSign = ComputeTurnYSignNow();

            direction = new Vector2(_origDirXSign, _turnYSign).normalized;
        }

        private void StartSTurn()
        {
            _sTurnTriggered = true;

            _state = MovementState.STurnClimbing45;
            _distanceRemaining = _diagOutDistance;

            _turnYSign = ComputeTurnYSignNow();

            direction = new Vector2(-_origDirXSign, _turnYSign).normalized;
        }

        private void ApplyFacingForDirectionX(float dirX)
        {
            if (!spriteRenderer) return;
            if (!_allowFlipX) return;

            // Project convention:
            // moving right => flipX = true, moving left => flipX = false
            spriteRenderer.flipX = (dirX > 0f);
        }
    }
}
