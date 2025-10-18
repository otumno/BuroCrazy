using UnityEngine;
using System.Collections;

public class GoToJanitorHomeExecutor : ActionExecutor
{
    public override bool IsInterruptible => true;
    protected override IEnumerator ActionRoutine()
    {
        if (!(staff is ServiceWorkerController worker)) { FinishAction(false); yield break; }
        
        float actualMaxWait = worker.maxIdleWait * (1f - worker.skills.pedantry);
        float waitTime = Random.Range(worker.minIdleWait, actualMaxWait);

        Transform homePoint = ScenePointsRegistry.Instance?.janitorHomePoint;
        if (homePoint != null)
        {
            worker.SetState(ServiceWorkerController.WorkerState.Idle);
            yield return staff.StartCoroutine(worker.MoveToTarget(homePoint.position, ServiceWorkerController.WorkerState.Idle));
        }

        yield return new WaitForSeconds(waitTime);
        FinishAction(true);
    }
}