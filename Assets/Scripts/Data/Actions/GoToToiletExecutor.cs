using UnityEngine;
using System.Collections;
using Managers;

public class GoToToiletExecutor : ActionExecutor
{
    public override bool IsInterruptible => false; 

    protected override IEnumerator ActionRoutine()
    {
        var toiletPoint = ScenePointsRegistry.Instance?.staffToiletPoint;

        if (toiletPoint != null)
        {
            yield return staff.StartCoroutine(staff.MoveToTarget(toiletPoint.transform.position, "Idle"));
            
            staff.thoughtBubble?.ShowPriorityMessage("...", 3f, Color.white);
            yield return new WaitForSeconds(3f);
            
            // ИСПРАВЛЕНИЕ: Сбрасываем нужду!
            staff.bladder = 0f; 
        }

        FinishAction(true);
    }
}
