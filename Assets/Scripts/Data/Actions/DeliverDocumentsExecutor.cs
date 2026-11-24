using UnityEngine;
using System.Collections;
using System.Linq;
using Managers;

public class DeliverDocumentsExecutor : ActionExecutor
{
    public override bool IsInterruptible => true;

    protected override IEnumerator ActionRoutine()
    {
        var intern = staff as InternController;
        if (intern == null || ArchiveManager.Instance == null) { FinishAction(false); yield break; }

        var targetStack = Object.FindObjectsByType<DocumentStack>(FindObjectsSortMode.None)
            .Where(s => !ArchiveManager.Instance.OwnsStack(s) && s.OrdinaryDocumentCount > 0)
            .OrderByDescending(s => s.CurrentSize)
            .FirstOrDefault();

        if (targetStack == null)
        {
            FinishAction(false);
            yield break;
        }

        var servicePoint = ScenePointsRegistry.Instance.allServicePoints.FirstOrDefault(sp => sp.documentStack == targetStack);
        if (servicePoint == null || servicePoint.internCollectionPoint == null)
        {
            FinishAction(false);
            yield break;
        }

        intern.SetState(InternController.InternState.TakingStackToArchive);
        intern.thoughtBubble?.ShowPriorityMessage("Заберу документы...", 3f, Color.gray);

        yield return intern.MoveToTarget(servicePoint.internCollectionPoint.position, intern.GetCurrentState());

        if (targetStack == null) { FinishAction(false); yield break; }

        var archive = ArchiveManager.Instance;
        Transform archivePoint = archive.RequestDropOffPoint();
        if (archivePoint == null) { FinishAction(false); yield break; }
        RegisterCleanup(() => { if (archive != null) archive.FreeOverflowPoint(archivePoint); });
        var transfer = new DocumentStackTransfer(targetStack, targetStack.CurrentSize);
        RegisterCleanup(transfer.Dispose);
        var stackHolder = staff.GetComponent<StackHolder>();
        RegisterCleanup(() => { if (stackHolder != null) stackHolder.HideStack(); });
        stackHolder?.ShowStack(transfer.Count, targetStack.maxStackSize);

        yield return intern.MoveToTarget(archivePoint.position, intern.GetCurrentState());
        
        int delivered = transfer.DeliverTo(archive.GetDropOffStack(archivePoint));
        stackHolder?.HideStack();
        ArchiveManager.Instance.FreeOverflowPoint(archivePoint);

        intern.SetState(InternController.InternState.Patrolling);
        ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
        FinishAction(delivered > 0);
    }
}
