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

        if (clerk.role != ClerkController.ClerkRole.Registrar && clerk.role != ClerkController.ClerkRole.Cashier)
        {
            return false;
        }
        
        var zone = ClientSpawner.GetZoneByDeskId(clerk.assignedWorkstation.deskId);
        return zone != null && zone.GetOccupyingClients().FirstOrDefault() == null;
    }

    public override System.Type GetExecutorType()
    {
        return typeof(ChairPatrolExecutor);
    }
}