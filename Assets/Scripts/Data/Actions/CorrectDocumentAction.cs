using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "Action_CorrectDocument", menuName = "Bureau/Actions/CorrectDocument")]
public class CorrectDocumentAction : StaffAction
{
    public override bool AreConditionsMet(StaffController staff)
    {
        if (staff.currentRole != StaffController.Role.Archivist || staff.IsOnBreak()) return false;

        // Условие: есть ли вообще ошибки для исправления?
        return DocumentQualityManager.Instance.GetCurrentAverageErrorRate() > 0;
    }

    public override System.Type GetExecutorType() { return typeof(CorrectDocumentExecutor); }
}