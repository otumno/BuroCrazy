using UnityEngine;
using Managers;

[CreateAssetMenu(fileName = "Action_DelegateArchiveFetch", menuName = "Bureau/Actions/DelegateArchiveFetch")]
public class Action_DelegateArchiveFetch : StaffAction
{
    public Action_DelegateArchiveFetch()
    {
        category = ActionCategory.System;
        priority = 40; // Очень высокий приоритет для стажеров
    }

    public override bool AreConditionsMet(StaffController staff)
    {
        if (staff.IsOnBreak() || (staff.currentRole != StaffController.Role.Intern && staff.currentRole != StaffController.Role.OfficeManager)) return false;
        
        return ArchiveRequestManager.Instance != null && ArchiveRequestManager.Instance.HasPendingDelegatedRequests();
    }

    public override System.Type GetExecutorType()
    {
        return typeof(DelegateArchiveFetchExecutor);
    }
}
