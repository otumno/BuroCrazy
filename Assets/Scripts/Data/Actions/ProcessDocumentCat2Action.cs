using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "Action_ProcessDocCat2", menuName = "Bureau/Actions/ProcessDocumentCat2")]
public class ProcessDocumentCat2Action : StaffAction
{
    public override bool AreConditionsMet(StaffController staff)
    {
        if (staff.rank < minRankRequired)
        {
            return false;
        }

        if (!(staff is ClerkController clerk) || clerk.IsOnBreak() || clerk.GetCurrentState() != ClerkController.ClerkState.Working)
        {
            return false;
        }

        var zone = ClientSpawner.GetZoneByDeskId(clerk.assignedWorkstation.deskId);
        if (zone == null) return false;
        
        ClientPathfinding client = zone.GetOccupyingClients().FirstOrDefault();
        if (client == null) return false;
        
        return client.documentChecked == true;
    }

    public override System.Type GetExecutorType()
    {
        return typeof(ProcessDocumentCat2Executor);
    }
}