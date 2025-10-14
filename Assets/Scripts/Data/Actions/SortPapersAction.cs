// Файл: Assets/Scripts/Data/Actions/SortPapersAction.cs
using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "Action_SortPapers", menuName = "Bureau/Actions/SortPapers")]
public class SortPapersAction : StaffAction
{
    public SortPapersAction()
    {
        category = ActionCategory.System; // Помечаем как системное
    }
    
    public override bool AreConditionsMet(StaffController staff)
    {
        if (!(staff is ClerkController clerk) || clerk.IsOnBreak() || clerk.assignedWorkstation == null)
        {
            return false;
        }

        var zone = ClientSpawner.GetZoneByDeskId(clerk.assignedWorkstation.deskId);
        if (zone == null) return false;

        return !zone.GetOccupyingClients().Any();
    }

    public override System.Type GetExecutorType()
    {
        return typeof(SortPapersExecutor);
    }
}