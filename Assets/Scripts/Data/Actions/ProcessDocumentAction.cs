// Файл: Assets/Scripts/Data/Actions/ProcessDocumentAction.cs
using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "Action_ProcessDocument", menuName = "Bureau/Actions/ProcessDocument")]
public class ProcessDocumentAction : StaffAction
{
    public override bool AreConditionsMet(StaffController staff)
    {
        // 1. Проверяем, что это Клерк и он не на перерыве
        if (!(staff is ClerkController clerk) || clerk.IsOnBreak() || clerk.assignedServicePoint == null)
        {
            return false;
        }

        // 2. Ищем, есть ли клиенты, которые уже находятся внутри зоны обслуживания этого клерка
        LimitedCapacityZone myZone = ClientSpawner.GetZoneByDeskId(clerk.assignedServicePoint.deskId);
        if (myZone == null)
        {
            return false;
        }

        // 3. Условие выполнено, если в зоне есть хотя бы один клиент, ожидающий обслуживания
        return myZone.GetOccupyingClients().Any();
    }

    public override System.Type GetExecutorType()
    {
        // Это действие будет выполняться скриптом ProcessDocumentExecutor
        return typeof(ProcessDocumentExecutor);
    }
}