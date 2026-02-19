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

        while (staff.IsOnDuty())
        {
            var target = points[Random.Range(0, points.Count)];
            if (target != null)
            {
                yield return staff.StartCoroutine(staff.MoveToTarget(target.transform.position, "Patrolling"));
                yield return new WaitForSeconds(Random.Range(3f, 7f));
            }

            // Небольшая пауза между точками
            yield return new WaitForSeconds(0.5f);
        }

        // Смена закончилась - завершаем действие
        FinishAction(true);
    }
}