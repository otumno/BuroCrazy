using UnityEngine;
using Managers;

[CreateAssetMenu(fileName = "Action_AccountantCoverSchemes", menuName = "Bureau/Actions/Accountant Cover Schemes")]
public class AccountantCoverSchemesAction : StaffAction
{
    public AccountantCoverSchemesAction()
    {
        category = ActionCategory.Tactic;
        priority = 5; // Низкий приоритет — пассивное покрытие, не вытесняет обслуживание
        actionType = ActionType.AccountantCoverSchemes;
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
        return typeof(AccountantCoverSchemesExecutor);
    }
}
