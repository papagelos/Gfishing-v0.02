using UnityEngine;

public sealed class DestroyAfterSeconds : MonoBehaviour
{
    [Min(0.01f)] public float lifetime = 0.35f;

    private void OnEnable()
    {
        Destroy(gameObject, lifetime);
    }
}
