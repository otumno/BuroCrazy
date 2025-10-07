using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "Action_DoBookkeeping", menuName = "Bureau/Actions/DoBookkeeping")]
public class DoBookkeepingAction : StaffAction
{
    public override bool AreConditionsMet(StaffController staff)
    {
        if (!(staff is ClerkController clerk) || clerk.IsOnBreak()) return false;
        // Доступно для Кассира (и будущих Бухгалтеров), когда у них нет клиента
        if (clerk.role != ClerkController.ClerkRole.Cashier) return false;

        var zone = ClientSpawner.GetZoneByDeskId(clerk.assignedServicePoint.deskId);
        return zone != null && !zone.GetOccupyingClients().Any();
    }
    public override System.Type GetExecutorType() { return typeof(DoBookkeepingExecutor); }
}