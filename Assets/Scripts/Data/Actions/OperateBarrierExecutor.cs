// Assets/Scripts/Data/Actions/OperateBarrierExecutor.cs
using System.Collections;
using Data.Calendar;
using Managers;
using UnityEngine;

public class OperateBarrierExecutor : ActionExecutor
{
    protected override IEnumerator ActionRoutine()
    {
        var guard = staff.GetComponent<GuardMovement>();
        if (guard == null)
        {
            FinishAction(false);
            yield break;
        }

        var barrier = GuardManager.Instance?.securityBarrier;
        if (barrier == null || barrier.interactionPoint == null)
        {
            FinishAction(false);
            yield break;
        }

        guard.SetState(GuardMovement.GuardState.OperatingBarrier);

        yield return staff.StartCoroutine(guard.MoveToTarget(barrier.interactionPoint.position, GuardMovement.GuardState.OperatingBarrier));

        var mover = staff.GetComponent<AgentMover>();
        if (mover != null && mover.IsSlipping)
        {
            Debug.Log($"[OperateBarrierExecutor] {staff.characterName} упал, операция отменена.");
            guard.SetState(GuardMovement.GuardState.Idle);
            FinishAction(false);
            yield break;
        }

        yield return new WaitForSeconds(2.0f);

        if (mover != null && mover.IsSlipping)
        {
            Debug.Log($"[OperateBarrierExecutor] {staff.characterName} упал, операция отменена.");
            guard.SetState(GuardMovement.GuardState.Idle);
            FinishAction(false);
            yield break;
        }

        barrier.ToggleBarrier();

        // Записываем открытие двери в отчет
        guard.unwrittenReportPoints++;

        guard.SetState(GuardMovement.GuardState.Idle);
        FinishAction(true);
    }
}