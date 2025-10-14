using UnityEngine;

[CreateAssetMenu(fileName = "Action_SystematizeArchive", menuName = "Bureau/Actions/SystematizeArchive")]
public class SystematizeArchiveAction : StaffAction
{
    public SystematizeArchiveAction()
    {
        category = ActionCategory.System;
    }
    
    public override bool AreConditionsMet(StaffController staff)
    {
        if (staff.currentRole != StaffController.Role.Archivist || staff.IsOnBreak())
        {
            return false;
        }

        // Условие: нет экстренных запросов и нет документов для архивации на столе.
        bool hasUrgentRequests = ArchiveRequestManager.Instance.HasPendingRequests();
        bool hasDocsToArchive = !ArchiveManager.Instance.mainDocumentStack.IsEmpty;

        return !hasUrgentRequests && !hasDocsToArchive;
    }

    public override System.Type GetExecutorType()
    {
        return typeof(SystematizeArchiveExecutor);
    }
}