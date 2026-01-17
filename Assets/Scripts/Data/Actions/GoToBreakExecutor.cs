// Assets/Scripts/Data/Actions/GoToBreakExecutor.cs
using UnityEngine;
using System.Collections;
using Managers;

public class GoToBreakExecutor : ActionExecutor
{
    public override bool IsInterruptible => true;
    private Waypoint occupiedPoint;

    protected override IEnumerator ActionRoutine()
    {
        // Получаем точку (Waypoint)
        occupiedPoint = ScenePointsRegistry.Instance?.RequestKitchenPoint();

        if (occupiedPoint != null)
        {
            // ИСПРАВЛЕНИЕ: occupiedPoint.transform.position
            yield return staff.StartCoroutine(staff.MoveToTarget(occupiedPoint.transform.position, "Break"));

            float breakDuration = 10f;
            float timer = 0;
            while (timer < breakDuration)
            {
                timer += 1f;
                staff.ChangeEnergy(2); // Восстанавливаем энергию
                staff.ChangeStress(-2); // Снижаем стресс
                yield return new WaitForSeconds(1f);
            }
            
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
        {
            ScenePointsRegistry.Instance.FreeKitchenPoint(occupiedPoint);
        }
    }
}