// Файл: ExperienceManager.cs
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

        // Если следующего ранга не существует (сотрудник достиг максимума) или он уже готов к повышению
        if (nextRankData == null || staff.isReadyForPromotion)
        {
            return;
        }

        // Проверяем, достаточно ли опыта для повышения
        if (staff.experiencePoints >= nextRankData.xpToNextRank)
        {
            staff.isReadyForPromotion = true;
            Debug.Log($"<color=green>{staff.name} ГОТОВ к повышению до ранга {nextRankData.rankName}!</color>");
            // Здесь мы можем в будущем вызвать уведомление для UI
        }
    }
}