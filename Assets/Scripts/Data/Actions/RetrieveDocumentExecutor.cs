using UnityEngine;
using System.Collections;
using Managers;

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

        archivist.thoughtBubble?.ShowPriorityMessage("Ищу выписку...", 2f, Color.yellow);
        yield return new WaitForSeconds(1.5f);

        int searchSteps = Random.Range(2, 4);
        string[] searchThoughts = { "Где же она...", "Так, так, так...", "Не на этой полке...", "Может здесь?", "Пыли-то сколько..." };
        for(int i = 0; i < searchSteps; i++)
        {
            archivist.thoughtBubble?.ShowPriorityMessage(searchThoughts[Random.Range(0, searchThoughts.Length)], 2f, Color.gray);
            yield return new WaitForSeconds(Random.Range(1.5f, 3f));
        }
        
        archivist.thoughtBubble?.ShowPriorityMessage("Ага, нашел!", 2f, Color.green);
        yield return new WaitForSeconds(1f);

        archivist.GetComponent<StackHolder>().ShowSingleDocumentSprite();

        var registrar = request.RequestingRegistrar;
        yield return staff.StartCoroutine(archivist.MoveToTarget(registrar.transform.position, archivist.GetCurrentStateName()));

        archivist.GetComponent<StackHolder>().HideStack();
        request.IsFulfilled = true;

        FinishAction(true);
    }
}