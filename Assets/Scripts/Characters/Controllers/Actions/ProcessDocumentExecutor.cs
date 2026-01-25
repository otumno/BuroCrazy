using UnityEngine;
using System.Collections;
using System.Linq;
using Managers;

public class ProcessDocumentExecutor : ActionExecutor
{
    public override bool IsInterruptible => false;

    protected override IEnumerator ActionRoutine()
    {
        if (!(staff is ClerkController clerk) || clerk.assignedWorkstation == null)
        {
            Debug.LogWarning("[ProcessDocumentExecutor] Clerk или assignedWorkstation равен null");
            FinishAction(false);
            yield break;
        }

        LimitedCapacityZone myZone = ClientSpawner.GetZoneByDeskId(clerk.assignedWorkstation.deskId);
        if (myZone == null)
        {
            Debug.LogWarning("[ProcessDocumentExecutor] Zone не найдена");
            FinishAction(false);
            yield break;
        }

        ClientPathfinding clientToServe = myZone?.GetOccupyingClients().FirstOrDefault();

        if (clientToServe == null)
        {
            Debug.LogWarning("[ProcessDocumentExecutor] Клиент для обслуживания не найден");
            FinishAction(false);
            yield break;
        }

        clerk.SetState(ClerkController.ClerkState.Working);
        clerk.thoughtBubble?.ShowPriorityMessage("Следующий!", 2f, Color.white);
        
        clerk.AssignClient(clientToServe);
        
        yield return new WaitUntil(() => clientToServe == null || clientToServe.stateMachine == null || clientToServe.stateMachine.GetTargetZone() != myZone);
        
        clerk.ServiceComplete();
        ActionType actionForXP = actionData != null ? actionData.actionType : ActionType.CheckDocument;
        ExperienceManager.Instance?.GrantXP(staff, actionForXP);

        clerk.SetState(ClerkController.ClerkState.Working);
        FinishAction(true);
    }
}