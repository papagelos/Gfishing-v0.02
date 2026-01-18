using UnityEngine;
using UnityEngine.Serialization;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;   // New Input System
#endif

namespace GalacticFishing
{
    public sealed class FishingLine : MonoBehaviour
    {
        [Header("Refs")]
        public Camera worldCamera;            // falls back to Camera.main
        public Transform rodTip;              // start point

        [Header("Tip / Cursor")]
        [FormerlySerializedAs("lockEndZToRodTip")]
        public bool lockEndZToRod = true;
        [Tooltip("Shift the end by N screen pixels toward/away from the cursor (0 = attach).")]
        public int endOffsetPixels = 0;

        [Header("Rendering")]
        [SerializeField] string sortingLayer = "Characters";
        [SerializeField] int    orderInLayer = 50;
        [SerializeField, Range(0.001f, 0.5f)] float lineWidthWorld = 0.04f;
        [SerializeField] Color startColor = new Color(0.90f, 0.95f, 1f, 0.70f);
        [SerializeField] Color endColor   = new Color(0.90f, 0.95f, 1f, 0.45f);

        [Header("Rope Physics (optional)")]
        public bool useRopePhysics = true;         // toggle sag/lag on/off
        [Range(4, 64)] public int segments = 24;   // number of rope points (when physics is on)
        [Min(0.001f)]  public float segmentLength = 0.12f;
        public Vector2 gravity = new Vector2(0f, -9.81f);
        [Range(1, 12)] public int constraintIterations = 6;
        [Range(0f, 1f)] public float tipFollow = 0.50f;  // 0 = free, 1 = glued to target
        [Range(0f, 1f)] public float airDamping = 0.03f;
        [Range(0f, 1f)] public float bendDamping = 0.10f;
        public bool ensureReach = true;            // snap tip to target if fully stretched

        LineRenderer _lr;

        // Rope buffers
        Vector3[] _curr;     // current positions
        Vector3[] _prev;     // previous positions (for Verlet)
        bool _initialized;

        // -------- Input helper: New Input System (no legacy Input API) --------
        Vector3 GetMouseScreenPos()
        {
#if ENABLE_INPUT_SYSTEM
            var m = Mouse.current;
            return m != null ? (Vector3)m.position.ReadValue() : Vector3.zero;
#else
            return Input.mousePosition; // fallback if define not present
#endif
        }

        void Awake()      => EnsureLR();
        void OnEnable()   { EnsureLR(); _initialized = false; }
        void OnValidate() => EnsureLR();

        void EnsureLR()
        {
            if (!_lr) _lr = GetComponent<LineRenderer>();
            if (!_lr) _lr = gameObject.AddComponent<LineRenderer>();

            _lr.enabled = true;
            // ❌ DO NOT set positionCount here — let LateUpdate decide (straight vs rope)
            _lr.numCapVertices = 4;
            _lr.numCornerVertices = 2;
            _lr.useWorldSpace = true;
            _lr.sortingLayerName = sortingLayer;
            _lr.sortingOrder = orderInLayer;

            // Width in world units + slight taper
            _lr.widthMultiplier = lineWidthWorld;      // e.g., 0.04
            var w = new AnimationCurve();
            w.AddKey(0f, 1f);                          // start = 1 * widthMultiplier
            w.AddKey(1f, 0.5f);                        // end   = 0.5 * widthMultiplier
            _lr.widthCurve = w;

            // Material
            if (_lr.sharedMaterial == null || _lr.sharedMaterial.shader == null)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader) _lr.sharedMaterial = new Material(shader);
            }

            // Color gradient
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(startColor, 0f), new GradientColorKey(endColor, 1f) },
                new[] { new GradientAlphaKey(startColor.a, 0f), new GradientAlphaKey(endColor.a, 1f) }
            );
            _lr.colorGradient = grad;
        }

        void LateUpdate()
        {
            if (!_lr || !rodTip) return;

            var cam = worldCamera ? worldCamera : Camera.main;
            Vector3 start = rodTip.position;
            Vector3 end   = start;

            if (cam)
            {
                // Make sure the Game view has focus so the mouse is updated.
                Vector3 mouse = GetMouseScreenPos();

                // Optional pixel offset so the line meets the cursor exactly
                if (endOffsetPixels != 0)
                {
                    Vector3 rodScreen = cam.WorldToScreenPoint(start);
                    Vector3 dirScreen = (mouse - rodScreen);
                    if (dirScreen.sqrMagnitude > 0.001f)
                    {
                        dirScreen = dirScreen.normalized * endOffsetPixels;
                        mouse += dirScreen;
                    }
                }

                if (cam.orthographic)
                {
                    end = cam.ScreenToWorldPoint(new Vector3(mouse.x, mouse.y, 0f));
                    end.z = lockEndZToRod ? start.z : cam.transform.position.z;
                }
                else
                {
                    float zDepth = Mathf.Abs(cam.transform.position.z - start.z);
                    if (zDepth < cam.nearClipPlane) zDepth = cam.nearClipPlane + 0.01f;
                    end = cam.ScreenToWorldPoint(new Vector3(mouse.x, mouse.y, zDepth));
                    if (lockEndZToRod) end.z = start.z;
                }
            }

            if (!useRopePhysics)
            {
                // Straight line mode
                _lr.positionCount = 2;
                _lr.SetPosition(0, start);
                _lr.SetPosition(1, end);
                return;
            }

            // Rope physics mode
            if (!_initialized || _curr == null || _curr.Length != segments)
                InitRope(start, end);

            Simulate(start, end, Time.deltaTime);

            _lr.positionCount = _curr.Length; // keep >2 in play
            _lr.SetPositions(_curr);
        }

        void InitRope(Vector3 start, Vector3 end)
        {
            if (segments < 4) segments = 4;
            _curr = new Vector3[segments];
            _prev = new Vector3[segments];

            // lay rope roughly along rod → end with some slack downward
            Vector3 dir = (end - start);
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.down;
            dir = dir.normalized;

            for (int i = 0; i < segments; i++)
            {
                _curr[i] = start + dir * (i * segmentLength) + Vector3.down * (i * 0.01f);
                _prev[i] = _curr[i];
            }
            _initialized = true;
        }

        void Simulate(Vector3 anchor, Vector3 tipTarget, float dt)
        {
            if (dt <= 0f) dt = Application.isPlaying ? Time.deltaTime : 1f / 60f;

            // Verlet integration (index 0 is pinned to anchor)
            for (int i = 1; i < segments; i++)
            {
                Vector3 p = _curr[i];
                Vector3 v = (_curr[i] - _prev[i]) * (1f - airDamping);
                Vector3 a = (Vector3)gravity;
                _prev[i] = p;
                _curr[i] = p + v + a * (dt * dt);
            }

            // Pin root to rod tip
            _curr[0] = anchor;
            _prev[0] = anchor;

            // Tip follows the target with some softness
            int last = segments - 1;
            _curr[last] = Vector3.Lerp(_curr[last], tipTarget, tipFollow);

            if (ensureReach)
            {
                float need = Vector3.Distance(anchor, tipTarget);
                float have = segmentLength * (segments - 1);
                if (need >= have * 0.995f) // at or beyond max length -> snap to target
                    _curr[last] = tipTarget;
            }

            // Distance constraints
            for (int it = 0; it < constraintIterations; it++)
            {
                for (int i = 0; i < segments - 1; i++) SolvePair(i, i + 1);
                for (int i = segments - 2; i >= 0; i--) SolvePair(i, i + 1);
                _curr[0] = anchor; // keep anchor pinned each pass
            }
        }

        void SolvePair(int i, int j)
        {
            Vector3 d = _curr[j] - _curr[i];
            float dist = d.magnitude;
            if (dist <= 0.00001f) return;

            float diff = (dist - segmentLength) / dist;
            Vector3 corr = d * diff * 0.5f * (1f - bendDamping);

            if (i != 0) _curr[i] += corr; // don’t move anchor
            _curr[j] -= corr;
        }
    }
}
