using UnityEngine;
using UnityEngine.UI;

public enum UiCanvasCategory { HUD = 0, Panel = 50, Menu = 100, Popup = 200, Top = 1000 }

[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas))]
[RequireComponent(typeof(GraphicRaycaster))]
public class UICanvasConfig : MonoBehaviour
{
    public UiCanvasCategory category = UiCanvasCategory.Menu;
    public Vector2 referenceResolution = new Vector2(1920, 1080);
    [Range(0f,1f)] public float matchWidthOrHeight = 0.5f;
    public bool bringToFrontOnEnable = true;
    public int orderOffset = 0;

    Canvas canvas;

    void Reset()
    {
        canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;

        var scaler = GetComponent<CanvasScaler>() ?? gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.matchWidthOrHeight = matchWidthOrHeight;
    }

    void Awake()
    {
        canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;

        var scaler = GetComponent<CanvasScaler>() ?? gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.matchWidthOrHeight = matchWidthOrHeight;
    }

    void OnEnable()
    {
        if (bringToFrontOnEnable) UiSorting.BringToFront(GetComponent<Canvas>(), category, orderOffset);
    }

    public void BringToFront() => UiSorting.BringToFront(GetComponent<Canvas>(), category, orderOffset);
}
