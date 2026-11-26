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
        if (desk == null || desk.clerkStandPoint == null || desk.documentStack == null)
        {
            staff.thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.guard.no_report_desk"), 2f, Color.red);
            FinishAction(false); 
            yield break; 
        }

        // Идем к столу
        yield return staff.MoveToTarget(desk.clerkStandPoint.position, "Walking");
        if (desk == null || desk.clerkStandPoint == null || Vector2.Distance(staff.transform.position, desk.clerkStandPoint.position) > 1f)
        { FinishAction(false); yield break; }

        guard.SetState(GuardMovement.GuardState.WritingReport);
        RegisterCleanup(() =>
        {
            if (guard != null && guard.GetCurrentState() == GuardMovement.GuardState.WritingReport)
                guard.SetState(GuardMovement.GuardState.Idle);
        });
        staff.thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.guard.writing_report"), 3f, Color.cyan);

        // Пишем отчет (время зависит от кол-ва очков)
        int points = guard.unwrittenReportPoints;
        if (points <= 0) { FinishAction(false); yield break; }
        float duration = points * 2f;
        ShowProcessing(desk.transform.position, duration);
        yield return new WaitForSeconds(duration);
        if (desk == null || desk.documentStack == null) { FinishAction(false); yield break; }

        // Сдаем отчет
        ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
        
        // Физически спавним рапорты на столе
        int deposited = 0;
        if (desk.documentStack != null)
        {
            for (int i = 0; i < Mathf.Min(points, guard.unwrittenReportPoints); i++)
            {
                if (!desk.documentStack.AddDocumentToStack()) break;
                deposited++;
            }
        }
        
        // Сбрасываем очки
        guard.unwrittenReportPoints -= deposited;
        
        if (deposited > 0) staff.thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.guard.report_submitted"), 2f, Color.green);

        FinishAction(deposited > 0);
    }
}
