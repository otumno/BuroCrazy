using UnityEngine;
using System.Collections;

public class ScreamInClosetExecutor : ActionExecutor
{
    public override bool IsInterruptible => false; // Нервный срыв нельзя прервать!

    protected override IEnumerator ActionRoutine()
    {
        if (!(staff is ServiceWorkerController worker)) { FinishAction(); yield break; }

        // 1. Меняем состояние и идем в "подсобку" (домашнюю точку)
        worker.SetState(ServiceWorkerController.WorkerState.StressedOut);
        Transform closetPoint = ScenePointsRegistry.Instance?.janitorHomePoint;
        if (closetPoint != null)
        {
            yield return staff.StartCoroutine(worker.MoveToTarget(closetPoint.position, ServiceWorkerController.WorkerState.StressedOut));
        }

        // 2. Выпускаем пар
        worker.thoughtBubble?.ShowPriorityMessage("АААААААА!", 4f, Color.red);
        yield return new WaitForSeconds(5f);
        worker.thoughtBubble?.ShowPriorityMessage("НЕНАВИЖУ МУСОР!", 3f, Color.red);
        yield return new WaitForSeconds(5f);

        // 3. Сбрасываем выгорание и возвращаемся к работе
        worker.SetCurrentFrustration(0f);
        Debug.Log($"<color=cyan>{worker.name} выпустил пар. Выгорание сброшено.</color>");
        worker.SetState(ServiceWorkerController.WorkerState.Idle);

        FinishAction();
    }
}