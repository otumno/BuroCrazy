using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "Action_ServiceAtRegistration", menuName = "Bureau/Actions/ServiceAtRegistration")]
public class Action_ServiceAtRegistration : StaffAction
{
    public override bool AreConditionsMet(StaffController staff)
    {
        if (!(staff is ClerkController clerk) || clerk.role != ClerkController.ClerkRole.Registrar || clerk.IsOnBreak() || clerk.assignedWorkstation == null)
        {
            return false;
        }
        var zone = ClientSpawner.GetZoneByDeskId(clerk.assignedWorkstation.deskId);
        return zone != null && zone.GetOccupyingClients().Any();
    }

    public override System.Type GetExecutorType()
    {
        return typeof(ServiceAtRegistrationExecutor);
    }
}