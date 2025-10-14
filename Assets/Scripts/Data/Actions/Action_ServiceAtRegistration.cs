using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "Action_ServiceAtRegistration", menuName = "Bureau/Actions/ServiceAtRegistration")]
public class Action_ServiceAtRegistration : StaffAction
{
    public Action_ServiceAtRegistration()
    {
        category = ActionCategory.Tactic;
    }
    
    public override bool AreConditionsMet(StaffController staff)
    {
        // THE FIX IS HERE: We now compare ClerkController.ClerkRole with ClerkController.ClerkRole
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