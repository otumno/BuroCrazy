using UnityEngine;
using System.Collections;
using Managers;

public class TakeStackToArchiveExecutor : ActionExecutor
{
    public override bool IsInterruptible => false;

    protected override IEnumerator ActionRoutine()
    {
        if (!(staff is ClerkController clerk) || clerk.assignedWorkstation == null)
        {
            FinishAction(false);
            yield break;
        }

        yield return ReturnToWorkstation();
        if (!IsAtWorkstation) { FinishAction(false); yield break; }

        var deskStack = clerk.assignedWorkstation.documentStack;
        if (deskStack == null || deskStack.OrdinaryDocumentCount == 0)
        {
            FinishAction(false);
            yield break;
        }
        
        clerk.SetState(ClerkController.ClerkState.GoingToArchive);
        clerk.thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.delivery.desk_full"), 3f, new Color(1f, 0.5f, 0f));

        var archive = ArchiveManager.Instance;
        Transform archivePoint = archive != null ? archive.RequestDropOffPoint() : null;
        if (archivePoint == null) { FinishAction(false); yield break; }
        RegisterCleanup(() => { if (archive != null) archive.FreeOverflowPoint(archivePoint); });
        var transfer = new DocumentStackTransfer(deskStack, deskStack.CurrentSize);
        RegisterCleanup(transfer.Dispose);
        if (transfer.Count == 0) { FinishAction(false); yield break; }
        var stackHolder = staff.GetComponent<StackHolder>();
        RegisterCleanup(() => { if (stackHolder != null) stackHolder.HideStack(); });
        stackHolder?.ShowStack(transfer.Count, deskStack.maxStackSize);

        yield return clerk.MoveToTarget(archivePoint.position, ClerkController.ClerkState.AtArchive.ToString());
        if (archive == null || archivePoint == null || Vector2.Distance(clerk.transform.position, archivePoint.position) > 1f)
        { FinishAction(false); yield break; }
        
        clerk.thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.delivery.putting_down"), 2f, Color.gray);
        yield return new WaitForSeconds(2f);
        if (archive == null || archivePoint == null) { FinishAction(false); yield break; }

        int delivered = transfer.DeliverTo(archive.GetDropOffStack(archivePoint));
        transfer.Dispose();
        stackHolder?.HideStack();
        archive.FreeOverflowPoint(archivePoint);

        clerk.SetState(ClerkController.ClerkState.ReturningToWork);
        yield return ReturnToWorkstation();
        
        ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
        FinishAction(delivered > 0);
    }
}
