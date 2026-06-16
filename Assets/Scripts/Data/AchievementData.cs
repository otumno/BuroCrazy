// Файл: Assets/Scripts/Data/AchievementData.cs
using UnityEngine;
using System.Collections.Generic;
using Enums;
using Data.Creation;

[CreateAssetMenu(fileName = "Achv_", menuName = "Bureau/Achievement Data")]
public class AchievementData : ScriptableObject
{
    [Header("Основная информация")]
    [Tooltip("Уникальный ID, например, 'OPEN_FIRST_DOOR'")]
    public string achievementID;

    [Tooltip("Название достижения, которое увидит игрок")]
    public string displayName;

    [Tooltip("Описание, которое увидит игрок")]
    [TextArea(2, 4)]
    public string description;

    [Header("Тип")]
    [Tooltip("Тип элемента: Achievement - стандартная ачивка, Book - книга")]
    public BookType bookType = BookType.Achievement;

    [Header("Иконки")]
    [Tooltip("Иконка, которая показывается, когда ачивка ЗАБЛОКИРОВАНА (Ч/Б)")]
    public Sprite iconLocked;
    
    [Tooltip("Иконка, которая показывается, когда ачивка ОТКРЫТА (Цветная)")]
    public Sprite iconUnlocked;

    [Tooltip("Если true, достижение не будет видно в списке, пока не будет разблокировано")]
    public bool isSecret = false;

    [Tooltip("Если true, книга будет видна в списке ВСЕГДА, даже если не разблокирована")]
    public bool alwaysAvailable = false;

    [Header("Тест / Контент по умолчанию")]
    [Tooltip("Если true — ачивка считается разблокированной при первом запуске игры. " +
             "Используй для контента, который должен быть сразу доступен в архиве " +
             "(например, 'Мануал' или 'Титры'). Этот флаг срабатывает только один раз " +
             "при первом запуске — после разблокировки состояние сохраняется на диск.")]
    public bool isUnlockedByDefault = false;

    [Header("Награда - Комикс")]
    [Tooltip("Список спрайтов (страниц), которые будут показаны в просмотрщике комиксов")]
    public List<Sprite> comicPages;

    [Header("Награда - Книга директора")]
    [Tooltip("Ссылка на книгу директора (для bookType = Director)")]
    public List<BookPageData> directorBookPages;

    [Header("Код книги директора")]
    [Tooltip("Код для генерации страниц (A1B2C1D3E3) - используется если directorBookPages пуст")]
    public string directorBookCode = "";
}