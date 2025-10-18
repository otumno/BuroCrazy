// Файл: Assets/Scripts/Data/RankData.cs
using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Rank_New", menuName = "Bureau/Rank Configuration")]
public class RankData : ScriptableObject
{
    [Header("Основная информация")]
    public int rankLevel; // ВОССТАНОВЛЕНО: Числовой уровень ранга (0, 1, 2...)
    public string rankName;
    
    [Header("Геймплейные параметры этого ранга")]
    public int experienceRequired = 100;
    public int promotionCost = 500;
    public float salaryMultiplier = 1.1f;
    public int maxActions = 2;
    public int workPeriodsCount = 3; // ВОССТАНОВЛЕНО: Количество рабочих периодов

    [Header("Карьерный путь")]
    public StaffController.Role associatedRole;
    public List<StaffAction> unlockedActions;
    
    [Header("Следующие ступени карьеры")]
    public List<RankData> possiblePromotions;
}