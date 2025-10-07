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
    if (guard.unwrittenReportPoints <= 0) break;

    float writeTime = Random.Range(1f, 2f);
    yield return new WaitForSeconds(writeTime);

    // Пытаемся добавить документ и проверяем результат
    if (reportDesk.documentStack.AddDocumentToStack())
    {
        // Если получилось, списываем очко
        guard.unwrittenReportPoints--;
        Debug.Log($"Протокол написан. Осталось: {guard.unwrittenReportPoints}. Документ добавлен на стол.");
    }
    else
    {
        // Если стол забит, прерываем действие и выводим сообщение
        Debug.LogWarning($"Стол для отчетов ({reportDesk.name}) переполнен! {guard.name} не может продолжить.");
        guard.thoughtBubble?.ShowPriorityMessage("Стол завален!\nНе могу работать.", 3f, Color.red);
        break; // Выходим из цикла, очки не тратятся
    }
}
    
    Debug.Log($"<color=blue>{guard.name} закончил писать протоколы.</color>");
    guard.SetState(GuardMovement.GuardState.Idle);
    FinishAction();
}
}