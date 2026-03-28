using UnityEngine;

public class CursorController : MonoBehaviour
{
    [Tooltip("Текстура для кастомного курсора")]
    public Texture2D cursorTexture;

    [Tooltip("Текстура курсора при зажатой мыши (повернутая на 5 градусов)")]
    public Texture2D cursorTextureClicked;

    [Tooltip("Смещение точки клика от верхнего левого угла текстуры (в пикселях)")]
    public Vector2 hotspot = Vector2.zero; // По умолчанию - верхний левый угол

    [Tooltip("Режим курсора (Auto - система решает, Software - принудительно программный)")]
    public CursorMode cursorMode = CursorMode.Auto;

    void Start()
    {
        SetCustomCursor();
    }

    void Update()
    {
        // При нажатии меняем текстуру на повернутую
        if (Input.GetMouseButtonDown(0))
        {
            if (cursorTextureClicked != null)
            {
                Cursor.SetCursor(cursorTextureClicked, hotspot, cursorMode);
            }
        }
        // При отпускании возвращаем обычную
        else if (Input.GetMouseButtonUp(0))
        {
            SetCustomCursor();
        }
    }

    void SetCustomCursor()
    {
        if (cursorTexture != null)
        {
            Cursor.SetCursor(cursorTexture, hotspot, cursorMode);
        }
    }
}
