using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(PolygonCollider2D), typeof(Image))]
public class PolygonHitTest : MonoBehaviour, ICanvasRaycastFilter
{
    private PolygonCollider2D _collider;
    private Image _image;

    void Awake()
    {
        _collider = GetComponent<PolygonCollider2D>();
        _image = GetComponent<Image>();
    }

    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        // Эта магия превращает клик мыши в проверку попадания в коллайдер
        if (_collider == null) return true;

        // Для Canvas Screen Space - Overlay камера не нужна, но для Camera - нужна
        // Этот метод универсален
        Vector3 worldPoint;
        RectTransformUtility.ScreenPointToWorldPointInRectangle(
            GetComponent<RectTransform>(), 
            screenPoint, 
            eventCamera, 
            out worldPoint
        );

        return _collider.OverlapPoint(worldPoint);
    }
}