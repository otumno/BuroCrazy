using UnityEngine;
using System.Collections;

public class ScreamInClosetExecutor : ActionExecutor
{
    public override bool IsInterruptible => false;
    protected override IEnumerator ActionRoutine()
    {
        if (!(staff is ServiceWorkerController worker)) { FinishAction(false); yield break; }

        worker.SetState(ServiceWorkerController.WorkerState.StressedOut);
        Transform closetPoint = ScenePointsRegistry.Instance?.janitorHomePoint;
        if (closetPoint != null)
        {
            yield return staff.StartCoroutine(worker.MoveToTarget(closetPoint.position, ServiceWorkerController.WorkerState.StressedOut));
        }

        worker.thoughtBubble?.ShowPriorityMessage("АААААААА!", 4f, Color.red);
        yield return new WaitForSeconds(5f);
        worker.thoughtBubble?.ShowPriorityMessage("НЕНАВИЖУ МУСОР!", 3f, Color.red);
        yield return new WaitForSeconds(5f);
        
        worker.SetCurrentFrustration(0f);
        worker.SetState(ServiceWorkerController.WorkerState.Idle);

        FinishAction(true);
    }
}