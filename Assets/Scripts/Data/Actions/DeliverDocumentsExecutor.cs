using UnityEngine;
using System.Collections;
using System.Linq;
using Managers;

public class DeliverDocumentsExecutor : ActionExecutor
{
    public override bool IsInterruptible => true;

    protected override IEnumerator ActionRoutine()
    {
        if ((!(staff is InternController) && !(staff is OfficeManagerController) && staff.currentRole != StaffController.Role.Registrar) || ArchiveManager.Instance == null ||
            ScenePointsRegistry.Instance == null) { FinishAction(false); yield break; }

        var targetStack = Object.FindObjectsByType<DocumentStack>(FindObjectsSortMode.None)
            .Where(s => (staff.currentRole != StaffController.Role.Registrar || staff.assignedWorkstation != null &&
                staff.assignedWorkstation.CurrentClient == null && staff.assignedWorkstation.documentStack == s) && !ArchiveManager.Instance.OwnsStack(s) && s.OrdinaryDocumentCount > 0 &&
                ScenePointsRegistry.Instance.allServicePoints.Any(point => point != null && point.documentStack == s && point.internCollectionPoint != null))
            .OrderByDescending(s => s.CurrentSize)
            .FirstOrDefault();

        if (targetStack == null)
        {
            FinishAction(false);
            yield break;
        }

        var servicePoint = ScenePointsRegistry.Instance.allServicePoints.FirstOrDefault(sp => sp != null && sp.documentStack == targetStack);
        if (servicePoint == null || servicePoint.internCollectionPoint == null)
        {
            FinishAction(false);
            yield break;
        }

        string carryingState = staff is InternController ? "TakingStackToArchive" : staff is ClerkController ? "GoingToArchive" : "CarryingDoc";
        staff.thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.delivery.pickup"), 3f, Color.gray);

        yield return staff.MoveToTarget(servicePoint.internCollectionPoint.position, carryingState);

        if (targetStack == null || servicePoint == null || servicePoint.internCollectionPoint == null ||
            Vector2.Distance(staff.transform.position, servicePoint.internCollectionPoint.position) > 1f)
        { FinishAction(false); yield break; }

        var archive = ArchiveManager.Instance;
        var transfer = new DocumentStackTransfer(targetStack, targetStack.CurrentSize);
        RegisterCleanup(transfer.Dispose);
        if (transfer.Count == 0) { FinishAction(false); yield break; }
        var stackHolder = staff.GetComponent<StackHolder>();
        RegisterCleanup(() => { if (stackHolder != null) stackHolder.HideStack(); });
        int pickedUp = transfer.Count;
        yield return transfer.DeliverToArchive(archive, staff.transform, position => staff.MoveToTarget(position, carryingState), stackHolder);
        int delivered = pickedUp - transfer.Count;

        if (staff is InternController intern) intern.SetState(InternController.InternState.Patrolling);
        else if (staff is OfficeManagerController manager) manager.SetState(OfficeManagerController.ManagerState.Idle);
        else if (staff is ClerkController)
        {
            transfer.Dispose();
            stackHolder?.HideStack();
            yield return ReturnToWorkstation();
        }
        if (delivered > 0) ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
        FinishAction(delivered > 0);
    }
}
