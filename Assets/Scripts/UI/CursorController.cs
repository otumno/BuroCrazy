using UnityEngine;

public class CursorController : MonoBehaviour
{
    [Tooltip("Текстура для кастомного курсора")]
    public Texture2D cursorTexture;

    [Tooltip("Смещение точки клика от верхнего левого угла текстуры (в пикселях)")]
    public Vector2 hotspot = Vector2.zero; // По умолчанию - верхний левый угол

    [Tooltip("Режим курсора (Auto - система решает, Software - принудительно программный)")]
    public CursorMode cursorMode = CursorMode.Auto;

    void Start()
    {
        // Устанавливаем кастомный курсор при старте
        SetCustomCursor();
    }

    void SetCustomCursor()
    {
        // Проверяем, назначена ли текстура
        if (cursorTexture != null)
        {
            // Устанавливаем курсор
            // cursorTexture: Наша текстура
            // hotspot: Точка на текстуре, которая будет "кликать"
            // cursorMode: Режим (обычно Auto)
            Cursor.SetCursor(cursorTexture, hotspot, cursorMode);
            Debug.Log("Кастомный курсор установлен!");
        }
        else
        {
            Debug.LogWarning("Текстура курсора не назначена в CursorController!");
        }
    }

    // Опционально: Метод для возврата стандартного курсора
    // void SetDefaultCursor()
    // {
    //     Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    // }

    // Пример: Возвращаем стандартный курсор при выходе из игры
    // void OnApplicationQuit()
    // {
    //     SetDefaultCursor();
    // }
}