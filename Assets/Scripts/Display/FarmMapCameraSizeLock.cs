using UnityEngine;

[DefaultExecutionOrder(9999)]
public class FarmMapCameraSizeLock : MonoBehaviour
{
    public Camera cam;
    public bool lockSize = true;
    public float orthoSize = 8f;

    void Reset() => cam = GetComponent<Camera>();

    void LateUpdate()
    {
        if (!cam) cam = GetComponent<Camera>();
        if (!lockSize || !cam || !cam.orthographic) return;
        cam.orthographicSize = orthoSize;
    }
}
