using UnityEngine;
using System.Collections;

public class GoToWorkstationExecutor : ActionExecutor
{
    public override bool IsInterruptible => true;

    protected override IEnumerator ActionRoutine()
    {
        if (!(staff is ClerkController clerk) || clerk.assignedServicePoint == null) 
        { 
            FinishAction(); 
            yield break; 
        }

        // Регистрируем клерка как активного работника на этой станции
        ClientSpawner.AssignServiceProviderToDesk(clerk, clerk.assignedServicePoint.deskId);

        clerk.SetState(ClerkController.ClerkState.ReturningToWork);
        yield return staff.StartCoroutine(clerk.MoveToTarget(clerk.assignedServicePoint.clerkStandPoint.position, ClerkController.ClerkState.Working));

        // Бесконечно ждем, пока нас не прервет более важное дело (например, перерыв)
        while (true)
        {
            yield return new WaitForSeconds(5f);
        }
    }
}