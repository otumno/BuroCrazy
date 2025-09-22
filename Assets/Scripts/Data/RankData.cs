// Файл: RankData.cs
using UnityEngine;

[CreateAssetMenu(fileName = "Rank_0_Newbie", menuName = "Bureau/Rank Configuration")]
public class RankData : ScriptableObject
{
    [Header("Информация о ранге")]
    public int rankLevel; // Например, 0, 1, 2...
    public string rankName; // "Стажер", "Младший бюрократ"
    
    [Header("Требования и бонусы")]
    [Tooltip("Сколько всего опыта нужно для достижения СЛЕДУЮЩЕГО ранга.")]
    public int experienceRequired = 100;
    
    [Tooltip("Стоимость повышения до этого ранга.")]
    public int promotionCost = 500;
    
    [Tooltip("Сколько непрерывных периодов сотрудник этого ранга должен работать.")]
    public int workPeriodsCount = 3;
    
    [Tooltip("Множитель к базовой зарплате за период.")]
    public float salaryMultiplier = 1.0f;
}