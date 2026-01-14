using UnityEngine;
using UnityEngine.EventSystems;

public class UIHoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private float hoverScale = 1.3f;
    [SerializeField] private float speed = 12f;

    private Vector3 _baseScale;
    private Vector3 _targetScale;

    private void Awake()
    {
        _baseScale = transform.localScale;
        _targetScale = _baseScale;
    }

    public void OnPointerEnter(PointerEventData eventData) => _targetScale = _baseScale * hoverScale;
    public void OnPointerExit(PointerEventData eventData) => _targetScale = _baseScale;

    private void Update()
    {
        transform.localScale = Vector3.Lerp(transform.localScale, _targetScale, Time.unscaledDeltaTime * speed);
    }
}
