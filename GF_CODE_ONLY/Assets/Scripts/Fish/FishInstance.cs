using UnityEngine;

public class FishInstance : MonoBehaviour
{
    void OnEnable()  { FishWorldVisibility.Register(gameObject); }
    void OnDisable() { FishWorldVisibility.Unregister(gameObject); }
}
