using UnityEngine;
using System.Collections;
using System.Linq;

public class JanitorPatrolExecutor : ActionExecutor
{
    public override bool IsInterruptible => true;

    protected override IEnumerator ActionRoutine()
    {
        var worker = staff as ServiceWorkerController;
        if (worker == null) { FinishAction(); yield break; }

        worker.SetState(ServiceWorkerController.WorkerState.Patrolling);
        var patrolRoute = ScenePointsRegistry.Instance?.janitorPatrolPoints;

        if (patrolRoute == null || !patrolRoute.Any())
        {
            yield return new WaitForSeconds(10f);
            FinishAction();
            yield break;
        }

        int pointsToVisit = actionData.patrolPointsToVisit;
        for (int i = 0; i < pointsToVisit; i++)
        {
            var randomPoint = patrolRoute[Random.Range(0, patrolRoute.Count)];
            yield return staff.StartCoroutine(worker.MoveToTarget(randomPoint.position, ServiceWorkerController.WorkerState.Patrolling));
            yield return new WaitForSeconds(Random.Range(3f, 7f));
        }
        FinishAction();
    }
}