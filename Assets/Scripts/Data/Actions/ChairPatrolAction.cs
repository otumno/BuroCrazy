using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "Action_ChairPatrol", menuName = "Bureau/Actions/ChairPatrol")]
public class ChairPatrolAction : StaffAction
{
    public override bool AreConditionsMet(StaffController staff)
{
    if (!(staff is ClerkController clerk) || clerk.IsOnBreak())
    {
        return false;
    }

    // >>> ИЗМЕНЕНИЕ: Разрешаем действие и для Регистратора, и для Кассира <<<
    if (clerk.role != ClerkController.ClerkRole.Registrar && clerk.role != ClerkController.ClerkRole.Cashier)
    {
        return false;
    }

    // Условие: Сотрудник на рабочем месте и перед ним НЕТ клиента.
    var zone = ClientSpawner.GetZoneByDeskId(clerk.assignedServicePoint.deskId);
    return zone != null && zone.GetOccupyingClients().FirstOrDefault() == null;
}

    public override System.Type GetExecutorType()
    {
        return typeof(ChairPatrolExecutor);
    }
}