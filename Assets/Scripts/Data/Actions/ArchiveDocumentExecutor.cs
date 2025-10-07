using UnityEngine;
using System.Collections;

public class ArchiveDocumentExecutor : ActionExecutor
{
    public override bool IsInterruptible => true; // Можно прервать ради более важного поиска

    protected override IEnumerator ActionRoutine()
    {
        var archivist = staff; // Работает с любым StaffController
        if (archivist == null) { FinishAction(); yield break; }

        // 1. Берем один документ из стопки
        if (!ArchiveManager.Instance.mainDocumentStack.TakeOneDocument())
        {
            FinishAction(); // Если взять не удалось, завершаем
            yield break;
        }

        var stackHolder = archivist.GetComponent<StackHolder>();
        stackHolder?.ShowSingleDocumentSprite(); // Показываем один документ в руках

        // 2. Находим случайный шкаф
        var targetCabinet = ArchiveManager.Instance.GetRandomCabinet();
        if (targetCabinet == null) { FinishAction(); yield break; }

        // 3. Идем к шкафу
        archivist.thoughtBubble?.ShowPriorityMessage("Архивирую...", 3f, Color.gray);
        archivist.AgentMover.SetPath(PathfindingUtility.BuildPathTo(staff.transform.position, targetCabinet.transform.position, staff.gameObject));
        yield return new WaitUntil(() => !archivist.AgentMover.IsMoving());

        // 4. "Складываем" документ и завершаем
        yield return new WaitForSeconds(Random.Range(2f, 4f)); // Имитация работы
        stackHolder?.HideStack();

        ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
        FinishAction();
    }
}