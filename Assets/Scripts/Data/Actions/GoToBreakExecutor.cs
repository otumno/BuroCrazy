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
            yield return staff.StartCoroutine(staff.MoveToTarget(occupiedPoint.transform.position, "Break"));

            float breakDuration = 10f;
            float timer = 0;
            while (timer < breakDuration)
            {
                timer += 1f;
                // ИСПРАВЛЕНИЕ: Быстро восстанавливаем энергию
                staff.energy = Mathf.Clamp(staff.energy + 10f, 0f, 100f); 
                staff.stress = Mathf.Clamp(staff.stress - 10f, 0f, 100f); 
                yield return new WaitForSeconds(1f);
            }
            
            // Гарантия полной бодрости после отдыха
            staff.energy = 100f;
            staff.thoughtBubble?.ShowPriorityMessage("Перерыв окончен", 2f, Color.white);
        }
        else
        {
            staff.thoughtBubble?.ShowPriorityMessage("Нет места!", 2f, Color.red);
        }

        FinishAction(true);
    }

    private void OnDestroy()
    {
        if (occupiedPoint != null && ScenePointsRegistry.Instance != null)
            ScenePointsRegistry.Instance.FreeKitchenPoint(occupiedPoint);
    }
}
