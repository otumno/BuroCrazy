// Assets/Scripts/Data/Actions/ManageBarrierExecutor.cs
using System.Collections;
using Managers;
using UnityEngine;

public class ManageBarrierExecutor : ActionExecutor
{
    public override bool IsInterruptible => false;

    protected override IEnumerator ActionRoutine()
    {
        var guard = staff.GetComponent<GuardMovement>();
        if (guard == null) { FinishAction(false); yield break; }

        var barrier = GuardManager.Instance?.securityBarrier;
        if (barrier == null || barrier.interactionPoint == null) { FinishAction(false); yield break; }

        guard.SetState(GuardMovement.GuardState.OperatingBarrier);

        // Таймаут против застревания в коллайдере двери
        float timeout = 10f;
        staff.AgentMover.SetPath(Utilities.PathfindingUtility.BuildPathTo(staff.transform.position, barrier.interactionPoint.position, staff.gameObject));
        while (staff.AgentMover.IsMoving() && timeout > 0)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }
        staff.AgentMover.Stop();

        // Экшен вызывается только если состояние двери нужно изменить
        barrier.ToggleBarrier();
        guard.unwrittenReportPoints++; // Отчет за работу!
        
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySound(Scriptables.Audio.SoundID.UI_Click_Default);

        staff.thoughtBubble?.ShowPriorityMessage("Дверь переключена!", 2f, Color.green);

        yield return new WaitForSeconds(1f);
        guard.SetState(GuardMovement.GuardState.Idle);
        FinishAction(true);
    }
}
