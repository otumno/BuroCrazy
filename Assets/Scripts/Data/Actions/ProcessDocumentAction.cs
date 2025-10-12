// File: Assets/Scripts/Data/Actions/ProcessDocumentAction.cs
using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "Action_ProcessDocument", menuName = "Bureau/Actions/ProcessDocument")]
public class ProcessDocumentAction : StaffAction
{
    public override bool AreConditionsMet(StaffController staff)
    {
        // 1. Check if it's a Clerk and not on break, with a workstation assigned
        if (!(staff is ClerkController clerk) || clerk.IsOnBreak() || clerk.assignedWorkstation == null)
        {
            return false;
        }

        // 2. Find clients within the clerk's service zone
        LimitedCapacityZone myZone = ClientSpawner.GetZoneByDeskId(clerk.assignedWorkstation.deskId);
        if (myZone == null)
        {
            return false;
        }

        // 3. The condition is met if there's at least one client waiting
        return myZone.GetOccupyingClients().Any();
    }

    public override System.Type GetExecutorType()
    {
        return typeof(ProcessDocumentExecutor);
    }
}