// Assets/Scripts/Data/Actions/OperateBarrierExecutor.cs
using System.Collections;
using Data.Calendar;
using Managers;
using UnityEngine;

public class OperateBarrierExecutor : ActionExecutor
{
    protected override IEnumerator ActionRoutine()
    {
        // ИСПРАВЛЕНИЕ: GetComponent вместо pattern matching
        var guard = staff.GetComponent<GuardMovement>();
        if (guard == null)
        {
            FinishAction(false);
            yield break;
        }

        var barrier = GuardManager.Instance?.securityBarrier;
        // ИСПРАВЛЕНИЕ: Ищем interactionPoint (или guardInteractionPoint через алиас)
        if (barrier == null || barrier.interactionPoint == null)
        {
            FinishAction(false);
            yield break;
        }
        
        guard.SetState(GuardMovement.GuardState.OperatingBarrier);
        
        // ИСПРАВЛЕНИЕ: Передаем Enum (GuardMovement теперь умеет это обрабатывать)
        yield return staff.StartCoroutine(guard.MoveToTarget(barrier.interactionPoint.position, GuardMovement.GuardState.OperatingBarrier));
        
        yield return new WaitForSeconds(2.0f);
        
        // Логика времени... (упрощенно)
        // var currentPeriod = TimeManager.Instance.GetCurrentPeriodType();
        // ... barrier.ActivateBarrier();

        guard.SetState(GuardMovement.GuardState.Idle);
        FinishAction(true);
    }
}