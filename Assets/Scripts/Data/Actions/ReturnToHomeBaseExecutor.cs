using UnityEngine;
using System.Collections;

public class ReturnToHomeBaseExecutor : ActionExecutor
{
    // Это действие не прерываемо само по себе, но его прервет AI, запустив новое
    public override bool IsInterruptible => false; 

    protected override IEnumerator ActionRoutine()
    {
        if (!(staff is ServiceWorkerController worker))
        {
            FinishAction();
            yield break;
        }

        Transform homePoint = ScenePointsRegistry.Instance?.janitorHomePoint;
        if (homePoint == null)
        {
            // Если подсобка не указана, просто ждем на месте
            while (true)
            {
                yield return new WaitForSeconds(5f);
            }
        }

        worker.SetState(ServiceWorkerController.WorkerState.Idle);
        yield return staff.StartCoroutine(worker.MoveToTarget(homePoint.position, ServiceWorkerController.WorkerState.Idle));

        // Бесконечный цикл ожидания. Он будет прерван извне, когда AI найдет более приоритетную задачу.
        while (true)
        {
            staff.thoughtBubble?.ShowPriorityMessage("Все чисто...", 3f, Color.gray);
            yield return new WaitForSeconds(10f);
        }
    }
}