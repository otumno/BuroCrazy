using UnityEngine;
using System.Collections;
using System.Linq;

public class InternPatrolExecutor : ActionExecutor
{
    public override bool IsInterruptible => true;
    protected override IEnumerator ActionRoutine()
    {
        var intern = staff as InternController;
        if (intern == null) { FinishAction(false); yield break; }

        intern.SetState(InternController.InternState.Patrolling);
        intern.thoughtBubble?.ShowPriorityMessage("Патрулирую...", 5f, Color.gray);
        var patrolRoute = ScenePointsRegistry.Instance?.internPatrolPoints;
        if (patrolRoute == null || !patrolRoute.Any())
        {
            yield return new WaitForSeconds(10f);
            FinishAction(false);
            yield break;
        }

        int pointsToVisit = actionData.patrolPointsToVisit;
        for (int i = 0; i < pointsToVisit; i++)
        {
            var randomPoint = patrolRoute[Random.Range(0, patrolRoute.Count)];
            yield return staff.StartCoroutine(intern.MoveToTarget(randomPoint.position, InternController.InternState.Patrolling));
            yield return new WaitForSeconds(Random.Range(2f, 5f));
        }

        FinishAction(true);
    }
}