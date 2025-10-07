using UnityEngine;
using System.Collections;

public class TakeStackToArchiveExecutor : ActionExecutor
{
    public override bool IsInterruptible => false; // Нельзя прерывать сотрудника, несущего важные документы!

    protected override IEnumerator ActionRoutine()
    {
        if (!(staff is ClerkController clerk) || clerk.assignedServicePoint == null)
        {
            FinishAction();
            yield break;
        }

        // --- Шаг 1: Подготовка ---
        var deskStack = clerk.assignedServicePoint.documentStack;
        if (deskStack == null || deskStack.IsEmpty)
        {
            FinishAction(); // Если стопка уже пуста, выходим
            yield break;
        }
        
        // Меняем состояние клерка (и его эмоцию, если настроено в StateEmotionMap!)
        clerk.SetState(ClerkController.ClerkState.GoingToArchive);
        clerk.thoughtBubble?.ShowPriorityMessage("Стол завален!\nНесу в архив...", 3f, new Color(1f, 0.5f, 0f));

        // --- Шаг 2: Забираем документы со стола ---
        int docCount = deskStack.TakeEntireStack(); // Забираем все документы
        
        // Показываем стопку в руках у клерка
        var stackHolder = staff.GetComponent<StackHolder>();
        stackHolder?.ShowStack(docCount, deskStack.maxStackSize);

        // --- Шаг 3: Идем в архив ---
        Transform archivePoint = ArchiveManager.Instance.RequestDropOffPoint();
        if (archivePoint == null)
        {
            Debug.LogError($"{staff.name} не может отнести документы: в архиве нет места!");
            // В будущем здесь можно реализовать состояние "В панике, не знает, куда деть документы"
            FinishAction();
            yield break;
        }

        yield return staff.StartCoroutine(clerk.MoveToTarget(archivePoint.position, ClerkController.ClerkState.AtArchive));

        // --- Шаг 4: Складываем документы ---
        clerk.thoughtBubble?.ShowPriorityMessage("Складываю...", 2f, Color.gray);
        yield return new WaitForSeconds(2f); // Имитация раскладки

        // Добавляем документы в главную стопку архива
        for (int i = 0; i < docCount; i++)
        {
            ArchiveManager.Instance.mainDocumentStack.AddDocumentToStack();
        }
        stackHolder?.HideStack(); // Прячем стопку из рук
        ArchiveManager.Instance.FreeOverflowPoint(archivePoint); // Освобождаем точку, если занимали дополнительную

        // --- Шаг 5: Возвращаемся на рабочее место ---
        clerk.SetState(ClerkController.ClerkState.ReturningToWork);
        yield return staff.StartCoroutine(clerk.MoveToTarget(clerk.assignedServicePoint.clerkStandPoint.position, ClerkController.ClerkState.Working));
        
        ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
        FinishAction();
    }
}