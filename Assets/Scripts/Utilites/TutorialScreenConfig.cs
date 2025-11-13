// Файл: Assets/Scripts/UI/Tutorial/TutorialScreenConfig.cs (ФИНАЛЬНАЯ ВЕРСИЯ)
using UnityEngine;
using System.Collections.Generic;

public class TutorialScreenConfig : MonoBehaviour
{
    [Tooltip("Уникальный ID этой сцены (например, 'MainMenu' или 'GameScene')")]
    public string screenID;

    [Header("Настройки Задержек (в секундах)")]
    [Tooltip("Задержка перед появлением папки ПОСЛЕ загрузки сцены (чтобы UI успел прогрузиться).")]
    public float sceneLoadDelay = 1.0f; 

    [Tooltip("Задержка ПЕРЕД САМЫМ ПЕРВЫМ (в игре) появлением папки.")]
    public float firstEverAppearanceDelay = 2.0f;

    [Tooltip("Через N секунд после появления приветствия (в Idle или в новом контексте), показать ПЕРВУЮ подсказку.")]
    public float initialHintDelay = 3.0f;
    
    [Tooltip("Через N секунд после показа подсказки, АВТОМАТИЧЕСКИ показать следующую (если не нажать 'Next').")]
    public float nextHintDelay = 8.0f; 

    [Tooltip("Как часто менять сообщения в режиме 'Отдыха' (когда все подсказки показаны или нет контекста).")]
    public float idleMessageChangeDelay = 10.0f;

    [Header("Контекстные Подсказки")]
    [Tooltip("Список групп подсказок. Каждая группа привязана к своей панели UI.")]
	[SerializeReference]
    public List<TutorialContextGroup> contextGroups;
    
    [Header("Места 'Отдыха' (для Главного Меню)")]
    [Tooltip("Список случайных точек (RectTransform), куда маскот будет уходить, если ни одна панель не активна")]
    public List<RectTransform> idleSpots;
	
	[Header("Подсказки в режиме 'Отдыха'")]
    [Tooltip("Список 'советов' (Tips), которые папка будет показывать в режиме Idle, ПОСЛЕ того как все подсказки просмотрены (НЕ для MainMenu)")]
    [TextArea(2, 4)]
    public List<string> idleTips;
}