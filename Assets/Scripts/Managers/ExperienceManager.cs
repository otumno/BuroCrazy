using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class ExperienceManager : MonoBehaviour
{
    public static ExperienceManager Instance { get; set; }

    [Header("Базы данных")]
    [Tooltip("Перетащите сюда ассет ActionXP_Database")]
    public ActionXPData xpDatabase;
    [Tooltip("Перетащите сюда ВСЕ ассеты с рангами (Rank_0, Rank_1 и т.д.)")]
    public List<RankData> rankDatabase;

private void Awake()
{
    if (Instance == null) { Instance = this; }
    else { Destroy(gameObject); }
}

    /// <summary>
    /// Главный метод для начисления опыта сотруднику.
    /// </summary>
    public void GrantXP(StaffController staff, ActionType actionType)
    {
        if (xpDatabase == null || staff == null) return;

        int xpGained = xpDatabase.GetXpForAction(actionType);
        if (xpGained > 0)
        {
            staff.experiencePoints += xpGained;
            Debug.Log($"{staff.name} получил {xpGained} XP за действие {actionType}. Всего XP: {staff.experiencePoints}");
            CheckForRankUp(staff);
        }
    }

    private void CheckForRankUp(StaffController staff)
    {
        if (rankDatabase == null || rankDatabase.Count == 0) return;

        // Находим данные для СЛЕДУЮЩЕГО ранга
        RankData nextRankData = rankDatabase.FirstOrDefault(r => r.rankLevel == staff.rank + 1);

        if (nextRankData == null || staff.isReadyForPromotion)
        {
            return;
        }
        
        // <<< ИЗМЕНЕНИЕ: Сравниваем с experienceRequired, а не xpToNextRank >>>
        // Убедитесь, что в вашем RankData.cs поле называется 'experienceRequired'
        if (staff.experiencePoints >= nextRankData.experienceRequired)
        {
            staff.isReadyForPromotion = true;
            Debug.Log($"<color=green>{staff.name} ГОТОВ к повышению до ранга {nextRankData.rankName}!</color>");
        }
    }

    // <<< НОВЫЙ МЕТОД >>>
    /// <summary>
    /// Находит ранг по очкам опыта в списке rankDatabase.
    /// </summary>
    public RankData GetRankByXP(int xp)
    {
        if (rankDatabase == null || rankDatabase.Count == 0) return null;

        // Находим последний ранг в списке, для которого достаточно опыта.
        // Это требует, чтобы список rankDatabase был отсортирован по опыту.
        // !!! Убедитесь, что в вашем RankData.cs поле называется 'experienceRequired' !!!
        return rankDatabase.LastOrDefault(rank => xp >= rank.experienceRequired);
    }
}