using UnityEngine;
using System.Linq;
using Managers;
using Gameplay;

[CreateAssetMenu(fileName = "Action_SortPapers", menuName = "Bureau/Actions/SortPapers")]
public class SortPapersAction : StaffAction
{
    public SortPapersAction() { category = ActionCategory.System; priority = 1; }
    
    public override bool AreConditionsMet(StaffController staff)
    {
        if (!(staff is ClerkController clerk) || clerk.IsOnBreak() || clerk.assignedWorkstation == null) return false;

        ServicePoint workstation = clerk.assignedWorkstation;
        if (workstation.CurrentClient != null) return false;

        if (staff.currentRole == StaffController.Role.Registrar)
        {
            if (ClientQueueManager.Instance != null && ClientQueueManager.Instance.queue.Count > 0)
            {
                bool hasUncalled = ClientQueueManager.Instance.queue.Values.Any(ticket => !ClientQueueManager.Instance.currentlyCalledNumbers.Contains(ticket));
                if (hasUncalled) return false; // Запрет, если есть невызванные
            }

            var allClients = Object.FindObjectsByType<ClientPathfinding>(FindObjectsSortMode.None);
            foreach (var c in allClients)
            {
                if (c != null && !c.isLeavingSuccessfully && c.stateMachine != null)
                {
                    var state = c.stateMachine.GetCurrentState();
                    if (state != ClientState.Leaving && state != ClientState.LeavingUpset && state != ClientState.Enraged && 
                        state != ClientState.AtDesk1 && state != ClientState.AtDesk2 && state != ClientState.AtCashier)
                    {
                        if (c.stateMachine.MyServiceProvider == null)
                        {
                            return false; // Запрет, в зале есть свободные клиенты
                        }
                    }
                }
            }
        }
        else
        {
            var zone = ClientSpawner.GetZoneByDeskId(workstation.deskId);
            if (zone != null && (zone.GetOccupyingClients().Count > 0 || zone.waitingQueue.Count > 0)) return false;
        }

        return true;
    }

    public override float CalculateUtility(StaffController staff)
    {
        float utility = base.CalculateUtility(staff);
        var aiConfig = AIBalanceConfig.Instance;
        utility += aiConfig != null ? staff.skills.pedantry * aiConfig.pedantrySortBonus : 0f;
        return utility;
    }

    public override System.Type GetExecutorType() => typeof(SortPapersExecutor);
}
