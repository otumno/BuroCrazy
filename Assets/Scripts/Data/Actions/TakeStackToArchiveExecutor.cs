using UnityEngine;
using System.Collections;

public class TakeStackToArchiveExecutor : ActionExecutor
{
    public override bool IsInterruptible => false;

    protected override IEnumerator ActionRoutine()
    {
        if (!(staff is ClerkController clerk) || clerk.assignedWorkstation == null)
        {
            FinishAction();
            yield break;
        }

        var deskStack = clerk.assignedWorkstation.documentStack;
        if (deskStack == null || deskStack.IsEmpty)
        {
            FinishAction();
            yield break;
        }
        
        clerk.SetState(ClerkController.ClerkState.GoingToArchive);
        clerk.thoughtBubble?.ShowPriorityMessage("Стол завален!\nНесу в архив...", 3f, new Color(1f, 0.5f, 0f));

        int docCount = deskStack.TakeEntireStack();
        var stackHolder = staff.GetComponent<StackHolder>();
        stackHolder?.ShowStack(docCount, deskStack.maxStackSize);

        Transform archivePoint = ArchiveManager.Instance.RequestDropOffPoint();
        if (archivePoint == null)
        {
            Debug.LogError($"{staff.name} не может отнести документы: в архиве нет места!");
            FinishAction();
            yield break;
        }

        // ----- THE FIX IS HERE -----
        yield return staff.StartCoroutine(clerk.MoveToTarget(archivePoint.position, ClerkController.ClerkState.AtArchive.ToString()));
        
        clerk.thoughtBubble?.ShowPriorityMessage("Складываю...", 2f, Color.gray);
        yield return new WaitForSeconds(2f);

        for (int i = 0; i < docCount; i++)
        {
            ArchiveManager.Instance.mainDocumentStack.AddDocumentToStack();
        }
        stackHolder?.HideStack();
        ArchiveManager.Instance.FreeOverflowPoint(archivePoint);

        clerk.SetState(ClerkController.ClerkState.ReturningToWork);
        // ----- THE FIX IS HERE -----
        yield return staff.StartCoroutine(clerk.MoveToTarget(clerk.assignedWorkstation.clerkStandPoint.position, ClerkController.ClerkState.Working.ToString()));
        
        ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
        FinishAction();
    }
}