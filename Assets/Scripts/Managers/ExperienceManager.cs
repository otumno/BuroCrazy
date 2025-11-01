// Файл: Assets/Scripts/Managers/ExperienceManager.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class ExperienceManager : MonoBehaviour
{
    public static ExperienceManager Instance { get; set; }

    [Header("Базы данных")]
    public ActionXPData xpDatabase;
    public List<RankData> rankDatabase;
    
    private void Awake()
    {
        if (Instance == null) { Instance = this; }
        else { Destroy(gameObject); }
    }

    public void GrantXP(StaffController staff, ActionType actionType)
    {
        if (xpDatabase == null || staff == null) return;
        
        int xpGained = xpDatabase.GetXpForAction(actionType); // [cite: 609]
        
        if (xpGained > 0)
        {
            // <<< ИЗМЕНЕНИЕ: ВЫЗЫВАЕМ НОВЫЙ МЕТОД ВМЕСТО ПРЯМОГО ПРИСВОЕНИЯ >>>
            staff.AddExperienceAndCheckForPromotion(xpGained);
            
            // <<< СТАРАЯ СТРОКА (удалить или закомментировать) >>>
            // staff.experiencePoints += xpGained; 
        }
    }

    public RankData GetRankByXP(int xp)
    {
        if (rankDatabase == null || rankDatabase.Count == 0) return null;
        return rankDatabase.LastOrDefault(rank => xp >= rank.experienceRequired);
    }
}