using UnityEngine;
using System.Linq;
using Managers;

[CreateAssetMenu(fileName = "Action_ServiceAtRegistration", menuName = "Bureau/Actions/ServiceAtRegistration")]
public class Action_ServiceAtRegistration : StaffAction
{
    public Action_ServiceAtRegistration() { category = ActionCategory.Tactic; priority = 20; }
    
    public override bool AreConditionsMet(StaffController staff)
    {
        if (!(staff is ClerkController clerk) || staff.currentRole != StaffController.Role.Registrar || clerk.assignedWorkstation == null)
            return false;

        // 1. У стола уже кто-то стоит (закреплен)
        if (clerk.assignedWorkstation.CurrentClient != null) return true;

        // 2. В глобальной очереди есть НЕВЫЗВАННЫЕ талоны
        if (ClientQueueManager.Instance != null && ClientQueueManager.Instance.queue.Count > 0)
        {
            bool hasUncalled = ClientQueueManager.Instance.queue.Values.Any(ticket => !ClientQueueManager.Instance.currentlyCalledNumbers.Contains(ticket));
            if (hasUncalled) return true;
        }

        // 3. Легковесная проверка: есть ли клиенты в локальной зоне регистратуры
        var zone = ClientSpawner.GetZoneByDeskId(clerk.assignedWorkstation.deskId);
        if (zone != null && zone.GetOccupyingClients().Count > 0) return true;

        return false;
    }

    public override float CalculateUtility(StaffController staff)
    {
        float utility = base.CalculateUtility(staff);
        var aiConfig = Gameplay.AIBalanceConfig.Instance;
        utility *= (aiConfig != null ? 1f + (staff.skills.paperworkMastery * aiConfig.masteryWorkMultiplier) : 1f);

        if (AreConditionsMet(staff)) utility += 2000f; 
        return utility;
    }

    public override System.Type GetExecutorType() => typeof(ServiceAtRegistrationExecutor);
}
