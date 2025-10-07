using UnityEngine;
using System.Collections;
using System.Linq;

public class DeliverDocumentsExecutor : ActionExecutor
{
    public override bool IsInterruptible => true; // Можно прервать, если появится более важная задача

    protected override IEnumerator ActionRoutine()
    {
        var intern = staff as InternController;
        if (intern == null) { FinishAction(); yield break; }

        // --- Шаг 1: Найти самую полную стопку ---
        var targetStack = Object.FindObjectsByType<DocumentStack>(FindObjectsSortMode.None)
            .Where(s => s != ArchiveManager.Instance.mainDocumentStack && !s.IsEmpty) // Исключаем архивную и пустые стопки
            .OrderByDescending(s => s.CurrentSize) // Сортируем по убыванию размера
            .FirstOrDefault(); // Берем самую большую

        if (targetStack == null)
        {
            FinishAction(); // Пока собирались, документы уже унесли
            yield break;
        }

        // Находим ServicePoint, к которому относится эта стопка
        var servicePoint = ScenePointsRegistry.Instance.allServicePoints.FirstOrDefault(sp => sp.documentStack == targetStack);
        if (servicePoint == null || servicePoint.internCollectionPoint == null)
        {
            Debug.LogError($"Для стопки {targetStack.name} не найден ServicePoint или точка для стажера (internCollectionPoint)!");
            FinishAction();
            yield break;
        }

        // --- Шаг 2: Идем к столу за документами ---
        intern.SetState(InternController.InternState.TakingStackToArchive);
        intern.thoughtBubble?.ShowPriorityMessage("Заберу документы...", 3f, Color.gray);

        yield return staff.StartCoroutine(intern.MoveToTarget(servicePoint.internCollectionPoint.position, intern.GetCurrentState()));

        // --- Шаг 3: Забираем документы ---
        if (targetStack == null) { FinishAction(); yield break; } // Проверка на случай, если стопку удалили

        int docCount = targetStack.TakeEntireStack();
        var stackHolder = staff.GetComponent<StackHolder>();
        stackHolder?.ShowStack(docCount, targetStack.maxStackSize);

        // --- Шаг 4: Несем в архив ---
        Transform archivePoint = ArchiveManager.Instance.RequestDropOffPoint();
        if (archivePoint == null) { FinishAction(); yield break; }

        yield return staff.StartCoroutine(intern.MoveToTarget(archivePoint.position, intern.GetCurrentState()));

        // --- Шаг 5: Складываем и возвращаемся к делам ---
        for (int i = 0; i < docCount; i++) { ArchiveManager.Instance.mainDocumentStack.AddDocumentToStack(); }
        stackHolder?.HideStack();
        ArchiveManager.Instance.FreeOverflowPoint(archivePoint);

        intern.SetState(InternController.InternState.Patrolling); // Возвращаемся в состояние патрулирования
        ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
        FinishAction();
    }
}