using UnityEngine;
using System.Collections;
using System.Linq;

public class DeliverDocumentsExecutor : ActionExecutor
{
    public override bool IsInterruptible => true;

    protected override IEnumerator ActionRoutine()
    {
        var intern = staff as InternController;
        if (intern == null) { FinishAction(false); yield break; }

        var targetStack = Object.FindObjectsByType<DocumentStack>(FindObjectsSortMode.None)
            .Where(s => s != ArchiveManager.Instance.mainDocumentStack && !s.IsEmpty)
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

        yield return staff.StartCoroutine(intern.MoveToTarget(servicePoint.internCollectionPoint.position, intern.GetCurrentState()));

        if (targetStack == null) { FinishAction(false); yield break; }

        int docCount = targetStack.TakeEntireStack();
        var stackHolder = staff.GetComponent<StackHolder>();
        stackHolder?.ShowStack(docCount, targetStack.maxStackSize);

        Transform archivePoint = ArchiveManager.Instance.RequestDropOffPoint();
        if (archivePoint == null) { FinishAction(false); yield break; }

        yield return staff.StartCoroutine(intern.MoveToTarget(archivePoint.position, intern.GetCurrentState()));
        
        for (int i = 0; i < docCount; i++) { ArchiveManager.Instance.mainDocumentStack.AddDocumentToStack(); }
        stackHolder?.HideStack();
        ArchiveManager.Instance.FreeOverflowPoint(archivePoint);

        intern.SetState(InternController.InternState.Patrolling);
        ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
        FinishAction(true);
    }
}