// Assets/Scripts/Fish/FishTarget.cs
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class FishTarget : MonoBehaviour
{
    [Header("Reaction minigame settings")]
    [Range(0.05f, 0.60f)] public float greenZoneWidth = 0.18f; // fraction of bar width (hard fish = small)
    [Range(0f, 1f)]        public float greenZoneCenter = 0.5f; // 0..1 position along bar
    [Range(0.6f, 3.0f)]    public float markerSpeed = 1.4f;     // multiplier for oscillation speed

    [Header("Optional visuals")]
    public SpriteRenderer outlineWhenHover; // assign to glow on hover (optional)
}
