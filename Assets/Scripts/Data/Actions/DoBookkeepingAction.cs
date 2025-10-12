using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "Action_DoBookkeeping", menuName = "Bureau/Actions/DoBookkeeping")]
public class DoBookkeepingAction : StaffAction
{
    public override bool AreConditionsMet(StaffController staff)
    {
        if (!(staff is ClerkController clerk) || clerk.IsOnBreak()) return false;
        if (clerk.role != ClerkController.ClerkRole.Cashier) return false;
        
        var zone = ClientSpawner.GetZoneByDeskId(clerk.assignedWorkstation.deskId);
        return zone != null && !zone.GetOccupyingClients().Any();
    }
    public override System.Type GetExecutorType() { return typeof(DoBookkeepingExecutor); }
}