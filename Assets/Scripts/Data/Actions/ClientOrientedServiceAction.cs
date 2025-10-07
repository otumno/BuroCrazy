using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "Action_ClientOrientedService", menuName = "Bureau/Actions/ClientOrientedService")]
public class ClientOrientedServiceAction : StaffAction
{
    public override bool AreConditionsMet(StaffController staff)
    {
        // Действие доступно для клерков и регистраторов, которые не на перерыве и находятся на рабочем месте
        if (!(staff is ClerkController clerk) || clerk.IsOnBreak() || clerk.GetCurrentState() != ClerkController.ClerkState.Working)
        {
            return false;
        }

        // Проверяем, есть ли перед сотрудником клиент
        var zone = ClientSpawner.GetZoneByDeskId(clerk.assignedServicePoint.deskId);
        if (zone == null) return false;

        ClientPathfinding client = zone.GetOccupyingClients().FirstOrDefault();
        
        // Условие выполнено, если перед нами есть клиент
        return client != null;
    }

    public override System.Type GetExecutorType()
    {
        return typeof(ClientOrientedServiceExecutor);
    }
}