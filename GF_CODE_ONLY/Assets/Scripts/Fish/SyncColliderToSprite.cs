// Assets/Scripts/Fish/SyncColliderToSprite.cs
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(PolygonCollider2D))]
public class SyncColliderToSprite : MonoBehaviour
{
    [SerializeField] bool autoUpdate = true;  // keep true unless you call UpdateCollider() yourself

    SpriteRenderer _sr;
    PolygonCollider2D _poly;
    Sprite _lastSprite;
    readonly List<Vector2> _points = new List<Vector2>(128);

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        _poly = GetComponent<PolygonCollider2D>();
    }

    void OnEnable()
    {
        UpdateCollider(); // build once on enable
    }

    void LateUpdate()
    {
        if (!autoUpdate) return;
        if (_sr.sprite != _lastSprite)
            UpdateCollider();
    }

    /// <summary>Rebuild the PolygonCollider2D paths from the Sprite's Physics Shape.</summary>
    public void UpdateCollider()
    {
        _lastSprite = _sr.sprite;
        if (_lastSprite == null) return;

        int shapeCount = _lastSprite.GetPhysicsShapeCount();
        if (shapeCount <= 0)
        {
            // no custom physics shape; fall back to sprite rect
            _poly.pathCount = 1;
            _points.Clear();
            var rect = _lastSprite.bounds;
            _points.Add(new Vector2(rect.min.x, rect.min.y));
            _points.Add(new Vector2(rect.min.x, rect.max.y));
            _points.Add(new Vector2(rect.max.x, rect.max.y));
            _points.Add(new Vector2(rect.max.x, rect.min.y));
            _poly.SetPath(0, _points);
            return;
        }

        _poly.pathCount = shapeCount;
        for (int i = 0; i < shapeCount; i++)
        {
            _points.Clear();
            _lastSprite.GetPhysicsShape(i, _points);
            _poly.SetPath(i, _points);
        }

        // toggle to force refresh if needed
        _poly.enabled = false; _poly.enabled = true;
    }
}
