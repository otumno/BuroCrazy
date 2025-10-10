// Файл: Assets/Scripts/Data/Actions/ProcessDocumentCat2Action.cs
using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "Action_ProcessDocCat2", menuName = "Bureau/Actions/ProcessDocumentCat2")]
public class ProcessDocumentCat2Action : StaffAction
{
    public override bool AreConditionsMet(StaffController staff)
    {
        // --- НОВОЕ УСЛОВИЕ: Проверяем, что ранг сотрудника достаточен ---
        if (staff.rank < minRankRequired)
        {
            return false;
        }

        if (!(staff is ClerkController clerk) || clerk.IsOnBreak() || clerk.GetCurrentState() != ClerkController.ClerkState.Working)
        {
            return false;
        }

        var zone = ClientSpawner.GetZoneByDeskId(clerk.assignedServicePoint.deskId);
        if (zone == null) return false;
        
        ClientPathfinding client = zone.GetOccupyingClients().FirstOrDefault();
        if (client == null) return false;
        
        // Условие то же: документ должен быть проверен.
        return client.documentChecked == true;
    }

    public override System.Type GetExecutorType()
    {
        return typeof(ProcessDocumentCat2Executor);
    }
}