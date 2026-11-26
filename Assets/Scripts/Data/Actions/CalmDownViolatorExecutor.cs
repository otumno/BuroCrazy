// Assets/Scripts/Data/Actions/CalmDownViolatorExecutor.cs
using UnityEngine;
using System.Collections;
using Managers;

public class CalmDownViolatorExecutor : ActionExecutor
{
    public override bool IsInterruptible => false;

    protected override IEnumerator ActionRoutine()
    {
        var guard = staff.GetComponent<GuardMovement>();
        if (guard == null) { FinishAction(false); yield break; }

        var violator = GuardManager.Instance?.GetViolator();
        if (violator == null) { FinishAction(true); yield break; }

        guard.SetState(GuardMovement.GuardState.Chasing); // Используем Chasing для подхода
        staff.thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.guard.calm_down"), 2f, Color.yellow);

        // Подходим к нарушителю
        float chaseDeadline = Time.time + 20f;
        while (Time.time < chaseDeadline && violator != null && Vector3.Distance(staff.transform.position, violator.transform.position) > 2f)
        {
            staff.agentMover.SetTarget(violator.transform.position);
            yield return new WaitForSeconds(0.2f);
        }

        if (violator == null || Vector3.Distance(staff.transform.position, violator.transform.position) > 2f)
        { staff.AgentMover?.Stop(); FinishAction(false); yield break; }

        if (violator != null)
        {
            // Разговор
            guard.SetState(GuardMovement.GuardState.Talking);
            staff.agentMover.Stop();

            yield return new WaitForSeconds(guard.talkTime);

            // Результат
            if (violator == null || !violator.activeInHierarchy ||
                Vector3.Distance(staff.transform.position, violator.transform.position) > 2f ||
                GuardManager.Instance == null || !GuardManager.Instance.PacifyViolator(violator))
            { FinishAction(false); yield break; }
            staff.thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.guard.conflict_resolved"), 2f, Color.green);

            guard.unwrittenReportPoints++;

            // Ачивка: охранник впервые утихомирил нарушителя
            Managers.AchievementManager.Instance?.UnlockAchievement("Achv_GuardStory");
        }

        guard.SetState(GuardMovement.GuardState.Idle);
        FinishAction(true);
    }
}
