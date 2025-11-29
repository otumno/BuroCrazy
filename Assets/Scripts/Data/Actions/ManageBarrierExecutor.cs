using System.Collections;
using Data.Calendar;
using Managers;
using UnityEngine;

public class ManageBarrierExecutor : ActionExecutor
{
    protected override IEnumerator ActionRoutine()
    {
        if (!(staff is GuardMovement guard))
        {
            FinishAction(false);
            yield break;
        }

        var barrier = GuardManager.Instance.securityBarrier;
        if (barrier == null || barrier.guardInteractionPoint == null)
        {
            FinishAction(false);
            yield break;
        }
        
        guard.SetState(GuardMovement.GuardState.OperatingBarrier);
        yield return staff.StartCoroutine(guard.MoveToTarget(barrier.guardInteractionPoint.position, GuardMovement.GuardState.OperatingBarrier));
        yield return new WaitForSeconds(2.0f);

        var currentPeriodType = DayPeriodManager.Instance.CurrentPeriodType;
        if (currentPeriodType == CalendarDayPeriodType.Morning && barrier.IsActive())
        {
            barrier.DeactivateBarrier();
        }
        else if (currentPeriodType == CalendarDayPeriodType.Night && !barrier.IsActive())
        {
            barrier.ActivateBarrier();
        }

        guard.SetState(GuardMovement.GuardState.Idle);
        FinishAction(true);
    }
}