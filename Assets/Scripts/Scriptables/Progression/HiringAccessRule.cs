// Assets/Scripts/Scriptables/Progression/HiringAccessRule.cs
using UnityEngine;

namespace Scriptables.Progression
{
    /// <summary>
    /// Открывает найм роли вплоть до указанного уровня ранга (RankData.rankLevel).
    /// </summary>
    [System.Serializable]
    public class HiringAccessRule
    {
        public StaffController.Role role;

        [Tooltip("Максимальный уровень ранга, с которым кандидаты этой роли появляются на доске (0 — только начальный).")]
        [Min(0)]
        public int maxRankLevel;
    }
}
