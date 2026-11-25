using UnityEngine;
using System.Collections;
using Managers;

public class GoToBreakExecutor : ActionExecutor
{
    public override bool IsInterruptible => true;
    private Waypoint occupiedPoint;

    protected override IEnumerator ActionRoutine()
    {
        occupiedPoint = ScenePointsRegistry.Instance?.RequestKitchenPoint();

        if (occupiedPoint != null)
        {
            staff.BeginNeedActivity(false);
            RegisterCleanup(() => { if (staff != null) staff.ClearNeedActivity(); });
            yield return staff.MoveToTarget(occupiedPoint.transform.position, "OnBreak");
            if (occupiedPoint == null || Vector2.Distance(staff.transform.position, occupiedPoint.transform.position) > 2f)
            { FinishAction(false); yield break; }
            staff.CurrentSubStatus = UI.GameText.Get("staff.break.resting");

            float breakDuration = 10f;
            float timer = 0;
            while (timer < breakDuration)
            {
                if (occupiedPoint == null) { FinishAction(false); yield break; }
                timer += 1f;
                // ИСПРАВЛЕНИЕ: Быстро восстанавливаем энергию
                staff.energy = Mathf.Clamp(staff.energy + 10f, 0f, 100f); 
                staff.stress = Mathf.Clamp(staff.stress - 10f, 0f, 100f); 
                yield return new WaitForSeconds(1f);
            }
            
            // Гарантия полной бодрости после отдыха
            staff.energy = 100f;
            staff.thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.break.finished"), 2f, Color.white);
        }
        else
        {
            staff.thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.break.no_space"), 2f, Color.red);
            FinishAction(false);
            yield break;
        }

        staff.ClearNeedActivity();
        OnResourcesReleased();
        occupiedPoint = null;
        yield return ReturnToWorkstation();
        FinishAction(true);
    }

    protected override void OnResourcesReleased()
    {
        if (occupiedPoint != null && ScenePointsRegistry.Instance != null)
            ScenePointsRegistry.Instance.FreeKitchenPoint(occupiedPoint);
    }
}
