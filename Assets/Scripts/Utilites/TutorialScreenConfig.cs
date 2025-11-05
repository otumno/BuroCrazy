// Файл: Assets/Scripts/UI/Tutorial/TutorialScreenConfig.cs
using UnityEngine;
using System.Collections.Generic;

public class TutorialScreenConfig : MonoBehaviour
{
    [Tooltip("Уникальный ID этой сцены (например, 'MainMenu' или 'GameScene')")]
    public string screenID;

    [Header("Контекстные Подсказки")]
    [Tooltip("Список групп подсказок. Каждая группа привязана к своей панели UI.")]
    public List<TutorialContextGroup> contextGroups;
    
    [Header("Места 'Отдыха' (для Главного Меню)")]
    [Tooltip("Список случайных точек (RectTransform), куда маскот будет уходить, если ни одна панель не активна")]
    public List<RectTransform> idleSpots;
}