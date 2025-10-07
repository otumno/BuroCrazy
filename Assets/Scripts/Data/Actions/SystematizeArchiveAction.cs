using UnityEngine;

[CreateAssetMenu(fileName = "Action_SystematizeArchive", menuName = "Bureau/Actions/SystematizeArchive")]
public class SystematizeArchiveAction : StaffAction
{
    public override bool AreConditionsMet(StaffController staff)
    {
        // Действие для безделья: доступно всегда, если сотрудник - Архивариус и не на перерыве.
        if (staff.currentRole != StaffController.Role.Archivist || staff.IsOnBreak())
        {
            return false;
        }
        return true;
    }

    public override System.Type GetExecutorType()
    {
        return typeof(SystematizeArchiveExecutor);
    }
}