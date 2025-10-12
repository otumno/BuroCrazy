// File: Assets/Scripts/Characters/Controllers/Actions/ProcessDocumentExecutor.cs
using UnityEngine;
using System.Collections;
using System.Linq;

public class ProcessDocumentExecutor : ActionExecutor
{
    public override bool IsInterruptible => false; // Cannot interrupt client service

    protected override IEnumerator ActionRoutine()
    {
        if (!(staff is ClerkController clerk) || clerk.assignedWorkstation == null)
        {
            FinishAction();
            yield break;
        }

        // 1. Find the zone and client to serve
        LimitedCapacityZone myZone = ClientSpawner.GetZoneByDeskId(clerk.assignedWorkstation.deskId);
        ClientPathfinding clientToServe = myZone?.GetOccupyingClients().FirstOrDefault();

        if (clientToServe == null)
        {
            // Client left while we were about to serve them
            FinishAction();
            yield break;
        }

        clerk.SetState(ClerkController.ClerkState.Working);
        clerk.thoughtBubble?.ShowPriorityMessage("Next!", 2f, Color.white);
        
        // 2. Assign the client to the clerk for service
        clerk.AssignClient(clientToServe);
        
        // 3. Wait for the service to complete
        yield return new WaitUntil(() => clientToServe == null || clientToServe.stateMachine.GetTargetZone() != myZone);
        
        Debug.Log($"Clerk {clerk.name} finished serving client {clientToServe?.name ?? "who has left"}.");
        
        // 4. Finalize the service and add a document to the desk stack
        clerk.ServiceComplete();
        ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);

        // 5. Return to a waiting state and finish the action
        clerk.SetState(ClerkController.ClerkState.Working);
        FinishAction();
    }
}