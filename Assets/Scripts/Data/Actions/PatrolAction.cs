// Файл: Assets/Scripts/Data/Actions/PatrolAction.cs
using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "Action_Patrol", menuName = "Bureau/Actions/Patrol")]
public class PatrolAction : StaffAction
{
    public override bool AreConditionsMet(StaffController staff)
    {
        // У патруля почти всегда выполнены условия, если сотрудник на смене
        // и его роль позволяет это делать (проверяется по списку applicableRoles в ассете).
        return staff.IsOnDuty() && applicableRoles.Contains(staff.currentRole);
    }

    public override System.Type GetExecutorType()
    {
        // Это действие выполняется скриптом PatrolExecutor
        return typeof(PatrolExecutor);
    }
}