// Assets/Scripts/Data/Actions/WriteReportAction.cs
using UnityEngine;
using Managers;

[CreateAssetMenu(fileName = "Action_WriteReport", menuName = "Bureau/Actions/Guard/WriteReport")]
public class WriteReportAction : StaffAction
{
    public int minPointsRequired = 1;

    public override bool AreConditionsMet(StaffController staff)
    {
        var guard = staff.GetComponent<GuardMovement>();
        if (guard == null) return false;

        // Проверяем, накопились ли данные для отчета
        return guard.unwrittenReportPoints >= minPointsRequired;
    }

    public override System.Type GetExecutorType()
    {
        return typeof(WriteReportExecutor);
    }
}