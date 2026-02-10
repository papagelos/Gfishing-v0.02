using UnityEngine;

public sealed class GlobalSystemsBootstrap : MonoBehaviour
{
    private static GlobalSystemsBootstrap _instance;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning($"[Singleton] Duplicate {GetType().Name} found on {gameObject.name}. Removing component only to preserve container.", this);
            Destroy(this);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }
}
