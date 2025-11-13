// Файл: Assets/Scripts/UI/Tutorial/TutorialHelpSpot.cs (ОТКАТ к v10)
using UnityEngine;

[System.Serializable]
public class TutorialHelpSpot
{
    
    [Tooltip("Уникальный ID для сохранения (например, 'MainMenu_NewGameButton')")]
    public string spotID;
    
    [Tooltip("UI элемент (RectTransform), РЯДОМ с которым появится папка")]
    public RectTransform targetElement;

    [Tooltip("Смещение папки относительно targetElement")]
    public Vector2 mascotPositionOffset = new Vector2(0, 100);

    [Header("Настройки Маскота")]
    [Tooltip("Спрайт эмоции для этой подсказки")]
    public Sprite mascotEmotionSprite;
    
    [Header("Настройки Указателя")]
    [Tooltip("Спрайт 'руки' (один из 8 вариантов), который будет указывать")]
    public Sprite pointerSprite;
    [Tooltip("Смещение указателя относительно *папки*")]
    public Vector2 pointerPositionOffset = new Vector2(-50, 0);
    [Tooltip("Поворот указателя (ось Z)")]
    public float pointerRotation = 0f;

    [Header("Текст")]
    [Tooltip("Текст подсказки")]
    [TextArea(3, 5)]
    public string helpText;
}