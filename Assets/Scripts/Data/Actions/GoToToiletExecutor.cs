using UnityEngine;
using System.Collections;

public class GoToToiletExecutor : ActionExecutor
{
    public override bool IsInterruptible => false;

    protected override IEnumerator ActionRoutine()
    {
        Transform toiletPoint = ScenePointsRegistry.Instance?.staffToiletPoint;
        if (toiletPoint == null)
        {
            FinishAction(false);
            yield break;
        }

        staff.thoughtBubble?.ShowPriorityMessage("Нужно отойти...", 2f, Color.yellow);

        if (staff is ClerkController clerk)
        {
            clerk.SetState(ClerkController.ClerkState.GoingToToilet);
            yield return staff.StartCoroutine(staff.MoveToTarget(toiletPoint.position, ClerkController.ClerkState.AtToilet.ToString()));
            yield return new WaitForSeconds(10f);
            clerk.SetState(ClerkController.ClerkState.ReturningToWork);
        }
        else if (staff is GuardMovement guard)
        {
            guard.SetState(GuardMovement.GuardState.GoingToToilet);
            yield return staff.StartCoroutine(staff.MoveToTarget(toiletPoint.position, GuardMovement.GuardState.AtToilet.ToString()));
            yield return new WaitForSeconds(10f);
            guard.SetState(GuardMovement.GuardState.Idle);
        }
        
        staff.bladder = 0f;
        FinishAction(true);
    }
}