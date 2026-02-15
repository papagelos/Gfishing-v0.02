using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class SpriteOutlineThickMesh : MonoBehaviour
{
    [Header("Source")]
    public SpriteRenderer sourceRenderer;
    public Sprite sourceSprite;

    [Header("Build Mode")]
    [Tooltip("If true, only builds the side walls. Use this if you want to keep your own SpriteRenderers for front/back faces.")]
    public bool sidesOnly = false;

    [Tooltip("If true, also builds the back face when not in sidesOnly.")]
    public bool buildBackFace = true;

    [Tooltip("If true, back, sides, and front are separate submeshes so draw order is Back -> Sides -> Front (recommended).")]
    public bool splitFrontBackSubmeshes = true;

    [Header("Thickness")]
    [Min(0f)]
    public float thickness = 0.15f;

    [Tooltip("Tiny outward push for faces so they sit on the surface (prevents the 'sunken inside' look). Typical: 0.0005–0.003")]
    [Min(0f)]
    public float faceDepthBias = 0.0015f;

    [Header("Rendering")]
    public bool disableSourceRenderer = true;
    public bool copySortingFromSource = true;

    [Tooltip("Material for front/back faces.")]
    public Material frontBackMaterial;

    [Tooltip("Optional different material for the back face. If null, uses frontBackMaterial.")]
    public Material backFaceMaterial;

    [Tooltip("Material for side faces. Should support tiling/offset. URP/Unlit works. (Recommended: Transparent surface, ZWrite OFF)")]
    public Material sideMaterial;

    [Tooltip("If true, pushes the sprite texture to materials via MaterialPropertyBlock.")]
    public bool pushSpriteTextureToMaterial = true;

    [Tooltip("Optional tint (uses _Color if present).")]
    public Color tint = Color.white;

    [Header("Side UV Patch (to avoid tiny stamped sprite on the sides)")]
    public bool enableSideAutoPatch = true;

    [Range(0.02f, 0.9f)]
    public float sidePatchCoverage = 0.16f;

    [Range(0f, 0.45f)]
    public float sideSafeInset = 0.08f;

    [Range(0f, 0.45f)]
    public float sidePatchJitter = 0.10f;

    [Tooltip("Material index for side faces when splitFrontBackSubmeshes is OFF. Normally 1.")]
    public int sideMaterialIndex = 1;

    [Header("Robust Front/Back (fixes missing letters / zoomed texture)")]
    public bool useSpriteMeshForFrontBack = true;

    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");       // URP Unlit/Lit
    static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");  // URP tiling/offset
    static readonly int MainTexId = Shader.PropertyToID("_MainTex");       // fallback
    static readonly int MainTexStId = Shader.PropertyToID("_MainTex_ST");  // fallback tiling/offset

    MeshFilter _mf;
    MeshRenderer _mr;

    MaterialPropertyBlock _mpb0;
    MaterialPropertyBlock _mpb1;
    MaterialPropertyBlock _mpb2;

    void Reset()
    {
        _mf = GetComponent<MeshFilter>();
        _mr = GetComponent<MeshRenderer>();
        if (!sourceRenderer) sourceRenderer = GetComponentInParent<SpriteRenderer>();
        Rebuild();
    }

    void OnEnable()
    {
        _mf = GetComponent<MeshFilter>();
        _mr = GetComponent<MeshRenderer>();
        Rebuild();
    }

    void OnValidate()
    {
        thickness = Mathf.Max(0f, thickness);
        faceDepthBias = Mathf.Max(0f, faceDepthBias);
        sideMaterialIndex = Mathf.Max(0, sideMaterialIndex);
        Rebuild();
    }

#if UNITY_EDITOR
    void Update()
    {
        if (!Application.isPlaying)
            Rebuild();
    }
#endif

    Sprite GetSprite()
    {
        if (sourceRenderer && sourceRenderer.sprite) return sourceRenderer.sprite;
        return sourceSprite;
    }

    public void Rebuild()
    {
        if (_mf == null) _mf = GetComponent<MeshFilter>();
        if (_mr == null) _mr = GetComponent<MeshRenderer>();

        Sprite sp = GetSprite();
        if (!sp) return;

        int shapeCount = sp.GetPhysicsShapeCount();

        if (disableSourceRenderer && sourceRenderer)
            sourceRenderer.enabled = false;

        if (copySortingFromSource && sourceRenderer)
        {
            _mr.sortingLayerID = sourceRenderer.sortingLayerID;
            _mr.sortingOrder = sourceRenderer.sortingOrder;
        }

        // Build lists
        var verts = new List<Vector3>(8192);
        var uvs = new List<Vector2>(8192);

        var trisBack = new List<int>(8192);
        var trisSides = new List<int>(8192);
        var trisFront = new List<int>(8192);

        float zHalf = thickness * 0.5f;

        // Sides are the "true" shell thickness.
        float zBackShell = -zHalf;
        float zFrontShell = zHalf;

        // Faces get a tiny bias outward so they sit on the surface visually.
        float zBackFace = zBackShell - faceDepthBias;
        float zFrontFace = zFrontShell + faceDepthBias;

        // --- UV mapping helpers ---
        Bounds sb = sp.bounds;
        float xMin = sb.min.x, xSize = sb.size.x;
        float yMin = sb.min.y, ySize = sb.size.y;

        Rect tr = sp.textureRect;
        float texW = sp.texture.width;
        float texH = sp.texture.height;

        Vector2 ToAtlasUV(Vector2 p)
        {
            float nx = xSize > 0.000001f ? (p.x - xMin) / xSize : 0.5f;
            float ny = ySize > 0.000001f ? (p.y - yMin) / ySize : 0.5f;

            float u = (tr.xMin + nx * tr.width) / texW;
            float v = (tr.yMin + ny * tr.height) / texH;
            return new Vector2(u, v);
        }

        // UV bounds used for side patching
        bool hasUvBounds = false;
        Vector2 uvMinAll = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 uvMaxAll = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

        if (shapeCount > 0)
        {
            var poly = new List<Vector2>(256);
            for (int s = 0; s < shapeCount; s++)
            {
                poly.Clear();
                sp.GetPhysicsShape(s, poly);
                if (poly.Count < 3) continue;

                for (int i = 0; i < poly.Count; i++)
                {
                    Vector2 uv = ToAtlasUV(poly[i]);
                    uvMinAll = Vector2.Min(uvMinAll, uv);
                    uvMaxAll = Vector2.Max(uvMaxAll, uv);
                    hasUvBounds = true;
                }
            }
        }
        else
        {
            var suv = sp.uv;
            if (suv != null && suv.Length > 0)
            {
                for (int i = 0; i < suv.Length; i++)
                {
                    uvMinAll = Vector2.Min(uvMinAll, suv[i]);
                    uvMaxAll = Vector2.Max(uvMaxAll, suv[i]);
                    hasUvBounds = true;
                }
            }
        }

        // --- FACES ---
        bool builtFaces = false;

        if (!sidesOnly && frontBackMaterial != null)
        {
            // Robust path: use sprite mesh for face geometry so it matches SpriteRenderer.
            if (useSpriteMeshForFrontBack)
            {
                Vector2[] sv = sp.vertices;
                Vector2[] suv = sp.uv;
                ushort[] st = sp.triangles;

                if (sv != null && suv != null && st != null &&
                    sv.Length >= 3 && suv.Length == sv.Length && st.Length >= 3)
                {
                    // Front
                    int baseFront = verts.Count;
                    for (int i = 0; i < sv.Length; i++)
                    {
                        verts.Add(new Vector3(sv[i].x, sv[i].y, zFrontFace));
                        uvs.Add(suv[i]);
                    }
                    for (int i = 0; i < st.Length; i += 3)
                    {
                        trisFront.Add(baseFront + st[i + 0]);
                        trisFront.Add(baseFront + st[i + 1]);
                        trisFront.Add(baseFront + st[i + 2]);
                    }

                    // Back
                    if (buildBackFace)
                    {
                        int baseBack = verts.Count;
                        for (int i = 0; i < sv.Length; i++)
                        {
                            verts.Add(new Vector3(sv[i].x, sv[i].y, zBackFace));
                            uvs.Add(suv[i]);
                        }
                        // Reverse winding so it faces outward
                        for (int i = 0; i < st.Length; i += 3)
                        {
                            trisBack.Add(baseBack + st[i + 2]);
                            trisBack.Add(baseBack + st[i + 1]);
                            trisBack.Add(baseBack + st[i + 0]);
                        }
                    }

                    builtFaces = true;
                }
            }

            // Fallback: physics outline + earclip
            if (!builtFaces)
            {
                if (shapeCount > 0)
                {
                    var poly = new List<Vector2>(256);

                    for (int s = 0; s < shapeCount; s++)
                    {
                        poly.Clear();
                        sp.GetPhysicsShape(s, poly);
                        if (poly.Count < 3) continue;

                        if (SignedArea(poly) < 0f)
                            poly.Reverse();

                        var tri = EarClipTriangulate(poly);
                        if (tri.Count < 3) continue;

                        // Front
                        int baseFront = verts.Count;
                        for (int i = 0; i < poly.Count; i++)
                        {
                            Vector2 p = poly[i];
                            verts.Add(new Vector3(p.x, p.y, zFrontFace));
                            uvs.Add(ToAtlasUV(p));
                        }
                        for (int i = 0; i < tri.Count; i += 3)
                        {
                            trisFront.Add(baseFront + tri[i + 0]);
                            trisFront.Add(baseFront + tri[i + 1]);
                            trisFront.Add(baseFront + tri[i + 2]);
                        }

                        // Back
                        if (buildBackFace)
                        {
                            int baseBack = verts.Count;
                            for (int i = 0; i < poly.Count; i++)
                            {
                                Vector2 p = poly[i];
                                verts.Add(new Vector3(p.x, p.y, zBackFace));
                                uvs.Add(ToAtlasUV(p));
                            }
                            for (int i = 0; i < tri.Count; i += 3)
                            {
                                trisBack.Add(baseBack + tri[i + 2]);
                                trisBack.Add(baseBack + tri[i + 1]);
                                trisBack.Add(baseBack + tri[i + 0]);
                            }
                        }

                        builtFaces = true;
                    }
                }
            }
        }

        // --- SIDES ---
        bool hasSides = (sideMaterial != null && thickness > 0.000001f && shapeCount > 0);

        if (hasSides)
        {
            var poly = new List<Vector2>(256);

            for (int s = 0; s < shapeCount; s++)
            {
                poly.Clear();
                sp.GetPhysicsShape(s, poly);
                if (poly.Count < 2) continue;

                if (SignedArea(poly) < 0f)
                    poly.Reverse();

                int n = poly.Count;
                for (int i = 0; i < n; i++)
                {
                    Vector2 p0 = poly[i];
                    Vector2 p1 = poly[(i + 1) % n];

                    int q = verts.Count;

                    // Quad: p0 back -> p0 front -> p1 front -> p1 back
                    verts.Add(new Vector3(p0.x, p0.y, zBackShell));   uvs.Add(new Vector2(0f, 0f));
                    verts.Add(new Vector3(p0.x, p0.y, zFrontShell));  uvs.Add(new Vector2(0f, 1f));
                    verts.Add(new Vector3(p1.x, p1.y, zFrontShell));  uvs.Add(new Vector2(1f, 1f));
                    verts.Add(new Vector3(p1.x, p1.y, zBackShell));   uvs.Add(new Vector2(1f, 0f));

                    trisSides.Add(q + 0); trisSides.Add(q + 1); trisSides.Add(q + 2);
                    trisSides.Add(q + 0); trisSides.Add(q + 2); trisSides.Add(q + 3);
                }
            }
        }

        // If we have nothing to render, bail.
        if (verts.Count == 0)
            return;

        // --- Mesh build ---
        Mesh mesh = _mf.sharedMesh;
        if (mesh == null)
        {
            mesh = new Mesh();
            mesh.name = $"OutlineThick_{sp.name}";
            _mf.sharedMesh = mesh;
        }
        else
        {
            mesh.Clear();
        }

        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);

        // Submesh layout:
        // If splitFrontBackSubmeshes && faces built: Back (0) -> Sides (sideSubmesh) -> Front (frontSubmesh)
        // If sidesOnly: Sides only in submesh 0
        int sideSubmesh = -1;
        int frontSubmesh = -1;
        int subMeshCount = 0;

        bool wantFaces = (!sidesOnly && builtFaces && frontBackMaterial != null);
        bool wantSplit = (wantFaces && splitFrontBackSubmeshes);

        if (!wantFaces)
        {
            // Sides-only mesh
            if (hasSides && trisSides.Count > 0)
            {
                subMeshCount = 1;
                mesh.subMeshCount = subMeshCount;
                mesh.SetTriangles(trisSides, 0);
                _mr.sharedMaterials = new[] { sideMaterial };
            }
            else
            {
                // Nothing meaningful built
                return;
            }
        }
        else
        {
            if (wantSplit)
            {
                // Keep user’s sideMaterialIndex, but enforce ordering Back -> Sides -> Front.
                // back = 0
                sideSubmesh = hasSides ? Mathf.Max(1, sideMaterialIndex) : -1;
                frontSubmesh = hasSides ? (sideSubmesh + 1) : 1;

                subMeshCount = hasSides ? (frontSubmesh + 1) : 2;
                mesh.subMeshCount = subMeshCount;

                // Fill all submeshes with empty first
                for (int sm = 0; sm < subMeshCount; sm++)
                    mesh.SetTriangles(System.Array.Empty<int>(), sm);

                // Back (may be empty if buildBackFace=false)
                if (buildBackFace && trisBack.Count > 0)
                    mesh.SetTriangles(trisBack, 0);

                // Sides
                if (hasSides && trisSides.Count > 0)
                    mesh.SetTriangles(trisSides, sideSubmesh);

                // Front
                if (trisFront.Count > 0)
                    mesh.SetTriangles(trisFront, frontSubmesh);

                // Materials aligned with submesh slots
                var mats = new Material[subMeshCount];
                Material faceMat = frontBackMaterial;
                Material backMat = backFaceMaterial ? backFaceMaterial : faceMat;

                for (int i = 0; i < mats.Length; i++)
                    mats[i] = faceMat;

                mats[0] = backMat;

                if (hasSides && sideSubmesh >= 0 && sideSubmesh < mats.Length)
                    mats[sideSubmesh] = sideMaterial;

                mats[frontSubmesh] = faceMat;

                _mr.sharedMaterials = mats;
            }
            else
            {
                // Legacy mode: single front/back submesh + optional side submesh at sideMaterialIndex
                // (kept for compatibility, but NOT recommended if you're fighting depth issues)
                var trisFrontBackCombined = new List<int>(trisBack.Count + trisFront.Count);
                if (buildBackFace) trisFrontBackCombined.AddRange(trisBack);
                trisFrontBackCombined.AddRange(trisFront);

                bool hasSidesLegacy = (hasSides && trisSides.Count > 0);
                if (hasSidesLegacy)
                {
                    int sideSm = (sideMaterialIndex <= 0) ? 1 : sideMaterialIndex;
                    int smCount = Mathf.Max(2, sideSm + 1);

                    mesh.subMeshCount = smCount;
                    mesh.SetTriangles(trisFrontBackCombined, 0);

                    for (int sm = 1; sm < smCount; sm++)
                    {
                        if (sm == sideSm)
                            mesh.SetTriangles(trisSides, sm);
                        else
                            mesh.SetTriangles(System.Array.Empty<int>(), sm);
                    }

                    var mats = new Material[smCount];
                    for (int i = 0; i < mats.Length; i++)
                        mats[i] = frontBackMaterial;

                    mats[0] = frontBackMaterial;
                    mats[sideSm] = sideMaterial;
                    _mr.sharedMaterials = mats;
                }
                else
                {
                    mesh.subMeshCount = 1;
                    mesh.SetTriangles(trisFrontBackCombined, 0);
                    _mr.sharedMaterials = new[] { frontBackMaterial };
                }
            }
        }

        mesh.RecalculateBounds();
        mesh.RecalculateNormals();

        // --- Property blocks (per submesh index) ---
        if (_mpb0 == null) _mpb0 = new MaterialPropertyBlock();
        if (_mpb1 == null) _mpb1 = new MaterialPropertyBlock();
        if (_mpb2 == null) _mpb2 = new MaterialPropertyBlock();

        if (pushSpriteTextureToMaterial && sp.texture != null)
        {
            // Back face (submesh 0)
            ApplyMpbToSubmesh(0, sp, tint, enableSidePatch: false, hasUvBounds, uvMinAll, uvMaxAll);

            // Sides (if present)
            if (sideSubmesh >= 0)
                ApplyMpbToSubmesh(sideSubmesh, sp, tint, enableSidePatch: enableSideAutoPatch, hasUvBounds, uvMinAll, uvMaxAll);

            // Front (if present)
            if (frontSubmesh >= 0)
                ApplyMpbToSubmesh(frontSubmesh, sp, tint, enableSidePatch: false, hasUvBounds, uvMinAll, uvMaxAll);

            // Sides-only mode (single submesh 0)
            if (!wantFaces && _mr.sharedMaterials != null && _mr.sharedMaterials.Length == 1)
                ApplyMpbToSubmesh(0, sp, tint, enableSidePatch: enableSideAutoPatch, hasUvBounds, uvMinAll, uvMaxAll);
        }
        else
        {
            // Still apply tint if possible
            ApplyTintOnly(0, tint);
            if (sideSubmesh >= 0) ApplyTintOnly(sideSubmesh, tint);
            if (frontSubmesh >= 0) ApplyTintOnly(frontSubmesh, tint);
        }
    }

    void ApplyTintOnly(int submesh, Color c)
    {
        MaterialPropertyBlock mpb = GetBlock(submesh);
        _mr.GetPropertyBlock(mpb, submesh);
        mpb.SetColor(ColorId, c);
        _mr.SetPropertyBlock(mpb, submesh);
    }

    void ApplyMpbToSubmesh(int submesh, Sprite sp, Color c, bool enableSidePatch, bool hasUvBounds, Vector2 uvMin, Vector2 uvMax)
    {
        MaterialPropertyBlock mpb = GetBlock(submesh);

        _mr.GetPropertyBlock(mpb, submesh);

        mpb.SetTexture(BaseMapId, sp.texture);
        mpb.SetTexture(MainTexId, sp.texture);

        if (enableSidePatch && hasUvBounds)
        {
            ComputeSidePatchST(sp, uvMin, uvMax, out Vector4 st);
            mpb.SetVector(BaseMapStId, st);
            mpb.SetVector(MainTexStId, st);
        }
        else
        {
            mpb.SetVector(BaseMapStId, new Vector4(1f, 1f, 0f, 0f));
            mpb.SetVector(MainTexStId, new Vector4(1f, 1f, 0f, 0f));
        }

        mpb.SetColor(ColorId, c);

        _mr.SetPropertyBlock(mpb, submesh);
    }

    MaterialPropertyBlock GetBlock(int submesh)
    {
        // Just give stable blocks for the first few indices.
        if (submesh == 0) return _mpb0;
        if (submesh == 1) return _mpb1;
        return _mpb2;
    }

    void ComputeSidePatchST(Sprite sp, Vector2 uvMin, Vector2 uvMax, out Vector4 st)
    {
        Vector2 size = uvMax - uvMin;
        if (size.x <= 0.000001f || size.y <= 0.000001f)
        {
            st = new Vector4(1f, 1f, 0f, 0f);
            return;
        }

        float insetX = size.x * sideSafeInset;
        float insetY = size.y * sideSafeInset;

        Vector2 safeMin = new Vector2(uvMin.x + insetX, uvMin.y + insetY);
        Vector2 safeMax = new Vector2(uvMax.x - insetX, uvMax.y - insetY);

        Vector2 safeSize = safeMax - safeMin;
        if (safeSize.x <= 0.000001f || safeSize.y <= 0.000001f)
        {
            safeMin = uvMin;
            safeMax = uvMax;
            safeSize = safeMax - safeMin;
            if (safeSize.x <= 0.000001f || safeSize.y <= 0.000001f)
            {
                st = new Vector4(1f, 1f, 0f, 0f);
                return;
            }
        }

        float minSide = Mathf.Min(safeSize.x, safeSize.y);
        float patch = Mathf.Clamp(minSide * sidePatchCoverage, 0.0005f, minSide);

        Vector2 patchSize = new Vector2(patch, patch);
        Vector2 center = (safeMin + safeMax) * 0.5f;

        if (sidePatchJitter > 0.0001f)
        {
            int seed = StableSpriteSeed(sp);

            int saltX = unchecked((int)0xA1B2C3D4u);
            int saltY = unchecked((int)0x1F2E3D4Cu);

            float rx = HashToSigned01(seed ^ saltX);
            float ry = HashToSigned01(seed ^ saltY);

            Vector2 jitterRange = new Vector2(safeSize.x, safeSize.y) * sidePatchJitter;
            center += new Vector2(rx * jitterRange.x, ry * jitterRange.y);
        }

        Vector2 half = patchSize * 0.5f;
        center.x = Mathf.Clamp(center.x, safeMin.x + half.x, safeMax.x - half.x);
        center.y = Mathf.Clamp(center.y, safeMin.y + half.y, safeMax.y - half.y);

        Vector2 patchMin = center - half;

        st = new Vector4(patchSize.x, patchSize.y, patchMin.x, patchMin.y);
    }

    static int StableSpriteSeed(Sprite sp)
    {
        unchecked
        {
            int h = 17;
            h = h * 31 + (sp.name != null ? sp.name.GetHashCode() : 0);
            Rect r = sp.textureRect;
            h = h * 31 + r.x.GetHashCode();
            h = h * 31 + r.y.GetHashCode();
            h = h * 31 + r.width.GetHashCode();
            h = h * 31 + r.height.GetHashCode();
            Vector2 pv = sp.pivot;
            h = h * 31 + pv.x.GetHashCode();
            h = h * 31 + pv.y.GetHashCode();
            return h;
        }
    }

    static float HashToSigned01(int seed)
    {
        unchecked
        {
            uint x = (uint)seed;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;

            float f = (x & 0x00FFFFFFu) / 16777215f;
            return f * 2f - 1f;
        }
    }

    static float SignedArea(List<Vector2> p)
    {
        double a = 0;
        for (int i = 0; i < p.Count; i++)
        {
            Vector2 v0 = p[i];
            Vector2 v1 = p[(i + 1) % p.Count];
            a += (double)v0.x * v1.y - (double)v1.x * v0.y;
        }
        return (float)(a * 0.5);
    }

    static List<int> EarClipTriangulate(List<Vector2> poly)
    {
        var result = new List<int>(Mathf.Max(0, (poly.Count - 2) * 3));
        int n = poly.Count;
        if (n < 3) return result;

        var V = new List<int>(n);
        for (int i = 0; i < n; i++) V.Add(i);

        int guard = 0;
        while (V.Count > 2 && guard++ < 10000)
        {
            bool earFound = false;

            for (int i = 0; i < V.Count; i++)
            {
                int i0 = V[(i - 1 + V.Count) % V.Count];
                int i1 = V[i];
                int i2 = V[(i + 1) % V.Count];

                Vector2 a = poly[i0];
                Vector2 b = poly[i1];
                Vector2 c = poly[i2];

                if (!IsConvex(a, b, c)) continue;

                bool anyInside = false;
                for (int j = 0; j < V.Count; j++)
                {
                    int vi = V[j];
                    if (vi == i0 || vi == i1 || vi == i2) continue;
                    if (PointInTri(poly[vi], a, b, c))
                    {
                        anyInside = true;
                        break;
                    }
                }

                if (anyInside) continue;

                result.Add(i0);
                result.Add(i1);
                result.Add(i2);
                V.RemoveAt(i);
                earFound = true;
                break;
            }

            if (!earFound)
                break;
        }

        return result;
    }

    static bool IsConvex(Vector2 a, Vector2 b, Vector2 c)
    {
        Vector2 ab = b - a;
        Vector2 bc = c - b;
        float cross = ab.x * bc.y - ab.y * bc.x;
        return cross > 0.0000001f;
    }

    static bool PointInTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float s1 = Sign(p, a, b);
        float s2 = Sign(p, b, c);
        float s3 = Sign(p, c, a);
        bool hasNeg = (s1 < 0) || (s2 < 0) || (s3 < 0);
        bool hasPos = (s1 > 0) || (s2 > 0) || (s3 > 0);
        return !(hasNeg && hasPos);
    }

    static float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
    {
        return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
    }
}
