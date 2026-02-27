using UnityEngine;
using System.Collections.Generic;

namespace GalacticFishing.Minigames.Dungeon3D
{
    [DisallowMultipleComponent]
    public sealed class DungeonSpriteHitboxAutoSizer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private BoxCollider damageTrigger;
        [SerializeField] private MeshCollider contourTrigger;

        [Header("Tightness")]
        [SerializeField, Range(0.1f, 1f)] private float widthTightness = 1f;
        [SerializeField, Range(0.1f, 1f)] private float heightTightness = 1f;
        [SerializeField, Min(0.01f)] private float depth = 1f;
        [SerializeField, Min(0f)] private float minSize = 0.05f;

        [Header("Contour Mesh (Optional)")]
        [SerializeField] private bool useContourHitbox = false;

        [Header("Alpha Scan")]
        [SerializeField, Range(0, 255)] private byte alphaThreshold = 10;

        private Sprite _lastSprite;
        private bool _lastFlipX;
        private bool _lastFlipY;
        private Mesh _runtimeContourMesh;

        private void Reset()
        {
            CacheRefs();
            ForceRebuildNow();
        }

        private void Awake()
        {
            CacheRefs();
        }

        private void OnEnable()
        {
            ForceRebuildNow();
        }

        private void LateUpdate()
        {
            if (spriteRenderer == null)
                return;

            // Support contour-only setups: a MeshCollider hitbox can be valid even if no BoxCollider is assigned.
            if (damageTrigger == null && contourTrigger == null)
                return;

            Sprite current = spriteRenderer.sprite;
            bool flipX = spriteRenderer.flipX;
            bool flipY = spriteRenderer.flipY;

            if (current == _lastSprite && flipX == _lastFlipX && flipY == _lastFlipY)
                return;

            RebuildHitbox(current);
        }

        private void OnValidate()
        {
            CacheRefs();
            ForceRebuildNow();
        }

        private void CacheRefs()
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);

            if (damageTrigger == null)
                damageTrigger = GetComponentInChildren<BoxCollider>(true);

            if (contourTrigger == null)
                contourTrigger = GetComponentInChildren<MeshCollider>(true);
        }

        private void ForceRebuildNow()
        {
            if (spriteRenderer == null)
                return;

            if (damageTrigger == null && contourTrigger == null)
                return;

            RebuildHitbox(spriteRenderer.sprite);
        }

        private void RebuildHitbox(Sprite sprite)
        {
            _lastSprite = sprite;
            _lastFlipX = spriteRenderer != null && spriteRenderer.flipX;
            _lastFlipY = spriteRenderer != null && spriteRenderer.flipY;

            if (sprite == null)
                return;

            if (useContourHitbox && TryBuildContourHitbox(sprite))
            {
                // Keep the BoxCollider active as the solid physics body while the contour mesh remains a trigger for hits.
                SetBoxEnabled(true);
                SetContourEnabled(true);
                return;
            }

            SetContourEnabled(false);
            if (damageTrigger == null)
                return;

            SetBoxEnabled(true);

            // Prefer the renderer's current local bounds so the 3D trigger matches the visible sprite footprint.
            // This avoids undersized "small square" hitboxes and tracks sprite swaps without hand-tuning.
            if (spriteRenderer != null)
            {
                Bounds lb = spriteRenderer.localBounds;
                if (lb.size.x > 0f && lb.size.y > 0f)
                {
                    Vector3 size = lb.size;
                    size.x = Mathf.Max(minSize, size.x * widthTightness);
                    size.y = Mathf.Max(minSize, size.y * heightTightness);
                    size.z = Mathf.Max(0.01f, depth);

                    damageTrigger.center = new Vector3(lb.center.x, lb.center.y, damageTrigger.center.z);
                    damageTrigger.size = size;
                    return;
                }
            }

            Rect opaqueRect;
            if (!TryGetOpaqueBounds(sprite, out opaqueRect))
                opaqueRect = new Rect(0f, 0f, sprite.rect.width, sprite.rect.height);

            float ppu = Mathf.Max(0.0001f, sprite.pixelsPerUnit);

            float widthUnits = Mathf.Max(minSize, (opaqueRect.width / ppu) * widthTightness);
            float heightUnits = Mathf.Max(minSize, (opaqueRect.height / ppu) * heightTightness);
            Vector2 centerPx = opaqueRect.center;

            Vector3 centerLocal = new Vector3(
                (centerPx.x - sprite.pivot.x) / ppu,
                (centerPx.y - sprite.pivot.y) / ppu,
                damageTrigger.center.z
            );

            if (spriteRenderer != null)
            {
                if (spriteRenderer.flipX)
                    centerLocal.x = -centerLocal.x;
                if (spriteRenderer.flipY)
                    centerLocal.y = -centerLocal.y;
            }

            damageTrigger.center = centerLocal;
            damageTrigger.size = new Vector3(widthUnits, heightUnits, Mathf.Max(0.01f, depth));
        }

        private void SetBoxEnabled(bool enabled)
        {
            if (damageTrigger != null)
                damageTrigger.enabled = enabled;
        }

        private void SetContourEnabled(bool enabled)
        {
            if (contourTrigger != null)
                contourTrigger.enabled = enabled;
        }

        private bool TryBuildContourHitbox(Sprite sprite)
        {
            if (sprite == null)
                return false;

            if (sprite.GetPhysicsShapeCount() <= 0)
                return false;

            if (!EnsureContourCollider())
                return false;

            if (!TryBuildExtrudedContourMesh(sprite, out Mesh mesh))
                return false;

            ReplaceRuntimeContourMesh(mesh);
            contourTrigger.sharedMesh = null;
            contourTrigger.sharedMesh = _runtimeContourMesh;
            contourTrigger.convex = true;
            contourTrigger.isTrigger = true;
            return true;
        }

        private bool EnsureContourCollider()
        {
            if (contourTrigger == null)
                contourTrigger = GetComponent<MeshCollider>();

            if (contourTrigger == null)
                contourTrigger = gameObject.AddComponent<MeshCollider>();

            if (contourTrigger == null)
                return false;

            contourTrigger.convex = true;
            contourTrigger.isTrigger = true;
            return true;
        }

        private bool TryBuildExtrudedContourMesh(Sprite sprite, out Mesh mesh)
        {
            mesh = null;

            int shapeCount = sprite.GetPhysicsShapeCount();
            if (shapeCount <= 0)
                return false;

            var vertices = new List<Vector3>(128);
            var triangles = new List<int>(256);
            var path = new List<Vector2>(64);
            var tri2D = new List<int>(64);

            float halfDepth = Mathf.Max(0.01f, depth) * 0.5f;
            float zCenter = damageTrigger != null ? damageTrigger.center.z : 0f;
            float zFront = zCenter - halfDepth;
            float zBack = zCenter + halfDepth;

            bool anyPathBuilt = false;

            for (int s = 0; s < shapeCount; s++)
            {
                path.Clear();
                sprite.GetPhysicsShape(s, path);

                if (!NormalizePhysicsPath(path))
                    continue;

                ApplySpriteFlip(path);

                tri2D.Clear();
                if (!TryTriangulate(path, tri2D))
                    continue;

                AppendExtrudedPathMesh(path, tri2D, zFront, zBack, vertices, triangles);
                anyPathBuilt = true;
            }

            if (!anyPathBuilt || vertices.Count < 3 || triangles.Count < 3)
                return false;

            mesh = new Mesh
            {
                name = $"EnemyHitboxContour_{sprite.name}"
            };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0, true);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return true;
        }

        private bool NormalizePhysicsPath(List<Vector2> path)
        {
            if (path == null || path.Count < 3)
                return false;

            // Remove duplicate closing point if present and collapse consecutive duplicates.
            for (int i = path.Count - 1; i > 0; i--)
            {
                if ((path[i] - path[i - 1]).sqrMagnitude <= 0.0000001f)
                    path.RemoveAt(i);
            }

            if (path.Count >= 2 && (path[0] - path[path.Count - 1]).sqrMagnitude <= 0.0000001f)
                path.RemoveAt(path.Count - 1);

            if (path.Count < 3)
                return false;

            // Remove simple colinear points to help triangulation on authored physics shapes.
            for (int i = path.Count - 1; i >= 0 && path.Count >= 3; i--)
            {
                int prev = (i - 1 + path.Count) % path.Count;
                int next = (i + 1) % path.Count;
                Vector2 a = path[prev];
                Vector2 b = path[i];
                Vector2 c = path[next];
                if (Mathf.Abs(Cross(b - a, c - b)) <= 0.000001f)
                    path.RemoveAt(i);
            }

            if (path.Count < 3)
                return false;

            // Ensure CCW winding for front-face triangulation.
            if (SignedArea(path) < 0f)
                path.Reverse();

            return true;
        }

        private void ApplySpriteFlip(List<Vector2> path)
        {
            if (spriteRenderer == null || path == null)
                return;

            bool flipX = spriteRenderer.flipX;
            bool flipY = spriteRenderer.flipY;
            if (!flipX && !flipY)
                return;

            for (int i = 0; i < path.Count; i++)
            {
                Vector2 p = path[i];
                if (flipX) p.x = -p.x;
                if (flipY) p.y = -p.y;
                path[i] = p;
            }

            if (SignedArea(path) < 0f)
                path.Reverse();
        }

        private static void AppendExtrudedPathMesh(
            List<Vector2> path,
            List<int> tri2D,
            float zFront,
            float zBack,
            List<Vector3> vertices,
            List<int> triangles)
        {
            int n = path.Count;
            int frontBase = vertices.Count;

            for (int i = 0; i < n; i++)
            {
                Vector2 p = path[i];
                vertices.Add(new Vector3(p.x, p.y, zFront));
            }

            int backBase = vertices.Count;
            for (int i = 0; i < n; i++)
            {
                Vector2 p = path[i];
                vertices.Add(new Vector3(p.x, p.y, zBack));
            }

            // Front face (CCW)
            for (int i = 0; i < tri2D.Count; i += 3)
            {
                triangles.Add(frontBase + tri2D[i]);
                triangles.Add(frontBase + tri2D[i + 1]);
                triangles.Add(frontBase + tri2D[i + 2]);
            }

            // Back face (reverse winding)
            for (int i = 0; i < tri2D.Count; i += 3)
            {
                triangles.Add(backBase + tri2D[i + 2]);
                triangles.Add(backBase + tri2D[i + 1]);
                triangles.Add(backBase + tri2D[i]);
            }

            // Side walls
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;

                int fi = frontBase + i;
                int fj = frontBase + j;
                int bi = backBase + i;
                int bj = backBase + j;

                triangles.Add(fi);
                triangles.Add(fj);
                triangles.Add(bj);

                triangles.Add(fi);
                triangles.Add(bj);
                triangles.Add(bi);
            }
        }

        private static bool TryTriangulate(List<Vector2> polygon, List<int> outTriangles)
        {
            outTriangles.Clear();
            if (polygon == null || polygon.Count < 3)
                return false;

            var indices = new List<int>(polygon.Count);
            for (int i = 0; i < polygon.Count; i++)
                indices.Add(i);

            int guard = 0;
            int maxGuard = polygon.Count * polygon.Count;

            while (indices.Count > 2 && guard++ < maxGuard)
            {
                bool earFound = false;

                for (int i = 0; i < indices.Count; i++)
                {
                    int prev = indices[(i - 1 + indices.Count) % indices.Count];
                    int curr = indices[i];
                    int next = indices[(i + 1) % indices.Count];

                    Vector2 a = polygon[prev];
                    Vector2 b = polygon[curr];
                    Vector2 c = polygon[next];

                    if (!IsConvex(a, b, c))
                        continue;

                    bool containsOtherPoint = false;
                    for (int j = 0; j < indices.Count; j++)
                    {
                        int idx = indices[j];
                        if (idx == prev || idx == curr || idx == next)
                            continue;

                        if (PointInTriangle(polygon[idx], a, b, c))
                        {
                            containsOtherPoint = true;
                            break;
                        }
                    }

                    if (containsOtherPoint)
                        continue;

                    outTriangles.Add(prev);
                    outTriangles.Add(curr);
                    outTriangles.Add(next);
                    indices.RemoveAt(i);
                    earFound = true;
                    break;
                }

                if (!earFound)
                    break;
            }

            return outTriangles.Count >= 3;
        }

        private static bool IsConvex(Vector2 a, Vector2 b, Vector2 c)
        {
            return Cross(b - a, c - b) > 0.000001f;
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return (a.x * b.y) - (a.y * b.x);
        }

        private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            // Barycentric sign method with inclusive edge test.
            float d1 = Sign(p, a, b);
            float d2 = Sign(p, b, c);
            float d3 = Sign(p, c, a);

            bool hasNeg = (d1 < 0f) || (d2 < 0f) || (d3 < 0f);
            bool hasPos = (d1 > 0f) || (d2 > 0f) || (d3 > 0f);
            return !(hasNeg && hasPos);
        }

        private static float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
        {
            return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
        }

        private static float SignedArea(List<Vector2> path)
        {
            if (path == null || path.Count < 3)
                return 0f;

            float area = 0f;
            for (int i = 0; i < path.Count; i++)
            {
                Vector2 p0 = path[i];
                Vector2 p1 = path[(i + 1) % path.Count];
                area += (p0.x * p1.y) - (p1.x * p0.y);
            }

            return area * 0.5f;
        }

        private void ReplaceRuntimeContourMesh(Mesh newMesh)
        {
            if (_runtimeContourMesh != null && _runtimeContourMesh != newMesh)
            {
                if (Application.isPlaying)
                    Destroy(_runtimeContourMesh);
                else
                    DestroyImmediate(_runtimeContourMesh);
            }

            _runtimeContourMesh = newMesh;
        }

        private void OnDestroy()
        {
            if (_runtimeContourMesh == null)
                return;

            if (Application.isPlaying)
                Destroy(_runtimeContourMesh);
            else
                DestroyImmediate(_runtimeContourMesh);

            _runtimeContourMesh = null;
        }

        private bool TryGetOpaqueBounds(Sprite sprite, out Rect opaqueRect)
        {
            opaqueRect = default;

            Texture2D tex = sprite.texture;
            if (tex == null)
                return false;

            Rect textureRect = sprite.textureRect;
            int texWidth = tex.width;
            int texHeight = tex.height;

            int xMin = Mathf.Clamp(Mathf.FloorToInt(textureRect.xMin), 0, texWidth - 1);
            int xMax = Mathf.Clamp(Mathf.CeilToInt(textureRect.xMax) - 1, 0, texWidth - 1);
            int yMin = Mathf.Clamp(Mathf.FloorToInt(textureRect.yMin), 0, texHeight - 1);
            int yMax = Mathf.Clamp(Mathf.CeilToInt(textureRect.yMax) - 1, 0, texHeight - 1);

            if (xMax < xMin || yMax < yMin)
                return false;

            Color32[] pixels;
            try
            {
                pixels = tex.GetPixels32();
            }
            catch
            {
                // If Read/Write is disabled, gracefully fall back to full sprite rect.
                return false;
            }

            bool found = false;
            int minX = int.MaxValue;
            int minY = int.MaxValue;
            int maxX = int.MinValue;
            int maxY = int.MinValue;

            for (int y = yMin; y <= yMax; y++)
            {
                int rowBase = y * texWidth;
                for (int x = xMin; x <= xMax; x++)
                {
                    if (pixels[rowBase + x].a <= alphaThreshold)
                        continue;

                    if (!found)
                    {
                        found = true;
                        minX = maxX = x;
                        minY = maxY = y;
                        continue;
                    }

                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }

            if (!found)
                return false;

            float localMinX = minX - xMin;
            float localMaxX = (maxX - xMin) + 1f;
            float localMinY = minY - yMin;
            float localMaxY = (maxY - yMin) + 1f;
            opaqueRect = Rect.MinMaxRect(localMinX, localMinY, localMaxX, localMaxY);
            return true;
        }
    }
}
