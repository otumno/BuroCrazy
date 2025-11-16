using UnityEngine;
using System.Collections;

public class GoToJanitorHomeExecutor : ActionExecutor
{
    public override bool IsInterruptible => true;

    protected override IEnumerator ActionRoutine()
    {
        if (!(staff is ServiceWorkerController worker)) { FinishAction(); yield break; }

        Transform homePoint = ScenePointsRegistry.Instance?.janitorHomePoint;
        if (homePoint == null) {
            while (true) { yield return new WaitForSeconds(5f); }
        }

        worker.SetState(ServiceWorkerController.WorkerState.Idle);
        yield return staff.StartCoroutine(worker.MoveToTarget(homePoint.position, ServiceWorkerController.WorkerState.Idle));

        while (true)
        {
            yield return new WaitForSeconds(5f);
        }
    }
}