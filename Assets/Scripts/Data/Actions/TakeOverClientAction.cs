using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "Action_TakeOverClient", menuName = "Bureau/Actions/TakeOverClient")]
public class TakeOverClientAction : StaffAction
{
    public override bool AreConditionsMet(StaffController staff)
    {
        // Действие доступно только для Регистратора, который на смене и свободен
        if (!(staff is ClerkController registrar) || registrar.IsOnBreak() || registrar.role != ClerkController.ClerkRole.Registrar)
        {
            return false;
        }

        // Ищем на сцене ЛЮБОГО клиента, который застрял
        var allClients = Object.FindObjectsByType<ClientPathfinding>(FindObjectsSortMode.None);
        foreach (var client in allClients)
        {
            if (client.stateMachine.GetCurrentState() == ClientState.AtRegistration)
            {
                // Если клиент ждет у стойки, и его сотрудник НЕдоступен...
                if (client.stateMachine.MyServiceProvider != null && !client.stateMachine.MyServiceProvider.IsAvailableToServe)
                {
                    // ...то условие выполнено! Есть работа!
                    return true;
                }
            }
        }

        return false; // Не найдено застрявших клиентов
    }

    public override System.Type GetExecutorType()
    {
        return typeof(TakeOverClientExecutor);
    }
}