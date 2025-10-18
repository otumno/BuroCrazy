using UnityEngine;
using System.Collections;
using System.Linq;

public class ProcessDocumentExecutor : ActionExecutor
{
    public override bool IsInterruptible => false;

    protected override IEnumerator ActionRoutine()
    {
        if (!(staff is ClerkController clerk) || clerk.assignedWorkstation == null)
        {
            FinishAction(false);
            yield break;
        }

        LimitedCapacityZone myZone = ClientSpawner.GetZoneByDeskId(clerk.assignedWorkstation.deskId);
        ClientPathfinding clientToServe = myZone?.GetOccupyingClients().FirstOrDefault();

        if (clientToServe == null)
        {
            FinishAction(false);
            yield break;
        }

        clerk.SetState(ClerkController.ClerkState.Working);
        clerk.thoughtBubble?.ShowPriorityMessage("Следующий!", 2f, Color.white);
        
        clerk.AssignClient(clientToServe);
        
        yield return new WaitUntil(() => clientToServe == null || clientToServe.stateMachine.GetTargetZone() != myZone);
        
        clerk.ServiceComplete();
        ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);

        clerk.SetState(ClerkController.ClerkState.Working);
        FinishAction(true);
    }
}