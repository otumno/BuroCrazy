using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[CreateAssetMenu(fileName = "RankDatabase", menuName = "Experience/Rank Database")]
public class RankDatabase : ScriptableObject
{
    [Tooltip("Список всех рангов. Отсортируйте их по возрастанию 'experienceRequired'")]
    public List<RankData> ranks;

    public RankData GetRank(int experiencePoints)
    {
        // <<< ИСПРАВЛЕНО: Теперь используется правильное имя поля 'experienceRequired' >>>
        // Этот код находит самый высокий ранг, которого достиг персонаж.
        return ranks.LastOrDefault(rank => experiencePoints >= rank.experienceRequired);
    }
}