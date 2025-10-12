using UnityEngine;

[CreateAssetMenu(fileName = "Action_TakeStackToArchive", menuName = "Bureau/Actions/TakeStackToArchive")]
public class TakeStackToArchiveAction : StaffAction
{
    public override bool AreConditionsMet(StaffController staff)
    {
        if (!(staff is ClerkController clerk) || clerk.IsOnBreak())
        {
            return false;
        }

        if (clerk.assignedWorkstation == null || clerk.assignedWorkstation.documentStack == null)
        {
            return false;
        }

        return clerk.assignedWorkstation.documentStack.IsFull;
    }

    public override System.Type GetExecutorType()
    {
        return typeof(TakeStackToArchiveExecutor);
    }
}