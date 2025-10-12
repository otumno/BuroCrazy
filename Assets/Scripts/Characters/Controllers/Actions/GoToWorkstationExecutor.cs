using UnityEngine;
using System.Collections;

public class GoToWorkstationExecutor : ActionExecutor
{
    public override bool IsInterruptible => true;
    
    protected override IEnumerator ActionRoutine()
    {
        if (staff.assignedWorkstation == null) 
        { 
            staff.thoughtBubble?.ShowPriorityMessage("Мне не назначили\nрабочее место!", 4f, Color.red);
            yield return new WaitForSeconds(5f);
            FinishAction();
            yield break;
        }

        if (staff is IServiceProvider provider)
        {
            ClientSpawner.AssignServiceProviderToDesk(provider, staff.assignedWorkstation.deskId);
        }

        if (staff is ClerkController clerk)
        {
            clerk.SetState(ClerkController.ClerkState.ReturningToWork);
            // ----- ИЗМЕНЕНИЕ: Передаем состояние как строку -----
            yield return staff.StartCoroutine(clerk.MoveToTarget(staff.assignedWorkstation.clerkStandPoint.position, ClerkController.ClerkState.Working.ToString()));
        }
        else
        {
            yield return staff.StartCoroutine(staff.MoveToTarget(staff.assignedWorkstation.clerkStandPoint.position, ""));
        }

        Debug.Log($"[GoToWorkstationExecutor] {staff.name} прибыл на рабочее место. Освобождаю AI для принятия решений.");
        FinishAction();
    }
}