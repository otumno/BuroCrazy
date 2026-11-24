using UnityEngine;
using System.Collections;
using Managers;

public class RetrieveDocumentExecutor : ActionExecutor
{
    public override bool IsInterruptible => false;

    protected override IEnumerator ActionRoutine()
    {
        var archivist = staff;
        var request = ArchiveRequestManager.Instance?.GetNextRequest();
        if (archivist == null || request == null) { FinishAction(false); yield break; }

        RegisterCleanup(() => { if (!request.IsFulfilled) request.IsTaken = false; });
        var cabinet = ArchiveManager.Instance?.GetRandomCabinet();
        if (cabinet == null || request.RequestingRegistrar == null) { FinishAction(false); yield break; }
        yield return archivist.MoveToTarget(cabinet.transform.position, archivist.GetCurrentStateName());
        if (!request.IsActive || Vector2.Distance(archivist.transform.position, cabinet.transform.position) > 2.5f)
        { FinishAction(false); yield break; }

        archivist.thoughtBubble?.ShowPriorityMessage("Ищу выписку...", 2f, Color.yellow);
        yield return new WaitForSeconds(1.5f);

        int searchSteps = Random.Range(2, 4);
        string[] searchThoughts = { "Где же она...", "Так, так, так...", "Не на этой полке...", "Может здесь?", "Пыли-то сколько..." };
        for(int i = 0; i < searchSteps; i++)
        {
            if (!request.IsActive) { FinishAction(false); yield break; }
            archivist.thoughtBubble?.ShowPriorityMessage(searchThoughts[Random.Range(0, searchThoughts.Length)], 2f, Color.gray);
            yield return new WaitForSeconds(Random.Range(1.5f, 3f));
        }
        
        archivist.thoughtBubble?.ShowPriorityMessage("Ага, нашел!", 2f, Color.green);
        yield return new WaitForSeconds(1f);

        if (!request.IsActive) { FinishAction(false); yield break; }
        archivist.GetComponent<StackHolder>()?.ShowSingleDocumentSprite();

        var registrar = request.RequestingRegistrar;
        var handoffPoint = request.IsDelegated ? archivist.assignedWorkstation?.clerkStandPoint : registrar.transform;
        if (handoffPoint == null) { FinishAction(false); yield break; }
        yield return archivist.MoveToTarget(handoffPoint.position, archivist.GetCurrentStateName());
        if (!request.IsActive || Vector2.Distance(archivist.transform.position, handoffPoint.position) > 2.5f)
        { FinishAction(false); yield break; }

        archivist.GetComponent<StackHolder>()?.HideStack();
        ArchiveRequestManager.Instance?.FulfillRequest(request);

        Managers.AchievementManager.Instance?.UnlockAchievement("Achv_ArchivistStory");
        FinishAction(true);
    }
}
