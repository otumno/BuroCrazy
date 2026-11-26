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
        var transfer = new DocumentStackTransfer(deskStack, deskStack.CurrentSize);
        RegisterCleanup(transfer.Dispose);
        if (transfer.Count == 0) { FinishAction(false); yield break; }
        var stackHolder = staff.GetComponent<StackHolder>();
        RegisterCleanup(() => { if (stackHolder != null) stackHolder.HideStack(); });
        IEnumerator WalkToDropOff(Vector3 position)
        {
            yield return clerk.MoveToTarget(position, ClerkController.ClerkState.AtArchive.ToString());
            if (ArchiveManager.IsWithinReach(clerk.transform, position))
            {
                clerk.thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.delivery.putting_down"), 2f, Color.gray);
                yield return new WaitForSeconds(2f);
            }
        }

        int pickedUp = transfer.Count;
        yield return transfer.DeliverToArchive(archive, clerk.transform, WalkToDropOff, stackHolder);
        int delivered = pickedUp - transfer.Count;
        transfer.Dispose();
        stackHolder?.HideStack();

        clerk.SetState(ClerkController.ClerkState.ReturningToWork);
        yield return ReturnToWorkstation();
        
        if (delivered > 0) ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
        FinishAction(delivered > 0);
    }
}
