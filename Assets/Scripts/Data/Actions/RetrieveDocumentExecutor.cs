using UnityEngine;
using System.Collections;

public class RetrieveDocumentExecutor : ActionExecutor
{
    public override bool IsInterruptible => false; // Это самый высокий приоритет!

    protected override IEnumerator ActionRoutine()
    {
        var archivist = staff;
        var request = ArchiveRequestManager.Instance.GetNextRequest();
        if (archivist == null || request == null) { FinishAction(); yield break; }

        // 1. Идем в случайный шкаф за документом
        var cabinet = ArchiveManager.Instance.GetRandomCabinet();
        yield return staff.StartCoroutine(archivist.MoveToTarget(cabinet.transform.position, archivist.GetCurrentStateName())); // нужен MoveToTarget для StaffController

        // 2. Ищем документ
        archivist.thoughtBubble?.ShowPriorityMessage("Ищу выписку...", 4f, Color.yellow);
        yield return new WaitForSeconds(Random.Range(4f, 8f));
        archivist.GetComponent<StackHolder>().ShowSingleDocumentSprite();

        // 3. Несем документ к ожидающему регистратору
        var registrar = request.RequestingRegistrar;
        yield return staff.StartCoroutine(archivist.MoveToTarget(registrar.transform.position, archivist.GetCurrentStateName()));

        // 4. "Отдаем" документ и помечаем запрос как выполненный
        archivist.GetComponent<StackHolder>().HideStack();
        request.IsFulfilled = true;
        Debug.Log($"{archivist.name} доставил документ для {registrar.name}.");

        FinishAction();
    }
}