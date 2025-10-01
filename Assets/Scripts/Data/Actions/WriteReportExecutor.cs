using UnityEngine;
using System.Collections;

public class WriteReportExecutor : ActionExecutor
{
    protected override IEnumerator ActionRoutine()
{
    if (!(staff is GuardMovement guard))
    {
        FinishAction();
        yield break;
    }

    ServicePoint reportDesk = ScenePointsRegistry.Instance?.guardReportDesk;
    if (reportDesk == null || reportDesk.clerkStandPoint == null || reportDesk.documentStack == null)
    {
        Debug.LogError("Стол для отчетов (guardReportDesk) не настроен в ScenePointsRegistry!");
        FinishAction();
        yield break;
    }

    guard.SetState(GuardMovement.GuardState.WritingReport);

    // --- ДОБАВЛЕН ЛОГ ---
    staff.thoughtBubble?.ShowPriorityMessage("Нужно заполнить\nбумаги...", 2f, Color.yellow);
    Debug.Log($"<color=blue>{guard.name} отправляется на пост, чтобы написать {guard.unwrittenReportPoints} протоколов.</color>");

    yield return staff.StartCoroutine(guard.MoveToTarget(reportDesk.clerkStandPoint.position, GuardMovement.GuardState.WritingReport));

    Debug.Log($"<color=blue>{guard.name} прибыл на пост и начинает писать протоколы.</color>");

    int pointsAtStart = guard.unwrittenReportPoints;
    for (int i = 0; i < pointsAtStart; i++)
    {
        // Проверяем, остались ли еще протоколы (на случай, если значение изменится извне)
        if (guard.unwrittenReportPoints <= 0) break;

        // Тратим 1-2 секунды на один протокол
        float writeTime = Random.Range(1f, 2f);
        yield return new WaitForSeconds(writeTime);

        guard.unwrittenReportPoints--;
        reportDesk.documentStack.AddDocumentToStack();
        
        // --- ДОБАВЛЕН ЛОГ ---
        Debug.Log($"Протокол написан за {writeTime:F1} сек. Осталось: {guard.unwrittenReportPoints}. Документ добавлен на стол.");
    }
    
    Debug.Log($"<color=blue>{guard.name} закончил писать протоколы.</color>");
    guard.SetState(GuardMovement.GuardState.Idle);
    FinishAction();
}
}