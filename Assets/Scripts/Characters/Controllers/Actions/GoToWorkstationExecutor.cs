using UnityEngine;
using System.Collections;
using Managers;

public class GoToWorkstationExecutor : ActionExecutor
{
    public override bool IsInterruptible => true;
    
    protected override IEnumerator ActionRoutine()
    {
        if (staff.assignedWorkstation == null) 
        { 
            staff.thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.no_workstation"), 4f, Color.red);
            yield return new WaitForSeconds(5f);
            FinishAction(false);
            yield break;
        }

        if (staff is ClerkController clerk) clerk.SetState(ClerkController.ClerkState.ReturningToWork);
        yield return ReturnToWorkstation();
        FinishAction(IsAtWorkstation);
    }
}