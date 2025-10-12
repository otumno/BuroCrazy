using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "Action_ServiceAtCashier", menuName = "Bureau/Actions/ServiceAtCashier")]
public class Action_ServiceAtCashier : StaffAction
{
    public override bool AreConditionsMet(StaffController staff)
    {
        if (!(staff is ClerkController clerk) || clerk.role != ClerkController.ClerkRole.Cashier || clerk.IsOnBreak() || clerk.assignedWorkstation == null)
        {
            return false;
        }
        
        var zone = ClientSpawner.GetZoneByDeskId(clerk.assignedWorkstation.deskId);
        return zone != null && zone.GetOccupyingClients().Any(c => c.billToPay > 0);
    }

    public override System.Type GetExecutorType()
    {
        return typeof(ServiceAtCashierExecutor);
    }
}