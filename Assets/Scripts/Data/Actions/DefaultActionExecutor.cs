// Assets/Scripts/Data/Actions/DefaultActionExecutor.cs
using UnityEngine;
using System.Collections;
using Managers;

public class DefaultActionExecutor : ActionExecutor
{
    public override bool IsInterruptible => true;

    protected override IEnumerator ActionRoutine()
    {
        // ПРИМЕР: Guard идет на пост
        if (actionData.actionType == ActionType.GoToPost)
        {
            var point = ScenePointsRegistry.Instance?.guardPostPoint;
            if (point != null)
            {
                // ИСПРАВЛЕНИЕ: .transform.position
                yield return staff.MoveToTarget(point.transform.position, "Idle");
            }
        }
        // ПРИМЕР: Домой
        else if (actionData.actionType == ActionType.GoHome)
        {
            // EndShift records departure and owns the exit coroutine; deactivating here would
            // stop FinishAction and leave an obsolete executor attached for the next morning.
            staff.EndShift();
            yield break;
        }
        // ПРИМЕР: Кухня
        else if (actionData.actionType == ActionType.Eat || actionData.actionType == ActionType.Drink)
        {
            var point = ScenePointsRegistry.Instance?.RequestKitchenPoint();
            if (point != null)
            {
                yield return staff.MoveToTarget(point.transform.position, "Drinking");
                yield return new WaitForSeconds(5f);
                ScenePointsRegistry.Instance?.FreeKitchenPoint(point);
            }
        }

        FinishAction(true);
    }
}