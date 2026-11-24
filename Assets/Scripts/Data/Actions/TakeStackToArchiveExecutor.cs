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

        var deskStack = clerk.assignedWorkstation.documentStack;
        if (deskStack == null || deskStack.IsEmpty)
        {
            FinishAction(false);
            yield break;
        }
        
        clerk.SetState(ClerkController.ClerkState.GoingToArchive);
        clerk.thoughtBubble?.ShowPriorityMessage("Стол завален!\nНесу в архив...", 3f, new Color(1f, 0.5f, 0f));

        var archive = ArchiveManager.Instance;
        Transform archivePoint = archive != null ? archive.RequestDropOffPoint() : null;
        if (archivePoint == null) { FinishAction(false); yield break; }
        RegisterCleanup(() => { if (archive != null) archive.FreeOverflowPoint(archivePoint); });
        var transfer = new DocumentStackTransfer(deskStack, deskStack.CurrentSize);
        RegisterCleanup(transfer.Dispose);
        var stackHolder = staff.GetComponent<StackHolder>();
        RegisterCleanup(() => { if (stackHolder != null) stackHolder.HideStack(); });
        stackHolder?.ShowStack(transfer.Count, deskStack.maxStackSize);

        yield return clerk.MoveToTarget(archivePoint.position, ClerkController.ClerkState.AtArchive.ToString());
        
        clerk.thoughtBubble?.ShowPriorityMessage("Складываю...", 2f, Color.gray);
        yield return new WaitForSeconds(2f);

        int delivered = transfer.DeliverTo(archive.GetDropOffStack(archivePoint));
        transfer.Dispose();
        stackHolder?.HideStack();
        ArchiveManager.Instance.FreeOverflowPoint(archivePoint);

        clerk.SetState(ClerkController.ClerkState.ReturningToWork);
        yield return clerk.MoveToTarget(clerk.assignedWorkstation.clerkStandPoint.position, ClerkController.ClerkState.Working.ToString());
        
        ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
        FinishAction(delivered > 0);
    }
}
