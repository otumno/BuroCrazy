using UnityEngine;
using System.Collections;

public class PatrolExecutor : ActionExecutor
{
    protected override IEnumerator ActionRoutine()
    {
        GuardMovement guard = staff as GuardMovement;
        if (guard == null)
        {
            FinishAction(false);
            yield break;
        }

        guard.SetState(GuardMovement.GuardState.Patrolling);
        int pointsToVisit = actionData.patrolPointsToVisit;
        if (pointsToVisit <= 0) pointsToVisit = 1;

        for (int i = 0; i < pointsToVisit; i++)
        {
            var patrolTarget = guard.SelectNewPatrolPoint();
            if (patrolTarget != null)
            {
                yield return staff.StartCoroutine(guard.MoveToTarget(patrolTarget.position, GuardMovement.GuardState.WaitingAtWaypoint));
                ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
                yield return new WaitForSeconds(Random.Range(guard.minWaitTime, guard.maxWaitTime));
            }
            else
            {
                yield return new WaitForSeconds(3f);
                break;
            }
        }

        guard.unwrittenReportPoints++;
        FinishAction(true);
    }
}