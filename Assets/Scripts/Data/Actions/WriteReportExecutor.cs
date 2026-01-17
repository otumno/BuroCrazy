// Assets/Scripts/Data/Actions/WriteReportExecutor.cs
using UnityEngine;
using System.Collections;
using Managers;

public class WriteReportExecutor : ActionExecutor
{
    public override bool IsInterruptible => true;

    protected override IEnumerator ActionRoutine()
    {
        var guard = staff.GetComponent<GuardMovement>();
        if (guard == null) { FinishAction(false); yield break; }

        var desk = ScenePointsRegistry.Instance?.guardReportDesk;
        if (desk == null) 
        {
            staff.thoughtBubble?.ShowPriorityMessage("Нет стола для отчетов!", 2f, Color.red);
            FinishAction(false); 
            yield break; 
        }

        // Идем к столу
        yield return staff.StartCoroutine(staff.MoveToTarget(desk.clerkStandPoint.position, "Walking"));

        guard.SetState(GuardMovement.GuardState.WritingReport);
        staff.thoughtBubble?.ShowPriorityMessage("Пишу рапорт...", 3f, Color.cyan);

        // Пишем отчет (время зависит от кол-ва очков)
        float duration = guard.unwrittenReportPoints * 2f;
        yield return new WaitForSeconds(duration);

        // Сдаем отчет
        ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
        
        // Сбрасываем очки
        guard.unwrittenReportPoints = 0;
        
        staff.thoughtBubble?.ShowPriorityMessage("Рапорт сдан.", 2f, Color.green);

        guard.SetState(GuardMovement.GuardState.Idle);
        FinishAction(true);
    }
}