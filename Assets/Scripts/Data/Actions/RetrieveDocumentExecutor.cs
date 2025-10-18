using UnityEngine;
using System.Collections;

public class RetrieveDocumentExecutor : ActionExecutor
{
    public override bool IsInterruptible => false;

    protected override IEnumerator ActionRoutine()
    {
        var archivist = staff;
        var request = ArchiveRequestManager.Instance.GetNextRequest();
        if (archivist == null || request == null) { FinishAction(false); yield break; }

        var cabinet = ArchiveManager.Instance.GetRandomCabinet();
        yield return staff.StartCoroutine(archivist.MoveToTarget(cabinet.transform.position, archivist.GetCurrentStateName()));

        archivist.thoughtBubble?.ShowPriorityMessage("Ищу выписку...", 4f, Color.yellow);
        yield return new WaitForSeconds(Random.Range(4f, 8f));
        archivist.GetComponent<StackHolder>().ShowSingleDocumentSprite();

        var registrar = request.RequestingRegistrar;
        yield return staff.StartCoroutine(archivist.MoveToTarget(registrar.transform.position, archivist.GetCurrentStateName()));

        archivist.GetComponent<StackHolder>().HideStack();
        request.IsFulfilled = true;

        FinishAction(true);
    }
}