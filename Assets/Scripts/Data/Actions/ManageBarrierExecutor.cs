// Assets/Scripts/Data/Actions/ManageBarrierExecutor.cs
using UnityEngine;
using System.Collections;
using Managers;

public class ManageBarrierExecutor : ActionExecutor
{
    public override bool IsInterruptible => true;

    protected override IEnumerator ActionRoutine()
    {
        var guard = staff.GetComponent<GuardMovement>();
        if (guard == null) { FinishAction(false); yield break; }

        var barrier = ScenePointsRegistry.Instance?.securityBarrier;
        if (barrier == null) { FinishAction(false); yield break; }

        // Идем к шлагбауму
        // Используем guard.MoveToTarget (мост) или staff.MoveToTarget
        yield return staff.StartCoroutine(staff.MoveToTarget(barrier.interactionPoint.position, "Walking"));

        guard.SetState(GuardMovement.GuardState.OperatingBarrier);
        staff.thoughtBubble?.ShowPriorityMessage("Проверка пропуска...", 3f, Color.blue);

        // Имитация работы
        yield return new WaitForSeconds(3f);
        
        // barrier.Toggle(); // Если есть метод открытия
        
        guard.SetState(GuardMovement.GuardState.Idle);
        FinishAction(true);
    }
}