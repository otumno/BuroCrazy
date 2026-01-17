// Assets/Scripts/Data/Actions/JanitorPatrolExecutor.cs
using UnityEngine;
using System.Collections;
using Managers;

public class JanitorPatrolExecutor : ActionExecutor
{
    public override bool IsInterruptible => true;

    protected override IEnumerator ActionRoutine()
    {
        var points = ScenePointsRegistry.Instance?.janitorPatrolPoints;

        if (points == null || points.Count == 0)
        {
            FinishAction(false);
            yield break;
        }

        int index = 0;
        while (true)
        {
            var target = points[index];
            if (target != null)
            {
                // ИСПРАВЛЕНИЕ: target.transform.position
                yield return staff.StartCoroutine(staff.MoveToTarget(target.transform.position, "Cleaning"));
                
                // Имитация уборки
                yield return new WaitForSeconds(2f);
            }

            index = (index + 1) % points.Count;
            yield return null;
        }
    }
}