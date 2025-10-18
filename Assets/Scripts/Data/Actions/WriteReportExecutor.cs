using UnityEngine;
using System.Collections;

public class WriteReportExecutor : ActionExecutor
{
    protected override IEnumerator ActionRoutine()
    {
        if (!(staff is GuardMovement guard))
        {
            FinishAction(false);
            yield break;
        }

        ServicePoint reportDesk = ScenePointsRegistry.Instance?.guardReportDesk;
        if (reportDesk == null || reportDesk.clerkStandPoint == null || reportDesk.documentStack == null)
        {
            FinishAction(false);
            yield break;
        }

        guard.SetState(GuardMovement.GuardState.WritingReport);
        staff.thoughtBubble?.ShowPriorityMessage("Нужно заполнить\nбумаги...", 2f, Color.yellow);
        
        yield return staff.StartCoroutine(guard.MoveToTarget(reportDesk.clerkStandPoint.position, GuardMovement.GuardState.WritingReport.ToString()));

        int pointsAtStart = guard.unwrittenReportPoints;
        for (int i = 0; i < pointsAtStart; i++)
        {
            if (guard.unwrittenReportPoints <= 0) break;
            float writeTime = Random.Range(1f, 2f);
            yield return new WaitForSeconds(writeTime);

            if (reportDesk.documentStack.AddDocumentToStack())
            {
                guard.unwrittenReportPoints--;
            }
            else
            {
                guard.thoughtBubble?.ShowPriorityMessage("Стол завален!\nНе могу работать.", 3f, Color.red);
                break;
            }
        }
    
        guard.SetState(GuardMovement.GuardState.Idle);
        FinishAction(true);
    }
}