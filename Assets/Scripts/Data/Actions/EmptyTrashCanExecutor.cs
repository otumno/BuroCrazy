using UnityEngine;
using System.Collections;
using System.Linq;

public class EmptyTrashCanExecutor : ActionExecutor
{
    public override bool IsInterruptible => false;

    protected override IEnumerator ActionRoutine()
    {
        var worker = staff as ServiceWorkerController;
        if (worker == null) { FinishAction(); yield break; }

        // 1. Находим ближайший ПОЛНЫЙ бак
        var targetCan = TrashCan.AllTrashCans
            .Where(can => can.IsFull)
            .OrderBy(can => Vector3.Distance(staff.transform.position, can.transform.position))
            .FirstOrDefault();

        if (targetCan == null) { FinishAction(); yield break; }

        // 2. Идем к баку
        worker.SetState(ServiceWorkerController.WorkerState.GoingToMess);
        worker.thoughtBubble?.ShowPriorityMessage("Бак полон!", 2f, Color.red);
        yield return staff.StartCoroutine(worker.MoveToTarget(targetCan.transform.position, ServiceWorkerController.WorkerState.Cleaning));

        // 3. "Забираем" мусор, меняем швабру на мешок
        worker.thoughtBubble?.ShowPriorityMessage("...", 1.5f, Color.gray);
        if (worker.broomTransform != null) worker.broomTransform.gameObject.SetActive(false);
        if (worker.TrashBagObject != null) worker.TrashBagObject.SetActive(true);

        yield return new WaitForSeconds(1.5f);
        targetCan.Empty();
        Debug.Log($"{worker.name} опустошил бак {targetCan.name}.");

        // 4. Несем к контейнеру
        Transform dumpster = ScenePointsRegistry.Instance?.dumpsterPoint;
        if (dumpster != null)
        {
            yield return staff.StartCoroutine(worker.MoveToTarget(dumpster.position, ServiceWorkerController.WorkerState.Cleaning));

            worker.thoughtBubble?.ShowPriorityMessage("Готово.", 2f, Color.green);
            if (worker.TrashBagObject != null) worker.TrashBagObject.SetActive(false);
            if (worker.broomTransform != null) worker.broomTransform.gameObject.SetActive(true);

            yield return new WaitForSeconds(2f);
        }
        else // Если контейнер не найден
        {
            if (worker.TrashBagObject != null) worker.TrashBagObject.SetActive(false);
            if (worker.broomTransform != null) worker.broomTransform.gameObject.SetActive(true);
        }

        // 5. Завершаем
        worker.SetState(ServiceWorkerController.WorkerState.Idle);
        ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
        FinishAction();
    }
}