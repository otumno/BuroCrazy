using UnityEngine;
using System.Collections;

public class GoToBreakExecutor : ActionExecutor
{
    public override bool IsInterruptible => false;

    protected override IEnumerator ActionRoutine()
    {
        Transform breakPoint = ScenePointsRegistry.Instance?.RequestKitchenPoint();
        if (breakPoint == null)
        {
            staff.SetActionCooldown(actionData.actionType, 15f);
            FinishAction(false);
            yield break;
        }

        staff.thoughtBubble?.ShowPriorityMessage("Время обеда!", 2f, Color.cyan);
        float breakDuration = 30f * (1f + staff.skills.pedantry);

        if (staff is ClerkController clerk)
        {
            clerk.SetState(ClerkController.ClerkState.GoingToBreak);
            yield return staff.StartCoroutine(staff.MoveToTarget(breakPoint.position, ClerkController.ClerkState.OnBreak.ToString()));
            yield return new WaitForSeconds(breakDuration);
            clerk.SetState(ClerkController.ClerkState.ReturningToWork);
        }
        
        staff.energy = 1f;
        staff.frustration = Mathf.Max(0, staff.frustration - 0.5f);
        
        ScenePointsRegistry.Instance.FreeKitchenPoint(breakPoint);
        FinishAction(true);
    }
}