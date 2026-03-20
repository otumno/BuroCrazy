using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "Action_TakeOverClient", menuName = "Bureau/Actions/TakeOverClient")]
public class TakeOverClientAction : StaffAction
{
    public TakeOverClientAction()
    {
        category = ActionCategory.System;
    }
    
    public override bool AreConditionsMet(StaffController staff)
    {
        // ИСПОЛЬЗУЕМ staff.currentRole
        if (!(staff is ClerkController registrar) || registrar.IsOnBreak() || staff.currentRole != StaffController.Role.Registrar)
            return false;

        var allClients = Object.FindObjectsByType<ClientPathfinding>(FindObjectsSortMode.None);
        foreach (var client in allClients)
        {
            if (client != null && client.stateMachine != null && client.stateMachine.GetCurrentState() == ClientState.AtRegistration)
            {
                if (client.stateMachine.MyServiceProvider != null && !client.stateMachine.MyServiceProvider.IsAvailableToServe)
                {
                    return true;
                }
            }
        }
        return false;
    }

    public override System.Type GetExecutorType()
    {
        return typeof(TakeOverClientExecutor);
    }
}