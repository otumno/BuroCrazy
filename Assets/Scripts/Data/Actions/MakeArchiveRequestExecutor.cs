using UnityEngine;
using System.Collections;
using System.Linq;

public class MakeArchiveRequestExecutor : ActionExecutor
{
    public override bool IsInterruptible => false;

    protected override IEnumerator ActionRoutine()
    {
        var registrar = staff as ClerkController;
        var zone = ClientSpawner.GetZoneByDeskId(registrar.assignedWorkstation.deskId);
        var client = zone.GetOccupyingClients().FirstOrDefault(c => c.mainGoal == ClientGoal.GetArchiveRecord);

        if (registrar == null || client == null) { FinishAction(); yield break; }

        ArchiveRequestManager.Instance.CreateRequest(registrar, client);

        registrar.SetState(ClerkController.ClerkState.WaitingForArchive);
        client.stateMachine.SetState(ClientState.WaitingForDocument);

        var archivistDesk = ScenePointsRegistry.Instance.GetServicePointByID(3); 
        if (archivistDesk == null) { FinishAction(); yield break; }

        // ----- THE FIX IS HERE -----
        yield return staff.StartCoroutine(registrar.MoveToTarget(archivistDesk.clerkStandPoint.position, ClerkController.ClerkState.WaitingForArchive.ToString()));

        float waitTimer = 0f;
        float maxWaitTime = 60f;
        bool requestFulfilled = false;
        var request = ArchiveRequestManager.Instance.GetNextRequest(); // Assuming this gets the correct request back
        while(waitTimer < maxWaitTime)
        {
            if (request != null && request.IsFulfilled)
            {
                requestFulfilled = true;
                break;
            }
            waitTimer += Time.deltaTime;
            yield return null;
        }

        if (requestFulfilled)
        {
            registrar.GetComponent<StackHolder>().ShowSingleDocumentSprite(); 
            registrar.SetState(ClerkController.ClerkState.ReturningToWork);
            // ----- THE FIX IS HERE -----
            yield return staff.StartCoroutine(registrar.MoveToTarget(registrar.assignedWorkstation.clerkStandPoint.position, ClerkController.ClerkState.Working.ToString()));
            
            registrar.GetComponent<StackHolder>().HideStack();

            client.billToPay += 150; 
            client.stateMachine.SetGoal(ClientSpawner.GetCashierZone().waitingWaypoint);
            client.stateMachine.SetState(ClientState.MovingToGoal);
        }
        else
        {
            Debug.LogWarning($"Регистратор {registrar.name} не дождался Архивариуса и отменил запрос.");
            registrar.thoughtBubble?.ShowPriorityMessage("Архив не отвечает...\nИзвините.", 3f, Color.red);

            registrar.SetState(ClerkController.ClerkState.ReturningToWork);
            // ----- THE FIX IS HERE -----
            yield return staff.StartCoroutine(registrar.MoveToTarget(registrar.assignedWorkstation.clerkStandPoint.position, ClerkController.ClerkState.Working.ToString()));

            client.reasonForLeaving = ClientPathfinding.LeaveReason.Upset;
            client.stateMachine.SetGoal(ClientSpawner.Instance.exitWaypoint);
            client.stateMachine.SetState(ClientState.LeavingUpset);
        }

        registrar.ServiceComplete();
        FinishAction();
    }
}