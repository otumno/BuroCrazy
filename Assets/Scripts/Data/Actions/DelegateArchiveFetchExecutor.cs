using UnityEngine;
using System.Collections;
using Managers;

public class DelegateArchiveFetchExecutor : ActionExecutor
{
    public override bool IsInterruptible => false;

    protected override IEnumerator ActionRoutine()
    {
        var manager = ArchiveRequestManager.Instance;
        var request = manager?.GetNextDelegatedRequest();
        if (request == null) { FinishAction(false); yield break; }
        RegisterCleanup(() =>
        {
            if (!request.IsDelivered) request.IsTakenByCourier = false;
        });

        var archiveDesk = ScenePointsRegistry.Instance?.GetServicePointByID(3);
        if (archiveDesk?.internCollectionPoint == null && archiveDesk?.clerkStandPoint == null)
        { FinishAction(false); yield break; }

        staff.CurrentSubStatus = "Получает выписку в архиве";
        var collectionPoint = archiveDesk.internCollectionPoint != null
            ? archiveDesk.internCollectionPoint : archiveDesk.clerkStandPoint;
        yield return staff.MoveToTarget(collectionPoint.position, staff.GetCurrentStateName());
        if (!request.IsActive || Vector2.Distance(staff.transform.position, collectionPoint.position) > 2.5f)
        { FinishAction(false); yield break; }

        float elapsed = 0f;
        float timeout = Gameplay.AIBalanceConfig.Instance != null ? Gameplay.AIBalanceConfig.Instance.archiveWaitTimeout : 60f;
        while (request.IsActive && !request.IsFulfilled && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
        if (!request.IsActive || !request.IsFulfilled) { FinishAction(false); yield break; }

        staff.GetComponent<StackHolder>()?.ShowSingleDocumentSprite();
        staff.CurrentSubStatus = "Несёт выписку посетителю";
        // A mobile courier has no service desk/Waypoint. Walk to the waiting visitor instead
        // of issuing a desk call that only interns covering an actual workstation can serve.
        var client = request.WaitingClient;
        yield return staff.MoveToTarget(client.transform.position, staff.GetCurrentStateName());
        if (!request.IsActive || Vector2.Distance(staff.transform.position, client.transform.position) > 2.5f)
        { FinishAction(false); yield break; }

        var cashier = ClientSpawner.GetCashierZone()?.waitingWaypoint;
        if (cashier == null) { FinishAction(false); yield break; }
        manager.CompleteDelivery(request);
        client.billToPay += 150;
        ClientQueueManager.Instance?.RemoveClientFromQueue(client);
        client.stateMachine.SetGoal(cashier);
        client.stateMachine.SetState(ClientState.MovingToGoal);
        staff.thoughtBubble?.ShowPriorityMessage("Ваша выписка. Пройдите в кассу.", 3f, Color.green);
        staff.ShowActionEffect(true);
        ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
        FinishAction(true);
    }
}
