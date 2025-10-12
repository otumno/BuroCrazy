// Файл: Assets/Scripts/Data/Actions/TakeOverClientExecutor.cs
using UnityEngine;
using System.Collections;
using System.Linq;

public class TakeOverClientExecutor : ActionExecutor
{
    public override bool IsInterruptible => false;
    protected override IEnumerator ActionRoutine()
    {
        var registrar = staff as ClerkController;
        if (registrar == null) { FinishAction(); yield break; }

        var stuckClient = Object.FindObjectsByType<ClientPathfinding>(FindObjectsSortMode.None)
            .Where(c => c.stateMachine.GetCurrentState() == ClientState.AtRegistration && 
                        c.stateMachine.MyServiceProvider != null && 
                        !c.stateMachine.MyServiceProvider.IsAvailableToServe)
            .OrderBy(c => Vector3.Distance(staff.transform.position, c.transform.position))
            .FirstOrDefault();
            
        if (stuckClient == null)
        {
            FinishAction();
            yield break;
        }
        
        registrar.thoughtBubble?.ShowPriorityMessage("Не стойте здесь, я вами займусь!", 3f, Color.blue);
        yield return new WaitForSeconds(1f);

        var newDestination = registrar.assignedWorkstation.clientStandPoint;
        stuckClient.stateMachine.GetCalledToSpecificDesk(newDestination, stuckClient.stateMachine.MyQueueNumber, registrar);
        Debug.Log($"{registrar.name} перехватил клиента {stuckClient.name} от другого окна.");

        ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
        FinishAction();
    }
}