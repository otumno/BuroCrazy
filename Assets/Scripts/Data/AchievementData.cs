// Файл: Assets/Scripts/Data/AchievementData.cs
using UnityEngine;
using System.Collections.Generic;

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

    [Header("Иконки")]
    [Tooltip("Иконка, которая показывается, когда ачивка ЗАБЛОКИРОВАНА (Ч/Б)")]
    public Sprite iconLocked;
    
    [Tooltip("Иконка, которая показывается, когда ачивка ОТКРЫТА (Цветная)")]
    public Sprite iconUnlocked;

    [Tooltip("Если true, достижение не будет видно в списке, пока не будет разблокировано")]
    public bool isSecret = false;

    [Header("Награда - Комикс")]
    [Tooltip("Список спрайтов (страниц), которые будут показаны в просмотрщике комиксов")]
    public List<Sprite> comicPages;
}