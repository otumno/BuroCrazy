using System.Collections;
using Data.Calendar;
using Managers;
using UnityEngine;

// todo: can operate same logic as employees. just assign WorkingPeriods
public class OperateBarrierExecutor : ActionExecutor
{
    protected override IEnumerator ActionRoutine()
    {
        // only guards operate barrier
        if (staff is not GuardMovement guard)
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
        
        var currentPeriodType = Managers.TimeManager.Instance.GetCurrentPeriodType();
        if (currentPeriodType == CalendarDayPeriodType.Morning && barrier.IsActive())
        {
            barrier.DeactivateBarrier();
        }
        else if (currentPeriodType.IsNight() && !barrier.IsActive())
        {
            barrier.ActivateBarrier();
        }

        guard.SetState(GuardMovement.GuardState.Idle);
        FinishAction(true);
    }
}