using UnityEngine;
using System.Collections;
using System.Linq;
using Managers;

public class MakeArchiveRequestExecutor : ActionExecutor
{
    public override bool IsInterruptible => false;
    protected override IEnumerator ActionRoutine()
    {
        var registrar = staff as ClerkController;
        if (registrar == null || registrar.assignedWorkstation == null || ArchiveRequestManager.Instance == null)
        { FinishAction(false); yield break; }
        var zone = registrar.assignedWorkstation.GetComponentInParent<LimitedCapacityZone>();
        var client = zone?.GetOccupyingClients().FirstOrDefault(c => c != null && c.mainGoal == ClientGoal.GetArchiveRecord);
        if (client == null) { FinishAction(false); yield break; }
        ArchiveRequestManager.Instance.CreateRequest(registrar, client);
        var request = ArchiveRequestManager.Instance.GetOurRequest(registrar, client);
        if (request == null) { FinishAction(false); yield break; }
        bool serviceCompleted = false;
        RegisterCleanup(() =>
        {
            if (!request.IsDelivered) ArchiveRequestManager.Instance?.CancelRequest(request);
            if (!serviceCompleted && client != null) client.stateMachine?.CancelServiceCall(registrar);
        });

        registrar.SetState(ClerkController.ClerkState.WaitingForArchive);
        client.stateMachine.SetState(ClientState.WaitingForDocument);

        var archivistDesk = ScenePointsRegistry.Instance.GetServicePointByID(3); 
        if (archivistDesk == null) { FinishAction(false); yield break; }

        yield return registrar.MoveToTarget(archivistDesk.clerkStandPoint.position, ClerkController.ClerkState.WaitingForArchive.ToString());

        float waitTimer = 0f;
        float maxWaitTime = 60f;
        bool requestFulfilled = false;
        while(request.IsActive && waitTimer < maxWaitTime)
        {
            if (request.IsFulfilled)
            {
                requestFulfilled = true;
                break;
            }
            waitTimer += Time.deltaTime;
            yield return null;
        }

        if (!request.IsActive) { FinishAction(false); yield break; }
        if (requestFulfilled)
        {
            registrar.GetComponent<StackHolder>().ShowSingleDocumentSprite(); 
            registrar.SetState(ClerkController.ClerkState.ReturningToWork);
            yield return registrar.MoveToTarget(registrar.assignedWorkstation.clerkStandPoint.position, ClerkController.ClerkState.Working.ToString());
            registrar.GetComponent<StackHolder>().HideStack();

            if (!request.IsActive) { FinishAction(false); yield break; }
            ArchiveRequestManager.Instance.CompleteDelivery(request);
            ClientQueueManager.Instance?.RemoveClientFromQueue(client);
            client.billToPay += 150;
            client.stateMachine.SetGoal(ClientSpawner.GetCashierZone().waitingWaypoint);
            client.stateMachine.SetState(ClientState.MovingToGoal);
        }
        else
        {
            registrar.thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.archive.no_reply"), 3f, Color.red);
            registrar.SetState(ClerkController.ClerkState.ReturningToWork);
            yield return registrar.MoveToTarget(registrar.assignedWorkstation.clerkStandPoint.position, ClerkController.ClerkState.Working.ToString());
            
			client.ApplyStressJump(client.stressJump_Refusal);
			
            client.reasonForLeaving = ClientPathfinding.LeaveReason.Upset;
            client.stateMachine.SetGoal(ClientSpawner.Instance.exitWaypoint);
            client.stateMachine.SetState(ClientState.LeavingUpset);
        }
        
        serviceCompleted = true;
        registrar.ServiceComplete();
        FinishAction(true);
    }
}
