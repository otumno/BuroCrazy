using UnityEngine;

[CreateAssetMenu(fileName = "Action_ArchiveDocument", menuName = "Bureau/Actions/ArchiveDocument")]
public class ArchiveDocumentAction : StaffAction
{
    public override bool AreConditionsMet(StaffController staff)
    {
        // Условие: это Архивариус, он не на перерыве, и в главной стопке архива есть документы.
        if (staff.currentRole != StaffController.Role.Archivist || staff.IsOnBreak())
        {
            return false;
        }
        return !ArchiveManager.Instance.mainDocumentStack.IsEmpty;
    }

    public override System.Type GetExecutorType()
    {
        return typeof(ArchiveDocumentExecutor);
    }
}