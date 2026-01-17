// Assets/Scripts/Data/Actions/InternPatrolExecutor.cs
using UnityEngine;
using System.Collections;
using Managers;

public class InternPatrolExecutor : ActionExecutor
{
    public override bool IsInterruptible => true;

    protected override IEnumerator ActionRoutine()
    {
        var points = ScenePointsRegistry.Instance?.internPatrolPoints;

        if (points == null || points.Count == 0)
        {
            FinishAction(false);
            yield break;
        }

        while (true)
        {
            // Случайная точка
            var target = points[Random.Range(0, points.Count)];
            if (target != null)
            {
                // ИСПРАВЛЕНИЕ: target.transform.position
                yield return staff.StartCoroutine(staff.MoveToTarget(target.transform.position, "Patrolling"));
                yield return new WaitForSeconds(Random.Range(3f, 7f));
            }
            yield return null;
        }
    }
}