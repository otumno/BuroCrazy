using UnityEngine;

[CreateAssetMenu(fileName = "Action_WriteReport", menuName = "Bureau/Actions/WriteReport")]
public class WriteReportAction : StaffAction
{
    public override bool AreConditionsMet(StaffController staff)
    {
        // Условия просты: сотрудник должен быть охранником, и у него должны быть неописанные протоколы.
        if (staff is GuardMovement guard)
        {
            return guard.unwrittenReportPoints > 0;
        }
        return false;
    }

    public override System.Type GetExecutorType()
    {
        return typeof(WriteReportExecutor);
    }
}