using UnityEngine;

[CreateAssetMenu(fileName = "Action_RetrieveDocument", menuName = "Bureau/Actions/RetrieveDocument")]
public class RetrieveDocumentAction : StaffAction
{
    public override bool AreConditionsMet(StaffController staff)
    {
        if (staff.currentRole != StaffController.Role.Archivist || staff.IsOnBreak()) return false;
        // Условие: есть ли в очереди запросы?
        return ArchiveRequestManager.Instance.HasPendingRequests();
    }
    public override System.Type GetExecutorType() { return typeof(RetrieveDocumentExecutor); }
}