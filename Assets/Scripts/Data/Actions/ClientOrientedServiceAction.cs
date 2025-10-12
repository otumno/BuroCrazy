using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "Action_ClientOrientedService", menuName = "Bureau/Actions/ClientOrientedService")]
public class ClientOrientedServiceAction : StaffAction
{
    public override bool AreConditionsMet(StaffController staff)
    {
        if (!(staff is ClerkController clerk) || clerk.IsOnBreak() || clerk.GetCurrentState() != ClerkController.ClerkState.Working)
        {
            return false;
        }

        var zone = ClientSpawner.GetZoneByDeskId(clerk.assignedWorkstation.deskId);
        if (zone == null) return false;

        ClientPathfinding client = zone.GetOccupyingClients().FirstOrDefault();
        return client != null;
    }

    public override System.Type GetExecutorType()
    {
        return typeof(ClientOrientedServiceExecutor);
    }
}