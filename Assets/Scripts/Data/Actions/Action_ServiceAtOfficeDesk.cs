// Файл: Assets/Scripts/Data/Actions/Action_ServiceAtOfficeDesk.cs
using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "Action_ServiceAtOfficeDesk", menuName = "Bureau/Actions/ServiceAtOfficeDesk")]
public class Action_ServiceAtOfficeDesk : StaffAction
{
    public override bool AreConditionsMet(StaffController staff)
    {
        if (!(staff is ClerkController clerk) || clerk.role != ClerkController.ClerkRole.Regular || clerk.IsOnBreak() || clerk.assignedWorkstation == null)
        {
            return false;
        }
        
        var zone = ClientSpawner.GetZoneByDeskId(clerk.assignedWorkstation.deskId);
        // Условие простое: "Есть ли в моей зоне ХОТЯ БЫ ОДИН клиент?"
        return zone != null && zone.GetOccupyingClients().Any();
    }

    public override System.Type GetExecutorType()
    {
        return typeof(ServiceAtOfficeDeskExecutor);
    }
}