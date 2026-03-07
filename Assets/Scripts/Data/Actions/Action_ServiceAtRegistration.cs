// Файл: Assets/Scripts/Data/Actions/Action_ServiceAtRegistration.cs
using UnityEngine;
using System.Linq;
using Managers;

[CreateAssetMenu(fileName = "Action_ServiceAtRegistration", menuName = "Bureau/Actions/ServiceAtRegistration")]
public class Action_ServiceAtRegistration : StaffAction
{
    public Action_ServiceAtRegistration()
    {
        category = ActionCategory.Tactic;
        priority = 20;
    }
    
    public override bool AreConditionsMet(StaffController staff)
    {
        if (!(staff is ClerkController clerk) || clerk.clerkRole != ClerkController.ClerkRole.Registrar || clerk.IsOnBreak() || clerk.assignedWorkstation == null)
        {
            return false;
        }
        
        var zone = ClientSpawner.GetZoneByDeskId(clerk.assignedWorkstation.deskId);
        return zone != null && zone.GetOccupyingClients().Any();
    }

    public override float CalculateUtility(StaffController staff)
    {
        float utility = base.CalculateUtility(staff);

        if (staff is ClerkController clerk && clerk.assignedWorkstation != null)
        {
            var zone = ClientSpawner.GetZoneByDeskId(clerk.assignedWorkstation.deskId);
            if (zone != null)
            {
                var clients = zone.GetOccupyingClients();
                int clientCount = clients.Count;

                if (clientCount > 0)
                {
                    utility += clientCount * 5f;

                    float maxHeat = clients.Max(c => c.PatienceHeat);
                    utility += maxHeat * 80f;
                }
            }
        }
        return utility;
    }

    public override System.Type GetExecutorType()
    {
        return typeof(ServiceAtRegistrationExecutor);
    }
}