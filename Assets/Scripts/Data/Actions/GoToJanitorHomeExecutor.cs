// Assets/Scripts/Data/Actions/GoToJanitorHomeExecutor.cs
using UnityEngine;
using System.Collections;
using Managers;

public class GoToJanitorHomeExecutor : ActionExecutor
{
    public override bool IsInterruptible => false;

    protected override IEnumerator ActionRoutine()
    {
        var target = ScenePointsRegistry.Instance?.janitorHomePoint;
        
        if (target != null)
        {
            // ИСПРАВЛЕНИЕ: target.transform.position
            yield return staff.StartCoroutine(staff.MoveToTarget(target.transform.position, "Idle"));
            
            // Восстанавливаемся
            staff.ChangeEnergy(50);
            staff.thoughtBubble?.ShowPriorityMessage("Отдыхаю...", 2f, Color.cyan);
            yield return new WaitForSeconds(3f);
        }
        
        FinishAction(true);
    }
}