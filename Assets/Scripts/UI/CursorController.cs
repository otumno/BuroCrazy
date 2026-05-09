using UnityEngine;

public class CursorController : MonoBehaviour
{
    public static CursorController Instance { get; private set; }
    
    [Tooltip("Текстура для кастомного курсора")]
    public Texture2D cursorTexture;

    [Tooltip("Текстура курсора при зажатой мыши (повернутая на 5 градусов)")]
    public Texture2D cursorTextureClicked;

    [Tooltip("Смещение точки клика от верхнего левого угла текстуры (в пикселях)")]
    public Vector2 hotspot = Vector2.zero; // По умолчанию - верхний левый угол

    [Tooltip("Режим курсора (Auto - система решает, Software - принудительно программный)")]
    public CursorMode cursorMode = CursorMode.Auto;
    
    [Tooltip("Текстура курсора во время катсцен/туториалов")]
    public Texture2D cutsceneCursorTexture;
    
    private Texture2D _defaultCursorTexture;
    private Vector2 _defaultHotspot;

    private void Awake()
    {
        Instance = this;
        _defaultCursorTexture = cursorTexture;
        _defaultHotspot = hotspot;
    }
    
    public void SetCutsceneCursor()
    {
        SetCutsceneCursor(true);
    }

    public void SetCutsceneCursor(bool active)
    {
        if (active)
        {
            if (cutsceneCursorTexture != null)
            {
                Cursor.SetCursor(cutsceneCursorTexture, hotspot, cursorMode);
            }
        }
        else
        {
            SetDefaultCursor();
        }
    }

    public void SetDefaultCursor()
    {
        Cursor.SetCursor(_defaultCursorTexture, hotspot, cursorMode);
    }

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
