// Assets/Scripts/Data/Actions/JanitorPatrolExecutor.cs
using UnityEngine;
using System.Collections;
using Managers;
using Utilities;

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
                // --- ФИКС ТАЙМАУТА (КАК У СТАЖЕРА) ---
                float moveTimeout = 15f;
                staff.AgentMover.SetPath(PathfindingUtility.BuildPathTo(staff.transform.position, target.transform.position, staff.gameObject));
                
                while (staff.AgentMover.IsMoving() && moveTimeout > 0)
                {
                    moveTimeout -= Time.deltaTime;
                    yield return null;
                }

                if (moveTimeout <= 0)
                {
                    staff.AgentMover.Stop();
                    staff.thoughtBubble?.ShowPriorityMessage("Туда не пролезть...", 2f, Color.red);
                    FinishAction(false);
                    yield break;
                }
                // ------------------------------------
                
                // Имитация уборки
                yield return new WaitForSeconds(2f);
            }

            index = (index + 1) % points.Count;
            yield return null;
        }
    }
}