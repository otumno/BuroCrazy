// Файл: Assets/Scripts/Utilites/TutorialContextGroup.cs (ОТКАТ к v10)
using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class TutorialContextGroup // <-- ВОЗВРАЩАЕМ 'class'
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
    
    // (soundRepetitions удален, это правильно)

    [Header("Рука для Приветствия (Опционально)")]
    public Sprite greetingPointerSprite;
    public Vector2 greetingPointerOffset = new Vector2(-50, 0); 
    public float greetingPointerRotation = 0f; 

    [Header("Idle Tips (Контекстные)")]
    [TextArea(2, 4)]
    public List<string> contextIdleTips;

    [Header("Idle Spots (Контекстные)")]
    public List<RectTransform> contextIdleSpots;
}