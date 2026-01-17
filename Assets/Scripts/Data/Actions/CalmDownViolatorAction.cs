// Assets/Scripts/Data/Actions/CalmDownViolatorAction.cs
using UnityEngine;
using Managers;

[CreateAssetMenu(fileName = "Action_CalmDownViolator", menuName = "Bureau/Actions/Guard/CalmDownViolator")]
public class CalmDownViolatorAction : StaffAction
{
    public override bool AreConditionsMet(StaffController staff)
    {
        // ИСПРАВЛЕНИЕ: GetComponent
        var guard = staff.GetComponent<GuardMovement>();
        if (guard == null || guard.IsOnBreak()) return false;

        return GuardManager.Instance != null && GuardManager.Instance.currentViolator != null;
    }

    public override System.Type GetExecutorType()
    {
        return typeof(CalmDownViolatorExecutor);
    }
}