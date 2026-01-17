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
        // 1. Если сотрудник на перерыве — нельзя
        if (staff.IsOnBreak()) return false;

        // 2. Если это Бухгалтер (или Кассир, если хотите оставить старую логику)
        // Проверяем наличие стола в реестре
        if (ScenePointsRegistry.Instance == null || ScenePointsRegistry.Instance.bookkeepingDesk == null) 
            return false;

        // Для бухгалтера главное условие — наличие стола. 
        // Если это кассир, он может делать это только если нет клиентов (старая логика)
        if (staff is ClerkController clerk && clerk.role == ClerkController.ClerkRole.Cashier)
        {
             var zone = ClientSpawner.GetZoneByDeskId(clerk.assignedWorkstation.deskId);
             if (zone != null && zone.GetOccupyingClients().Count > 0) return false;
        }

        return true;
    }

    public override System.Type GetExecutorType()
    {
        return typeof(DoBookkeepingExecutor);
    }
}