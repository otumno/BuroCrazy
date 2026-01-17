// Assets/Scripts/Data/Actions/GoToToiletExecutor.cs
using UnityEngine;
using System.Collections;
using Managers;

public class GoToToiletExecutor : ActionExecutor
{
    public override bool IsInterruptible => false; // В туалет лучше не прерывать

    protected override IEnumerator ActionRoutine()
    {
        // Получаем Waypoint
        var toiletPoint = ScenePointsRegistry.Instance?.staffToiletPoint;

        if (toiletPoint != null)
        {
            // ИСПРАВЛЕНИЕ: toiletPoint.transform.position
            yield return staff.StartCoroutine(staff.MoveToTarget(toiletPoint.transform.position, "Idle"));
            
            staff.thoughtBubble?.ShowPriorityMessage("...", 3f, Color.white);
            yield return new WaitForSeconds(3f);
            
            // Восстанавливаем нужду (если есть такой параметр)
            // staff.needs.bladder = 0; 
        }

        FinishAction(true);
    }
}