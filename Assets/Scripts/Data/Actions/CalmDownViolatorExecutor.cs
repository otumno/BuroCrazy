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

        var violator = GuardManager.Instance?.currentViolator;
        if (violator == null) { FinishAction(true); yield break; }

        guard.SetState(GuardMovement.GuardState.Chasing); // Используем Chasing для подхода
        staff.thoughtBubble?.ShowPriorityMessage("Гражданин, успокойтесь!", 2f, Color.yellow);

        // Подходим к нарушителю
        while (violator != null && Vector3.Distance(staff.transform.position, violator.transform.position) > 2f)
        {
            staff.agentMover.SetTarget(violator.transform.position);
            yield return new WaitForSeconds(0.2f);
        }

        if (violator != null)
        {
            // Разговор
            guard.SetState(GuardMovement.GuardState.Talking);
            staff.agentMover.Stop();

            yield return new WaitForSeconds(guard.talkTime);

            // Результат
            // GuardManager.Instance.PacifyViolator(violator);
            staff.thoughtBubble?.ShowPriorityMessage("Конфликт исчерпан.", 2f, Color.green);

            guard.unwrittenReportPoints++;

            // Ачивка: охранник впервые утихомирил нарушителя
            Managers.AchievementManager.Instance?.UnlockAchievement("Achv_GuardStory");
        }

        guard.SetState(GuardMovement.GuardState.Idle);
        FinishAction(true);
    }
}