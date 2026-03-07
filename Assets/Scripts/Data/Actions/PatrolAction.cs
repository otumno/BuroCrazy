// Файл: Assets/Scripts/Data/Actions/PatrolAction.cs
using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "Action_Patrol", menuName = "Bureau/Actions/Patrol")]
public class PatrolAction : StaffAction
{
    public override bool AreConditionsMet(StaffController staff)
    {
        if (!staff.IsOnDuty() || !applicableRoles.Contains(staff.currentRole)) return false;

        // ПРОВЕРКА ДЛЯ ОХРАНЫ
        var points = Managers.ScenePointsRegistry.Instance?.guardPatrolPoints;
        return points != null && points.Count > 0 && points[0] != null;
    }

    public override System.Type GetExecutorType()
    {
        // Это действие выполняется скриптом PatrolExecutor
        return typeof(PatrolExecutor);
    }
}