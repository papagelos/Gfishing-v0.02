using GalacticFishing.Minigames.HexWorld;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

namespace GalacticFishing.Minigames.Dungeon3D
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PlayerController3D : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float moveSpeed = 2.0f;
        [SerializeField, Range(1f, 4f)] private float verticalCompensation = 1.41f;
        [SerializeField, Range(0f, 1f)] private float gamepadDeadZone = 0.2f;
        [SerializeField, Min(0.01f)] private float mouseFollowStopDistance = 0.15f;

        [Header("Debug")]
        [SerializeField] private bool showMovementDebugOverlay;
        [SerializeField, Min(0.01f)] private float debugHexSize = 0.5f;
        [SerializeField, Min(0.01f)] private float debugScreenSpeedSmoothing = 0.2f;
        [SerializeField, Min(0.05f)] private float debugScreenSpeedPeakHoldSeconds = 0.5f;

        [Header("Refs")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Rigidbody body;
        [SerializeField] private CapsuleCollider capsule;
        [SerializeField] private BillboardToCamera billboard;

        [Header("Directional Sprites")]
        [SerializeField] private Sprite north;
        [SerializeField] private Sprite northEast;
        [SerializeField] private Sprite east;
        [SerializeField] private Sprite southEast;
        [SerializeField] private Sprite south;
        [SerializeField] private Sprite southWest;
        [SerializeField] private Sprite west;
        [SerializeField] private Sprite northWest;

        private Vector2 _moveInput;
        private Vector3 _lastMoveWorld = Vector3.forward;
        private Camera _mainCamera;
        private float _debugWorldSpeedMps;
        private float _debugScreenSpeedPxPerSec;
        private float _debugScreenSpeedPeakHoldPxPerSec;
        private float _debugScreenSpeedPeakHoldTimer;
        private float _debugTilesPerMinute;
        private float _debugDirectionAngleDeg;
        private bool _debugDirectionIdle;
        private Vector3 _debugPrevScreenPos;
        private bool _debugHasPrevScreenPos;

        public float VerticalCompensation => verticalCompensation;

        private void Awake()
        {
            EnsureComponents();
            ConfigurePhysics();
            ConfigureVisuals();
            ApplyFacingFromInput(Vector2.up);
        }

        private void Update()
        {
            if (_mainCamera == null)
                _mainCamera = Camera.main;

            HandleDebugOverlayToggle();
            _moveInput = ReadMoveInput();

            if (_moveInput.sqrMagnitude > 0.0001f)
            {
                _lastMoveWorld = ComputeCameraRelativeMove(_moveInput);
                ApplyFacingFromInput(_moveInput);
            }

            UpdateMovementDebugMetrics();
        }

        private void FixedUpdate()
        {
            if (body == null)
                return;

            Vector3 worldMove = ComputeCameraRelativeMove(_moveInput);
            body.linearVelocity = worldMove * moveSpeed;
        }

        private Vector2 ReadMoveInput()
        {
            Vector2 fromKeys = Vector2.zero;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                    fromKeys.x -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                    fromKeys.x += 1f;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                    fromKeys.y += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                    fromKeys.y -= 1f;
            }

            if (fromKeys.sqrMagnitude > 1f)
                fromKeys.Normalize();

            // Priority: explicit keyboard input wins over gamepad/mouse steering.
            if (fromKeys.sqrMagnitude > 0.0001f)
                return fromKeys;

            Gamepad pad = Gamepad.current;
            if (pad != null)
            {
                Vector2 stick = pad.leftStick.ReadValue();
                if (stick.sqrMagnitude >= gamepadDeadZone * gamepadDeadZone)
                    return stick;
            }

            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed)
            {
                // Do not steer when clicking UI (e.g., extraction button).
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                    return Vector2.zero;

                if (TryReadMouseFollowInput(mouse, out Vector2 mouseDir))
                    return mouseDir;
            }

            return Vector2.zero;
        }

        private bool TryReadMouseFollowInput(Mouse mouse, out Vector2 input)
        {
            input = Vector2.zero;
            if (mouse == null)
                return false;

            Camera cam = _mainCamera != null ? _mainCamera : Camera.main;
            if (cam == null)
                return false;

            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            Plane movePlane = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));
            if (!movePlane.Raycast(ray, out float enter))
                return false;

            Vector3 target = ray.GetPoint(enter);
            Vector3 delta = target - transform.position;
            delta.y = 0f;

            if (delta.sqrMagnitude < mouseFollowStopDistance * mouseFollowStopDistance)
                return false;

            Vector3 camForward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
            Vector3 camRight = Vector3.ProjectOnPlane(cam.transform.right, Vector3.up).normalized;
            if (camRight.sqrMagnitude < 0.0001f || camForward.sqrMagnitude < 0.0001f)
                return false;

            Vector3 moveDir = delta.normalized;
            input = new Vector2(
                Vector3.Dot(moveDir, camRight),
                Vector3.Dot(moveDir, camForward));

            if (input.sqrMagnitude > 1f)
                input.Normalize();

            return input.sqrMagnitude > 0.0001f;
        }

        private Vector3 ComputeCameraRelativeMove(Vector2 input)
        {
            if (input.sqrMagnitude <= 0.0001f)
                return Vector3.zero;

            // Normalize first so compensation is applied to direction, not raw stick/key magnitude.
            Vector2 normalizedInput = input.normalized;

            Camera cam = _mainCamera != null ? _mainCamera : Camera.main;
            if (cam == null)
                return new Vector3(normalizedInput.x, 0f, normalizedInput.y);

            Vector3 camForward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
            Vector3 camRight = Vector3.ProjectOnPlane(cam.transform.right, Vector3.up).normalized;

            if (camRight.sqrMagnitude < 0.0001f || camForward.sqrMagnitude < 0.0001f)
                return new Vector3(normalizedInput.x, 0f, normalizedInput.y);

            Vector3 moveH = camRight * normalizedInput.x;
            Vector3 moveV = camForward * (normalizedInput.y * verticalCompensation);
            Vector3 worldMove = moveH + moveV;
            if (worldMove.sqrMagnitude <= 0.0001f)
                return Vector3.zero;

            return worldMove;
        }

        private void HandleDebugOverlayToggle()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f8Key.wasPressedThisFrame)
                showMovementDebugOverlay = !showMovementDebugOverlay;
        }

        private void UpdateMovementDebugMetrics()
        {
            Vector3 velocity = body != null ? body.linearVelocity : Vector3.zero;
            Vector3 planarVelocity = new Vector3(velocity.x, 0f, velocity.z);
            _debugWorldSpeedMps = planarVelocity.magnitude;

            float hexStepDistance = Mathf.Sqrt(3f) * Mathf.Max(0.01f, debugHexSize);
            _debugTilesPerMinute = (_debugWorldSpeedMps / hexStepDistance) * 60f;

            Vector3 direction = planarVelocity.sqrMagnitude > 0.0001f ? planarVelocity : _lastMoveWorld;
            direction.y = 0f;
            _debugDirectionIdle = planarVelocity.sqrMagnitude <= 0.0001f;
            if (direction.sqrMagnitude > 0.0001f)
            {
                float angle = Mathf.Atan2(direction.z, direction.x) * Mathf.Rad2Deg;
                if (angle < 0f)
                    angle += 360f;
                _debugDirectionAngleDeg = angle;
            }

            Camera cam = _mainCamera != null ? _mainCamera : Camera.main;
            if (cam == null)
            {
                _debugScreenSpeedPxPerSec = 0f;
                _debugHasPrevScreenPos = false;
                return;
            }

            Vector3 screenPos = cam.WorldToScreenPoint(transform.position);
            float rawScreenSpeedPxPerSec = 0f;
            if (_debugHasPrevScreenPos && screenPos.z > 0f && _debugPrevScreenPos.z > 0f)
            {
                Vector2 a = new Vector2(screenPos.x, screenPos.y);
                Vector2 b = new Vector2(_debugPrevScreenPos.x, _debugPrevScreenPos.y);
                rawScreenSpeedPxPerSec = Vector2.Distance(a, b) / Mathf.Max(Time.deltaTime, 0.0001f);
            }

            float smoothTime = Mathf.Max(0.01f, debugScreenSpeedSmoothing);
            float lerpT = 1f - Mathf.Exp(-Time.deltaTime / smoothTime);
            _debugScreenSpeedPxPerSec = Mathf.Lerp(_debugScreenSpeedPxPerSec, rawScreenSpeedPxPerSec, lerpT);
            UpdateDebugScreenSpeedPeakHold();

            _debugPrevScreenPos = screenPos;
            _debugHasPrevScreenPos = true;
        }

        private void UpdateDebugScreenSpeedPeakHold()
        {
            float holdSeconds = Mathf.Max(0.05f, debugScreenSpeedPeakHoldSeconds);
            if (_debugScreenSpeedPxPerSec >= _debugScreenSpeedPeakHoldPxPerSec)
            {
                _debugScreenSpeedPeakHoldPxPerSec = _debugScreenSpeedPxPerSec;
                _debugScreenSpeedPeakHoldTimer = holdSeconds;
                return;
            }

            _debugScreenSpeedPeakHoldTimer -= Time.deltaTime;
            if (_debugScreenSpeedPeakHoldTimer <= 0f)
            {
                _debugScreenSpeedPeakHoldPxPerSec = _debugScreenSpeedPxPerSec;
                _debugScreenSpeedPeakHoldTimer = holdSeconds;
            }
        }

        private void OnGUI()
        {
            if (!showMovementDebugOverlay)
                return;

            const float Width = 240f;
            const float Height = 128f;
            GUILayout.BeginArea(new Rect(12f, 12f, Width, Height), $"Move Debug (F8)", GUI.skin.window);
            GUILayout.Label($"World m/s: {_debugWorldSpeedMps:F2}");
            GUILayout.Label($"Screen px/s: {_debugScreenSpeedPxPerSec:F0}");
            GUILayout.Label($"Screen px/s (peak): {_debugScreenSpeedPeakHoldPxPerSec:F0}");
            GUILayout.Label($"Tiles/min: {_debugTilesPerMinute:F1}");
            GUILayout.Label($"Angle: {_debugDirectionAngleDeg:F1}°{(_debugDirectionIdle ? " (idle)" : string.Empty)}");
            GUILayout.EndArea();
        }

        private void ApplyFacingFromInput(Vector2 input)
        {
            if (spriteRenderer == null || input.sqrMagnitude < 0.0001f)
                return;

            float angle = Mathf.Atan2(input.y, input.x) * Mathf.Rad2Deg;
            int octant = PositiveMod(Mathf.RoundToInt(angle / 45f), 8); // 0=E, 2=N, 4=W, 6=S

            bool flipX;
            Sprite facing = ResolveSpriteForOctant(octant, out flipX);
            if (facing != null)
                spriteRenderer.sprite = facing;
            spriteRenderer.flipX = flipX;
        }

        private Sprite ResolveSpriteForOctant(int octant, out bool flipX)
        {
            flipX = false;

            switch (octant)
            {
                case 0: // E
                    {
                        Sprite fallback = FirstSprite(east, southEast, northEast, south, north);
                        if (fallback != null) return fallback;
                        if (west != null) { flipX = true; return west; }
                        return FirstSprite(northWest, southWest);
                    }
                case 1: // NE
                    return FirstSprite(northEast, north, east, northWest);
                case 2: // N
                    return FirstSprite(north, northEast, northWest, east);
                case 3: // NW
                    {
                        if (northWest != null) return northWest;
                        Sprite fallback = FirstSprite(north, west, south, southWest);
                        if (fallback != null) return fallback;
                        if (northEast != null) { flipX = true; return northEast; }
                        return FirstSprite(east, southEast);
                    }
                case 4: // W
                    {
                        if (west != null) return west;
                        Sprite fallback = FirstSprite(southWest, northWest, south, north);
                        if (fallback != null) return fallback;
                        if (east != null) { flipX = true; return east; }
                        return FirstSprite(northEast, southEast);
                    }
                case 5: // SW
                    return FirstSprite(southWest, south, west, southEast);
                case 6: // S
                    return FirstSprite(south, southWest, southEast, east, west);
                case 7: // SE
                    {
                        if (southEast != null) return southEast;
                        Sprite fallback = FirstSprite(south, east, west, north);
                        if (fallback != null) return fallback;
                        if (southWest != null) { flipX = true; return southWest; }
                        return FirstSprite(northEast, northWest);
                    }
                default:
                    return FirstSprite(south, southEast, southWest, east, west, north, northEast, northWest);
            }
        }

        private static Sprite FirstSprite(params Sprite[] sprites)
        {
            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] != null)
                    return sprites[i];
            }

            return null;
        }

        private void EnsureComponents()
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();

            if (body == null)
                body = GetComponent<Rigidbody>();
            if (body == null)
                body = gameObject.AddComponent<Rigidbody>();

            if (capsule == null)
                capsule = GetComponent<CapsuleCollider>();
            if (capsule == null)
                capsule = gameObject.AddComponent<CapsuleCollider>();

            if (billboard == null)
                billboard = GetComponent<BillboardToCamera>();
            if (billboard == null)
                billboard = gameObject.AddComponent<BillboardToCamera>();
        }

        private void ConfigurePhysics()
        {
            if (body != null)
            {
                body.useGravity = false;
                body.isKinematic = false;
                body.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
                body.interpolation = RigidbodyInterpolation.Interpolate;
            }

            if (capsule != null)
            {
                capsule.center = new Vector3(0f, 0.9f, 0f);
                capsule.height = 1.8f;
                capsule.radius = 0.3f;
            }
        }

        private void ConfigureVisuals()
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.sortingLayerName = "Characters";
                if (spriteRenderer.sortingOrder < 10)
                    spriteRenderer.sortingOrder = 10;
            }

            if (billboard != null)
            {
                billboard.yAxisOnly = true;
                if (billboard.spriteRenderer == null)
                    billboard.spriteRenderer = spriteRenderer;
            }
        }

        private static int PositiveMod(int value, int modulus)
        {
            int m = value % modulus;
            return m < 0 ? m + modulus : m;
        }
    }
}
