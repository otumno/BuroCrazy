using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "Action_MakeArchiveRequest", menuName = "Bureau/Actions/MakeArchiveRequest")]
public class MakeArchiveRequestAction : StaffAction
{
    public override bool AreConditionsMet(StaffController staff)
    {
        if (!(staff is ClerkController registrar) || registrar.role != ClerkController.ClerkRole.Registrar || registrar.IsOnBreak()) return false;
        var zone = ClientSpawner.GetZoneByDeskId(registrar.assignedWorkstation.deskId);
        if (zone == null) return false;
        
        bool hasClientForRequest = zone.GetOccupyingClients().Any(c => c.mainGoal == ClientGoal.GetArchiveRecord);
        if (!hasClientForRequest) return false;

        var retrieveActionType = ActionType.RetrieveDocument; 
        bool isArchivistAvailable = HiringManager.Instance.AllStaff
            .Any(s => 
                s.currentRole == StaffController.Role.Archivist && 
                s.IsOnDuty() && 
                !s.IsOnBreak() &&
                s.activeActions.Any(a => a.actionType == retrieveActionType)
            );

        return isArchivistAvailable;
    }
    public override System.Type GetExecutorType() { return typeof(MakeArchiveRequestExecutor); }
}