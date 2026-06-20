using UnityEngine;
using Managers;

[CreateAssetMenu(fileName = "Action_AccountantMoneyGen", menuName = "Bureau/Actions/Accountant Money Gen")]
public class AccountantMoneyGenAction : StaffAction
{
    public AccountantMoneyGenAction()
    {
        category = ActionCategory.Tactic;
        priority = 5; // Низкий приоритет — пассивная генерация, не должна вытеснять обслуживание
        actionType = ActionType.AccountantMoneyGen;
    }

    public override bool AreConditionsMet(StaffController staff)
    {
        if (!(staff is ClerkController clerk) ||
            clerk.clerkRole != ClerkController.ClerkRole.Accountant ||
            clerk.IsOnBreak() ||
            clerk.assignedWorkstation == null)
            return false;

        if (ScenePointsRegistry.Instance == null || ScenePointsRegistry.Instance.bookkeepingDesk == null)
            return false;

        // Запускаем только когда бухгалтер реально сидит за рабочим столом
        var state = clerk.GetCurrentState();
        if (state != ClerkController.ClerkState.Working) return false;

        return true;
    }

    public override System.Type GetExecutorType()
    {
        return typeof(AccountantMoneyGenExecutor);
    }
}
