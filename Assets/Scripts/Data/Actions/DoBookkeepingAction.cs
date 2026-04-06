using UnityEngine;
using Managers;

[CreateAssetMenu(fileName = "Action_DoBookkeeping", menuName = "Bureau/Actions/DoBookkeeping")]
public class DoBookkeepingAction : StaffAction
{
    public DoBookkeepingAction()
    {
        // Используем Tactic, так как Work у вас нет в Enum, а Tactic точно есть
        category = ActionCategory.Tactic;
        priority = 10;
        actionType = ActionType.DoBookkeeping; // Убедитесь, что это есть в Enum ActionType
    }

    public override bool AreConditionsMet(StaffController staff)
    {
        if (!(staff is ClerkController clerk) || clerk.clerkRole != ClerkController.ClerkRole.Accountant || clerk.IsOnBreak() || clerk.assignedWorkstation == null)
        {
            return false;
        }
        
        if (ScenePointsRegistry.Instance == null || ScenePointsRegistry.Instance.bookkeepingDesk == null)
            return false;

        return true;
    }

    public override System.Type GetExecutorType()
    {
        return typeof(DoBookkeepingExecutor);
    }
}