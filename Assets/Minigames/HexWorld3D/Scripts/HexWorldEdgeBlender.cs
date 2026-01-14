// Assets/Minigames/HexWorld3D/Scripts/HexWorldEdgeBlender.cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace GalacticFishing.Minigames.HexWorld
{
    /// <summary>
    /// Spawns thin "edge strip" meshes along borders where adjacent OWNED tiles have different top materials.
    /// Uses SG_EdgeStrip material + MaterialPropertyBlock (no per-tile material instances).
    ///
    /// Improvements vs the original:
    /// - Generates a wavy strip mesh (not a perfect rectangle) so it doesn't look like a clean decal strip.
    /// - Optional dual-band: slightly biased toward each neighbor tint on each side of the seam.
    /// - Optional texture blend support: passes two textures/world-scales to the shader if you add them to SG_EdgeStrip.
    /// </summary>
    public sealed class HexWorldEdgeBlender : MonoBehaviour
    {
        [Serializable]
        public struct StyleTint
        {
            public HexWorldTileStyle style;
            public Color edgeTint;
        }

        [Header("Refs")]
        [Tooltip("Parent that contains the spawned tile instances (your 'Tiles' transform). If empty, it will try to find a child named 'Tiles'.")]
        [SerializeField] private Transform tilesParent;

        [Tooltip("Material that uses SG_EdgeStrip (Transparent).")]
        [SerializeField] private Material edgeStripMaterial;

        [Header("Style -> Tint (optional). If missing, we try reading a color from the material.")]
        [SerializeField] private List<StyleTint> styleTints = new();

        [Header("Strip look")]
        [Tooltip("World-space width of the strip (total width). Start ~0.02-0.06 and tweak.")]
        [SerializeField] private float stripWidth = 0.03f;

        [Tooltip("Scales the strip length so it doesn't slam into corners. 0.92-0.99 is usually nice.")]
        [Range(0.70f, 1.00f)]
        [SerializeField] private float stripLengthScale = 0.98f;

        [Tooltip("Small lift above tile surface to avoid z-fighting.")]
        [SerializeField] private float yOffset = 0.002f;

        [Tooltip("Shader Alpha parameter (multiplies the mask).")]
        [Range(0f, 1f)]
        [SerializeField] private float alpha = 0.70f;

        [Tooltip("Shader Feather parameter (softness of edge).")]
        [Range(0f, 1f)]
        [SerializeField] private float feather = 0.55f;

        [SerializeField] private float noiseScale = 14f;
        [SerializeField] private float noiseStrength = 0.20f;

        [Header("Make it look less like a straight strip")]
        [Tooltip("If ON, the strip mesh itself is wavy (recommended).")]
        [SerializeField] private bool useWavyMesh = true;

        [Tooltip("How many segments along the edge (more = smoother waviness). 10-18 is good.")]
        [Range(4, 30)]
        [SerializeField] private int wavySegments = 14;

        [Tooltip("How much the strip edges wobble (fraction of half-width). 0.15-0.40 is good.")]
        [Range(0f, 0.60f)]
        [SerializeField] private float edgeWaviness = 0.32f;

        [Tooltip("How much the strip centerline wanders across the seam (fraction of half-width). 0.10-0.35 is good.")]
        [Range(0f, 0.60f)]
        [SerializeField] private float centerWander = 0.22f;

        [Tooltip("Controls how frequently waviness changes along the edge.")]
        [SerializeField] private float wavinessFrequency = 2.2f;

        [Header("Optional: Dual-band (looks more like two materials bleeding)")]
        [Tooltip("If ON, spawns two slightly offset wavy strips, one biased toward A tint and one toward B tint.")]
        [SerializeField] private bool dualBand = true;

        [Tooltip("Offset of each band toward its tile (fraction of stripWidth). 0.12-0.25.")]
        [Range(0f, 0.40f)]
        [SerializeField] private float dualBandOffsetFrac = 0.18f;

        [Tooltip("Band width relative to stripWidth. 0.70-0.95.")]
        [Range(0.3f, 1.2f)]
        [SerializeField] private float dualBandWidthFrac = 0.86f;

        [Tooltip("How much each band color biases toward its tile tint (0 = pure mix, 1 = pure tile tint).")]
        [Range(0f, 1f)]
        [SerializeField] private float dualBandColorBias = 0.65f;

        [Tooltip("Multiplier applied to alpha for each band (so stacking doesn't get too strong).")]
        [Range(0f, 1.2f)]
        [SerializeField] private float dualBandAlphaMul = 0.75f;

        [Header("Top material detection")]
        [Tooltip("If >=0 forces which material slot is TOP. -1 = auto-detect by material name containing 'top', else fallback to slot 1.")]
        [SerializeField] private int topMaterialSlotOverride = -1;

        [Header("Shader property reference names (only change if yours differ)")]
        [SerializeField] private string colorRef = "_Color";
        [SerializeField] private string alphaRef = "_Alpha";
        [SerializeField] private string featherRef = "_Feather";
        [SerializeField] private string noiseScaleRef = "_NoiseScale";
        [SerializeField] private string noiseStrengthRef = "_NoiseStrength";

        [Header("Optional texture blending (only works if you add these properties to SG_EdgeStrip)")]
        [SerializeField] private bool sendNeighborTexturesToShader = true;
        [SerializeField] private string texARef = "_TexA";
        [SerializeField] private string texBRef = "_TexB";
        [SerializeField] private string worldScaleARef = "_WorldScaleA";
        [SerializeField] private string worldScaleBRef = "_WorldScaleB";

        private Transform _edgesParent;
        private Mesh _unitStripMesh;
        private MaterialPropertyBlock _mpb;

        private int _lastOwnedCount = -1;

        private int _lastSignature = int.MinValue;


        // cache: top-material -> tint
        private Dictionary<Material, Color> _topMatToTint;

        // cached shader IDs
        private int _idColor, _idAlpha, _idFeather, _idNoiseScale, _idNoiseStrength;
        private int _idTexA, _idTexB, _idWorldScaleA, _idWorldScaleB;

        // does the shader actually have those props?
        private bool _hasTexBlendProps;

        // runtime meshes (so we can destroy them on rebuild)
        private readonly List<Mesh> _runtimeMeshes = new();

        private void Awake()
        {
            if (!tilesParent)
            {
                var t = transform.Find("Tiles");
                if (t) tilesParent = t;
            }

            _edgesParent = transform.Find("EdgeStrips");
            if (!_edgesParent)
            {
                var go = new GameObject("EdgeStrips");
                go.transform.SetParent(transform, false);
                _edgesParent = go.transform;
            }

            _unitStripMesh = BuildUnitStripMesh();
            _mpb = new MaterialPropertyBlock();

            _idColor = Shader.PropertyToID(colorRef);
            _idAlpha = Shader.PropertyToID(alphaRef);
            _idFeather = Shader.PropertyToID(featherRef);
            _idNoiseScale = Shader.PropertyToID(noiseScaleRef);
            _idNoiseStrength = Shader.PropertyToID(noiseStrengthRef);

            _idTexA = Shader.PropertyToID(texARef);
            _idTexB = Shader.PropertyToID(texBRef);
            _idWorldScaleA = Shader.PropertyToID(worldScaleARef);
            _idWorldScaleB = Shader.PropertyToID(worldScaleBRef);

            _hasTexBlendProps =
                edgeStripMaterial &&
                edgeStripMaterial.HasProperty(_idTexA) &&
                edgeStripMaterial.HasProperty(_idTexB) &&
                edgeStripMaterial.HasProperty(_idWorldScaleA) &&
                edgeStripMaterial.HasProperty(_idWorldScaleB);

            RebuildLookup();
        }

        private void Start()
        {
            RebuildAll();
        }

       private void Update()
{
    if (!Application.isPlaying) return;

    int sig = ComputeOwnedSignature();
    if (sig != _lastSignature)
    {
        _lastSignature = sig;
        RebuildAll();
    }
}


        [ContextMenu("Rebuild Edge Strips Now")]
        public void RebuildAll()
        {
            if (!tilesParent || !edgeStripMaterial) return;

            RebuildLookup();
            ClearEdges();

            // Collect owned tiles by coord
            var owned = new Dictionary<HexCoord, HexWorld3DTile>();
            var allTiles = tilesParent.GetComponentsInChildren<HexWorld3DTile>(true);
            foreach (var t in allTiles)
            {
                if (!t) continue;
                if (t.IsFrontier) continue;
                owned[t.Coord] = t;
            }

            // Spawn one strip per shared edge (dedupe using coord ordering)
            foreach (var kv in owned)
            {
                HexCoord a = kv.Key;
                HexWorld3DTile tileA = kv.Value;

                for (int dir = 0; dir < HexCoord.NeighborDirs.Length; dir++)
                {
                    HexCoord b = a.Neighbor(dir);
                    if (!owned.TryGetValue(b, out var tileB)) continue;

                    // dedupe: only create when a < b
                    if (!IsCoordLess(a, b)) continue;

                    var topMatA = GetTopMaterial(tileA.gameObject);
                    var topMatB = GetTopMaterial(tileB.gameObject);

                    if (topMatA == null || topMatB == null) continue;
                    if (topMatA == topMatB) continue; // same look -> no blend strip

                    Color tintA = GetTintForTopMat(topMatA);
                    Color tintB = GetTintForTopMat(topMatB);

                    // Optional: try to read textures/world-scales from the top materials
                    TryGetTopMaterialTextureAndScale(topMatA, out var texA, out var scaleA);
                    TryGetTopMaterialTextureAndScale(topMatB, out var texB, out var scaleB);

                    SpawnBlend(tileA, tileB, a, b, tintA, tintB, texA, texB, scaleA, scaleB);
                }
            }
        }

        private void SpawnBlend(
            HexWorld3DTile tileA,
            HexWorld3DTile tileB,
            HexCoord coordA,
            HexCoord coordB,
            Color tintA,
            Color tintB,
            Texture texA,
            Texture texB,
            float scaleA,
            float scaleB)
        {
            // Base mix (used if you keep SG_EdgeStrip as “flat color strip”)
            Color mix = (tintA + tintB) * 0.5f;
            mix.a = 1f;

            if (!dualBand)
            {
                SpawnOneStrip(tileA, tileB, coordA, coordB, mix, 0f, 1f, alpha, texA, texB, scaleA, scaleB);
                return;
            }

            // Two bands: one biased toward A, one biased toward B
            float offset = stripWidth * dualBandOffsetFrac;

            Color colA = Color.Lerp(mix, tintA, dualBandColorBias); colA.a = 1f;
            Color colB = Color.Lerp(mix, tintB, dualBandColorBias); colB.a = 1f;

            float bandWidthMul = dualBandWidthFrac;
            float bandAlpha = alpha * dualBandAlphaMul;

            // A band: push toward A (negative along dir from A->B)
            SpawnOneStrip(tileA, tileB, coordA, coordB, colA, -offset, bandWidthMul, bandAlpha, texA, texB, scaleA, scaleB);

            // B band: push toward B (positive along dir from A->B)
            SpawnOneStrip(tileA, tileB, coordA, coordB, colB, +offset, bandWidthMul, bandAlpha, texA, texB, scaleA, scaleB);
        }

        /// <summary>
        /// offsetAlongDir is in world units along the A->B direction (positive toward B).
        /// widthMul scales stripWidth.
        /// alphaOverride allows dualBand to reduce opacity.
        /// </summary>
        private void SpawnOneStrip(
            HexWorld3DTile tileA,
            HexWorld3DTile tileB,
            HexCoord coordA,
            HexCoord coordB,
            Color color,
            float offsetAlongDir,
            float widthMul,
            float alphaOverride,
            Texture texA,
            Texture texB,
            float scaleA,
            float scaleB)
        {
            Vector3 aPos = tileA.transform.position;
            Vector3 bPos = tileB.transform.position;

            Vector3 delta = bPos - aPos;
            delta.y = 0f;
            float centerDist = delta.magnitude;
            if (centerDist < 0.0001f) return;

            Vector3 dir = delta / centerDist;                  // points from A to B (on XZ)
            Vector3 edgeDir = Vector3.Cross(Vector3.up, dir);  // along the shared edge

            // Regular hex: edge length = centerDist / sqrt(3)
            float edgeLength = (centerDist / Mathf.Sqrt(3f)) * stripLengthScale;

            float topY = Mathf.Max(GetTopY(tileA.gameObject), GetTopY(tileB.gameObject)) + yOffset;

            Vector3 mid = (aPos + bPos) * 0.5f;
            mid += dir * offsetAlongDir; // push into A or B a bit
            mid.y = topY;

            // Deterministic seed per edge so it doesn't "shuffle" every rebuild
            int seed = EdgeSeed(coordA, coordB);

            var go = new GameObject("EdgeStrip");
            go.transform.SetParent(_edgesParent, false);
            go.transform.position = mid;

            // IMPORTANT: makes local Z = dir (A->B), local X = edgeDir
            go.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);

            var mf = go.AddComponent<MeshFilter>();
            if (useWavyMesh)
            {
                var mesh = BuildWavyStripMesh(seed, wavySegments, edgeWaviness, centerWander, wavinessFrequency);
                mf.sharedMesh = mesh;
                _runtimeMeshes.Add(mesh);
            }
            else
            {
                mf.sharedMesh = _unitStripMesh;
            }

            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = edgeStripMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            // Mesh: local X = length, local Z = width (because LookRotation sets forward=dir => local Z along seam normal)
            float finalWidth = Mathf.Max(0.0001f, stripWidth * widthMul);
            go.transform.localScale = new Vector3(edgeLength, 1f, finalWidth);

            _mpb.Clear();
            _mpb.SetColor(_idColor, color);
            _mpb.SetFloat(_idAlpha, alphaOverride);
            _mpb.SetFloat(_idFeather, feather);
            _mpb.SetFloat(_idNoiseScale, noiseScale);
            _mpb.SetFloat(_idNoiseStrength, noiseStrength);

            // Optional: if you add _TexA/_TexB + scales to SG_EdgeStrip, this enables REAL texture blending.
            if (sendNeighborTexturesToShader && _hasTexBlendProps)
            {
                if (texA) _mpb.SetTexture(_idTexA, texA);
                if (texB) _mpb.SetTexture(_idTexB, texB);
                _mpb.SetFloat(_idWorldScaleA, scaleA);
                _mpb.SetFloat(_idWorldScaleB, scaleB);
            }

            mr.SetPropertyBlock(_mpb);
        }

        private void ClearEdges()
        {
            // destroy runtime meshes we created
            for (int i = 0; i < _runtimeMeshes.Count; i++)
            {
                if (_runtimeMeshes[i]) Destroy(_runtimeMeshes[i]);
            }
            _runtimeMeshes.Clear();

            if (!_edgesParent) return;
            for (int i = _edgesParent.childCount - 1; i >= 0; i--)
            {
                var c = _edgesParent.GetChild(i);
                if (c) Destroy(c.gameObject);
            }
        }

        private int CountOwnedTiles()
        {
            if (!tilesParent) return 0;
            int count = 0;
            var all = tilesParent.GetComponentsInChildren<HexWorld3DTile>(true);
            foreach (var t in all)
                if (t && !t.IsFrontier) count++;
            return count;
        }

private int ComputeOwnedSignature()
{
    if (!tilesParent) return 0;

    unchecked
    {
        int sig = 17;

        var allTiles = tilesParent.GetComponentsInChildren<HexWorld3DTile>(true);
        foreach (var t in allTiles)
        {
            if (!t || t.IsFrontier) continue;

            var top = GetTopMaterial(t.gameObject);
            int topId = top ? top.GetInstanceID() : 0;

            // Order-independent-ish hash per tile, combined via XOR.
            int h = 23;
            h = (h * 31) + t.Q;
            h = (h * 31) + t.R;
            h = (h * 31) + topId;

            sig ^= h;
        }

        return sig;
    }
}


        private void RebuildLookup()
        {
            _topMatToTint = new Dictionary<Material, Color>();

            foreach (var st in styleTints)
            {
                if (!st.style) continue;
                if (st.style.materials == null || st.style.materials.Length == 0) continue;
                var top = st.style.materials[0];
                if (!top) continue;

                _topMatToTint[top] = st.edgeTint;
            }
        }

        private Color GetTintForTopMat(Material topMat)
        {
            if (topMat != null && _topMatToTint != null && _topMatToTint.TryGetValue(topMat, out var c))
                return c;

            // fallback: try grab base color from the material
            if (topMat != null)
            {
                if (topMat.HasProperty("_BaseColor")) return topMat.GetColor("_BaseColor");
                if (topMat.HasProperty("_Color")) return topMat.GetColor("_Color");
            }
            return Color.gray;
        }

        private float GetTopY(GameObject root)
        {
            var r = root.GetComponentInChildren<Renderer>();
            return r ? r.bounds.max.y : root.transform.position.y;
        }

        private Material GetTopMaterial(GameObject root)
        {
            var mr = root.GetComponentInChildren<MeshRenderer>();
            if (!mr) return null;

            var mats = mr.sharedMaterials;
            if (mats == null || mats.Length == 0) return null;
            if (mats.Length == 1) return mats[0];

            int topIndex = ResolveTopMaterialIndex(mats);
            topIndex = Mathf.Clamp(topIndex, 0, mats.Length - 1);
            return mats[topIndex];
        }

        private int ResolveTopMaterialIndex(Material[] current)
        {
            if (current == null || current.Length == 0) return 0;

            if (topMaterialSlotOverride >= 0 && topMaterialSlotOverride < current.Length)
                return topMaterialSlotOverride;

            for (int i = 0; i < current.Length; i++)
            {
                var m = current[i];
                if (!m) continue;
                var n = m.name;
                if (string.IsNullOrEmpty(n)) continue;

                n = n.ToLowerInvariant();
                if (n.Contains("tiletop") || n.Contains("_top") || n.Contains(" top") || n.Contains("top_") || n == "top")
                    return i;
            }

            return (current.Length > 1) ? 1 : 0;
        }

        private static bool IsCoordLess(HexCoord a, HexCoord b)
        {
            if (a.q != b.q) return a.q < b.q;
            return a.r < b.r;
        }

        private static int EdgeSeed(HexCoord a, HexCoord b)
        {
            unchecked
            {
                // order-independent seed (so A-B and B-A are same)
                int aq = a.q, ar = a.r, bq = b.q, br = b.r;
                if (bq < aq || (bq == aq && br < ar))
                {
                    (aq, bq) = (bq, aq);
                    (ar, br) = (br, ar);
                }

                int h = 17;
                h = h * 31 + aq;
                h = h * 31 + ar;
                h = h * 31 + bq;
                h = h * 31 + br;
                return h;
            }
        }

        private static Mesh BuildUnitStripMesh()
        {
            // Unit strip centered at origin:
            // X = length axis (-0.5..+0.5)
            // Z = width axis  (-0.5..+0.5)
            // UV.x across width (Z), UV.y along length (X)
            var m = new Mesh();
            m.name = "UnitEdgeStrip_XLen_ZWidth";

            var v = new[]
            {
                new Vector3(-0.5f, 0f, -0.5f),
                new Vector3(+0.5f, 0f, -0.5f),
                new Vector3(-0.5f, 0f, +0.5f),
                new Vector3(+0.5f, 0f, +0.5f),
            };

            var uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
            };

            var tris = new[] { 0, 2, 1, 2, 3, 1 };

            m.vertices = v;
            m.uv = uv;
            m.triangles = tris;
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>
        /// Creates a strip mesh where the outer edges are wavy and the centerline can wander a bit.
        /// Still uses UV.x across width (0..1) and UV.y along length (0..1).
        /// Mesh is unit-sized: X in [-0.5..0.5], Z around [-0.5..0.5] with deformation.
        /// </summary>
        private static Mesh BuildWavyStripMesh(int seed, int segments, float waviness, float centerWander, float freq)
        {
            segments = Mathf.Clamp(segments, 1, 128);

            // Keep deformation sane
            waviness = Mathf.Clamp01(waviness);
            centerWander = Mathf.Clamp01(centerWander);

            var mesh = new Mesh();
            mesh.name = $"WavyEdgeStrip_{seed}";

            int vertCount = (segments + 1) * 2;
            var verts = new Vector3[vertCount];
            var uvs = new Vector2[vertCount];
            var norms = new Vector3[vertCount];
            var tris = new int[segments * 6];

            // Perlin seeds
            float s1 = (seed * 0.01713f) % 1000f;
            float s2 = (seed * 0.03173f) % 1000f;

            // Build two vertices per segment: left (u=0) and right (u=1)
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;        // 0..1 along length
                float x = Mathf.Lerp(-0.5f, 0.5f, t); // unit length axis

                // Noise in [-1..+1]
                float nWidth = Mathf.PerlinNoise(s1 + t * freq, s2) * 2f - 1f;
                float nCenter = Mathf.PerlinNoise(s2 + t * (freq * 0.85f), s1) * 2f - 1f;

                // Half-width baseline is 0.5. We vary it and also shift centerline.
                float halfWidth = 0.5f * (1f + nWidth * waviness);
                halfWidth = Mathf.Clamp(halfWidth, 0.18f, 0.80f);

                float centerShift = (0.5f * centerWander) * nCenter;

                float zLeft = (-halfWidth) + centerShift;
                float zRight = (+halfWidth) + centerShift;

                int vi = i * 2;
                verts[vi + 0] = new Vector3(x, 0f, zLeft);
                verts[vi + 1] = new Vector3(x, 0f, zRight);

                uvs[vi + 0] = new Vector2(0f, t);
                uvs[vi + 1] = new Vector2(1f, t);

                norms[vi + 0] = Vector3.up;
                norms[vi + 1] = Vector3.up;

                if (i < segments)
                {
                    int ti = i * 6;

                    int a = vi + 0;
                    int b = vi + 1;
                    int c = vi + 2;
                    int d = vi + 3;

                    // two triangles: a-c-b and c-d-b
                    tris[ti + 0] = a;
                    tris[ti + 1] = c;
                    tris[ti + 2] = b;

                    tris[ti + 3] = c;
                    tris[ti + 4] = d;
                    tris[ti + 5] = b;
                }
            }

            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.normals = norms;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static readonly string[] _texPropCandidates = { "_Basemap", "_BaseMap", "_MainTex" };
        private static readonly string[] _scalePropCandidates = { "_WorldScale", "_Tiling", "_Scale" };

        private static void TryGetTopMaterialTextureAndScale(Material mat, out Texture tex, out float worldScale)
        {
            tex = null;
            worldScale = 1f;
            if (!mat) return;

            // Texture
            for (int i = 0; i < _texPropCandidates.Length; i++)
            {
                string p = _texPropCandidates[i];
                if (mat.HasProperty(p))
                {
                    tex = mat.GetTexture(p);
                    if (tex) break;
                }
            }

            // Scale (your SG_Tiletop_WorldUV uses _WorldScale)
            for (int i = 0; i < _scalePropCandidates.Length; i++)
            {
                string p = _scalePropCandidates[i];
                if (mat.HasProperty(p))
                {
                    worldScale = mat.GetFloat(p);
                    break;
                }
            }

            if (Mathf.Approximately(worldScale, 0f))
                worldScale = 1f;
        }
    }
}
