// Файл: Assets/Scripts/UI/Tutorial/TutorialContextGroup.cs (ФИНАЛЬНАЯ ВЕРСИЯ)
using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class TutorialContextGroup
{
    [Tooltip("Уникальный ID этого контекста (например, 'DirectorDesk')")]
    public string contextID;

    [Tooltip("Панель UI, которая должна быть активна, чтобы показать эти подсказки")]
    public GameObject contextPanel; 

    [Tooltip("Если true, маскот ПОЛНОСТЬЮ скроется, пока эта панель активна.")]
    public bool muteTutorial = false; 

    [Tooltip("Список подсказок для этого контекста")]
    public List<TutorialHelpSpot> helpSpots;

    [Header("Приветствие для этого контекста")]
    [Tooltip("Список приветствий (выбирается случайно)")]
    public List<string> greetingTexts;
    
    [Tooltip("Спрайт эмоции для приветствия")]
    public Sprite greetingEmotion;
    
    [Tooltip("Высота листка для приветствия")]
    public float greetingSheetHeight = 150f;
    
    [Tooltip("За сколько шагов лист приветствия достигнет высоты?")]
    public int greetingHeightSteps = 10;
    
    [Tooltip("Кол-во 'бипов' для приветствия")]
    public int greetingSoundRepetitions = 3;

    [Header("Рука для Приветствия (Опционально)")]
    [Tooltip("Спрайт 'руки' (например, 'idle рука'), который будет показан с приветствием")]
    public Sprite greetingPointerSprite;
    [Tooltip("Смещение указателя относительно *папки*")]
    public Vector2 greetingPointerOffset = new Vector2(-50, 0);
    [Tooltip("Поворот указателя (ось Z)")]
    public float greetingPointerRotation = 0f;

    // --- <<< НАЧАЛО ИЗМЕНЕНИЙ (Проблема В) >>> ---
    [Header("Idle Tips (Контекстные)")]
    [Tooltip("Список 'советов' (Tips), которые папка будет показывать в режиме Idle, ПОСЛЕ того как все подсказки в этом контексте просмотрены. Если пусто, используются Idle Tips из TutorialScreenConfig.")]
    [TextArea(2, 4)]
    public List<string> contextIdleTips;
    // --- <<< КОНЕЦ ИЗМЕНЕНИЙ (Проблема В) >>> ---

    [Header("Idle Spots (Контекстные)")]
    [Tooltip("Список точек 'отдыха' (RectTransform), которые будут использоваться, когда этот контекст активен, но все подсказки в нем показаны. Если пусто, используются Idle Spots из TutorialScreenConfig.")]
    public List<RectTransform> contextIdleSpots;
}