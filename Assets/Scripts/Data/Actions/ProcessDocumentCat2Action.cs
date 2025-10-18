// Файл: Assets/Scripts/Data/Actions/Action_ProcessDocumentCat2.cs
using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "Action_ProcessDocCat2", menuName = "Бюрократия/Тактические/Работа в офисе (Кат. 2)")]
public class ProcessDocumentCat2Action : StaffAction
{
    public ProcessDocumentCat2Action()
    {
        category = ActionCategory.Tactic;
    }

    // НОВАЯ ЛОГИКА: Условие теперь такое же, как у "Обслуживания за столом"
    public override bool AreConditionsMet(StaffController staff)
    {
        if (staff.currentRank.rankLevel < minRankRequired)
        {
            return false;
        }

        if (!(staff is ClerkController clerk) || clerk.role != ClerkController.ClerkRole.Regular || clerk.IsOnBreak() || clerk.assignedWorkstation == null)
        {
            return false;
        }

        var zone = ClientSpawner.GetZoneByDeskId(clerk.assignedWorkstation.deskId);
        return zone != null && zone.GetOccupyingClients().Any();
    }

    public override System.Type GetExecutorType()
    {
        return typeof(ProcessDocumentCat2Executor);
    }
}