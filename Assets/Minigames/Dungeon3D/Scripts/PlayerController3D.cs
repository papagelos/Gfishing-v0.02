using GalacticFishing.Minigames.HexWorld;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GalacticFishing.Minigames.Dungeon3D
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PlayerController3D : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float moveSpeed = 5f;
        [SerializeField, Range(0f, 1f)] private float gamepadDeadZone = 0.2f;

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

            _moveInput = ReadMoveInput();

            if (_moveInput.sqrMagnitude > 0.0001f)
            {
                _lastMoveWorld = ComputeCameraRelativeMove(_moveInput);
                ApplyFacingFromInput(_moveInput);
            }
        }

        private void FixedUpdate()
        {
            if (body == null)
                return;

            Vector3 worldMove = ComputeCameraRelativeMove(_moveInput);
            body.velocity = worldMove * moveSpeed;
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

            Gamepad pad = Gamepad.current;
            if (pad != null)
            {
                Vector2 stick = pad.leftStick.ReadValue();
                if (stick.sqrMagnitude >= gamepadDeadZone * gamepadDeadZone)
                    return stick;
            }

            return fromKeys;
        }

        private Vector3 ComputeCameraRelativeMove(Vector2 input)
        {
            if (input.sqrMagnitude > 1f)
                input.Normalize();

            Camera cam = _mainCamera != null ? _mainCamera : Camera.main;
            if (cam == null)
                return new Vector3(input.x, 0f, input.y);

            Vector3 camRight = cam.transform.right;
            Vector3 camForward = cam.transform.forward;
            camRight.y = 0f;
            camForward.y = 0f;

            if (camRight.sqrMagnitude < 0.0001f || camForward.sqrMagnitude < 0.0001f)
                return new Vector3(input.x, 0f, input.y);

            camRight.Normalize();
            camForward.Normalize();

            Vector3 worldMove = camRight * input.x + camForward * input.y;
            if (worldMove.sqrMagnitude > 1f)
                worldMove.Normalize();

            return worldMove;
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
                    return FirstSprite(east, northEast, southEast, north, south);
                case 1: // NE
                    return FirstSprite(northEast, north, east, northWest);
                case 2: // N
                    return FirstSprite(north, northEast, northWest, east);
                case 3: // NW
                    if (northWest != null) return northWest;
                    if (northEast != null) { flipX = true; return northEast; }
                    return FirstSprite(north, west, east);
                case 4: // W
                    if (west != null) return west;
                    if (east != null) { flipX = true; return east; }
                    return FirstSprite(northWest, southWest, north, south);
                case 5: // SW
                    return FirstSprite(southWest, south, west, southEast);
                case 6: // S
                    return FirstSprite(south, southWest, southEast, west);
                case 7: // SE
                    if (southEast != null) return southEast;
                    if (southWest != null) { flipX = true; return southWest; }
                    return FirstSprite(south, east, west);
                default:
                    return FirstSprite(north, east, south, west);
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
