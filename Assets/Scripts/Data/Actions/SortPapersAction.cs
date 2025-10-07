using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "Action_SortPapers", menuName = "Bureau/Actions/SortPapers")]
public class SortPapersAction : StaffAction
{
    public override bool AreConditionsMet(StaffController staff)
    {
        if (!(staff is ClerkController clerk) || clerk.IsOnBreak() || clerk.GetCurrentState() != ClerkController.ClerkState.Working)
        {
            return false;
        }

        // Находим зону, чтобы проверить, есть ли в ней клиенты
        var zone = ClientSpawner.GetZoneByDeskId(clerk.assignedServicePoint.deskId);
        if (zone == null) return false;

        // --- ГЛАВНОЕ УСЛОВИЕ ДЛЯ РУТИНЫ ---
        // Действие доступно, ТОЛЬКО ЕСЛИ у стойки НЕТ клиента.
        return zone.GetOccupyingClients().FirstOrDefault() == null;
    }

    public override System.Type GetExecutorType()
    {
        return typeof(SortPapersExecutor);
    }
}