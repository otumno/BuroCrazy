// Файл: Assets/Scripts/Data/Actions/ChairPatrolAction.cs
using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "Action_ChairPatrol", menuName = "Bureau/Actions/ChairPatrol")]
public class ChairPatrolAction : StaffAction
{
    public ChairPatrolAction()
    {
        category = ActionCategory.System; // Помечаем как системное
    }

    public override bool AreConditionsMet(StaffController staff)
    {
        // Условия: сотрудник - клерк/регистратор/кассир, он не на перерыве,
        // и перед ним НЕТ клиента для обслуживания.
        if (!(staff is ClerkController clerk) || clerk.IsOnBreak() || clerk.assignedWorkstation == null)
        {
            return false;
        }

        var zone = ClientSpawner.GetZoneByDeskId(clerk.assignedWorkstation.deskId);
        return zone != null && !zone.GetOccupyingClients().Any();
    }

    public override System.Type GetExecutorType()
    {
        return typeof(ChairPatrolExecutor);
    }
}