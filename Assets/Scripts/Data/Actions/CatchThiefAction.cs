using UnityEngine;

[CreateAssetMenu(fileName = "Action_CatchThief", menuName = "Bureau/Actions/CatchThief")]
public class CatchThiefAction : StaffAction
{
    public override bool AreConditionsMet(StaffController staff)
    {
        if (!(staff is GuardMovement guard) || guard.IsOnBreak())
        {
            return false;
        }

        return GuardManager.Instance != null && GuardManager.Instance.GetThiefToCatch() != null;
    }

    public override System.Type GetExecutorType()
    {
        return typeof(CatchThiefExecutor);
    }
}