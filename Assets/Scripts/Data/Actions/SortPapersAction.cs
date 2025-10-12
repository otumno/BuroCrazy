using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "Action_SortPapers", menuName = "Bureau/Actions/SortPapers")]
public class SortPapersAction : StaffAction
{
    public override bool AreConditionsMet(StaffController staff)
    {
        if (!(staff is ClerkController clerk) || clerk.IsOnBreak() || clerk.GetCurrentState() != ClerkController.ClerkState.Working)
        {
            return false;
        }

        var zone = ClientSpawner.GetZoneByDeskId(clerk.assignedWorkstation.deskId);
        if (zone == null) return false;

        return zone.GetOccupyingClients().FirstOrDefault() == null;
    }

    public override System.Type GetExecutorType()
    {
        return typeof(SortPapersExecutor);
    }
}