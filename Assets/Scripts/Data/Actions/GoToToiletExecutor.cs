using UnityEngine;
using System.Collections;
using Managers;

public class GoToToiletExecutor : ActionExecutor
{
    public override bool IsInterruptible => false; 

    protected override IEnumerator ActionRoutine()
    {
        var toiletPoint = ScenePointsRegistry.Instance?.staffToiletPoint;

        if (toiletPoint != null)
        {
            staff.BeginNeedActivity(true);
            RegisterCleanup(() => { if (staff != null) staff.ClearNeedActivity(); });
            yield return staff.MoveToTarget(toiletPoint.transform.position, "AtToilet");
            if (toiletPoint == null || Vector2.Distance(staff.transform.position, toiletPoint.transform.position) > 2f)
            { FinishAction(false); yield break; }
            staff.CurrentSubStatus = UI.GameText.Get("staff.toilet.using");
            
            staff.thoughtBubble?.ShowPriorityMessage("...", 3f, Color.white);
            yield return new WaitForSeconds(3f);
            if (toiletPoint == null) { FinishAction(false); yield break; }
            
            // ИСПРАВЛЕНИЕ: Сбрасываем нужду!
            staff.bladder = 0f; 
        }
        else { FinishAction(false); yield break; }

        yield return ReturnToWorkstation();
        FinishAction(true);
    }
}
