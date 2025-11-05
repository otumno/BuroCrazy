// Файл: Assets/Scripts/UI/Tutorial/TutorialContextGroup.cs
using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class TutorialContextGroup
{
    [Tooltip("Уникальный ID этого контекста (например, 'DirectorDesk')")]
    public string contextID;

    [Tooltip("Панель UI, которая должна быть активна, чтобы показать эти подсказки")]
    public GameObject contextPanel; // <-- Вот оно!

    [Tooltip("Список подсказок для этого контекста")]
    public List<TutorialHelpSpot> helpSpots;

    [Header("Приветствие для этого контекста")]
    [Tooltip("Текст, который маскот скажет при первом появлении в этом контексте")]
    public string greetingText = "Привет! Давай я покажу, что здесь к чему.";
    [Tooltip("Спрайт эмоции для приветствия")]
    public Sprite greetingEmotion;
    
    [Tooltip("Высота листка для приветствия")]
    public float greetingSheetHeight = 150f;
    
    [Tooltip("Кол-во 'бипов' для приветствия")]
    public int greetingSoundRepetitions = 3;
}