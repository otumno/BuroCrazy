using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "Action_ProcessDocCat1", menuName = "Bureau/Actions/ProcessDocumentCat1")]
public class ProcessDocumentCat1Action : StaffAction
{
    public override bool AreConditionsMet(StaffController staff)
    {
        if (!(staff is ClerkController clerk) || clerk.IsOnBreak() || clerk.GetCurrentState() != ClerkController.ClerkState.Working)
        {
            return false;
        }

        // Находим клиента, которого обслуживает клерк
        var zone = ClientSpawner.GetZoneByDeskId(clerk.assignedServicePoint.deskId);
        if (zone == null) return false;

        ClientPathfinding client = zone.GetOccupyingClients().FirstOrDefault();
        if (client == null) return false;

        // --- ГЛАВНОЕ УСЛОВИЕ ---
        // Действие доступно, ТОЛЬКО ЕСЛИ документ клиента уже прошел проверку.
        return client.documentChecked == true;
    }

    public override System.Type GetExecutorType()
    {
        // Исполнитель для этого действия
        return typeof(ProcessDocumentCat1Executor);
    }
}